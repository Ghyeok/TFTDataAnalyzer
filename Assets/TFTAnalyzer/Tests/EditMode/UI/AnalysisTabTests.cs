using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.Collection;
using TFTAnalyzer.Editor.UI.Data;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class AnalysisTabTests
    {
        private OwnedEditorWindowScope _windows;
        private EditorWindow _host;
        private VisualElement _tree;
        private AnalysisViewModel _vm;
        private AnalysisTab _binding;
        private FakeService _service;
        private int _dialogs, _writes;
        private string _exportPath = "";
        private IReadOnlyList<MetricPresentation> _exported;
        private Action _duringDialog;

        [SetUp]
        public void SetUp()
        {
            _windows = new OwnedEditorWindowScope();
            try
            {
                _dialogs = 0; _writes = 0; _exportPath = ""; _exported = null; _duringDialog = null;
                _service = new FakeService();
                _vm = new AnalysisViewModel(_service);
                _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()),
                    DataManagerTestSupport.Identity());
                _host = _windows.Create<EditorWindow>();
                Rebuild();
            }
            catch { TearDown(); throw; }
        }

        [TearDown]
        public void TearDown()
        {
            try { _binding?.Dispose(); }
            finally
            {
                try { _vm?.Dispose(); }
                finally { _windows?.Dispose(); }
            }
        }

        [UnityTest]
        public IEnumerator OnlyExplicitRefreshStartsLocalWorkAndBusyCancelReachesTheSameOperation()
        {
            Assert.That(_service.Calls, Is.Zero);
            Submit("analysis-refresh");
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_service.Calls, Is.EqualTo(1));
            Assert.That(_tree.Q<Button>("analysis-refresh").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Button>("analysis-cancel").enabledInHierarchy, Is.True);
            Submit("analysis-cancel");
            Assert.That(_service.Token.IsCancellationRequested, Is.True);
            _service.Pending.SetCanceled();
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Button>("analysis-export").enabledInHierarchy, Is.False);
            Assert.That(_dialogs, Is.Zero);
            Assert.That(_writes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RealFilterEventsIntersectWithoutLoadingAndRebindPreservesTheVisibleValues()
        {
            _tree.Q<DropdownField>("analysis-region").value = "asia";
            _tree.Q<DropdownField>("analysis-unit").value = "TEST18_Unit";
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_vm.Filters.Region, Is.EqualTo("asia"));
            Assert.That(_vm.Filters.Unit, Is.EqualTo("TEST18_Unit"));
            var oldField = _tree.Q<DropdownField>("analysis-unit");
            Rebuild();
            oldField.value = "obsolete";
            Assert.That(_tree.Q<DropdownField>("analysis-region").value, Is.EqualTo("asia"));
            Assert.That(_tree.Q<DropdownField>("analysis-unit").value, Is.EqualTo("TEST18_Unit"));
            Assert.That(_vm.Filters.Unit, Is.EqualTo("TEST18_Unit"));
            Assert.That(_service.Calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RebindingKeepsRunningOperationAndDisposalPreventsLateUiMutation()
        {
            Submit("analysis-refresh");
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_service.Calls, Is.EqualTo(1));
            var oldStatus = _tree.Q<Label>("analysis-status");
            var before = oldStatus.text;
            Rebuild();
            Assert.That(_service.Calls, Is.EqualTo(1));
            Assert.That(_service.Token.IsCancellationRequested, Is.False);
            _binding.Dispose();
            _vm.Dispose();
            Assert.That(_service.Token.IsCancellationRequested, Is.True);
            _service.Pending.SetCanceled();
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(oldStatus.text, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator TablesPageExplicitlyAndExportAllFilteredPagesFromApprovedPresentations()
        {
            Submit("analysis-refresh");
            _service.Pending.SetResult(AnalysisFixture.Snapshot(100));
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q("analysis-rows").childCount, Is.EqualTo(8));
            Assert.That(_tree.Q("analysis-pairs").childCount, Is.EqualTo(20));
            Assert.That(_tree.Q<Label>("analysis-pairs-page").text, Does.Contain("1 / 2").And.Contain("28행"));
            Assert.That(_tree.Q<Label>("analysis-pairwise-heading").text, Is.EqualTo("상대 배치 우위율"));
            Assert.That(_tree.Q<Label>("analysis-pairwise-heading").tooltip, Is.EqualTo(MetricRowViewModel.PairwiseTooltip));
            var copy = string.Join(" ", _tree.Q("analysis-rows").Query<Label>().ToList().Select(label => label.text));
            Assert.That(copy, Does.Contain("100").And.Contain("95%").And.Contain("UTC").And.Contain("참가자"));
            Assert.That(_tree.Q<Label>("analysis-period").text, Does.Contain("1970-01-01").And.Contain("관측 경기"));
            Submit("analysis-pairs-next");
            Assert.That(_tree.Q("analysis-pairs").childCount, Is.EqualTo(8));
            Assert.That(_tree.Q<Label>("analysis-pairs-page").text, Does.Contain("2 / 2"));
            _exportPath = "test.csv";
            Submit("analysis-export");
            Assert.That(_exported.Count, Is.EqualTo(36), "CSV must include all filtered pages.");
            Assert.That(_exported.All(value => value.Ratios.Count > 0), Is.True);
            _tree.Q<DropdownField>("analysis-unit").value = "A";
            _tree.Q<DropdownField>("analysis-item").value = "ITEM_A";
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q("analysis-rows").childCount, Is.EqualTo(1));
            Assert.That(_tree.Q("analysis-pairs").childCount, Is.EqualTo(7));
            Submit("analysis-export");
            Assert.That(_exported.Count, Is.EqualTo(8));
            Assert.That(_service.Calls, Is.EqualTo(1));
            Assert.That(_writes, Is.EqualTo(2));
            _exportPath = "";
            Submit("analysis-export");
            Assert.That(_writes, Is.EqualTo(2), "Cancelled save dialog must not write.");
        }

        [UnityTest]
        public IEnumerator SuppressedRowsNeverShowPercentAndSelectionClearingImmediatelyBlocksExport()
        {
            Submit("analysis-refresh");
            _service.Pending.SetResult(AnalysisFixture.Snapshot(99));
            yield return EditorWindowTestSupport.WaitForRender();
            var text = string.Join(" ", _tree.Q("analysis-rows").Query<Label>().ToList().Select(label => label.text));
            Assert.That(text, Does.Contain("표본 부족").And.Contain("99").And.Not.Contain("%"));
            _exportPath = "test.csv";
            Submit("analysis-export");
            Assert.That(_exported.All(value => value.Ratios.Count == 0), Is.True);
            _vm.SetCatalog(null);
            Submit("analysis-export");
            Assert.That(_writes, Is.EqualTo(1), "The model guard blocks stale export before its queued repaint.");
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Button>("analysis-export").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Label>("analysis-catalog").text, Does.Contain("검증된 행"));
        }

        [UnityTest]
        public IEnumerator FilterChangeWhileSaveDialogPumpsEventsRejectsTheOldFilteredExport()
        {
            Submit("analysis-refresh");
            _service.Pending.SetResult(AnalysisFixture.Snapshot(100));
            yield return EditorWindowTestSupport.WaitForRender();
            _exportPath = "test.csv";
            _duringDialog = () =>
            {
                _tree.Q<DropdownField>("analysis-unit").value = "A";
                _tree.Q<DropdownField>("analysis-item").value = "ITEM_A";
            };
            Submit("analysis-export");
            Assert.That(_dialogs, Is.EqualTo(1));
            Assert.That(_writes, Is.Zero);
            Assert.That(_tree.Q<Label>("analysis-export-status").text, Does.Contain("변경"));
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("analysis-export-status").text, Does.Contain("변경"));
        }

        [UnityTest]
        public IEnumerator ActualWindowTransfersVerifiedLegacySelectionAndRebuildThenCloseKeepsOneCancelableRun()
        {
            var data = new DataManagerViewModel(new DataManagerFakeService
            { Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity(), legacy: true) } });
            var window = _windows.Create<TftDataAnalyzerWindow>(value =>
            {
                CollectionUITestSupport.SetWindowModel(value, "_analysis", _vm);
                CollectionUITestSupport.SetWindowModel(value, "_dataManager", data);
                CollectionUITestSupport.SetWindowModel(value, "_collection", new CollectionViewModel(new CollectionFakeService()));
            });
            window.CreateGUI();
            Assert.That(_vm.Catalog, Is.Null);
            window.rootVisualElement.Q<ListView>("catalog-list").SetSelection(0);
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_vm.Catalog.Manifest.Identity, Is.Null);
            Assert.That(_vm.ActiveIdentity, Is.EqualTo(DataManagerTestSupport.Identity()));
            CollectionUITestSupport.Submit(window.rootVisualElement, "analysis-tab-button");
            CollectionUITestSupport.Submit(window.rootVisualElement, "analysis-refresh");
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_service.Calls, Is.EqualTo(1));
            window.CreateGUI();
            Assert.That(_service.Calls, Is.EqualTo(1));
            Assert.That(_service.Token.IsCancellationRequested, Is.False);
            Assert.That(window.rootVisualElement.Q<Label>("analysis-catalog").text,
                Does.Contain("16.18.1").And.Contain("official-only"));
            var label = window.rootVisualElement.Q<Label>("analysis-status");
            var before = label.text;
            window.Close();
            Assert.That(_service.Token.IsCancellationRequested, Is.True);
            _service.Pending.SetResult(AnalysisFixture.Snapshot(1));
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(label.text, Is.EqualTo(before));
        }

        private void Rebuild()
        {
            _binding?.Dispose();
            _host.rootVisualElement.Clear();
            _tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            _tree.Q("analysis-tab").RemoveFromClassList("tft-tab-hidden");
            _host.rootVisualElement.Add(_tree);
            _binding = new AnalysisTab(_tree.Q("analysis-tab"), _vm,
                () => { _dialogs++; _duringDialog?.Invoke(); return _exportPath; }, (path, rows) => { _writes++; _exported = rows; });
        }

        private void Submit(string name) => CollectionUITestSupport.Submit(_tree, name);

        private sealed class FakeService : IAnalysisService
        {
            internal int Calls;
            internal CancellationToken Token;
            internal TaskCompletionSource<AnalysisSnapshot> Pending = new TaskCompletionSource<AnalysisSnapshot>();
            public Task<AnalysisSnapshot> LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token)
            { Calls++; Token = token; return Pending.Task; }
        }
    }
}
