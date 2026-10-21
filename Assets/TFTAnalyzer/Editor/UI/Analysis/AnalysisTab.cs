using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI.Analysis
{
    public sealed class AnalysisTab : IDisposable
    {
        private const int PageSize = 20;
        private const string All = "전체";
        private static readonly string[] FilterNames = { "region", "queue", "tier", "unit", "trait", "item", "augment" };
        private readonly VisualElement _tab;
        private readonly AnalysisViewModel _vm;
        private readonly Func<string> _chooseExportPath;
        private readonly Action<string, IReadOnlyList<MetricPresentation>> _writeExport;
        private readonly DropdownField[] _filters;
        private readonly IntegerField _minimum;
        private readonly Button _refresh, _cancel, _export, _rowsPrevious, _rowsNext, _pairsPrevious, _pairsNext;
        private IReadOnlyList<MetricRowViewModel> _renderedRows, _renderedPairs;
        private int _rowPage, _pairPage;
        private bool _disposed, _queued, _preserveExportNotice;
        private EditorApplication.CallbackFunction _pendingExport;

        public AnalysisTab(VisualElement tab, AnalysisViewModel model, Func<string> chooseExportPath,
            Action<string, IReadOnlyList<MetricPresentation>> writeExport)
        {
            _tab = tab ?? throw new ArgumentNullException(nameof(tab));
            _vm = model ?? throw new ArgumentNullException(nameof(model));
            _chooseExportPath = chooseExportPath ?? throw new ArgumentNullException(nameof(chooseExportPath));
            _writeExport = writeExport ?? throw new ArgumentNullException(nameof(writeExport));
            _filters = FilterNames.Select(name => tab.Q<DropdownField>("analysis-" + name)).ToArray();
            _minimum = tab.Q<IntegerField>("analysis-minimum-samples");
            _minimum.RegisterValueChangedCallback(OnMinimumChanged);
            foreach (var field in _filters) field.RegisterValueChangedCallback(OnFilterChanged);
            _refresh = tab.Q<Button>("analysis-refresh");
            _cancel = tab.Q<Button>("analysis-cancel");
            _export = tab.Q<Button>("analysis-export");
            _rowsPrevious = tab.Q<Button>("analysis-rows-previous");
            _rowsNext = tab.Q<Button>("analysis-rows-next");
            _pairsPrevious = tab.Q<Button>("analysis-pairs-previous");
            _pairsNext = tab.Q<Button>("analysis-pairs-next");
            _refresh.clicked += Refresh;
            _cancel.clicked += Cancel;
            _export.clicked += Export;
            _rowsPrevious.clicked += PreviousRows;
            _rowsNext.clicked += NextRows;
            _pairsPrevious.clicked += PreviousPairs;
            _pairsNext.clicked += NextPairs;
            _vm.Changed += QueueRender;
            Render();
        }

        private void OnMinimumChanged(ChangeEvent<int> evt)
        { if (!_disposed) _vm.SetMinimumSamples(evt.newValue); }

        private void OnFilterChanged(ChangeEvent<string> evt)
        {
            if (_disposed || _vm.IsBusy) return;
            string Value(int index) => _filters[index].value == All ? null : _filters[index].value;
            _vm.SetFilters(new AnalysisFilters(Value(0), Value(1), Value(2), Value(3), Value(4), Value(5), Value(6)));
            if (!_preserveExportNotice) _tab.Q<Label>("analysis-export-status").text = "";
        }

        private async void Refresh()
        {
            if (_disposed || !_vm.CanRefresh) return;
            _tab.Q<Label>("analysis-export-status").text = "";
            await _vm.RefreshAsync();
        }
        private void Cancel() { if (!_disposed) _vm.Cancel(); }

        private void Export()
        {
            if (_disposed || !_vm.CanExport) return;
            CancelPendingExport();
            // Take the entire filtered presentation snapshot before the modal dialog pumps Editor events.
            var snapshot = _vm.Snapshot;
            var identity = _vm.ActiveIdentity;
            var rows = _vm.Rows;
            var pairs = _vm.Pairs;
            var visibleFilters = _filters.Select(field => field.value).ToArray();
            var presentations = Array.AsReadOnly(_vm.Rows.Concat(_vm.Pairs).Select(row => row.Presentation).ToArray());
            var status = _tab.Q<Label>("analysis-export-status");
            bool IsCurrent() => _vm.CanExport && ReferenceEquals(snapshot, _vm.Snapshot) && Equals(identity, _vm.ActiveIdentity) &&
                identity.Patch.SetNumber == _vm.ActiveIdentity.Patch.SetNumber &&
                ReferenceEquals(rows, _vm.Rows) && ReferenceEquals(pairs, _vm.Pairs) &&
                visibleFilters.SequenceEqual(_filters.Select(field => field.value), StringComparer.Ordinal);
            void Reject()
            {
                _preserveExportNotice = true;
                status.text = "분석 대상이 변경됐습니다. 현재 결과로 다시 저장해 주세요.";
            }
            try
            {
                var path = _chooseExportPath();
                if (_disposed || string.IsNullOrEmpty(path)) return;
                if (!IsCurrent()) { Reject(); return; }
                // Nested input events drain after this button callback, including changes that return to the old value.
                EditorApplication.CallbackFunction complete = null;
                complete = () =>
                {
                    EditorApplication.update -= complete;
                    if (!ReferenceEquals(_pendingExport, complete)) return;
                    _pendingExport = null;
                    if (_disposed) return;
                    try
                    {
                        if (!IsCurrent()) { Reject(); return; }
                        _writeExport(path, presentations);
                        status.text = "개인 연구용 CSV를 저장했습니다.";
                    }
                    catch (Exception) { status.text = "CSV를 저장하지 못했습니다. 저장 위치를 확인하고 다시 시도해 주세요."; }
                };
                _pendingExport = complete;
                EditorApplication.update += complete;
            }
            catch (Exception) { if (!_disposed) status.text = "CSV를 저장하지 못했습니다. 저장 위치를 확인하고 다시 시도해 주세요."; }
        }

        private void CancelPendingExport()
        {
            if (_pendingExport != null) EditorApplication.update -= _pendingExport;
            _pendingExport = null;
        }

        private void QueueRender()
        {
            if (_disposed || _queued) return;
            _queued = true;
            EditorApplication.update += Flush;
        }
        private void Flush()
        {
            EditorApplication.update -= Flush;
            _queued = false;
            if (!_disposed) Render();
            _preserveExportNotice = false;
        }

        private void Render()
        {
            _minimum.SetValueWithoutNotify(_vm.MinimumSamples);
            _tab.Q<Label>("analysis-minimum-status").text = _vm.MinimumStatusText;
            _refresh.SetEnabled(_vm.CanRefresh);
            _cancel.SetEnabled(_vm.IsBusy);
            _export.SetEnabled(_vm.CanExport);
            _tab.Q("analysis-filters").SetEnabled(!_vm.IsBusy);
            _tab.Q<Label>("analysis-catalog").text = DescribeCatalog(_vm.ActiveIdentity);
            _tab.Q<Label>("analysis-status").text = _vm.StatusText;
            _tab.Q<Label>("analysis-period").text = "불러온 범위의 관측 경기 기간: " + Period(_vm.Snapshot?.FirstPlayedAt, _vm.Snapshot?.LastPlayedAt);
            RenderFilters();
            if (!_preserveExportNotice && (!ReferenceEquals(_renderedRows, _vm.Rows) || !ReferenceEquals(_renderedPairs, _vm.Pairs)))
                _tab.Q<Label>("analysis-export-status").text = "";
            if (!ReferenceEquals(_renderedRows, _vm.Rows)) _rowPage = 0;
            if (!ReferenceEquals(_renderedPairs, _vm.Pairs)) _pairPage = 0;
            _renderedRows = _vm.Rows;
            _renderedPairs = _vm.Pairs;
            RenderTable("rows", _vm.Rows, ref _rowPage, false);
            RenderTable("pairs", _vm.Pairs, ref _pairPage, true);
        }

        private void RenderFilters()
        {
            var rows = _vm.Snapshot?.AllRows ?? Array.Empty<MetricRowViewModel>();
            var descriptors = rows.Select(row => row.First).Where(value => value != null).ToArray();
            var current = _vm.Filters;
            var selected = new[] { current.Region, current.Queue, current.Tier, current.Unit, current.Trait, current.Item, current.Augment };
            var options = new IEnumerable<string>[]
            {
                rows.Select(row => row.Presentation.Scope?.Region), rows.Select(row => row.Presentation.Scope?.QueueId),
                rows.Select(row => row.Presentation.Scope?.TierBucket), descriptors.SelectMany(value => value.UnitIds),
                descriptors.SelectMany(value => value.TraitIds), descriptors.SelectMany(value => value.ItemIds),
                descriptors.SelectMany(value => value.AugmentIds)
            };
            for (var index = 0; index < _filters.Length; index++)
            {
                var choices = new[] { All }.Concat(options[index].Concat(new[] { selected[index] })
                    .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)).ToList();
                _filters[index].choices = choices;
                _filters[index].SetValueWithoutNotify(string.IsNullOrEmpty(selected[index]) ? All : selected[index]);
            }
            // IDs remain the actual values; catalog names only decorate the menu and selected label.
            _filters[3].formatListItemCallback = _filters[3].formatSelectedValueCallback = id => EntityName(id, _vm.Catalog?.Units.Select(x => new KeyValuePair<string, LocalizedText>(x.StableId, x.Name)));
            _filters[4].formatListItemCallback = _filters[4].formatSelectedValueCallback = id => EntityName(id, _vm.Catalog?.Traits.Select(x => new KeyValuePair<string, LocalizedText>(x.StableId, x.Name)));
            _filters[5].formatListItemCallback = _filters[5].formatSelectedValueCallback = id => EntityName(id, _vm.Catalog?.Items.Select(x => new KeyValuePair<string, LocalizedText>(x.StableId, x.Name)));
            _filters[6].formatListItemCallback = _filters[6].formatSelectedValueCallback = id => EntityName(id, _vm.Catalog?.Augments.Select(x => new KeyValuePair<string, LocalizedText>(x.StableId, x.Name)));
        }

        private static string EntityName(string id, IEnumerable<KeyValuePair<string, LocalizedText>> records)
        {
            if (id == All || records == null) return id;
            var name = records.FirstOrDefault(value => value.Key == id).Value?.DisplayValue;
            return string.IsNullOrEmpty(name) ? id : name + " · " + id;
        }

        private void RenderTable(string name, IReadOnlyList<MetricRowViewModel> rows, ref int page, bool pairwise)
        {
            var totalPages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
            page = Math.Max(0, Math.Min(page, totalPages - 1));
            var table = _tab.Q("analysis-" + name);
            table.Clear();
            if (rows.Count == 0)
                table.Add(new Label(_vm.Snapshot == null ? "로컬 집계를 새로고침하면 결과가 표시됩니다." : "현재 필터와 일치하는 관측 결과가 없습니다.")
                    { name = "analysis-" + name + "-empty" });
            foreach (var row in rows.Skip(page * PageSize).Take(PageSize))
            {
                var card = new VisualElement();
                card.AddToClassList("tft-analysis-row");
                AddLabel(card, "composition", pairwise
                    ? "A: " + row.First.SummaryText + " [" + row.First.Fingerprint.Substring(0, 8) + "]\nB: " +
                      row.Second.SummaryText + " [" + row.Second.Fingerprint.Substring(0, 8) + "]"
                    : row.CompositionText);
                AddLabel(card, "scope", row.ScopeText);
                AddLabel(card, "sample", pairwise
                    ? $"표본: {row.Presentation.SampleCount}개 비교 가능 로비 · 모호한 로비 제외: {row.Presentation.AmbiguousLobbyCount}"
                    : $"표본: {row.Presentation.SampleCount}건 참가자 관측");
                AddLabel(card, "state", "표시 상태: " + row.StateText);
                if (!string.IsNullOrEmpty(row.AverageText)) AddLabel(card, "average", row.AverageText);
                AddLabel(card, "ratio", row.RatioText);
                AddLabel(card, "period", "관측 경기 기간: " + Period(row.Presentation.FirstPlayedAt, row.Presentation.LastPlayedAt));
                if (pairwise) card.tooltip = MetricRowViewModel.PairwiseTooltip;
                table.Add(card);
            }
            _tab.Q<Label>("analysis-" + name + "-page").text = $"{page + 1} / {totalPages} 페이지 · 전체 {rows.Count}행";
            _tab.Q<Button>("analysis-" + name + "-previous").SetEnabled(page > 0);
            _tab.Q<Button>("analysis-" + name + "-next").SetEnabled(page + 1 < totalPages);
        }

        private static void AddLabel(VisualElement parent, string name, string text)
        {
            var label = new Label(text) { name = "analysis-row-" + name };
            label.AddToClassList("tft-wrapped-text");
            parent.Add(label);
        }
        private static string Period(DateTimeOffset? first, DateTimeOffset? last) => first.HasValue && last.HasValue
            ? first.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) + " ~ " +
              last.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : "관측 없음";
        private static string DescribeCatalog(CatalogIdentity identity)
        {
            if (identity == null) return "활성 카탈로그: 데이터 관리 탭에서 검증된 행을 선택해 주세요.";
            var patch = identity.Patch;
            return $"활성 카탈로그: {patch.SetId} (번호 {patch.SetNumber}) · TFT 패치 {patch.PatchId}\n" +
                   $"원본 게임 버전 {patch.SourceGameVersion} · 출처 {identity.ProfileId}";
        }
        private void PreviousRows() { _rowPage--; Render(); }
        private void NextRows() { _rowPage++; Render(); }
        private void PreviousPairs() { _pairPage--; Render(); }
        private void NextPairs() { _pairPage++; Render(); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelPendingExport();
            _vm.Changed -= QueueRender;
            EditorApplication.update -= Flush;
            _queued = false;
            foreach (var field in _filters) field.UnregisterValueChangedCallback(OnFilterChanged);
            _minimum.UnregisterValueChangedCallback(OnMinimumChanged);
            _refresh.clicked -= Refresh;
            _cancel.clicked -= Cancel;
            _export.clicked -= Export;
            _rowsPrevious.clicked -= PreviousRows;
            _rowsNext.clicked -= NextRows;
            _pairsPrevious.clicked -= PreviousPairs;
            _pairsNext.clicked -= NextPairs;
        }
    }
}
