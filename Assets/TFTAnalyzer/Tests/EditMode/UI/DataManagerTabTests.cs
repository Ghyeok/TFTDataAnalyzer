using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.UI.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class DataManagerTabTests
    {
        private EditorWindow _host;
        private VisualElement _tree;
        private DataManagerViewModel _vm;
        private DataManagerFakeService _service;
        private DataManagerTab _binding;
        private bool _approve;
        private int _confirmations;
        private OwnedEditorWindowScope _windows;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _windows = new OwnedEditorWindowScope();
            try
            {
                _approve = false;
                _confirmations = 0;
                _service = new DataManagerFakeService
                {
                    Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()),
                        DataManagerTestSupport.Row(DataManagerTestSupport.Identity(profile:
                            CatalogSourceProfile.OfficialPlusCommunityDragon), "After") }
                };
                _vm = new DataManagerViewModel(_service);
                _host = _windows.Create<EditorWindow>();
                Rebuild();
            }
            catch { TearDown(); throw; }
            // Binding construction renders synchronously; no delayed model change exists here.
            yield break;
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
        public IEnumerator RealFieldsAndImportButtonRequireConsentAndResetTheVisibleOptIn()
        {
            SetInputs();
            Submit("import-button");
            yield return WaitForEditorRender();
            Assert.That(_confirmations, Is.EqualTo(1));
            Assert.That(_service.Imports, Is.Empty);
            Assert.That(_tree.Q<Toggle>("communitydragon-toggle").value, Is.False);

            _approve = true;
            SetInputs();
            Submit("import-button");
            yield return WaitForEditorRender();
            Assert.That(_confirmations, Is.EqualTo(2));
            Assert.That(_service.Imports, Has.Count.EqualTo(1));
            Assert.That(_service.Imports[0].Identity.ProfileId, Is.EqualTo("official-plus-communitydragon"));
            Assert.That(_service.Imports[0].Patch.SetId, Is.EqualTo("TFTSet18"));
            Assert.That(_service.Imports[0].Patch.SourceGameVersion, Is.EqualTo("16.18.1"));
            Assert.That(_tree.Q<Toggle>("communitydragon-toggle").value, Is.False);
            Assert.That(_tree.Q<Label>("data-status").text, Is.EqualTo(_vm.StatusText));
        }

        [UnityTest]
        public IEnumerator BusyControlsAndCancelWaitForTheExistingOperationToSettle()
        {
            var pending = new TaskCompletionSource<StaticImportResult>();
            _service.Run = token => pending.Task;
            _approve = true;
            SetInputs();
            Submit("import-button");
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Importing));
            yield return WaitForEditorRender();
            Assert.That(_tree.Q<Button>("import-button").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<TextField>("set-id").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<ListView>("catalog-list").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Button>("compare-button").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Button>("cancel-button").enabledInHierarchy, Is.True);
            Submit("cancel-button");
            Assert.That(_service.LastToken.IsCancellationRequested, Is.True);
            Assert.That(_tree.Q<Button>("import-button").enabledInHierarchy, Is.False);
            pending.SetCanceled();
            yield return WaitForOperationAndRender();
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Cancelled));
            Assert.That(_tree.Q<Button>("import-button").enabledInHierarchy, Is.True);
            Assert.That(_tree.Q<Toggle>("communitydragon-toggle").value, Is.False);
        }

        [UnityTest]
        public IEnumerator ListSelectionAndDirectionalDiffAppearThroughRealUIEvents()
        {
            _tree.Q<ListView>("catalog-list").SetSelection(1);
            yield return WaitForEditorRender();
            Assert.That(_vm.ActiveIdentity.ProfileId, Is.EqualTo("official-plus-communitydragon"));
            Assert.That(_tree.Q<Label>("active-catalog").text,
                Does.Contain("16.18.1").And.Contain("official-plus-communitydragon"));
            _tree.Q<DropdownField>("diff-before").index = 0;
            _tree.Q<DropdownField>("diff-after").index = 1;
            Submit("compare-button");
            yield return WaitForEditorRender();
            var diff = _tree.Q("catalog-diff");
            var text = string.Join("\n", diff.Query<Label>().ToList().Select(label => label.text));
            Assert.That(text, Does.Contain("official-only").And.Contain("official-plus-communitydragon"));
            Assert.That(diff.Query<Foldout>().ToList(), Is.Not.Empty);
            Assert.That(text, Does.Contain("name.enUs").And.Contain("Before").And.Contain("After"));
            Assert.That(_vm.Diff.Units.Changed, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisposedBindingCannotChangeInputOrReceiveScheduledUpdatesAndRebuildKeepsActiveIdentity()
        {
            _tree.Q<ListView>("catalog-list").SetSelection(1);
            yield return WaitForEditorRender();
            var oldToggle = _tree.Q<Toggle>("communitydragon-toggle");
            var oldStatus = _tree.Q<Label>("data-status");
            var oldText = oldStatus.text;
            var pending = new TaskCompletionSource<IReadOnlyList<string>>();
            _service.Versions = token => pending.Task;
            var operation = _vm.CheckForUpdatesAsync();
            // Dispose before the coalesced update runs; then bind the same VM to a new tree.
            Rebuild();
            oldToggle.value = true;
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            pending.SetResult(new[] { "16.18.2" });
            yield return WaitForOperationAndRender();
            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(oldStatus.text, Is.EqualTo(oldText));
            Assert.That(_tree.Q<Label>("data-status").text, Is.EqualTo(_vm.StatusText));
            Assert.That(_tree.Q<Label>("active-catalog").text, Does.Contain("official-plus-communitydragon"));
            Assert.That(_service.VersionCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FewerThanTwoHealthyCatalogsShowsAnEmptyDiffStateAndDisablesComparison()
        {
            _binding.Dispose();
            _vm.Dispose();
            _service.Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()) };
            _vm = new DataManagerViewModel(_service);
            Rebuild();
            Assert.That(_tree.Q<Button>("compare-button").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<DropdownField>("diff-before").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<DropdownField>("diff-after").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Label>("diff-empty-state").text, Is.Not.Empty);
            Assert.That(_service.VersionCalls, Is.Zero);
            yield break;
        }

        private void Rebuild()
        {
            _binding?.Dispose();
            _host.rootVisualElement.Clear();
            _tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            _host.rootVisualElement.Add(_tree);
            _binding = new DataManagerTab(_tree.Q("data-tab"), _vm, summary =>
            {
                _confirmations++;
                Assert.That(summary, Does.Contain("TFTSet18").And.Contain("18.2")
                    .And.Contain("16.18.1").And.Contain("official-plus-communitydragon"));
                return _approve;
            });
        }

        private IEnumerator WaitForOperationAndRender()
        {
            var settled = !_vm.IsBusy;
            Action observe = () => settled = !_vm.IsBusy;
            _vm.Changed += observe;
            try
            {
                var deadline = EditorApplication.timeSinceStartup + 5;
                while (!settled)
                {
                    Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                        "The operation did not publish its settled state.");
                    yield return null;
                }
            }
            finally { _vm.Changed -= observe; }
            yield return WaitForEditorRender();
        }

        private IEnumerator WaitForEditorRender() => EditorWindowTestSupport.WaitForRender();

        private void SetInputs()
        {
            _tree.Q<TextField>("set-id").value = "TFTSet18";
            _tree.Q<IntegerField>("set-number").value = 18;
            _tree.Q<TextField>("tft-patch").value = "18.2";
            _tree.Q<TextField>("source-version").value = "16.18.1";
            _tree.Q<Toggle>("communitydragon-toggle").value = true;
        }

        private void Submit(string name)
        {
            var button = _tree.Q<Button>(name);
            Assert.That(button, Is.Not.Null);
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
