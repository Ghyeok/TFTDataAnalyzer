using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.Collection;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    internal static class CollectionUITestSupport
    {
        internal static void SetWindowModel(TftDataAnalyzerWindow window, string field, object value) =>
            typeof(TftDataAnalyzerWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(window, value);

        internal static void UseOfflineCollection(TftDataAnalyzerWindow window) =>
            SetWindowModel(window, "_collection", new CollectionViewModel(new CollectionFakeService()));

        internal static void Submit(VisualElement root, string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null);
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        internal static IEnumerator WaitForSettled(CollectionViewModel vm)
        {
            var deadline = EditorApplication.timeSinceStartup + 5;
            while (vm.IsBusy)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Collection did not settle.");
                yield return null;
            }
            yield return WaitForRender();
        }

        internal static IEnumerator WaitForRender() => EditorWindowTestSupport.WaitForRender();
    }
}
