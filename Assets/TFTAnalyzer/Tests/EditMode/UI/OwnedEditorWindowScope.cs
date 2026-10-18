using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TFTAnalyzer.Tests.UI
{
    internal sealed class OwnedEditorWindowScope : IDisposable
    {
        private readonly List<EditorWindow> _owned = new List<EditorWindow>();
        private readonly EditorWindow _originalFocus;
        private readonly Action<EditorWindow> _close;
        private bool _disposed;
        internal IReadOnlyCollection<int> OriginalWindowIds { get; }

        internal OwnedEditorWindowScope(Action<EditorWindow> close = null)
        {
            _originalFocus = EditorWindow.focusedWindow;
            OriginalWindowIds = Array.AsReadOnly(Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Select(window => window.GetInstanceID()).ToArray());
            _close = close ?? (window => window.Close());
        }

        internal T Create<T>(Action<T> initialize = null) where T : EditorWindow =>
            (T)Create(typeof(T), window => initialize?.Invoke((T)window));

        internal EditorWindow Create(Type type, Action<EditorWindow> initialize = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OwnedEditorWindowScope));
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            _owned.Add(window);
            try
            {
                initialize?.Invoke(window);
                window.Show();
            }
            catch (Exception initializationError)
            {
                // Close dereferences the native parent before Show; failed setup owns only this exact object.
                try { if (window != null) UnityEngine.Object.DestroyImmediate(window); }
                catch (Exception cleanupError)
                { throw new AggregateException("EditorWindow initialization and cleanup failed.", initializationError, cleanupError); }
                throw;
            }
            return window;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var errors = new List<Exception>();
            try
            {
                foreach (var window in _owned)
                {
                    if (window == null) continue;
                    try { _close(window); } catch (Exception error) { errors.Add(error); }
                    finally
                    {
                        // Also remove a tracked survivor without hiding any real Close failure.
                        if (window != null)
                            try { UnityEngine.Object.DestroyImmediate(window); } catch (Exception error) { errors.Add(error); }
                    }
                }
            }
            finally
            {
                _owned.Clear();
                if (_originalFocus != null)
                    try { _originalFocus.Focus(); } catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count > 0) throw new AggregateException("Owned EditorWindow cleanup failed.", errors);
        }
    }
}
