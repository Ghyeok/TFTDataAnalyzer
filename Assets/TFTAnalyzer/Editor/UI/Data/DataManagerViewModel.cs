using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Diff;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;

namespace TFTAnalyzer.Editor.UI.Data
{
    public enum DataManagerState { Idle, Checking, AwaitingApproval, Importing, Succeeded, Failed, Cancelled }

    public sealed class DataManagerViewModel : IDisposable
    {
        private readonly IDataManagerService _service;
        private readonly Func<DateTimeOffset> _utcNow;
        private CancellationTokenSource _operation;
        private bool _disposed;
        public event Action Changed;
        public IReadOnlyList<PatchRowViewModel> Rows { get; private set; } = Array.Empty<PatchRowViewModel>();
        public IReadOnlyList<string> Versions { get; private set; } = Array.Empty<string>();
        public PatchKey InputPatch { get; private set; }
        public bool IncludeCommunityDragon { get; private set; }
        public DataManagerState State { get; private set; }
        public bool IsBusy => _operation != null;
        public string ErrorCode { get; private set; } = "";
        internal string LastImportFailureType { get; private set; } = "";
        internal string LastImportFailureStack { get; private set; } = "";
        public string StatusText { get; private set; } = "";
        public CatalogIdentity ActiveIdentity { get; private set; }
        public CatalogIdentity DiffBeforeIdentity { get; private set; }
        public CatalogIdentity DiffAfterIdentity { get; private set; }
        public CatalogDiff Diff { get; private set; }
        public StaticImportReportViewModel LastImportReport { get; private set; }

        public DataManagerViewModel(IDataManagerService service)
            : this(service, () => DateTimeOffset.UtcNow) { }

        public DataManagerViewModel(IDataManagerService service, Func<DateTimeOffset> utcNow)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            StatusText = "로컬 카탈로그를 선택하거나 가져올 패치를 입력해 주세요.";
            ReloadRows();
        }

        public bool TrySetInput(PatchKey patch, bool includeCommunityDragon)
        {
            if (_disposed || IsBusy) return false;
            InputPatch = patch;
            IncludeCommunityDragon = includeCommunityDragon;
            Notify();
            return true;
        }

        public async Task CheckForUpdatesAsync()
        {
            if (_disposed || IsBusy) return;
            var operation = Begin(DataManagerState.Checking, "사용 가능한 원본 버전을 확인하고 있습니다.");
            try
            {
                var versions = await _service.FetchVersionsAsync(operation.Token);
                if (_disposed) return;
                operation.Token.ThrowIfCancellationRequested();
                Versions = Array.AsReadOnly(versions.ToArray());
                SetStatus(DataManagerState.Idle, "버전 목록을 확인했습니다. 세트와 TFT 패치는 직접 입력해 주세요.");
            }
            catch (OperationCanceledException) { if (!_disposed) SetStatus(DataManagerState.Cancelled, "버전 확인을 취소했습니다."); }
            catch (Exception) { if (!_disposed) SetStatus(DataManagerState.Failed, "버전 목록을 가져오지 못했습니다. 다시 시도해 주세요.", "VersionCheckFailed"); }
            finally
            {
                IncludeCommunityDragon = false;
                End(operation);
            }
        }

        public async Task ImportAsync(Func<string, bool> confirm)
        {
            if (_disposed || IsBusy) return;
            LastImportFailureType = LastImportFailureStack = "";
            var operation = Begin(DataManagerState.AwaitingApproval, "가져올 카탈로그를 확인해 주세요.");
            var started = false;
            try
            {
                if (InputPatch == null || confirm == null) throw new ArgumentException();
                var request = new StaticImportRequest(new CatalogIdentity(InputPatch,
                    IncludeCommunityDragon ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly));
                var directory = _service.ResolveDestination(request);
                var patch = request.Patch;
                var summary = $"세트: {patch.SetId} (번호 {patch.SetNumber})\nTFT 패치: {patch.PatchId}\n" +
                              $"원본 게임 버전: {patch.SourceGameVersion}\n출처 프로필: {request.Identity.ProfileId}\n저장 위치: {directory}";
                var approved = confirm(summary);
                IncludeCommunityDragon = false;
                if (_disposed) return;
                if (!approved)
                {
                    SetStatus(DataManagerState.Cancelled, "가져오기를 승인하지 않아 다운로드하지 않았습니다.");
                    return;
                }
                operation.Token.ThrowIfCancellationRequested();
                if (!string.Equals(directory, _service.ResolveDestination(request), StringComparison.Ordinal))
                {
                    SetStatus(DataManagerState.Failed, "저장 위치가 변경되었습니다. 가져올 대상과 위치를 다시 확인해 주세요.", "DestinationChanged");
                    return;
                }
                var startedUtc = _utcNow();
                var pendingReport = StaticImportReportViewModel.FromResult(request.Identity,
                    request.IncludeCommunityDragon, startedUtc, null);
                started = true;
                LastImportReport = pendingReport;
                SetStatus(DataManagerState.Importing, "카탈로그를 가져오고 검증하고 있습니다.");
                var result = await _service.ImportAsync(request, operation.Token);
                if (_disposed) return;
                LastImportReport = StaticImportReportViewModel.FromResult(request.Identity,
                    request.IncludeCommunityDragon, startedUtc, result);
                // RunAsync's successful return means publication finished; late cancellation cannot undo it.
                if (!result.Published)
                    SetStatus(DataManagerState.Failed, "데이터 검증을 통과하지 못해 카탈로그를 게시하지 않았습니다.", "ValidationRejected");
                else
                {
                    if (ReloadRows())
                        SetStatus(DataManagerState.Succeeded, "카탈로그를 가져왔습니다. 로컬 목록에서 선택해 주세요.");
                    else
                        SetStatus(DataManagerState.Failed, "카탈로그는 저장되었으나 로컬 목록을 읽지 못했습니다. 자산 상태를 확인한 뒤 창을 다시 열어 주세요.", "LocalCatalogsUnavailable");
                }
            }
            catch (OperationCanceledException) { if (!_disposed) SetStatus(DataManagerState.Cancelled, "가져오기를 취소했습니다."); }
            catch (StaticImportRecoveryException recovery)
            {
                if (!_disposed)
                {
                    switch (recovery.RecoveryState)
                    {
                        case StaticImportRecoveryState.PublishedUnindexed:
                            SetStatus(DataManagerState.Failed, "카탈로그는 저장되었으나 목록에 반영되지 않았습니다. 같은 조건으로 다시 가져와 목록을 복구해 주세요.", "PublishedUnindexed");
                            break;
                        case StaticImportRecoveryState.PublishedIndexedRefreshPending:
                            SetStatus(DataManagerState.Failed, "카탈로그와 목록을 저장했습니다. Unity에서 자산을 새로 고쳐 주세요.", "PublishedIndexedRefreshPending");
                            break;
                        default:
                            SetStatus(DataManagerState.Failed, "카탈로그는 저장되었으나 목록 상태를 확인할 수 없습니다. 목록을 점검한 뒤 다시 가져와 주세요.", "PublishedIndexStateUncertain");
                            break;
                    }
                }
            }
            catch (Exception error)
            {
                LastImportFailureType = error.GetType().FullName;
                // Method frames only: retain the throwing location without messages, data, or file paths.
                try
                {
                    var frames = new System.Diagnostics.StackTrace(error, false).GetFrames();
                    LastImportFailureStack = frames == null ? "" : string.Join(Environment.NewLine,
                        frames.Select(frame => frame.GetMethod()).Where(method => method != null)
                            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}"));
                }
                catch (Exception) { LastImportFailureStack = ""; }
                if (!_disposed) SetStatus(DataManagerState.Failed,
                    started ? "가져오지 못했습니다. 입력과 연결 상태를 확인한 뒤 다시 시도해 주세요." : "세트·패치·원본 버전과 저장 위치를 확인해 주세요.",
                    started ? "ImportFailed" : "InvalidInput");
            }
            finally
            {
                IncludeCommunityDragon = false;
                End(operation);
            }
        }

        public void CancelImport()
        {
            if (!_disposed) _operation?.Cancel();
        }

        public bool SelectActiveCatalog(CatalogIdentity identity)
        {
            if (_disposed || IsBusy || Find(identity) == null) return false;
            ActiveIdentity = identity;
            Notify();
            return true;
        }

        public bool Compare(CatalogIdentity before, CatalogIdentity after)
        {
            if (_disposed || IsBusy) return false;
            var previous = Find(before);
            var current = Find(after);
            if (previous == null || current == null || before.Equals(after)) return false;
            Diff = new CatalogDiffer().Compare(previous.Catalog, current.Catalog);
            DiffBeforeIdentity = before;
            DiffAfterIdentity = after;
            Notify();
            return true;
        }

        private PatchRowViewModel Find(CatalogIdentity identity) =>
            identity == null ? null : Rows.FirstOrDefault(row => row.IsSelectable && row.Identity.Equals(identity));

        private bool ReloadRows()
        {
            var loaded = true;
            try { Rows = Array.AsReadOnly(_service.LoadLocalCatalogs().ToArray()); }
            catch (Exception)
            {
                loaded = false;
                Rows = Array.Empty<PatchRowViewModel>();
                SetStatus(DataManagerState.Failed, "로컬 카탈로그 목록을 읽지 못했습니다. 자산 상태를 확인해 주세요.", "LocalCatalogsUnavailable");
            }
            if (Find(ActiveIdentity) == null) ActiveIdentity = null;
            Diff = null;
            DiffBeforeIdentity = DiffAfterIdentity = null;
            return loaded;
        }

        private CancellationTokenSource Begin(DataManagerState state, string status)
        {
            _operation = new CancellationTokenSource();
            SetStatus(state, status);
            return _operation;
        }

        private void End(CancellationTokenSource operation)
        {
            if (ReferenceEquals(_operation, operation)) _operation = null;
            operation.Dispose();
            Notify();
        }

        private void SetStatus(DataManagerState state, string text, string error = "")
        {
            State = state;
            StatusText = text;
            ErrorCode = error;
            Notify();
        }

        private void Notify() { if (!_disposed) Changed?.Invoke(); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IncludeCommunityDragon = false;
            Changed = null;
            _operation?.Cancel();
        }
    }
}
