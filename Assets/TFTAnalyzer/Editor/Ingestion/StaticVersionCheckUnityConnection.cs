using System;
using System.Collections.Generic;
using System.IO;
using TFTAnalyzer.Editor.Persistence;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TFTAnalyzer.Editor.Ingestion
{
    [InitializeOnLoad]
    internal static class StaticVersionCheckUnityConnection
    {
        internal static StaticVersionCheckScheduler Scheduler { get; }
        private static readonly UnityVersionCheckHost Host;
        private static readonly StaticVersionCheckBootstrap Bootstrap;

        static StaticVersionCheckUnityConnection()
        {
            try
            {
                var paths = new StaticCatalogPaths(Path.GetDirectoryName(Application.dataPath));
                Scheduler = new StaticVersionCheckScheduler(new UnityWebRequestTransport(), () => DateTimeOffset.UtcNow,
                    paths.ProjectRoot, () => LastImportedSourceVersion.Select(new PublishedCatalogLoader(paths).Load(
                        AssetDatabase.LoadAssetAtPath<CatalogIndex>(paths.IndexAssetPath))));
                Host = new UnityVersionCheckHost();
                Bootstrap = new StaticVersionCheckBootstrap(Scheduler, Host);
                AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
                EditorApplication.quitting += Shutdown;
                Bootstrap.Start();
            }
            catch (Exception) { Shutdown(); }
        }

        private static void Shutdown()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;
            Bootstrap?.Dispose();
            Scheduler?.Dispose();
            Host?.Dispose();
        }
    }

    internal sealed class UnityVersionCheckHost : IStaticVersionCheckHost, IErrorCallbacks, IDisposable
    {
        private const string TestRunKey = "TFTAnalyzer.StaticVersionCheck.TestRun";
        private bool _busy, _disposed;
        private readonly Dictionary<Action, EditorApplication.CallbackFunction> _scheduled = new Dictionary<Action, EditorApplication.CallbackFunction>();
        public bool IsBatchMode => Application.isBatchMode;
        public bool IsPlaying => EditorApplication.isPlayingOrWillChangePlaymode;
        public bool IsBusy => EditorApplication.isCompiling || EditorApplication.isUpdating;
        public bool IsTestRunning => SessionState.GetBool(TestRunKey, false);
        public event Action StateChanged;
        public event Action Reloading;

        internal UnityVersionCheckHost()
        {
            _busy = IsBusy;
            TestRunnerApi.RegisterTestCallback(this);
            EditorApplication.update += ObserveIdleTransition;
            EditorApplication.playModeStateChanged += PlayStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }
        public void Schedule(Action callback)
        {
            if (_disposed || _scheduled.ContainsKey(callback)) return;
            EditorApplication.CallbackFunction owned = callback.Invoke;
            _scheduled.Add(callback, owned);
            EditorApplication.delayCall += owned;
        }
        public void Unschedule(Action callback)
        {
            if (!_scheduled.TryGetValue(callback, out var owned)) return;
            EditorApplication.delayCall -= owned;
            _scheduled.Remove(callback);
        }
        private void ObserveIdleTransition()
        {
            var busy = IsBusy;
            if (busy == _busy) return;
            _busy = busy;
            StateChanged?.Invoke();
        }
        private void PlayStateChanged(PlayModeStateChange state) => StateChanged?.Invoke();
        private void BeforeReload() => Reloading?.Invoke();
        public void RunStarted(ITestAdaptor testsToRun) { SessionState.SetBool(TestRunKey, true); StateChanged?.Invoke(); }
        public void RunFinished(ITestResultAdaptor result) => EndTestRun();
        public void OnError(string message) => EndTestRun();
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        private void EndTestRun() { SessionState.SetBool(TestRunKey, false); StateChanged?.Invoke(); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            TestRunnerApi.UnregisterTestCallback(this);
            foreach (var owned in _scheduled.Values) EditorApplication.delayCall -= owned;
            _scheduled.Clear();
            EditorApplication.update -= ObserveIdleTransition;
            EditorApplication.playModeStateChanged -= PlayStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            StateChanged = null;
            Reloading = null;
        }
    }
}
