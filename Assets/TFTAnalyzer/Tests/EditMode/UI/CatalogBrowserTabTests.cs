using System.Collections;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class CatalogBrowserTabTests
    {
        private OwnedEditorWindowScope _windows;
        private EditorWindow _host;
        private VisualElement _tree;
        private CatalogBrowserViewModel _vm;
        private CatalogBrowserTab _binding;
        private int _iconLoads;

        [SetUp]
        public void SetUp()
        {
            _iconLoads = 0;
            _windows = new OwnedEditorWindowScope();
            try
            {
                _host = _windows.Create<EditorWindow>();
                _vm = new CatalogBrowserViewModel();
                _vm.SetCatalog(CatalogBrowserViewModelTests.Catalog(), DataManagerTestSupport.Identity());
                Rebuild();
            }
            catch { TearDown(); throw; }
        }

        [TearDown]
        public void TearDown()
        {
            try { _binding?.Dispose(); }
            finally { try { _vm?.Dispose(); } finally { _windows?.Dispose(); } }
        }

        private void Rebuild()
        {
            _binding?.Dispose(); _host.rootVisualElement.Clear();
            _tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            _host.rootVisualElement.Add(_tree);
            _tree.Q<Foldout>("catalog-browser").value = true;
            _binding = new CatalogBrowserTab(_tree.Q("catalog-browser"), _vm, _ => { _iconLoads++; return null; });
        }

        [Test]
        public void DataTabContainsLocalRecordSearchAndDetails()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            var browser = tree.Q("data-tab").Q<Foldout>("catalog-browser");
            Assert.That(browser, Is.Not.Null, "Static catalog contents are not yet browsable.");
            Assert.That(browser.Q<DropdownField>("browser-kind"), Is.Not.Null);
            Assert.That(browser.Q<TextField>("browser-search"), Is.Not.Null);
            Assert.That(browser.Q<ListView>("browser-results"), Is.Not.Null);
            Assert.That(browser.Q("browser-details"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SearchKindAndSelectionEventsShowTheActualRecord()
        {
            _tree.Q<TextField>("browser-search").value = "nature";
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_vm.Search, Is.EqualTo("nature"));
            _tree.Q<ListView>("browser-results").SetSelection(0);
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("browser-detail-text").text, Does.Contain("자연").And.Contain("비용: 3"));
            _tree.Q<TextField>("browser-search").value = "";
            _tree.Q<DropdownField>("browser-kind").index = 2;
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_vm.Kind, Is.EqualTo(CatalogRecordKind.Item));
            Assert.That(_vm.Selected, Is.Null);
            _tree.Q<ListView>("browser-results").SetSelection(0);
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("browser-detail-text").text, Does.Contain("Blade").And.Contain("영어로 대체"));
            Assert.That(_tree.Q<Label>("browser-detail-text").enableRichText, Is.False);
            Assert.That(_tree.Q<Label>("browser-detail-text").text, Does.Contain("<b>설명</b>"));
        }

        [UnityTest]
        public IEnumerator PagingAndCatalogRemovalClearOldDetails()
        {
            var units = Enumerable.Range(0, 51).Select(i => new UnitRecord("U" + i.ToString("D3"), new LocalizedText("", "Unit " + i), 1, null, "", null));
            _vm.SetCatalog(CatalogBrowserViewModelTests.Catalog(units: units), DataManagerTestSupport.Identity());
            yield return EditorWindowTestSupport.WaitForRender();
            Submit("browser-next"); yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_vm.Page, Is.EqualTo(1));
            _tree.Q<ListView>("browser-results").SetSelection(0); yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("browser-detail-text").text, Does.Contain("U050"));
            _vm.SetCatalog(null, null); yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("browser-detail-text").text, Is.Empty);
            Assert.That(_tree.Q<Image>("browser-icon").image, Is.Null);
            Assert.That(_tree.Q("browser-details").ClassListContains("tft-tab-hidden"), Is.True);
            Assert.That(_tree.Q<Label>("browser-status").text, Does.Contain("카탈로그"));
        }

        [UnityTest]
        public IEnumerator RebuildPreservesSelectionAndDisposedFieldsCannotMutateTheModel()
        {
            var oldSearch = _tree.Q<TextField>("browser-search");
            oldSearch.value = "nature"; yield return EditorWindowTestSupport.WaitForRender();
            _tree.Q<ListView>("browser-results").SetSelection(0); yield return EditorWindowTestSupport.WaitForRender();
            var selected = _vm.Selected; Rebuild(); oldSearch.value = "obsolete";
            Assert.That(_vm.Search, Is.EqualTo("nature")); Assert.That(_vm.Selected, Is.SameAs(selected));
            Assert.That(_tree.Q<TextField>("browser-search").value, Is.EqualTo("nature"));
            _vm.SetFilter(CatalogRecordKind.Trait, ""); _binding.Dispose();
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<DropdownField>("browser-kind").index, Is.Zero, "Disposed pending callback must not render.");
        }

        [UnityTest]
        public IEnumerator IconsRejectForeignPathsBeforeCallingTheLoaderAndMissingImagesAreSafe()
        {
            foreach (var path in new[] { "https://example.com/x.png", "C:/private.png", "../x.png",
                "Assets/TFTAnalyzer/Data/Generated/Icons/../private.png", "Assets/other.png" })
            {
                SetIcon(path); yield return EditorWindowTestSupport.WaitForRender();
                Assert.That(_iconLoads, Is.Zero, path);
                Assert.That(_tree.Q<Label>("browser-icon-missing").ClassListContains("tft-tab-hidden"), Is.False);
            }
            SetIcon("Assets/TFTAnalyzer/Data/Generated/Icons/" + new string('a', 64) + ".png");
            yield return EditorWindowTestSupport.WaitForRender(); Assert.That(_iconLoads, Is.EqualTo(1));
            Assert.That(_tree.Q<Image>("browser-icon").image, Is.Null);
        }

        private void SetIcon(string path)
        {
            var units = new[] { new UnitRecord("X", new LocalizedText("아이콘", "Icon"), 1, null, path, null) };
            _vm.SetCatalog(CatalogBrowserViewModelTests.Catalog(units: units), DataManagerTestSupport.Identity()); _vm.Select("X");
        }

        [UnityTest]
        public IEnumerator IconLoaderFailureFallsBackWithoutExposingTheException()
        {
            _binding.Dispose(); var calls = 0;
            _binding = new CatalogBrowserTab(_tree.Q("catalog-browser"), _vm, _ =>
            { calls++; throw new System.IO.IOException("private-path-marker"); });
            SetIcon("Assets/TFTAnalyzer/Data/Generated/Icons/" + new string('a', 64) + ".png");
            yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(calls, Is.EqualTo(1)); Assert.That(_tree.Q<Image>("browser-icon").image, Is.Null);
            Assert.That(_tree.Q<Label>("browser-icon-missing").ClassListContains("tft-tab-hidden"), Is.False);
            Assert.That(string.Join("\n", _tree.Query<Label>().ToList().Select(x => x.text)), Does.Not.Contain("private-path-marker"));
        }

        private void Submit(string name)
        {
            var button = _tree.Q<Button>(name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
