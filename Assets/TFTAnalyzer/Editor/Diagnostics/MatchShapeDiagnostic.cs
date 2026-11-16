using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>Asks the user to confirm the run preconditions that code cannot observe.</summary>
    internal interface IMatchShapeConfirmation
    {
        /// <summary>The user restarted Unity after renewing the key so the process environment holds it.</summary>
        bool ConfirmUnityRestartedAfterKeyRenewal(string summary);
        /// <summary>No match collection is in progress in any analyzer window.</summary>
        bool ConfirmNoCollectionInProgress();
    }

    /// <summary>Blocks domain reloads while requests are outstanding.</summary>
    internal interface IAssemblyReloadLock
    {
        void Lock();
        void Unlock();
    }

    /// <summary>Receives a schema-validated aggregate report.</summary>
    internal interface IMatchShapeReportSink
    {
        /// <summary>Verifies the report location (outside stores, no links, writable) before any network send.</summary>
        void Preflight();
        void Publish(byte[] utf8Json, CancellationToken token);
    }

    /// <summary>Controlled outcome codes only; never carries URLs, keys, identifiers or exception text.</summary>
    internal sealed class MatchShapeRunResult
    {
        public string Status { get; }
        public string ReasonCode { get; }
        public int ListSends { get; }
        public int DetailSends { get; }
        public bool ReportPublished { get; }
        public string ReportFailureCode { get; }
        public string Classification { get; }
        public bool RunConsumed { get; }

        internal MatchShapeRunResult(string status, string reasonCode, int listSends, int detailSends,
            bool reportPublished, string reportFailureCode, string classification, bool runConsumed)
        {
            Status = status; ReasonCode = reasonCode; ListSends = listSends; DetailSends = detailSends;
            ReportPublished = reportPublished; ReportFailureCode = reportFailureCode; Classification = classification;
            RunConsumed = runConsumed;
        }

        /// <summary>A run that stopped before any network access; it is never a consumed run.</summary>
        internal static MatchShapeRunResult PreconditionFailed(string reason)
            => new MatchShapeRunResult("precondition_failed", reason, 0, 0, false, "none", "undecidable", false);
    }

    /// <summary>
    /// Measures real match response shapes with a hard request budget, separate from the collection path.
    /// It never uses RiotApiClient retries, MatchCollectionService, MatchAnonymizer.Read or any Persistence store.
    /// </summary>
    internal sealed class MatchShapeDiagnostic
    {
        public const int DetailLimit = 20;
        private const int MaximumIdListBytes = 64 * 1024;
        private readonly IRiotApiTransport _transport;
        private readonly IRiotApiKeyProvider _keys;
        private readonly EnvironmentSeedPuuidProvider _seeds;
        private readonly IMatchShapeReportSink _sink;
        private readonly IMatchShapeConfirmation _confirmation;
        private readonly IAssemblyReloadLock _reloadLock;
        private readonly Func<TimeSpan> _elapsed;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        /// <summary>Construction reads nothing: no environment, credential, network or file access.</summary>
        public MatchShapeDiagnostic(IRiotApiTransport transport, IRiotApiKeyProvider keys, EnvironmentSeedPuuidProvider seeds,
            IMatchShapeReportSink sink, IMatchShapeConfirmation confirmation, IAssemblyReloadLock reloadLock,
            Func<TimeSpan> elapsed = null, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
            _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            _reloadLock = reloadLock ?? throw new ArgumentNullException(nameof(reloadLock));
            // Null means a per-run Stopwatch: spacing is measured monotonically, never with the wall clock.
            _elapsed = elapsed;
            _delay = delay ?? ((duration, cancellation) => Task.Delay(duration, cancellation));
        }

        /// <summary>Checks preconditions before any send, then runs at most 1 + 20 single-attempt requests and publishes one aggregate report.</summary>
        public async Task<MatchShapeRunResult> RunAsync(MatchShapeDiagnosticInput input, CancellationToken token)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var key = ReadKey();
            if (key == null) return Precondition("api_key_unavailable");
            Uri listUri;
            try { listUri = RiotRouting.MatchIds(input.Region, _seeds.GetSeeds()[0], 0, DetailLimit); }
            catch { return Precondition("seed_unavailable"); }
            if (!Confirm(input)) return Precondition("confirmation_declined");
            // The report location must be usable before any request is sent; otherwise nothing is sent and no report is written.
            try { _sink.Preflight(); }
            catch { return Precondition("report_path_unavailable"); }

            var headers = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-Riot-Token"] = key });
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var budget = new DiagnosticRequestBudget(_transport, DetailLimit, _elapsed ?? (() => stopwatch.Elapsed), _delay);
            var accumulator = new MatchShapeAccumulator(input);
            var state = new RunState();
            var locked = false;
            try
            {
                _reloadLock.Lock();
                locked = true;
                try { await ExecuteAsync(input, listUri, headers, budget, accumulator, state, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { state.Cancelled = true; }
                // Failures outside the transport (e.g. body disposal or aggregation) keep a distinct, text-free code.
                catch { if (state.Reason == null) Stop(state, budget, state.Stage ?? "list", "internal_error"); }
                budget.Close();
                return Publish(accumulator, state, budget, token);
            }
            finally
            {
                if (locked)
                {
                    try { _reloadLock.Unlock(); }
                    catch { /* An unlock failure must not replace the controlled result with native text. */ }
                }
            }
        }

        /// <summary>Sends the single list request, then each detail request in order, stopping at the first non-2xx or transport failure.</summary>
        private static async Task ExecuteAsync(MatchShapeDiagnosticInput input, Uri listUri,
            IReadOnlyDictionary<string, string> headers, DiagnosticRequestBudget budget, MatchShapeAccumulator accumulator,
            RunState state, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            state.Stage = "list";
            IReadOnlyList<Uri> details;
            using (var body = await SendStageAsync(budget, listUri, headers, "list", state, token))
            {
                if (body == null) return;
                details = ReadIdList(input.Region, body, token);
            }
            if (details == null)
            {
                Stop(state, budget, "list", "invalid_response");
                return;
            }
            state.Stage = "detail";
            var reader = new MatchShapeReader(input);
            foreach (var uri in details)
            {
                token.ThrowIfCancellationRequested();
                using (var body = await SendStageAsync(budget, uri, headers, "detail", state, token))
                {
                    if (body == null) return;
                    MatchShapeSample sample = null;
                    try { sample = reader.Read(body, token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch { /* Unreadable payloads are counted without retaining parser diagnostics. */ }
                    if (sample == null) accumulator.AddUnreadable();
                    else accumulator.Add(sample);
                }
            }
            state.Stage = null;
            state.Completed = true;
        }

        /// <summary>Performs one budgeted attempt; returns the 2xx body or null after recording a controlled stop reason.</summary>
        private static async Task<Stream> SendStageAsync(DiagnosticRequestBudget budget, Uri uri,
            IReadOnlyDictionary<string, string> headers, string stage, RunState state, CancellationToken token)
        {
            RiotApiResponse response = null;
            try
            {
                response = await budget.SendAsync(uri, headers, token);
                token.ThrowIfCancellationRequested();
                if (response == null)
                {
                    Stop(state, budget, stage, "invalid_response");
                    return null;
                }
                var status = response.StatusCode;
                if (status >= 200 && status < 300) return response.TakeBody();
                Stop(state, budget, stage, StatusReason(status));
                return null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (InvalidOperationException error) when (error.Message.StartsWith("diagnostic_budget", StringComparison.Ordinal))
            {
                Stop(state, budget, stage, error.Message == DiagnosticRequestBudget.ClockStalled ? "clock_stalled" : "budget_exhausted");
                return null;
            }
            catch
            {
                Stop(state, budget, stage, "transport_error");
                return null;
            }
            finally { response?.Dispose(); }
        }

        /// <summary>Maps an HTTP status to a fixed reason; list and detail stages are prefixed separately.</summary>
        private static string StatusReason(long status)
        {
            switch (status)
            {
                case 401: return "unauthorized";
                case 403: return "forbidden";
                case 404: return "not_found";
                case 429: return "rate_limited";
            }
            return status >= 500 && status <= 599 ? "server_error" : "http_error";
        }

        private static void Stop(RunState state, DiagnosticRequestBudget budget, string stage, string reason)
        {
            budget.Close();
            if (state.Reason != null) return;
            state.Stage = stage;
            state.Reason = stage + "_" + reason;
        }

        /// <summary>Reads a bounded JSON array of at most 20 unique, nonblank match IDs into memory-only detail URIs.</summary>
        private static IReadOnlyList<Uri> ReadIdList(string region, Stream body, CancellationToken token)
        {
            try
            {
                var bytes = new byte[MaximumIdListBytes + 1];
                var length = 0;
                int read;
                while ((read = body.Read(bytes, length, bytes.Length - length)) > 0)
                {
                    length += read;
                    if (length > MaximumIdListBytes) return null;
                }
                token.ThrowIfCancellationRequested();
                using (var text = new StringReader(new UTF8Encoding(false, true).GetString(bytes, 0, length)))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 4 })
                {
                    var array = JArray.Load(reader);
                    if (reader.Read() || array.Count > DetailLimit) return null;
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    var uris = new List<Uri>(array.Count);
                    foreach (var value in array)
                    {
                        if (value.Type != JTokenType.String) return null;
                        var id = (string)value;
                        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !seen.Add(id)) return null;
                        uris.Add(RiotRouting.Match(region, id));
                    }
                    Array.Clear(bytes, 0, bytes.Length);
                    return uris.AsReadOnly();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return null; }
        }

        /// <summary>Validates and publishes the aggregate report; publication failures are reported as fixed codes only.</summary>
        private MatchShapeRunResult Publish(MatchShapeAccumulator accumulator, RunState state,
            DiagnosticRequestBudget budget, CancellationToken token)
        {
            var status = state.Cancelled ? "cancelled" : state.Reason != null ? "aborted" : "completed";
            var unauthorizedList = state.Reason == "list_unauthorized" || state.Reason == "list_forbidden";
            var consumed = budget.TotalSends > 0 && !unauthorizedList;
            string failure = null, classification = null;
            var published = false;
            try
            {
                var report = accumulator.BuildReport(new MatchShapeRunSummary(status, status != "completed",
                    state.Stage ?? "none", state.Reason ?? (state.Cancelled ? "cancelled" : "none"),
                    budget.ListSends, budget.DetailSends, budget.Limit, consumed));
                classification = (string)report["classification"]?["result"];
                if (!MatchShapeReportSchema.IsValid(report)) failure = "report_schema_invalid";
                else
                {
                    var bytes = new UTF8Encoding(false).GetBytes(report.ToString(Formatting.Indented));
                    // A cancelled run still records its partial state; a completed run's publication honours cancellation.
                    _sink.Publish(bytes, state.Cancelled ? CancellationToken.None : token);
                    published = true;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                failure = "report_publish_cancelled";
                status = "cancelled";
            }
            catch { failure = failure ?? "report_publish_failed"; }
            return new MatchShapeRunResult(status, state.Reason ?? (status == "cancelled" ? "cancelled" : "none"),
                budget.ListSends, budget.DetailSends, published, failure ?? "none", classification ?? "undecidable", consumed);
        }

        /// <summary>Reads the key once for this run without echoing it; the value lives only in this call's headers.</summary>
        private string ReadKey()
        {
            try
            {
                var key = _keys.GetRequiredKey();
                return EnvironmentRiotApiKeyProvider.IsSafeKey(key) ? key.Trim() : null;
            }
            catch { return null; }
        }

        /// <summary>Requires both explicit confirmations; dialog failures count as a decline.</summary>
        private bool Confirm(MatchShapeDiagnosticInput input)
        {
            try
            {
                var summary = string.Format(CultureInfo.InvariantCulture,
                    "실제 Riot API 경기 형태 진단(1단계)을 실행합니다.\n\n" +
                    "지역 경로: {0}\n대상 큐: {1}\n세트 번호: {2}\n선택 카탈로그 릴리스: {3}\n" +
                    "요청 상한: 목록 1회(count={4}) + 상세 최대 {4}회 = 최대 {5}회, 요청마다 시도 1회, 간격 1.2초 이상\n" +
                    "429·401·403·404·5xx·전송 오류에서 즉시 중단하며 자동 재시도하지 않습니다.\n" +
                    "보고서는 비식별 집계만 Library/TftAnalyzerDiagnostics/match-shape/에 저장합니다.\n\n" +
                    "API 키를 갱신한 뒤 Unity를 재시작해 환경 변수가 반영되었습니까?",
                    input.Region, input.TargetQueueId, input.SetNumber, input.CatalogRelease, DetailLimit,
                    DetailLimit + DiagnosticRequestBudget.ListLimit);
                return _confirmation.ConfirmUnityRestartedAfterKeyRenewal(summary) &&
                       _confirmation.ConfirmNoCollectionInProgress();
            }
            catch { return false; }
        }

        private static MatchShapeRunResult Precondition(string reason) => MatchShapeRunResult.PreconditionFailed(reason);

        private sealed class RunState
        {
            public string Stage;
            public string Reason;
            public bool Cancelled;
            public bool Completed;
        }
    }
}
