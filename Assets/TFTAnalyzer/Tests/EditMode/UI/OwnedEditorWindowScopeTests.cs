using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class OwnedEditorWindowScopeTests
    {
        [UnityTest]
        public IEnumerator NormalRepeatedAndAlreadyClosedCleanupPreserveAnUnownedWindow()
        {
            var userFocus = EditorWindow.focusedWindow;
            var sentinel = ScriptableObject.CreateInstance<EditorWindow>();
            EditorWindow owned = null, alreadyClosed = null;
            OwnedEditorWindowScope scope = null;
            try
            {
                sentinel.Show();
                sentinel.Focus();
                yield return null;
                var originalFocus = EditorWindow.focusedWindow;
                scope = new OwnedEditorWindowScope();
                owned = scope.Create<EditorWindow>();
                alreadyClosed = scope.Create<EditorWindow>();
                alreadyClosed.Close();

                Assert.DoesNotThrow(() => scope.Dispose());
                Assert.DoesNotThrow(() => scope.Dispose());

                Assert.That(owned == null, Is.True, "The owned window must close, including on repeated disposal.");
                Assert.That(sentinel != null, Is.True, "An existing unowned window must remain open.");
                Assert.That(scope.OriginalWindowIds, Does.Contain(sentinel.GetInstanceID()));
                if (originalFocus != null) Assert.That(EditorWindow.focusedWindow, Is.SameAs(originalFocus));
            }
            finally
            {
                try { scope?.Dispose(); }
                finally { CloseCreatedAndRestoreFocus(userFocus, true, owned, alreadyClosed, sentinel); }
            }
        }

        [Test]
        public void InitializationExceptionStillClosesTheCreatedWindowWithoutClosingUnownedObjects()
        {
            var userFocus = EditorWindow.focusedWindow;
            var sentinel = ScriptableObject.CreateInstance<EditorWindow>();
            EditorWindow owned = null;
            var scope = new OwnedEditorWindowScope();
            try
            {
                Assert.Throws<InvalidOperationException>(() => scope.Create<EditorWindow>(window =>
                {
                    owned = window;
                    throw new InvalidOperationException("fabricated-setup-failure");
                }));

                scope.Dispose();

                Assert.That(owned == null, Is.True, "Ownership must be registered before initialization or Show can fail.");
                Assert.That(sentinel != null, Is.True);
            }
            finally
            {
                try { scope.Dispose(); }
                finally { CloseCreatedAndRestoreFocus(userFocus, false, owned, sentinel); }
            }
        }

        [Test]
        public void OneCleanupFailureIsObservableAndDoesNotSkipOtherOwnedWindows()
        {
            var userFocus = EditorWindow.focusedWindow;
            EditorWindow first = null, second = null;
            var scope = new OwnedEditorWindowScope(window =>
            {
                var fail = ReferenceEquals(window, first);
                window.Close();
                if (fail) throw new InvalidOperationException("fabricated-close-failure");
            });
            try
            {
                first = scope.Create<EditorWindow>();
                second = scope.Create<EditorWindow>();

                var error = Assert.Throws<AggregateException>(() => scope.Dispose());

                Assert.That(error.InnerExceptions, Has.Count.EqualTo(1));
                Assert.That(first == null, Is.True);
                Assert.That(second == null, Is.True, "A failed close must not prevent closing the next owned window.");
                Assert.DoesNotThrow(() => scope.Dispose());
            }
            finally
            {
                try { scope.Dispose(); }
                finally { CloseCreatedAndRestoreFocus(userFocus, true, first, second); }
            }
        }

        private static void CloseCreatedAndRestoreFocus(EditorWindow userFocus, bool shown, params EditorWindow[] windows)
        {
            // Independent exact-reference fallback also cleans tests when the scope under test is broken.
            var errors = new List<Exception>();
            try
            {
                foreach (var window in windows)
                {
                    if (window != null)
                    {
                        try { if (shown) window.Close(); } catch (Exception error) { errors.Add(error); }
                        finally
                        {
                            // Only unshown objects or survivors of Close use the exact-object fallback.
                            if (window != null)
                                try { UnityEngine.Object.DestroyImmediate(window); } catch (Exception error) { errors.Add(error); }
                        }
                    }
                }
            }
            finally
            {
                if (userFocus != null)
                    try { userFocus.Focus(); } catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count > 0) throw new AggregateException("Cleanup regression fallback failed.", errors);
        }
    }
}
