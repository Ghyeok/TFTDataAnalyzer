using System.Collections;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Editor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class TftDataAnalyzerWindowTests
    {
        private const string UxmlPath = "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml";
        private const string UssPath = "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss";
        private const string MissingUxml = "Assets/TFTAnalyzer/Editor/UI/__missing_window_test__.uxml";
        private const string MissingUss = "Assets/TFTAnalyzer/Editor/UI/__missing_window_test__.uss";
        private const string Disclaimer = "과거 관측 결과이며 전투 예측이나 플레이 권고가 아닙니다.";
        private static readonly string[] TabNames = { "data-tab", "collection-tab", "analysis-tab", "about-tab" };
        private TftDataAnalyzerWindow _window;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _window = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
            _window.Show();
            _window.CreateGUI();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
                Object.DestroyImmediate(_window);
        }

        [Test]
        public void CreateGUI_LoadsRealAssetsAndShowsOnlyDataInitially()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath), Is.Not.Null);
            var toolbar = _window.rootVisualElement.Q<Toolbar>();
            Assert.That(toolbar, Is.Not.Null, "Tab navigation belongs in an Editor toolbar.");
            foreach (var tab in TabNames)
            {
                Assert.That(_window.rootVisualElement.Query<VisualElement>(tab).ToList(), Has.Count.EqualTo(1));
                Assert.That(toolbar.Q<Button>(tab + "-button"), Is.Not.Null);
            }

            AssertOnlyVisibleTab("data-tab");
            AssertPersistentDisclaimer();
            Assert.That(_window.rootVisualElement.Q("ui-load-error"), Is.Null);
        }

        [UnityTest]
        public IEnumerator SubmittingEachToolbarButtonChangesOnlyTheContentAndKeepsFooter()
        {
            foreach (var tab in new[] { "collection-tab", "analysis-tab", "about-tab", "data-tab" })
            {
                ActivateTab(tab);
                yield return null;
                AssertOnlyVisibleTab(tab);
                AssertPersistentDisclaimer();
            }
        }

        [UnityTest]
        public IEnumerator RepeatedCreateGUI_PreservesSelectionWithoutDuplicatingTreeOrStyles()
        {
            ActivateTab("analysis-tab");
            yield return null;
            _window.CreateGUI();
            _window.CreateGUI();
            yield return null;

            AssertOnlyVisibleTab("analysis-tab");
            foreach (var tab in TabNames)
            {
                Assert.That(_window.rootVisualElement.Query<VisualElement>(tab).ToList(), Has.Count.EqualTo(1));
                Assert.That(_window.rootVisualElement.Query<Button>(tab + "-button").ToList(), Has.Count.EqualTo(1));
            }
            Assert.That(_window.rootVisualElement.Query<Toolbar>().ToList(), Has.Count.EqualTo(1));
            AssertPersistentDisclaimer();
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            Assert.That(CountStylesheetReferences(_window.rootVisualElement, stylesheet), Is.EqualTo(1));

            ActivateTab("collection-tab");
            yield return null;
            AssertOnlyVisibleTab("collection-tab");
        }

        [UnityTest]
        public IEnumerator SerializedWindowState_RestoresSelectedTabInAnotherInstance()
        {
            ActivateTab("about-tab");
            yield return null;
            var state = EditorJsonUtility.ToJson(_window);
            var restored = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(state, restored);
                restored.CreateGUI();
                restored.Show();
                yield return null;
                AssertOnlyVisibleTab("about-tab", restored);
            }
            finally
            {
                Object.DestroyImmediate(restored);
            }
        }

        [UnityTest]
        public IEnumerator InvalidSerializedSelection_FallsBackToData()
        {
            ActivateTab("analysis-tab");
            yield return null;
            using (var serializedWindow = new SerializedObject(_window))
            {
                var selection = serializedWindow.FindProperty("selectedTab");
                selection.stringValue = "unknown-tab";
                serializedWindow.ApplyModifiedPropertiesWithoutUndo();
                serializedWindow.Update();
                Assert.That(selection.stringValue, Is.EqualTo("unknown-tab"), "Invalid selection must reach serialized state.");
            }
            _window.CreateGUI();
            yield return null;
            AssertOnlyVisibleTab("data-tab");
            AssertPersistentDisclaimer();
        }

        [UnityTest]
        public IEnumerator MissingUxml_ShowsItsPathAndPersistentDisclaimer()
        {
            yield return AssertMissingAssets(MissingUxml, UssPath, MissingUxml);
        }

        [UnityTest]
        public IEnumerator MissingUss_ShowsItsPathAndPersistentDisclaimer()
        {
            yield return AssertMissingAssets(UxmlPath, MissingUss, MissingUss);
        }

        [UnityTest]
        public IEnumerator MissingBothAssets_IdentifiesBothPathsAndKeepsDisclaimer()
        {
            yield return AssertMissingAssets(MissingUxml, MissingUss, MissingUxml, MissingUss);
        }

        [UnityTest]
        public IEnumerator CreateGUI_AfterLoadFailureRestoresWorkingTabsAndSelection()
        {
            ActivateTab("collection-tab");
            yield return null;
            _window.RebuildUI(MissingUxml, MissingUss);
            yield return null;
            Assert.That(_window.rootVisualElement.Q<Label>("ui-load-error"), Is.Not.Null);
            _window.CreateGUI();
            yield return null;

            Assert.That(_window.rootVisualElement.Q("ui-load-error"), Is.Null);
            AssertOnlyVisibleTab("collection-tab");
            AssertPersistentDisclaimer();
            ActivateTab("analysis-tab");
            yield return null;
            AssertOnlyVisibleTab("analysis-tab");
        }

        [UnityTest]
        public IEnumerator AtMinimumSize_ContentAndDisclaimerUseWrappingText()
        {
            var position = _window.position;
            _window.position = new Rect(position.x, position.y, _window.minSize.x, _window.minSize.y);
            foreach (var tab in TabNames)
            {
                ActivateTab(tab);
                yield return null;
                var labels = _window.rootVisualElement.Q(tab).Query<Label>().ToList();
                Assert.That(labels, Is.Not.Empty, "Each tab needs a purpose and a future connection notice.");
                foreach (var label in labels.Where(label => !string.IsNullOrWhiteSpace(label.text)))
                    Assert.That(label.resolvedStyle.whiteSpace, Is.EqualTo(WhiteSpace.Normal));
                AssertPersistentDisclaimer();
                Assert.That(_window.rootVisualElement.Q<Label>("historical-data-disclaimer").resolvedStyle.whiteSpace,
                    Is.EqualTo(WhiteSpace.Normal));
            }
        }

        private IEnumerator AssertMissingAssets(string uxmlPath, string ussPath, params string[] missingPaths)
        {
            _window.RebuildUI(uxmlPath, ussPath);
            yield return null;
            var errors = _window.rootVisualElement.Query<Label>("ui-load-error").ToList();
            Assert.That(errors, Has.Count.EqualTo(1));
            foreach (var path in missingPaths)
                Assert.That(errors[0].text, Does.Contain(path));
            Assert.That(errors[0].resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(_window.rootVisualElement.Q<Toolbar>(), Is.Null, "A failed load must replace stale navigation.");
            AssertPersistentDisclaimer();
        }

        private void ActivateTab(string tab)
        {
            var button = _window.rootVisualElement.Q<Button>(tab + "-button");
            Assert.That(button, Is.Not.Null);
            button.Focus();
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = button;
                button.SendEvent(click);
            }
        }

        private void AssertOnlyVisibleTab(string selected, TftDataAnalyzerWindow window = null)
        {
            var root = (window == null ? _window : window).rootVisualElement;
            foreach (var tab in TabNames)
            {
                var content = root.Q(tab);
                Assert.That(content, Is.Not.Null);
                Assert.That(content.resolvedStyle.display,
                    tab == selected ? Is.EqualTo(DisplayStyle.Flex) : Is.EqualTo(DisplayStyle.None), tab);
            }
        }

        private void AssertPersistentDisclaimer()
        {
            var labels = _window.rootVisualElement.Query<Label>("historical-data-disclaimer").ToList();
            Assert.That(labels, Has.Count.EqualTo(1));
            Assert.That(labels[0].text, Is.EqualTo(Disclaimer));
            Assert.That(labels[0].resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            foreach (var tab in TabNames)
            {
                var content = _window.rootVisualElement.Q(tab);
                if (content != null)
                    Assert.That(content.Contains(labels[0]), Is.False, "Footer must be outside every tab.");
            }
        }

        private static int CountStylesheetReferences(VisualElement element, StyleSheet stylesheet)
        {
            var count = 0;
            for (var index = 0; index < element.styleSheets.count; index++)
                if (element.styleSheets[index] == stylesheet)
                    count++;
            foreach (var child in element.Children())
                count += CountStylesheetReferences(child, stylesheet);
            return count;
        }
    }
}
