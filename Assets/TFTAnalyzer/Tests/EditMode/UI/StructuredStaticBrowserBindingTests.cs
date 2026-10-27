using System.Collections;
using NUnit.Framework;
using TFTAnalyzer.Editor.UI.Data;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class StructuredStaticBrowserBindingTests
    {
        private OwnedEditorWindowScope _windows; private EditorWindow _host; private VisualElement _tree;
        private CatalogBrowserViewModel _vm; private CatalogBrowserTab _binding;
        [SetUp] public void Before()
        {
            _windows = new OwnedEditorWindowScope();
            try
            {
                _host = _windows.Create<EditorWindow>(); _vm = new CatalogBrowserViewModel();
                var c = StructuredStaticBrowserTests.Catalog(longJson: true); _vm.SetCatalog(c, c.Manifest.Identity); _vm.Select("TEST18_Unit");
                _tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree(); _host.rootVisualElement.Add(_tree);
                _tree.Q<Foldout>("catalog-browser").value = true;
                _tree.Q<Foldout>("browser-structured").value = true; _tree.Q<Foldout>("browser-extensions").value = true;
                _binding = new CatalogBrowserTab(_tree.Q("catalog-browser"), _vm, _ => null);
            }
            catch { After(); throw; }
        }
        [TearDown] public void After() { try { _binding?.Dispose(); } finally { try { _vm?.Dispose(); } finally { _windows?.Dispose(); } } }
        private static void Submit(Button button)
        { Assert.That(button, Is.Not.Null); using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); } }

        [Test]
        public void StructuredStatic_RealUxmlProvidesTwoBoundedSectionsAndReadOnlyRawText()
        {
            Assert.That(_tree.Q<Foldout>("browser-structured"), Is.Not.Null);
            Assert.That(_tree.Q<Foldout>("browser-extensions"), Is.Not.Null);
            Assert.That(_tree.Q<TextField>("browser-extension-text").isReadOnly, Is.True);
        }

        [UnityTest]
        public IEnumerator StructuredStatic_ActualPageButtonsRenderLastValuesAndRawTail()
        {
            Submit(_tree.Q<Button>("browser-structured-next")); yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("browser-structured-status").text, Does.Contain("2/2"));
            Assert.That(_tree.Q("browser-structured-rows").Q<Label>(className: "tft-structured-value").text, Does.Contain("20"));
            Submit(_tree.Q("browser-extension-rows").Q<Button>()); yield return EditorWindowTestSupport.WaitForRender();
            Submit(_tree.Q<Button>("browser-extension-text-next")); yield return EditorWindowTestSupport.WaitForRender();
            Submit(_tree.Q<Button>("browser-extension-text-next")); yield return EditorWindowTestSupport.WaitForRender();
            Assert.That(_tree.Q<TextField>("browser-extension-text").value, Does.Contain("TAIL-END"));
            foreach (var label in _tree.Q("browser-structured-rows").Query<Label>().ToList()) Assert.That(label.enableRichText, Is.False);
        }

        [UnityTest]
        public IEnumerator StructuredStatic_DisposeCancelsQueuedDetailRender()
        {
            var label = _tree.Q<Label>("browser-structured-status"); Assert.That(label, Is.Not.Null); var before = label.text;
            StructuredStaticBrowserTests.Call(_vm, "SetStructuredPage", 1); _binding.Dispose();
            yield return EditorWindowTestSupport.WaitForRender(); Assert.That(label.text, Is.EqualTo(before));
        }
    }
}
