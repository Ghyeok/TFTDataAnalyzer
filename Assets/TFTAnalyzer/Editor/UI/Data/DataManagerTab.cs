using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Diff;
using TFTAnalyzer.Domain;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI.Data
{
    public sealed class DataManagerTab : IDisposable
    {
        private readonly DataManagerViewModel _vm;
        private readonly Func<string, bool> _confirm;
        private readonly VisualElement _tab;
        private readonly TextField _setId, _patch, _version;
        private readonly IntegerField _setNumber;
        private readonly Toggle _supplement;
        private readonly Button _check, _import, _cancel, _compare;
        private readonly Button _validationPrevious, _validationNext, _mergePrevious, _mergeNext;
        private readonly ListView _list;
        private readonly DropdownField _before, _after, _baseline;
        private IReadOnlyList<PatchRowViewModel> _boundRows;
        private List<PatchRowViewModel> _healthy = new List<PatchRowViewModel>();
        private CatalogDiff _renderedDiff;
        private StaticImportReportViewModel _renderedImportReport;
        private int _validationPage, _mergePage;
        private bool _disposed, _queued, _rendering;

        public DataManagerTab(VisualElement tab, DataManagerViewModel vm, Func<string, bool> confirm)
        {
            _tab = tab ?? throw new ArgumentNullException(nameof(tab));
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
            _setId = tab.Q<TextField>("set-id");
            _setNumber = tab.Q<IntegerField>("set-number");
            _patch = tab.Q<TextField>("tft-patch");
            _version = tab.Q<TextField>("source-version");
            _supplement = tab.Q<Toggle>("communitydragon-toggle");
            _check = tab.Q<Button>("check-updates-button");
            _import = tab.Q<Button>("import-button");
            _cancel = tab.Q<Button>("cancel-button");
            _compare = tab.Q<Button>("compare-button");
            _validationPrevious = tab.Q<Button>("import-report-validation-previous");
            _validationNext = tab.Q<Button>("import-report-validation-next");
            _mergePrevious = tab.Q<Button>("import-report-merge-previous");
            _mergeNext = tab.Q<Button>("import-report-merge-next");
            _list = tab.Q<ListView>("catalog-list");
            _before = tab.Q<DropdownField>("diff-before");
            _after = tab.Q<DropdownField>("diff-after");
            _baseline = tab.Q<DropdownField>("import-baseline");
            _setId.RegisterValueChangedCallback(OnTextChanged);
            _patch.RegisterValueChangedCallback(OnTextChanged);
            _version.RegisterValueChangedCallback(OnTextChanged);
            _setNumber.RegisterValueChangedCallback(OnNumberChanged);
            _supplement.RegisterValueChangedCallback(OnSupplementChanged);
            _baseline.RegisterValueChangedCallback(OnBaselineChanged);
            _check.clicked += Check;
            _import.clicked += Import;
            _cancel.clicked += Cancel;
            _compare.clicked += Compare;
            _validationPrevious.clicked += PreviousValidation;
            _validationNext.clicked += NextValidation;
            _mergePrevious.clicked += PreviousMerge;
            _mergeNext.clicked += NextMerge;
            _list.selectionType = SelectionType.Single;
            _list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _list.makeItem = MakeRow;
            _list.bindItem = BindRow;
            _list.selectionChanged += Select;
            _vm.Changed += QueueRender;
            Render();
        }

        private void OnTextChanged(ChangeEvent<string> evt) => CaptureInput();
        private void OnNumberChanged(ChangeEvent<int> evt) => CaptureInput();
        private void OnSupplementChanged(ChangeEvent<bool> evt) => CaptureInput();

        private void OnBaselineChanged(ChangeEvent<string> evt)
        {
            if (_disposed || _rendering) return;
            var index = _baseline.index;
            if (index == 0) _vm.TrySelectImportBaseline(null);
            else if (index > 0 && index <= _healthy.Count)
                _vm.TrySelectImportBaseline(_healthy[index - 1].Identity);
            else QueueRender();
        }

        private void CaptureInput()
        {
            if (_disposed || _rendering) return;
            PatchKey patch = null;
            try { patch = new PatchKey(_setId.value, _setNumber.value, _patch.value, _version.value); }
            catch (ArgumentException) { }
            if (!_vm.TrySetInput(patch, _supplement.value)) QueueRender();
        }

        private async void Check() { if (!_disposed) await _vm.CheckForUpdatesAsync(); }
        private async void Import()
        {
            if (_disposed || _vm.IsBusy) return;
            CaptureInput();
            await _vm.ImportAsync(_confirm);
        }
        private void Cancel() { if (!_disposed) _vm.CancelImport(); }
        private void Compare()
        {
            if (_disposed || _vm.IsBusy || _before.index < 0 || _after.index < 0 ||
                _before.index >= _healthy.Count || _after.index >= _healthy.Count) return;
            _vm.Compare(_healthy[_before.index].Identity, _healthy[_after.index].Identity);
        }
        private void Select(IEnumerable<object> selection)
        {
            if (_disposed || _rendering) return;
            var row = selection.OfType<PatchRowViewModel>().FirstOrDefault();
            if (row != null && !_vm.SelectActiveCatalog(row.Identity)) QueueRender();
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
        }

        private void Render()
        {
            _rendering = true;
            try
            {
                var busy = _vm.IsBusy;
                _tab.Q("import-inputs").SetEnabled(!busy);
                _check.SetEnabled(!busy);
                _import.SetEnabled(!busy);
                _cancel.SetEnabled(busy);
                _list.SetEnabled(!busy);
                _supplement.SetValueWithoutNotify(_vm.IncludeCommunityDragon);
                if (_vm.InputPatch != null)
                {
                    _setId.SetValueWithoutNotify(_vm.InputPatch.SetId);
                    _setNumber.SetValueWithoutNotify(_vm.InputPatch.SetNumber);
                    _patch.SetValueWithoutNotify(_vm.InputPatch.PatchId);
                    _version.SetValueWithoutNotify(_vm.InputPatch.SourceGameVersion);
                }
                _tab.Q<Label>("data-status").text = _vm.StatusText;
                RenderImportReport();
                _tab.Q<Label>("available-versions").text = _vm.Versions.Count == 0
                    ? "버전 확인 버튼을 누르면 사용 가능한 원본 버전 목록을 표시합니다."
                    : string.Join(" · ", _vm.Versions);
                if (!ReferenceEquals(_boundRows, _vm.Rows))
                {
                    _boundRows = _vm.Rows;
                    _list.itemsSource = _boundRows.ToList();
                    _list.Rebuild();
                    _healthy = _boundRows.Where(row => row.IsSelectable).ToList();
                    var choices = _healthy.Select(row => row.DisplayIdentity).ToList();
                    _before.choices = new List<string>(choices);
                    _after.choices = new List<string>(choices);
                    if (choices.Count > 0) _before.SetValueWithoutNotify(choices[0]);
                    if (choices.Count > 1) _after.SetValueWithoutNotify(choices[1]);
                }
                var active = _vm.Rows.FirstOrDefault(row => row.IsSelectable && row.Identity.Equals(_vm.ActiveIdentity));
                var baselineChoices = new List<string> { "선택하지 않음" };
                baselineChoices.AddRange(_healthy.Select((row, index) =>
                    $"{index + 1}. {row.DisplayIdentity} · 해시 {row.ContentHashPrefix}"));
                var baselineIndex = _vm.ImportBaselineIdentity == null ? 0 :
                    _healthy.FindIndex(row => row.Identity.Equals(_vm.ImportBaselineIdentity)) + 1;
                if (_vm.ImportBaselineIdentity != null && baselineIndex == 0)
                {
                    baselineChoices.Add("선택한 기준을 찾을 수 없음");
                    baselineIndex = baselineChoices.Count - 1;
                }
                _baseline.choices = baselineChoices;
                _baseline.SetValueWithoutNotify(baselineChoices[baselineIndex]);
                PlainText(_baseline);
                var baselineStatus = _tab.Q<Label>("import-baseline-status");
                baselineStatus.enableRichText = false;
                baselineStatus.text = _vm.ImportBaselineStatusText;
                _list.SetSelectionWithoutNotify(active == null ? Array.Empty<int>() :
                    new[] { _vm.Rows.ToList().IndexOf(active) });
                _tab.Q<Label>("active-catalog").text = active == null ? "활성 카탈로그: 선택하지 않음" :
                    "활성 카탈로그: " + active.DisplayIdentity;
                var summary = _tab.Q<Label>("active-change-summary");
                summary.enableRichText = false;
                summary.text = active == null ? "변경 요약: 카탈로그를 선택해 주세요." : active.ChangeSummaryText;
                _tab.Q<Label>("catalog-empty-state").EnableInClassList("tft-tab-hidden", _vm.Rows.Count != 0);
                var canCompare = _healthy.Count >= 2;
                _before.SetEnabled(!busy && canCompare);
                _after.SetEnabled(!busy && canCompare);
                _compare.SetEnabled(!busy && canCompare);
                _tab.Q<Label>("diff-empty-state").text = canCompare
                    ? "이전·이후 카탈로그를 선택하고 비교해 주세요."
                    : "비교하려면 검증된 로컬 카탈로그가 두 개 이상 필요합니다.";
                _tab.Q("diff-empty-state").EnableInClassList("tft-tab-hidden", _vm.Diff != null);
                if (!ReferenceEquals(_renderedDiff, _vm.Diff)) RenderDiff();
            }
            finally { _rendering = false; }
        }

        private void PreviousValidation() => ChangeReportPage(true, -1);
        private void NextValidation() => ChangeReportPage(true, 1);
        private void PreviousMerge() => ChangeReportPage(false, -1);
        private void NextMerge() => ChangeReportPage(false, 1);

        /// <summary>Moves only within the retained snapshot; never invokes the import service.</summary>
        private void ChangeReportPage(bool validation, int delta)
        {
            if (_disposed || _vm.LastImportReport == null) return;
            var rows = validation ? _vm.LastImportReport.ValidationRows : _vm.LastImportReport.MergeRows;
            var page = validation ? _validationPage : _mergePage;
            page = Math.Max(0, Math.Min(Math.Max(0, (rows.Count - 1) / 20), page + delta));
            if (validation) _validationPage = page; else _mergePage = page;
            RenderImportReport();
        }

        /// <summary>Labels the original request and distinguishes missing details from a genuine empty report.</summary>
        private void RenderImportReport()
        {
            var report = _vm.LastImportReport;
            if (!ReferenceEquals(report, _renderedImportReport))
            {
                _renderedImportReport = report;
                _validationPage = _mergePage = 0;
            }
            var summary = _tab.Q<Label>("import-report-summary");
            summary.enableRichText = false;
            if (report == null) summary.text = "이 창에서 실행한 가져오기 기록이 없습니다.";
            else
            {
                var identity = report.RequestedIdentity;
                var patch = identity.Patch;
                summary.text = $"요청: {patch.SetId} (세트 {patch.SetNumber}) · TFT {patch.PatchId} · 원본 {patch.SourceGameVersion} · {identity.ProfileId}\n" +
                    "시작 UTC: " + report.StartedUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) +
                    (report.IncludeSupplement ? " · 보완 사용 요청: 예\n" : " · 보완 사용 요청: 아니요\n") +
                    (report.ResultAvailable
                        ? (report.Published == true ? "게시됨" : "게시하지 않음") + (report.Idempotent == true ? " · 동일 내용 재사용" : "")
                        : (_vm.IsBusy && _vm.State == DataManagerState.Importing ? "진행 중 · " : "") + "상세 보고서 없음 · 게시 여부 미확인") +
                    (report.HasValidation ? $"\n검증 오류 {report.ErrorCount} · 경고 {report.WarningCount}" : "\n검증 상세 없음") +
                    (report.ResultAvailable ? $"\n공식 값 유지 {report.OfficialWinsCount} · 빈 필드 보완 {report.SupplementFillCount}" : "") +
                    (report.TextTruncated ? "\n값 길이 상한으로 일부 텍스트 생략" : "");
            }
            RenderReportRows("validation", report?.ValidationRows, report?.ValidationTotal, _validationPage,
                _validationPrevious, _validationNext);
            RenderReportRows("merge", report?.MergeRows, report?.MergeTotal, _mergePage,
                _mergePrevious, _mergeNext);
        }

        /// <summary>Renders at most twenty plain-text entries and discloses omitted detail separately from totals.</summary>
        private void RenderReportRows(string section, IReadOnlyList<string> rows, int? total, int page,
            Button previous, Button next)
        {
            var container = _tab.Q(section == "validation" ? "import-report-validation" : "import-report-merges");
            container.Clear();
            var count = rows?.Count ?? 0;
            foreach (var text in (rows ?? Array.Empty<string>()).Skip(page * 20).Take(20))
            {
                var label = new Label(text) { enableRichText = false };
                label.AddToClassList("tft-wrapped-text");
                label.style.marginBottom = 8;
                container.Add(label);
            }
            var notice = _tab.Q<Label>("import-report-" + section + "-page");
            notice.enableRichText = false;
            notice.text = !total.HasValue ? "상세 없음" : total.Value == 0 ? "0건" :
                $"{page + 1} / {Math.Max(1, (count + 19) / 20)} 페이지 · 보유 {count} / 전체 {total.Value}건" +
                (total.Value > count ? " · 일부 상세 누락" : "");
            previous.SetEnabled(count > 0 && page > 0);
            next.SetEnabled((page + 1) * 20 < count);
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("tft-catalog-row");
            foreach (var name in new[] { "identity", "metadata", "validation", "directory" })
            {
                var label = new Label { name = name };
                label.AddToClassList("tft-wrapped-text");
                row.Add(label);
            }
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            var row = _boundRows[index];
            element.EnableInClassList("tft-catalog-invalid", !row.IsSelectable);
            element.Q<Label>("identity").text = row.DisplayIdentity;
            element.Q<Label>("metadata").text = row.SourceBadge + " · 수집 UTC: " +
                (row.RetrievedUtc == DateTime.UnixEpoch && !row.IsSelectable ? "확인 불가" :
                    row.RetrievedUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture));
            element.Q<Label>("validation").text = row.ValidationText + " · 해시 " + row.ContentHashPrefix;
            element.Q<Label>("directory").text = "위치: " + (string.IsNullOrEmpty(row.Directory) ? "확인 불가" : row.Directory);
        }

        /// <summary>Renders the retained comparison records and identities as literal text, including Foldout titles and paged values.</summary>
        private void RenderDiff()
        {
            _renderedDiff = _vm.Diff;
            var container = _tab.Q("catalog-diff");
            container.Clear();
            if (_vm.Diff == null) return;
            var before = _vm.DiffBeforeRow;
            var after = _vm.DiffAfterRow;
            container.Add(new Label("이전: " + (before?.DisplayIdentity ?? CatalogDiffFieldDisplay.Unavailable)));
            container.Add(new Label("이후: " + (after?.DisplayIdentity ?? CatalogDiffFieldDisplay.Unavailable)));
            AddEntityDiff(container, "기물", _vm.Diff.Units, before?.Catalog.Units, after?.Catalog.Units, x => x.StableId);
            AddEntityDiff(container, "시너지", _vm.Diff.Traits, before?.Catalog.Traits, after?.Catalog.Traits, x => x.StableId);
            AddEntityDiff(container, "아이템", _vm.Diff.Items, before?.Catalog.Items, after?.Catalog.Items, x => x.StableId);
            AddEntityDiff(container, "증강", _vm.Diff.Augments, before?.Catalog.Augments, after?.Catalog.Augments, x => x.StableId);
            PlainText(container);
        }

        /// <summary>Uses the first record for each stable ID to display directional changes with previews and access to complete values.</summary>
        private static void AddEntityDiff<T>(VisualElement container, string name, EntityDiff diff,
            IReadOnlyList<T> previous, IReadOnlyList<T> current, Func<T, string> stableId) where T : class
        {
            var before = (previous ?? Array.Empty<T>()).GroupBy(stableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var after = (current ?? Array.Empty<T>()).GroupBy(stableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var details = new Foldout { text = $"{name} · 추가 {diff.AddedIds.Count} · 삭제 {diff.RemovedIds.Count} · 변경 {diff.Changed.Count}", value = false };
            foreach (var id in diff.AddedIds) details.Add(new Label("추가: " + id));
            foreach (var id in diff.RemovedIds) details.Add(new Label("삭제: " + id));
            foreach (var change in diff.Changed)
            {
                var record = new Foldout { text = change.StableId, value = false };
                before.TryGetValue(change.StableId, out var oldRecord);
                after.TryGetValue(change.StableId, out var newRecord);
                foreach (var field in change.Fields)
                {
                    var fieldRoot = new VisualElement { name = "diff-field-" + field.FieldName };
                    var display = new CatalogDiffFieldDisplay(field, oldRecord, newRecord);
                    var label = new Label($"{field.FieldName}\n이전: {Preview(display.Before)}\n이후: {Preview(display.After)}")
                        { name = "diff-field-summary" };
                    label.AddToClassList("tft-wrapped-text");
                    fieldRoot.Add(label);
                    if (display.Before.Length > 1024 || display.After.Length > 1024)
                    {
                        var full = new Foldout { name = "diff-field-full", text = "전체 값 · 읽기 전용 페이지에서 선택·복사", value = false };
                        full.Add(FullValue("이전", "diff-before-full", display.Before));
                        full.Add(FullValue("이후", "diff-after-full", display.After));
                        fieldRoot.Add(full);
                    }
                    record.Add(fieldRoot);
                }
                details.Add(record);
            }
            container.Add(details);
        }

        /// <summary>Limits the original-value preview prefix to 1024 UTF-16 units without splitting a surrogate pair and marks omitted text.</summary>
        private static string Preview(string value)
        {
            if (value.Length <= 1024) return value;
            var length = 1024;
            if (char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length])) length--;
            return value.Substring(0, length) + "… (일부 생략 · 전체 값에서 확인)";
        }

        /// <summary>Creates read-only, copyable pages of at most 4096 UTF-16 units with intact surrogate pairs and plain-text navigation.</summary>
        private static VisualElement FullValue(string title, string name, string value)
        {
            var root = new VisualElement { name = name };
            root.Add(new Label(title));
            var text = new TextField { name = "diff-value-text", multiline = true, isReadOnly = true,
                verticalScrollerVisibility = ScrollerVisibility.Auto };
            text.AddToClassList("tft-extension-text");
            root.Add(text);
            var navigation = new VisualElement();
            navigation.AddToClassList("tft-diff-value-navigation");
            var previous = new Button { name = "diff-value-previous", text = "이전 페이지" };
            var next = new Button { name = "diff-value-next", text = "다음 페이지" };
            var notice = new Label { name = "diff-value-page" };
            navigation.Add(previous); navigation.Add(notice); navigation.Add(next);
            root.Add(navigation);
            var offsets = new List<int> { 0 };
            while (offsets[offsets.Count - 1] < value.Length)
            {
                var end = Math.Min(value.Length, offsets[offsets.Count - 1] + 4096);
                if (end < value.Length && char.IsHighSurrogate(value[end - 1]) && char.IsLowSurrogate(value[end])) end--;
                offsets.Add(end);
            }
            var count = Math.Max(1, offsets.Count - 1);
            var page = 0;
            void RenderPage()
            {
                var start = offsets[page];
                text.SetValueWithoutNotify(value.Substring(start, page + 1 < offsets.Count ? offsets[page + 1] - start : 0));
                notice.text = $"{page + 1} / {count} 페이지";
                previous.SetEnabled(page > 0);
                next.SetEnabled(page + 1 < count);
                PlainText(root);
            }
            previous.clicked += () => { if (page > 0) { page--; RenderPage(); } };
            next.clicked += () => { if (page + 1 < count) { page++; RenderPage(); } };
            RenderPage();
            return root;
        }

        /// <summary>Disables rich-text interpretation throughout generated labels, Foldout titles, and text-field content.</summary>
        private static void PlainText(VisualElement root)
        {
            foreach (var element in root.Query<TextElement>().ToList()) element.enableRichText = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _vm.Changed -= QueueRender;
            EditorApplication.update -= Flush;
            _queued = false;
            _setId.UnregisterValueChangedCallback(OnTextChanged);
            _patch.UnregisterValueChangedCallback(OnTextChanged);
            _version.UnregisterValueChangedCallback(OnTextChanged);
            _setNumber.UnregisterValueChangedCallback(OnNumberChanged);
            _supplement.UnregisterValueChangedCallback(OnSupplementChanged);
            _baseline.UnregisterValueChangedCallback(OnBaselineChanged);
            _check.clicked -= Check;
            _import.clicked -= Import;
            _cancel.clicked -= Cancel;
            _compare.clicked -= Compare;
            _validationPrevious.clicked -= PreviousValidation;
            _validationNext.clicked -= NextValidation;
            _mergePrevious.clicked -= PreviousMerge;
            _mergeNext.clicked -= NextMerge;
            _list.selectionChanged -= Select;
        }
    }
}
