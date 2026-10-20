using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;

namespace TFTAnalyzer.Editor.Ingestion
{
    internal sealed class StaticVersionCheckScheduler : IDisposable
    {
        internal event Action Changed;
        private readonly DataDragonAdapter _adapter;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly Func<string> _importedVersion;
        private readonly string _statePath;
        private readonly TimeSpan _timeout;
        private readonly object _gate = new object();
        private Task _inFlight;
        private CancellationTokenSource _pending;
        private int _generation;
        private bool _disposed;
        internal string LatestVersion { get; private set; } = "";
        internal string Notice
        {
            get
            {
                string imported;
                try { imported = _importedVersion(); } catch { return ""; }
                return NoticeFor(imported);
            }
        }
        internal string NoticeFor(string imported) =>
            IsVersion(imported) && IsVersion(LatestVersion) && imported != LatestVersion
                ? $"새 원본 게임 버전 {LatestVersion}이 있습니다. 마지막 가져오기 원본 버전은 {imported}입니다. 원하면 카탈로그 가져오기를 직접 실행해 주세요."
                : "";

        internal StaticVersionCheckScheduler(IHttpTransport transport, Func<DateTimeOffset> utcNow,
            string projectRoot, Func<string> importedVersion, TimeSpan? timeout = null)
        {
            _adapter = new DataDragonAdapter(transport ?? throw new ArgumentNullException(nameof(transport)));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _importedVersion = importedVersion ?? throw new ArgumentNullException(nameof(importedVersion));
            _statePath = Path.Combine(new AnalyzerStorePaths(projectRoot).RootDirectory, "version-check.json");
            _timeout = timeout ?? TimeSpan.FromSeconds(15);
            if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        internal Task CheckAsync(CancellationToken token)
        {
            lock (_gate)
            {
                if (_disposed) return Task.CompletedTask;
                if (_inFlight != null && !_inFlight.IsCompleted) return _inFlight;
                if (token.IsCancellationRequested) return Task.CompletedTask;
                _inFlight = RunAsync(token, _generation);
                return _inFlight;
            }
        }

        private async Task RunAsync(CancellationToken token, int generation)
        {
            CancellationTokenSource cancellation = null;
            try
            {
                var now = _utcNow().ToUniversalTime();
                var state = ReadState();
                token.ThrowIfCancellationRequested();
                if (state != null && (!state.Valid || state.CheckedUtc > now))
                {
                    SaveState(now, state.SeenVersion);
                    var changed = LatestVersion != state.SeenVersion;
                    LatestVersion = state.SeenVersion;
                    if (changed) Changed?.Invoke();
                    return;
                }
                if (state != null)
                {
                    var changed = LatestVersion != state.SeenVersion;
                    LatestVersion = state.SeenVersion;
                    if (changed) Changed?.Invoke();
                    if (now - state.CheckedUtc < TimeSpan.FromHours(24))
                    {
                        return;
                    }
                }
                token.ThrowIfCancellationRequested();
                var previous = state?.SeenVersion ?? "";
                SaveState(now, previous);
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                _pending = cancellation;
                cancellation.CancelAfter(_timeout);
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellation.Token.Register(() => cancelled.TrySetResult(true)))
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var request = _adapter.FetchVersionsAsync(cancellation.Token);
                    if (await Task.WhenAny(request, cancelled.Task) != request)
                    {
                        ObserveLateFailure(request);
                        return;
                    }
                    var versions = await request;
                    if (_disposed || generation != _generation || cancellation.IsCancellationRequested) return;
                    var latest = versions.FirstOrDefault(IsVersion);
                    if (latest == null) return;
                    SaveState(now, latest);
                    if (_disposed || generation != _generation || cancellation.IsCancellationRequested) return;
                    LatestVersion = latest;
                    Changed?.Invoke();
                }
            }
            // Editor initialization has no error surface containing network bodies or private filesystem paths.
            catch (Exception) { }
            finally
            {
                if (ReferenceEquals(_pending, cancellation)) _pending = null;
                cancellation?.Dispose();
            }
        }

        private static void ObserveLateFailure(Task request) => request.ContinueWith(
            failed => { var observed = failed.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        private State ReadState()
        {
            AnalyzerStorePaths.RejectReparsePoints(_statePath);
            string json;
            try { json = File.ReadAllText(_statePath, new UTF8Encoding(false, true)); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (DecoderFallbackException) { return new State(); }
            JObject document;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                {
                    document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) return new State();
                }
            }
            catch (JsonException) { return new State(); }
            var seen = document["lastSeenVersion"]?.Type == JTokenType.String ? document.Value<string>("lastSeenVersion") : "";
            var state = new State { SeenVersion = IsVersion(seen) ? seen : "" };
            var rawTime = document["lastCheckedUtc"]?.Type == JTokenType.String ? document.Value<string>("lastCheckedUtc") : null;
            state.Valid = document.Count == 2 && document["lastSeenVersion"]?.Type == JTokenType.String &&
                (seen == "" || IsVersion(seen)) && rawTime != null &&
                (rawTime.EndsWith("Z", StringComparison.Ordinal) || rawTime.EndsWith("+00:00", StringComparison.Ordinal)) &&
                DateTimeOffset.TryParse(rawTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out state.CheckedUtc) && state.CheckedUtc.Offset == TimeSpan.Zero;
            return state;
        }

        private void SaveState(DateTimeOffset time, string seen)
        {
            AnalyzerStorePaths.RejectReparsePoints(_statePath);
            var directory = Path.GetDirectoryName(_statePath);
            Directory.CreateDirectory(directory);
            var staging = _statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                AnalyzerStorePaths.RejectReparsePoints(staging);
                var json = new JObject { ["lastCheckedUtc"] = time.ToString("O", CultureInfo.InvariantCulture), ["lastSeenVersion"] = seen };
                var bytes = new UTF8Encoding(false, true).GetBytes(json.ToString(Formatting.None));
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                AnalyzerStorePaths.RejectReparsePoints(staging);
                AnalyzerStorePaths.RejectReparsePoints(_statePath);
                if (File.Exists(_statePath)) File.Replace(staging, _statePath, null); else File.Move(staging, _statePath);
            }
            finally
            {
                try { AnalyzerStorePaths.RejectReparsePoints(staging); if (File.Exists(staging)) File.Delete(staging); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static bool IsVersion(string value) => value != null && value.Length <= 64 &&
            Regex.IsMatch(value, @"\A[0-9]{1,9}\.[0-9]{1,9}\.[0-9]{1,9}(?:\.[0-9]{1,9})?\z");

        internal void CancelPending()
        {
            lock (_gate)
            {
                _generation++;
                _pending?.Cancel();
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelPending();
            Changed = null;
        }
        private sealed class State
        {
            internal bool Valid;
            internal DateTimeOffset CheckedUtc;
            internal string SeenVersion = "";
        }
    }

    internal interface IStaticVersionCheckHost
    {
        bool IsBatchMode { get; }
        bool IsPlaying { get; }
        bool IsBusy { get; }
        bool IsTestRunning { get; }
        event Action StateChanged;
        event Action Reloading;
        void Schedule(Action callback);
        void Unschedule(Action callback);
    }

    internal sealed class StaticVersionCheckBootstrap : IDisposable
    {
        private readonly StaticVersionCheckScheduler _scheduler;
        private readonly IStaticVersionCheckHost _host;
        private bool _started, _queued, _disposed;
        internal StaticVersionCheckBootstrap(StaticVersionCheckScheduler scheduler, IStaticVersionCheckHost host)
        { _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler)); _host = host ?? throw new ArgumentNullException(nameof(host)); }
        internal void Start()
        {
            if (_started || _disposed) return;
            _started = true;
            _host.StateChanged += OnStateChanged;
            _host.Reloading += Dispose;
            Queue();
        }
        private void Queue()
        {
            if (_disposed || _queued || _host.IsBatchMode) return;
            _queued = true;
            _host.Schedule(Run);
        }
        private void OnStateChanged()
        {
            if (_disposed) return;
            if (_host.IsTestRunning || _host.IsPlaying || _host.IsBatchMode)
            {
                _host.Unschedule(Run); _queued = false;
                _scheduler.CancelPending();
                return;
            }
            Queue();
        }
        private async void Run()
        {
            _host.Unschedule(Run); _queued = false;
            if (_disposed || _host.IsBatchMode || _host.IsPlaying || _host.IsBusy || _host.IsTestRunning) return;
            await _scheduler.CheckAsync(CancellationToken.None);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _host.Unschedule(Run); _queued = false;
            _host.StateChanged -= OnStateChanged;
            _host.Reloading -= Dispose;
            _scheduler.CancelPending();
        }
    }
}
