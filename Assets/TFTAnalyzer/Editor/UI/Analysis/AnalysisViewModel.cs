using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.UI.Analysis
{
    public sealed class AnalysisViewModel : IDisposable
    {
        private readonly IAnalysisService _service;
        private CancellationTokenSource _operation;
        private int _generation;
        private bool _disposed;
        public event Action Changed;
        public StaticCatalog Catalog { get; private set; }
        public CatalogIdentity ActiveIdentity { get; private set; }
        public AnalysisSnapshot Snapshot { get; private set; }
        public AnalysisFilters Filters { get; private set; } = new AnalysisFilters();
        public IReadOnlyList<MetricRowViewModel> Rows { get; private set; } = Array.Empty<MetricRowViewModel>();
        public IReadOnlyList<MetricRowViewModel> Pairs { get; private set; } = Array.Empty<MetricRowViewModel>();
        public bool IsBusy => _operation != null;
        public bool CanRefresh => !_disposed && !IsBusy && Catalog != null && ActiveIdentity != null;
        public bool CanExport => !_disposed && !IsBusy && Snapshot != null && (Rows.Count != 0 || Pairs.Count != 0);
        public string StatusText { get; private set; } = "데이터 관리 탭에서 검증된 카탈로그를 선택해 주세요.";
        public string ErrorCode { get; private set; } = "";
        public AnalysisViewModel(IAnalysisService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }

        public void SetCatalog(StaticCatalog catalog, CatalogIdentity identity = null)
        {
            if (_disposed) return;
            // A legacy manifest has no identity: only the separately verified official-only pair is admitted.
            identity = identity ?? catalog?.Manifest.Identity;
            if (!AnalysisService.MatchesCatalog(catalog, identity)) { catalog = null; identity = null; }
            if (ReferenceEquals(Catalog, catalog) && AnalysisService.SameIdentity(ActiveIdentity, identity)) return;
            _generation++; _operation?.Cancel();
            Catalog = catalog; ActiveIdentity = identity;
            Snapshot = null; Rows = Array.Empty<MetricRowViewModel>(); Pairs = Array.Empty<MetricRowViewModel>();
            ErrorCode = "";
            StatusText = catalog == null ? "데이터 관리 탭에서 검증된 카탈로그를 선택해 주세요." : "로컬 집계 갱신을 눌러 저장된 익명 경기 기록을 읽고 집계 캐시를 재생성하세요.";
            Notify();
        }

        public void SetFilters(AnalysisFilters filters)
        {
            if (_disposed) return;
            Filters = filters ?? new AnalysisFilters(); ApplyFilters(); Notify();
        }

        public async Task RefreshAsync()
        {
            if (!CanRefresh) return;
            var operation = new CancellationTokenSource(); _operation = operation;
            var generation = ++_generation; var catalog = Catalog; var identity = ActiveIdentity;
            Snapshot = null; Rows = Array.Empty<MetricRowViewModel>(); Pairs = Array.Empty<MetricRowViewModel>();
            ErrorCode = ""; StatusText = "로컬 익명 경기 기록을 읽고 집계 캐시를 갱신하고 있습니다."; Notify();
            try
            {
                var snapshot = await _service.LoadAsync(catalog, identity, operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                if (!Current(generation, operation)) return;
                if (snapshot == null || !AnalysisService.SameIdentity(snapshot.Identity, identity)) throw new InvalidOperationException();
                Snapshot = snapshot; ApplyFilters();
            }
            catch (OperationCanceledException)
            {
                if (Current(generation, operation)) { ErrorCode = ""; StatusText = "로컬 집계 갱신을 취소했습니다."; }
            }
            catch (Exception)
            {
                if (Current(generation, operation))
                { ErrorCode = "analysis_load_failed"; StatusText = "로컬 집계를 읽지 못했습니다. 저장소를 확인한 뒤 다시 시도해 주세요."; }
            }
            finally
            {
                if (ReferenceEquals(_operation, operation)) _operation = null;
                operation.Dispose(); if (!_disposed) Notify();
            }
        }

        private bool Current(int generation, CancellationTokenSource operation) =>
            !_disposed && generation == _generation && ReferenceEquals(_operation, operation);

        private void ApplyFilters()
        {
            if (Snapshot == null) return;
            Rows = Array.AsReadOnly(Snapshot.AllRows.Where(x => ScopeMatches(x) && BoardMatches(x.First)).ToArray());
            Pairs = Array.AsReadOnly(Snapshot.AllPairs.Where(x => ScopeMatches(x) && (BoardMatches(x.First) || BoardMatches(x.Second))).ToArray());
            if (!IsBusy || ErrorCode.Length == 0)
                StatusText = Rows.Count == 0 && Pairs.Count == 0 ? "선택한 범위에 표시할 과거 관측 결과가 없습니다." :
                    "과거 관측 결과입니다. 지역·큐·티어 기록 라벨이 다른 범위는 합산하지 않습니다.";
        }
        private bool ScopeMatches(MetricRowViewModel row) =>
            Match(Filters.Region, row.Presentation.Scope.Region) && Match(Filters.Queue, row.Presentation.Scope.QueueId) && Match(Filters.Tier, row.Presentation.Scope.TierBucket);
        private bool BoardMatches(CompositionDescriptor board) => board != null &&
            Member(Filters.Unit, board.UnitIds) && Member(Filters.Trait, board.TraitIds) && Member(Filters.Item, board.ItemIds) && Member(Filters.Augment, board.AugmentIds);
        private static bool Match(string selected, string actual) => string.IsNullOrEmpty(selected) || string.Equals(selected, actual, StringComparison.Ordinal);
        private static bool Member(string selected, IReadOnlyList<string> values) => string.IsNullOrEmpty(selected) || values.Contains(selected, StringComparer.Ordinal);
        public void Cancel()
        {
            if (_disposed || _operation == null) return;
            _operation.Cancel(); StatusText = "로컬 집계 갱신을 취소하고 있습니다."; Notify();
        }
        private void Notify() { if (!_disposed) Changed?.Invoke(); }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _generation++; _operation?.Cancel(); Changed = null;
        }
    }
}
