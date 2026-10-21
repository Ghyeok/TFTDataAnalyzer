using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Connects bounded user-seed history requests to immediate anonymization and durable anonymous storage.</summary>
    public sealed class MatchCollectionService
    {
        private const int PageSize = 100;
        private const int MaximumIdPages = 10;
        private readonly StaticCatalog _catalog;
        private readonly ObservationStore _store;
        private readonly RiotApiClient _client;
        private readonly EnvironmentSeedPuuidProvider _seeds;
        private readonly Action<CollectionQuarantineReport> _saveReport;

        /// <summary>Uses the existing key-safe client and store; constructing a service causes no network or environment reads.</summary>
        public MatchCollectionService(StaticCatalog catalog, ObservationStore store, RiotApiClient client = null,
            EnvironmentSeedPuuidProvider seeds = null)
            : this(catalog, store, client, seeds, null) { }

        /// <summary>Injects only the anonymous report sink for deterministic publication-failure tests.</summary>
        internal MatchCollectionService(StaticCatalog catalog, ObservationStore store, RiotApiClient client,
            EnvironmentSeedPuuidProvider seeds, Action<CollectionQuarantineReport> saveReport)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _client = client ?? new RiotApiClient();
            _seeds = seeds ?? new EnvironmentSeedPuuidProvider();
            _saveReport = saveReport ?? new CollectionQuarantineReportStore(_store.Paths).Save;
        }

        /// <summary>Attempts at most the requested unique matches and ten ID pages; cancellation preserves committed records.</summary>
        public async Task<MatchCollectionSummary> CollectAsync(MatchCollectionRequest request,
            IProgress<CollectionProgress> progress, CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var anonymizer = new MatchAnonymizer(_catalog, request.ActiveCatalog);
            var state = new Counts();
            var context = CollectionQuarantineReport.ContextHash(_catalog, request);
            try { await CollectCoreAsync(request, anonymizer, state, progress, token); }
            catch (OperationCanceledException) { state.Cancelled = true; }
            catch { if (token.IsCancellationRequested) state.Cancelled = true; else state.Failed++; }
            finally
            {
                if (state.Quarantine.QuarantinedMatches > 0)
                {
                    try
                    {
                        _saveReport(state.Quarantine.Freeze(context, DateTime.UtcNow));
                        state.ReportSaved = true;
                    }
                    catch { state.ReportFailed = true; }
                }
                Notify(progress, state);
            }
            // Publish after report persistence; returns inside the loop must not capture a stale summary.
            return state.Snapshot();
        }

        /// <summary>Runs the existing bounded collection loop without publishing a premature terminal summary.</summary>
        private async Task CollectCoreAsync(MatchCollectionRequest request, MatchAnonymizer anonymizer, Counts state,
            IProgress<CollectionProgress> progress, CancellationToken token)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            token.ThrowIfCancellationRequested();
            var seeds = _seeds.GetSeeds();
            foreach (var seed in seeds)
            {
                for (var start = 0; ; start += PageSize)
                {
                    token.ThrowIfCancellationRequested();
                    if (state.Requested >= request.MaximumUniqueMatches) return;
                    if (state.IdPages >= MaximumIdPages)
                    {
                        state.ScanLimitReached = true;
                        return;
                    }
                    IReadOnlyList<string> ids;
                    try
                    {
                        state.IdPages++;
                        ids = await _client.GetMatchIdsAsync(request.Region, seed, start, PageSize, token);
                    }
                    catch (RiotApiException error)
                    {
                        token.ThrowIfCancellationRequested();
                        var stop = state.Record(error);
                        Notify(progress, state);
                        if (stop) return;
                        break;
                    }
                    foreach (var id in ids)
                    {
                        token.ThrowIfCancellationRequested();
                        if (state.Requested >= request.MaximumUniqueMatches) return;
                        if (!seen.Add(id))
                        {
                            state.Duplicate++;
                            Notify(progress, state);
                            continue;
                        }
                        state.Requested++;
                        if (await CollectOne(id, request, anonymizer, state, token))
                        {
                            Notify(progress, state);
                            return;
                        }
                        Notify(progress, state);
                        token.ThrowIfCancellationRequested();
                    }
                    if (ids.Count < PageSize) break;
                }
            }
        }

        /// <summary>Owns one response through parse/filter/commit and discards every raw value before reporting progress.</summary>
        private async Task<bool> CollectOne(string id, MatchCollectionRequest request, MatchAnonymizer anonymizer,
            Counts state, CancellationToken token)
        {
            try
            {
                using (var stream = await _client.GetMatchStreamAsync(request.Region, id, token))
                using (token.Register(() => Release(stream)))
                {
                    state.Fetched++;
                    token.ThrowIfCancellationRequested();
                    var result = anonymizer.Read(stream, request.Region, request.TierBucket);
                    token.ThrowIfCancellationRequested();
                    if (!result.IsAccepted)
                    {
                        state.Filtered++;
                        return state.Quarantine.Add(result.UnknownStaticIds);
                    }
                    var observation = result.Observation;
                    if (!string.Equals(observation.MatchHash, ContentHasher.Sha256Hex("match-v1|" + id), StringComparison.Ordinal))
                    {
                        state.Failed++;
                        return false;
                    }
                    if (observation.Scope.QueueId != request.QueueId.ToString(CultureInfo.InvariantCulture))
                    {
                        state.Filtered++;
                        return false;
                    }
                    if (await _store.AppendIfNewAsync(observation, token)) state.Stored++;
                    else state.Duplicate++;
                }
                return false;
            }
            catch (OperationCanceledException) { throw; }
            catch (MatchPayloadException error)
            {
                token.ThrowIfCancellationRequested();
                if (error.Code == "catalog_mismatch") state.Filtered++; else state.Failed++;
                return false;
            }
            catch (RiotApiException error)
            {
                token.ThrowIfCancellationRequested();
                return state.Record(error);
            }
            catch
            {
                token.ThrowIfCancellationRequested();
                state.Failed++;
                // Storage or cleanup failures stop the run; never keep writing into an uncertain store.
                return true;
            }
        }

        /// <summary>Interrupts an active stream without exposing arbitrary cleanup diagnostics.</summary>
        private static void Release(Stream stream)
        {
            try { stream.Dispose(); } catch { }
        }

        /// <summary>Observer failures do not undo a commit or expose exception contents.</summary>
        private static void Notify(IProgress<CollectionProgress> progress, Counts state)
        {
            try { progress?.Report(new CollectionProgress(state.Snapshot())); } catch { }
        }

        private sealed class Counts
        {
            internal int Requested, Fetched, Stored, Duplicate, Filtered, Failed, IdPages, StatusCode;
            internal bool Cancelled, ScanLimitReached;
            internal DateTimeOffset? RetryAfterUtc;
            internal readonly CollectionQuarantineAccumulator Quarantine = new CollectionQuarantineAccumulator();
            internal bool ReportSaved, ReportFailed;

            /// <summary>Retains controlled HTTP diagnostics and stops on credential, authorization or quota failures.</summary>
            internal bool Record(RiotApiException error)
            {
                Failed++;
                StatusCode = error.StatusCode >= 100 && error.StatusCode <= 599 ? (int)error.StatusCode : 0;
                RetryAfterUtc = error.RetryAfterUtc;
                return error.Code == "api_key_unavailable" || error.Code == "rate_limited" ||
                       error.StatusCode == 401 || error.StatusCode == 403;
            }

            /// <summary>Copies counters without raw identifiers or mutable references.</summary>
            internal MatchCollectionSummary Snapshot() => new MatchCollectionSummary(Requested, Fetched, Stored,
                Duplicate, Filtered, Failed, Cancelled, IdPages, ScanLimitReached, StatusCode, RetryAfterUtc,
                Quarantine.QuarantinedMatches, ReportSaved, ReportFailed, Quarantine.Truncated);
        }
    }
}
