using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.UI.Analysis
{
    public sealed class AnalysisViewModel : IDisposable
    {
        private readonly IAnalysisService _service;
        private CancellationTokenSource _operation;
        private AnalysisSnapshot _baseSnapshot;
        private int _generation;
        private bool _disposed;
        public event Action Changed;
        public StaticCatalog Catalog { get; private set; }
        public CatalogIdentity ActiveIdentity { get; private set; }
        public AnalysisSnapshot Snapshot { get; private set; }
        public AnalysisFilters Filters { get; private set; } = new AnalysisFilters();
        public IReadOnlyList<IReadOnlyList<string>> FilterOptions => _baseSnapshot?.FilterOptions;
        public IReadOnlyList<MetricRowViewModel> Rows { get; private set; } = Array.Empty<MetricRowViewModel>();
        public IReadOnlyList<MetricRowViewModel> Pairs { get; private set; } = Array.Empty<MetricRowViewModel>();
        public bool IsBusy => _operation != null;
        public int MinimumSamples { get; private set; } = MetricPolicy.MinimumSampleFloor;
        private bool ValidMinimum => MinimumSamples >= MetricPolicy.MinimumSampleFloor;
        public string MinimumStatusText => !ValidMinimum ? "최소 표본 수는 100 이상으로 입력해 주세요." :
            Snapshot == null ? "100 이상으로 설정할 수 있습니다. 기준 변경 후 로컬 집계를 갱신해 주세요." :
            $"현재 결과의 적용 기준: {Snapshot.MinimumSamples}건 이상 (조합: 참가자 관측, 상대 배치: 비교 가능 로비)";
        public bool CanRefresh => !_disposed && !IsBusy && ValidMinimum && Catalog != null && ActiveIdentity != null;
        public bool CanExport => !_disposed && !IsBusy && ValidMinimum && Snapshot != null &&
            Snapshot.MinimumSamples >= MinimumSamples && (Rows.Count != 0 || Pairs.Count != 0);
        public string StatusText { get; private set; } = "데이터 관리 탭에서 검증된 카탈로그를 선택해 주세요.";
        public string ErrorCode { get; private set; } = "";
        public AnalysisViewModel(IAnalysisService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); }

        public void SetMinimumSamples(int minimumSamples)
        {
            if (_disposed || minimumSamples == MinimumSamples) return;
            MinimumSamples = minimumSamples;
            _generation++; _operation?.Cancel();
            _baseSnapshot = null; ClearResults(); SetReadyStatus(); Notify();
        }

        private void ClearResults()
        { Snapshot = null; Rows = Array.Empty<MetricRowViewModel>(); Pairs = Array.Empty<MetricRowViewModel>(); }

        private void SetReadyStatus()
        {
            ErrorCode = ValidMinimum ? "" : "analysis_minimum_samples_invalid";
            StatusText = !ValidMinimum ? MinimumStatusText : Catalog == null ?
                "데이터 관리 탭에서 검증된 카탈로그를 선택해 주세요." :
                "로컬 집계 갱신을 눌러 저장된 익명 경기 기록에 현재 기준을 적용하세요.";
        }

        public void SetCatalog(StaticCatalog catalog, CatalogIdentity identity = null)
        {
            if (_disposed) return;
            // A legacy manifest has no identity: only the separately verified official-only pair is admitted.
            identity = identity ?? catalog?.Manifest.Identity;
            if (!AnalysisService.MatchesCatalog(catalog, identity)) { catalog = null; identity = null; }
            if (ReferenceEquals(Catalog, catalog) && AnalysisService.SameIdentity(ActiveIdentity, identity)) return;
            _generation++; _operation?.Cancel();
            Catalog = catalog; ActiveIdentity = identity;
            _baseSnapshot = null; ClearResults(); SetReadyStatus();
            Notify();
        }

        public void SetFilters(AnalysisFilters filters)
        {
            if (_disposed) return;
            filters = filters ?? new AnalysisFilters();
            if (SameFilters(Filters, filters)) return;
            Filters = filters;
            _generation++; _operation?.Cancel();
            ClearResults();
            if (_baseSnapshot == null) { SetReadyStatus(); Notify(); return; }
            _ = RecalculateAsync(_baseSnapshot, filters);
        }

        private static bool SameFilters(AnalysisFilters a, AnalysisFilters b) =>
            a.Region == b.Region && a.Queue == b.Queue && a.Tier == b.Tier && a.Unit == b.Unit &&
            a.Trait == b.Trait && a.Item == b.Item && a.Augment == b.Augment;

        private async Task RecalculateAsync(AnalysisSnapshot baseline, AnalysisFilters filters)
        {
            var operation = new CancellationTokenSource(); _operation = operation; var generation = _generation;
            ErrorCode = ""; StatusText = "선택한 조건에 맞는 익명 관측으로 다시 집계하고 있습니다."; Notify();
            try
            {
                var result = await Task.Run(() => baseline.Recalculate(filters, operation.Token), operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                if (!Current(generation, operation)) return;
                Snapshot = result; ApplyResults();
            }
            catch (OperationCanceledException)
            { if (Current(generation, operation)) StatusText = "조건별 집계를 취소했습니다. 다시 갱신해 주세요."; }
            catch (Exception)
            { if (Current(generation, operation)) { ErrorCode = "analysis_recalculate_failed"; StatusText = "조건별 집계에 실패했습니다. 다시 갱신해 주세요."; } }
            finally
            {
                if (ReferenceEquals(_operation, operation)) _operation = null;
                operation.Dispose(); if (!_disposed) Notify();
            }
        }

        public async Task RefreshAsync()
        {
            if (!CanRefresh) return;
            var operation = new CancellationTokenSource(); _operation = operation;
            var generation = ++_generation; var catalog = Catalog; var identity = ActiveIdentity; var minimum = MinimumSamples;
            _baseSnapshot = null; ClearResults();
            ErrorCode = ""; StatusText = "로컬 익명 경기 기록을 읽고 집계 캐시를 갱신하고 있습니다."; Notify();
            try
            {
                var snapshot = await _service.LoadAsync(catalog, identity, operation.Token, minimum);
                operation.Token.ThrowIfCancellationRequested();
                if (!Current(generation, operation)) return;
                if (snapshot == null || !AnalysisService.SameIdentity(snapshot.Identity, identity) ||
                    snapshot.MinimumSamples < minimum) throw new InvalidOperationException();
                _baseSnapshot = snapshot;
                var filters = Filters;
                var result = SameFilters(filters, new AnalysisFilters()) ? snapshot :
                    await Task.Run(() => snapshot.Recalculate(filters, operation.Token), operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                if (!Current(generation, operation)) return;
                Snapshot = result;
                ApplyResults();
            }
            catch (OperationCanceledException)
            {
                if (Current(generation, operation)) { ErrorCode = ""; StatusText = "로컬 집계 갱신을 취소했습니다."; }
            }
            catch (TFTAnalyzer.Editor.Persistence.AggregateGenerationLimitException error)
            {
                if (Current(generation, operation))
                { ErrorCode = "analysis_cache_limit"; StatusText = error.IsStagingLimit
                    ? "미완성 분석 임시 작업이 32개에 도달했습니다. 작업이 끝난 뒤 저장소 aggregates/3/generations의 .staging- 폴더를 직접 정리해 주세요."
                    : "분석 캐시가 32개에 도달했습니다. 저장소 aggregates/3/generations의 이전 generation- 폴더를 직접 정리해 주세요."; }
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

        private void ApplyResults()
        {
            if (Snapshot == null) return;
            Rows = Snapshot.AllRows;
            Pairs = Snapshot.AllPairs;
            if (!IsBusy || ErrorCode.Length == 0)
                StatusText = Rows.Count == 0 && Pairs.Count == 0 ? "선택한 범위에 표시할 과거 관측 결과가 없습니다." :
                    "과거 관측 결과입니다. 지역·큐·티어 기록 라벨이 다른 범위는 합산하지 않습니다.";
        }
        public void Cancel()
        {
            if (_disposed || _operation == null) return;
            _operation.Cancel(); if (ValidMinimum) StatusText = "로컬 집계 갱신을 취소하고 있습니다."; Notify();
        }
        private void Notify() { if (!_disposed) Changed?.Invoke(); }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _generation++; _operation?.Cancel(); Changed = null;
        }
    }
}
