using System.Collections;
using NUnit.Framework;
using UnityEditor;

namespace TFTAnalyzer.Tests.UI
{
    internal static class EditorWindowTestSupport
    {
        internal static IEnumerator WaitForRender()
        {
            var reachedUpdate = false;
            EditorApplication.CallbackFunction barrier = null;
            barrier = () =>
            {
                EditorApplication.update -= barrier;
                reachedUpdate = true;
            };
            EditorApplication.update += barrier;
            try
            {
                // Registered after the binding's one-shot render on the same actual Editor event.
                var deadline = EditorApplication.timeSinceStartup + 5;
                while (!reachedUpdate)
                {
                    Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                        "The Editor did not reach the scheduled UI render boundary.");
                    yield return null;
                }
            }
            finally { EditorApplication.update -= barrier; }
        }
    }
}
