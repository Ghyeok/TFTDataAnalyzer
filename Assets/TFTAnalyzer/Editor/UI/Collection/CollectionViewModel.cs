using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.UI.Collection
{
    public enum CollectionState { Idle, AwaitingApproval, Running, Cancelling, Succeeded, Failed, Cancelled }

    public sealed class CollectionViewModel : IDisposable
    {
        private readonly ICollectionService _service;
        private readonly SynchronizationContext _context;
        private CancellationTokenSource _operation;
        private StaticCatalog _selectedCatalog, _runningCatalog;
        private CatalogIdentity _selectedIdentity, _runningIdentity;
        private bool _disposed, _inputValid = true;
        private string _inputPlatform = "KR", _inputTier = "ALL";
        private int _inputQueue = 1100, _inputMaximum = 100;
        public event Action Changed;
        public bool ApiKeyConfigured { get; private set; }
        public bool SeedConfigured { get; private set; }
        public bool IsBusy => _operation != null;
        public bool CanStart => !_disposed && !IsBusy && _inputValid && Catalog != null && ActiveIdentity != null && ApiKeyConfigured && SeedConfigured;
        public CollectionState State { get; private set; }
        public string StatusText { get; private set; } = "데이터 관리 탭에서 검증된 카탈로그를 선택해 주세요.";
        public string ErrorCode { get; private set; } = "";
        public StaticCatalog Catalog => IsBusy ? _runningCatalog : _selectedCatalog;
        public CatalogIdentity ActiveIdentity => IsBusy ? _runningIdentity : _selectedIdentity;
        public string Platform { get; private set; } = "KR";
        public int QueueId { get; private set; } = 1100;
        public string TierBucket { get; private set; } = "ALL";
        public int MaximumMatches { get; private set; } = 100;
        public CollectionProgressViewModel Progress { get; private set; } = new CollectionProgressViewModel();

        internal int DraftQueueId => _inputQueue;
        internal int DraftMaximumMatches => _inputMaximum;

        internal string GetDraftPlatform(IList<string> allowedChoices)
        {
            var platform = (_inputPlatform ?? "").Trim().ToUpperInvariant();
            return allowedChoices.Contains(platform) ? platform : Platform;
        }

        internal string GetDraftTier(IList<string> allowedChoices) =>
            allowedChoices.Contains(_inputTier) ? _inputTier : TierBucket;

        public CollectionViewModel(ICollectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _context = SynchronizationContext.Current;
            RefreshConfiguration();
        }

        public void SetCatalog(StaticCatalog catalog, CatalogIdentity identity = null)
        {
            if (_disposed) return;
            // Schema 1 has no profile metadata: accept only the explicitly supplied verified row identity.
            identity = identity ?? catalog?.Manifest.Identity;
            if (!MatchesCatalog(catalog, identity))
            {
                catalog = null;
                identity = null;
            }
            if (ReferenceEquals(_selectedCatalog, catalog) && Equals(_selectedIdentity, identity)) return;
            _selectedCatalog = catalog;
            _selectedIdentity = identity;
            if (!IsBusy) ValidateLatestInput();
            Notify();
        }

        private static bool MatchesCatalog(StaticCatalog catalog, CatalogIdentity identity)
        {
            if (catalog == null || identity == null) return false;
            var patch = catalog.Manifest.Patch;
            var supplied = identity.Patch;
            // CatalogIdentity.Equals deliberately omits SetNumber; admission must validate it too.
            if (patch.SetNumber != supplied.SetNumber ||
                !string.Equals(patch.SetId, supplied.SetId, StringComparison.Ordinal) ||
                !string.Equals(patch.PatchId, supplied.PatchId, StringComparison.Ordinal) ||
                !string.Equals(patch.SourceGameVersion, supplied.SourceGameVersion, StringComparison.Ordinal)) return false;
            return catalog.Manifest.SchemaVersion == 1
                ? identity.SourceProfile == CatalogSourceProfile.OfficialOnly
                : identity.Equals(catalog.Manifest.Identity);
        }

        public bool TrySetInput(string platform, int queueId, string tierBucket, int maximumMatches)
        {
            if (_disposed || IsBusy) return false;
            _inputPlatform = platform;
            _inputQueue = queueId;
            _inputTier = tierBucket;
            _inputMaximum = maximumMatches;
            ValidateLatestInput();
            Notify();
            return _inputValid;
        }

        private void ValidateLatestInput()
        {
            try
            {
                // Reuse the backend's route, tier and cap allowlists without exposing rejected values.
                var request = new MatchCollectionRequest(_inputPlatform, _selectedIdentity,
                    _inputQueue, _inputTier, _inputMaximum);
                Platform = _inputPlatform.Trim().ToUpperInvariant();
                QueueId = request.QueueId;
                TierBucket = request.TierBucket;
                MaximumMatches = request.MaximumUniqueMatches;
                _inputValid = true;
            }
            catch (ArgumentException) { _inputValid = false; }
        }

        public void RefreshConfiguration()
        {
            if (_disposed || IsBusy) return;
            try { ApiKeyConfigured = _service.ApiKeyConfigured; } catch { ApiKeyConfigured = false; }
            try { SeedConfigured = _service.SeedConfigured; } catch { SeedConfigured = false; }
            Notify();
        }

        public async Task StartAsync(Func<string, bool> confirm)
        {
            if (_disposed || IsBusy) return;
            var catalog = _selectedCatalog;
            var identity = _selectedIdentity;
            MatchCollectionRequest request;
            try
            {
                if (!_inputValid || catalog == null || identity == null || confirm == null) throw new ArgumentException();
                request = new MatchCollectionRequest(Platform, identity, QueueId, TierBucket, MaximumMatches);
            }
            catch (ArgumentException)
            {
                SetStatus(CollectionState.Failed, "카탈로그와 지역·큐·티어 기록 라벨·상한(1–1,000)을 확인해 주세요.", "InvalidInput");
                return;
            }
            RefreshConfiguration();
            if (!ApiKeyConfigured || !SeedConfigured)
            {
                SetStatus(CollectionState.Failed, "API 키와 사용자 제공 시드의 환경 설정을 확인하고 설정 확인을 눌러 주세요.", "ConfigurationUnavailable");
                return;
            }
            var operation = new CancellationTokenSource();
            _operation = operation;
            _runningCatalog = catalog;
            _runningIdentity = identity;
            Progress = new CollectionProgressViewModel();
            var started = false;
            var acceptsProgress = true;
            try
            {
                SetStatus(CollectionState.AwaitingApproval, "수집 조건을 확인해 주세요.");
                var patch = request.ActiveCatalog.Patch;
                var summary = $"지역/경로: {Platform} / {request.Region}\n" +
                              $"세트: {patch.SetId} (번호 {patch.SetNumber})\nTFT 패치: {patch.PatchId}\n" +
                              $"원본 게임 버전: {patch.SourceGameVersion}\n출처 프로필: {request.ActiveCatalog.ProfileId}\n" +
                              $"큐: {request.QueueId}\n티어 기록 라벨: {request.TierBucket} (실제 랭크를 검증하지 않음)\n" +
                              $"고유 경기 상세 시도 상한: {request.MaximumUniqueMatches}\n" +
                              "사용자 제공 시드만 조회하며 다른 참가자로 시드를 확장하지 않습니다.\n" +
                              "상한은 저장 성공 수가 아닌 고유 경기 상세 조회 시도 수입니다. 필터·중복·실패로 저장 수는 작을 수 있습니다.";
                var approved = confirm(summary);
                if (_disposed) return;
                if (!approved)
                {
                    SetStatus(CollectionState.Cancelled, "수집을 승인하지 않아 요청하지 않았습니다.");
                    return;
                }
                operation.Token.ThrowIfCancellationRequested();
                started = true;
                SetStatus(CollectionState.Running, "과거 경기를 수집하고 익명 관측을 저장하고 있습니다.");
                var reporter = new RunProgress(_context, value =>
                {
                    if (_disposed || !acceptsProgress || !ReferenceEquals(_operation, operation) || value?.Summary == null) return;
                    Progress = new CollectionProgressViewModel(value.Summary);
                    Notify();
                });
                var result = await _service.CollectAsync(catalog, request, reporter, operation.Token);
                acceptsProgress = false;
                if (_disposed) return;
                // The service's settled result remains authoritative after a late cancellation request.
                ApplyResult(result ?? throw new InvalidOperationException());
            }
            catch (OperationCanceledException)
            {
                acceptsProgress = false;
                if (!_disposed)
                {
                    Progress = new CollectionProgressViewModel(new MatchCollectionSummary(Progress.Requested,
                        Progress.Fetched, Progress.Stored, Progress.Duplicate, Progress.Filtered, Progress.Failed,
                        true, Progress.IdPages, Progress.ScanLimitReached, 0, Progress.RetryAfterUtc));
                    SetStatus(CollectionState.Cancelled, "수집을 취소했습니다. 이미 저장한 익명 관측은 보존됩니다.");
                }
            }
            catch (RiotApiException error)
            {
                acceptsProgress = false;
                if (!_disposed) ApplyResult(new MatchCollectionSummary(Progress.Requested, Progress.Fetched,
                    Progress.Stored, Progress.Duplicate, Progress.Filtered, Progress.Failed + 1, false,
                    Progress.IdPages, false, error.StatusCode >= 100 && error.StatusCode <= 599 ? (int)error.StatusCode : 0,
                    error.RetryAfterUtc));
            }
            catch (Exception)
            {
                acceptsProgress = false;
                if (!_disposed) SetStatus(CollectionState.Failed,
                    started ? "수집을 완료하지 못했습니다. 저장한 익명 관측은 보존됩니다. 설정과 연결을 확인하고 수동으로 다시 시도해 주세요."
                        : "수집 조건 확인을 완료하지 못했습니다. 조건을 확인한 뒤 다시 시작해 주세요.",
                    started ? "CollectionFailed" : "ConfirmationFailed");
            }
            finally
            {
                acceptsProgress = false;
                if (ReferenceEquals(_operation, operation)) _operation = null;
                _runningCatalog = null;
                _runningIdentity = null;
                operation.Dispose();
                if (!_disposed) ValidateLatestInput();
                Notify();
            }
        }

        private void ApplyResult(MatchCollectionSummary result)
        {
            Progress = new CollectionProgressViewModel(result);
            if (result.Cancelled)
                SetStatus(CollectionState.Cancelled, "수집을 취소했습니다. 이미 저장한 익명 관측은 보존됩니다.");
            else if (result.StatusCode == 401)
                SetStatus(CollectionState.Failed, "API 키 인증에 실패했습니다(401). 환경 설정을 확인한 뒤 수동으로 다시 시도해 주세요.", "Unauthorized");
            else if (result.StatusCode == 403)
                SetStatus(CollectionState.Failed, "API 접근이 거부되었습니다(403). 키의 권한을 확인한 뒤 수동으로 다시 시도해 주세요.", "Forbidden");
            else if (result.StatusCode == 429)
                SetStatus(CollectionState.Failed, "요청 제한으로 수집을 중단했습니다(429). " +
                    (result.RetryAfterUtc.HasValue ? result.RetryAfterUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) + " 이후 " : "제한을 확인한 뒤 ") +
                    "수동으로 다시 시도해 주세요. 자동으로 재개하지 않습니다.", "RateLimited");
            else if (result.Failed > 0)
                SetStatus(CollectionState.Failed, "일부 요청 또는 저장이 실패하여 수집이 종료되었습니다. 저장 수를 확인하고 연결·설정을 점검한 뒤 수동으로 다시 시도해 주세요.", "PartialFailure");
            else if (result.ScanLimitReached)
                SetStatus(CollectionState.Succeeded, "경기 ID 탐색 상한에 도달해 중단했습니다. 상세 시도 상한에 도달한 것은 아닙니다. 저장 수를 확인해 주세요.", "ScanLimitReached");
            else
                SetStatus(CollectionState.Succeeded, "수집이 완료되었습니다. 시도·필터·중복과 실제 저장 수를 확인해 주세요.");
        }

        public void Cancel()
        {
            if (_disposed || _operation == null || _operation.IsCancellationRequested) return;
            var operation = _operation;
            SetStatus(CollectionState.Cancelling, "취소를 요청했습니다. 진행 중인 작업이 끝날 때까지 기다려 주세요.");
            try { operation.Cancel(); } catch { }
        }

        private void SetStatus(CollectionState state, string text, string code = "")
        {
            State = state;
            StatusText = text;
            ErrorCode = code;
            Notify();
        }

        private void Notify() { if (!_disposed) Changed?.Invoke(); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Changed = null;
            try { _operation?.Cancel(); } catch { }
        }

        private sealed class RunProgress : IProgress<CollectionProgress>
        {
            private readonly SynchronizationContext _context;
            private readonly Action<CollectionProgress> _report;
            internal RunProgress(SynchronizationContext context, Action<CollectionProgress> report)
            { _context = context; _report = report; }
            public void Report(CollectionProgress value)
            {
                if (_context == null || ReferenceEquals(SynchronizationContext.Current, _context)) _report(value);
                else _context.Post(_ => _report(value), null);
            }
        }
    }
}
