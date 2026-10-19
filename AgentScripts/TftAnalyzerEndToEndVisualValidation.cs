using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Editor.UI.Collection;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

using TFTAnalyzer.Editor.Compliance;
using TFTAnalyzer.Editor.UI.About;

// Loaded only by run_script. One exact owned frame hosts fabricated end-to-end workflow.
public static class EndToEndScopedVisualValidation
{
    private static IntPtr[] _nativeBaseline = Array.Empty<IntPtr>();
    private const string Prefix = "TftAnalyzerEndToEndValidation_";
    private static string Root => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library/TftAnalyzerValidation");
    private static string OwnershipPath => Path.Combine(Root, "e2e-visual-owned.json");
    private static Type ContainerType => typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow", true);
    public static Task<object> Run() => Execute(false, false);
    public static Task<object> RunFailureProbe() => Execute(true, false);
    public static Task<object> RunInitializationFailureProbe() => Execute(false, true);
    public static object CleanupLastOwned()
    {
        if (!File.Exists(OwnershipPath))
            return new { Verified = false, Reason = "OwnershipRecordUnavailable", OwnedRemaining = -1 };
        var record = JObject.Parse(File.ReadAllText(OwnershipPath));
        var id = (int)record["Id"];
        var name = (string)record["Name"];
        if (string.IsNullOrEmpty(name) || !name.StartsWith(Prefix, StringComparison.Ordinal))
            return new { Verified = false, Reason = "OwnershipRecordInvalid", OwnedRemaining = -1 };
        // The running job observes this before its next frame/capture, even if CLI transport timed out.
        record["AbortRequested"] = true;
        File.WriteAllText(OwnershipPath, record.ToString(Formatting.Indented));
        var owned = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(value =>
            value.GetInstanceID() == id && value.name == name);
        var cleanupFailure = "";
        try { ReleaseOwned(owned, (bool?)record["ShowAttempted"] == true); }
        catch { cleanupFailure = "ExactOwnedCleanupFailed"; }
        var priorFocus = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(value =>
            value.GetInstanceID() == (int)record["FocusId"]);
        if (priorFocus != null) priorFocus.Focus();
        var afterIds = WindowIds();
        var afterContainers = ContainerIds();
        var beforeIds = record["BeforeIds"].ToObject<int[]>();
        var beforeContainers = record["BeforeContainerIds"].ToObject<int[]>();
        var remaining = afterIds.Contains(id) ? 1 : 0;
        var missing = beforeIds.Except(afterIds).ToArray();
        var added = afterIds.Except(beforeIds).ToArray();
        var missingContainers = beforeContainers.Except(afterContainers).ToArray();
        var addedContainers = afterContainers.Except(beforeContainers).ToArray();
        var restored = (int)record["FocusId"] == 0 ||
            (priorFocus != null && EditorWindow.focusedWindow == priorFocus);
        return new
        {
            Verified = cleanupFailure == "" && remaining == 0 && missing.Length == 0 && added.Length == 0 &&
                missingContainers.Length == 0 && addedContainers.Length == 0 && restored,
            Failure = cleanupFailure, OwnedRemaining = remaining, MissingWindowIds = missing, AddedWindowIds = added,
            BeforeIds = beforeIds, AfterIds = afterIds, BeforeContainerIds = beforeContainers,
            AfterContainerIds = afterContainers, MissingContainers = missingContainers, AddedContainers = addedContainers,
            OriginalFocusRestored = restored
        };
    }

    private static async Task<object> Execute(bool failAfterShow, bool failBeforeShow)
    {
        Directory.CreateDirectory(Root);
        var focus = EditorWindow.focusedWindow;
        var focusId = focus == null ? 0 : focus.GetInstanceID();
        var before = WindowIds(); var containers = ContainerIds();
        _nativeBaseline = NativeEditorWindows();
        var name = Prefix + Guid.NewGuid().ToString("N");
        var id = 0; var shown = false; var expected = false;
        var failure = ""; var cleanupFailure = ""; var stage = "BeforeCreate";
        var captures = new List<object>();
        TftDataAnalyzerWindow window = null;
        IDisposable scenario = null;
        WriteOwnership(id, name, before, containers, focusId, false);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            try
            {
                var scenarioType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("TFTAnalyzer.Tests.UI.TftAnalyzerEndToEndScenario", false))
                    .SingleOrDefault(t => t != null);
                if (scenarioType == null) throw new InvalidOperationException("ActualEndToEndScenarioUnavailable");
                scenario = (IDisposable)Activator.CreateInstance(scenarioType, true);
                window = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
                id = window.GetInstanceID(); window.name = name;
                WriteOwnership(id, name, before, containers, focusId, false);
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                scenarioType.GetMethod("ConfigureWindow", flags).Invoke(scenario, new object[] { window });
                window.titleContent = new GUIContent("TFT Fabricated Workflow Validation");
                stage = "BeforeShow";
                if (failBeforeShow) throw new ExpectedProbe();
                shown = true; WriteOwnership(id, name, before, containers, focusId, true);
                window.Show();
                stage = "AfterShow";
                if (failAfterShow) throw new ExpectedProbe();
                window.position = new Rect(80, 80, 940, 700);
                await Settle(window, id, name, deadline.Token);
                window.CreateGUI();
                await Settle(window, id, name, deadline.Token);
                Func<string, Task> captureStage = async area =>
                {
                    foreach (var width in new[] { 940, 500 })
                    {
                        deadline.Token.ThrowIfCancellationRequested();
                        stage = area + width;
                        window.position = new Rect(80, 80, width, width == 940 ? 700 : 320);
                        var scrollName = area == "import" ? "data-scroll" : area + "-scroll";
                        var scroll = window.rootVisualElement.Q<ScrollView>(scrollName);
                        if (scroll == null) throw new InvalidOperationException("StageScrollUnavailable");
                        scroll.scrollOffset = Vector2.zero;
                        await Settle(window, id, name, deadline.Token);
                        var targetName = area == "import" ? "catalog-list" :
                            area == "collection" ? "collection-status" : "analysis-pairwise-heading";
                        var target = window.rootVisualElement.Q(targetName);
                        if (target == null) throw new InvalidOperationException("StageTargetUnavailable");
                        AlignTop(scroll, target);
                        await Settle(window, id, name, deadline.Token);
                        captures.Add(Capture(window, width, area, scroll));
                    }
                };
                var run = scenarioType.GetMethod("RunAsync", flags);
                if (run == null || run.GetParameters().Length != 3)
                    throw new InvalidOperationException("CancelableScenarioContractUnavailable");
                stage = "ActualFabricatedPipeline";
                await (Task)run.Invoke(scenario, new object[] { window, captureStage, deadline.Token });
                stage = "Completed";
            }
            catch (ExpectedProbe) { expected = true; failure = "ExpectedProbeFailure"; }
            catch (OperationCanceledException) { failure = "ValidationDeadlineOrAbort"; }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
                failure = "VisualValidationFailed:" + cause.GetType().Name;
                File.WriteAllText(Path.Combine(Root, "e2e-visual-failure-detail.txt"), cause.GetType().FullName + Environment.NewLine + cause.StackTrace);
                var data = scenario?.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(scenario) as DataManagerViewModel;
                File.WriteAllText(Path.Combine(Root, "e2e-visual-failure-state.json"), JsonConvert.SerializeObject(new
                {
                    Stage = stage, DataState = data?.State.ToString(), DataErrorCode = data?.ErrorCode
                }, Formatting.Indented));
            }
            finally
            {
                deadline.Cancel();
                try { ReleaseOwned(window, shown); } catch { cleanupFailure = "OwnedWindowCloseFailed"; }
                finally
                {
                    try { scenario?.Dispose(); } catch { cleanupFailure += " ScenarioCleanupFailed"; }
                    try { if (focus != null) focus.Focus(); } catch { cleanupFailure += " OriginalFocusRestoreFailed"; }
                }
            }
        }
        var after = WindowIds(); var afterContainers = ContainerIds();
        var missing = before.Except(after).ToArray(); var added = after.Except(before).ToArray();
        var missingContainers = containers.Except(afterContainers).ToArray();
        var addedContainers = afterContainers.Except(containers).ToArray();
        var restored = focusId == 0 || (focus != null && EditorWindow.focusedWindow == focus);
        var remaining = after.Contains(id) ? 1 : 0;
        var report = new
        {
            Passed = cleanupFailure == "" && remaining == 0 && missing.Length == 0 && added.Length == 0 &&
                missingContainers.Length == 0 && addedContainers.Length == 0 && restored &&
                ((failBeforeShow || failAfterShow) ? expected : failure == "" && captures.Count == 6),
            Failure = failure, CleanupFailure = cleanupFailure, Stage = stage,
            FabricatedTransportsOnly = true, RealPipelineAndAssetDatabase = true, UserCredentialsUsed = false,
            BeforeIds = before, AfterIds = after, BeforeContainerIds = containers, AfterContainerIds = afterContainers,
            OwnedIds = new[] { id }, OwnedWindowsRemaining = remaining,
            MissingWindowIds = missing, AddedWindowIds = added, MissingContainers = missingContainers, AddedContainers = addedContainers,
            ExistingWindowsPreserved = missing.Length == 0, OriginalFocusRestored = restored,
            PixelVisualReviewRequired = true, Captures = captures.ToArray()
        };
        var file = failBeforeShow ? "e2e-visual-initialization-failure-cleanup.json" :
            failAfterShow ? "e2e-visual-failure-cleanup.json" : "e2e-visual-final.json";
        File.WriteAllText(Path.Combine(Root, file), JsonConvert.SerializeObject(report, Formatting.Indented));
        return report;
    }

    private static object Capture(TftDataAnalyzerWindow window, int width, string area, ScrollView scroll)
    {
        var root = window.rootVisualElement;
        if (root.panel == null || scroll == null) throw new InvalidOperationException("RenderedStageContractMissing");
        var rect = window.position;
        var height = (int)root.panel.visualTree.worldBound.height;
        if (height <= 0 || (int)rect.width != width) throw new InvalidOperationException("CaptureGeometryInvalid");
        var path = Path.Combine(Root, "e2e-window-" + width + "-" + area + ".png");
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            texture.SetPixels(InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x, rect.y), width, height));
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        return new { Path = path, Width = width, Height = height, Area = area,
            ScrollMaximum = scroll.verticalScroller.highValue, ScrollOffset = scroll.scrollOffset.ToString(),
            Labels = root.Query<Label>().ToList().Select(label => new { label.name, label.text }).ToArray(),
            PixelVisualReviewRequired = true };
    }

    private static void WriteOwnership(int id, string name, int[] before, int[] containers, int focus, bool shown)
    {
        File.WriteAllText(OwnershipPath, JsonConvert.SerializeObject(new
        {
            Id = id, Name = name, BeforeIds = before, BeforeContainerIds = containers,
            FocusId = focus, ShowAttempted = shown, AbortRequested = false
        }, Formatting.Indented));
    }

    private static void Inject(EditorWindow window, string field, object value)
    {
        var member = window.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (member == null) throw new InvalidOperationException("ExpectedWindowFieldUnavailable");
        member.SetValue(window, value);
    }

    private static async Task Settle(EditorWindow window, int id, string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (window == null || window.GetInstanceID() != id || window.name != name ||
            (bool?)JObject.Parse(File.ReadAllText(OwnershipPath))["AbortRequested"] == true)
            throw new OperationCanceledException();
        window.Focus();
        window.Repaint();
        ShowExactOwnedNativeWindow(window);
        await Task.Delay(160, token);
        token.ThrowIfCancellationRequested();
        if (window == null) throw new OperationCanceledException();
    }

    private static void ShowExactOwnedNativeWindow(EditorWindow window)
    {
        if (Application.platform != RuntimePlatform.WindowsEditor) return;
        var added = NativeEditorWindows().Except(_nativeBaseline).ToArray();
        // Match the single frame created after this run's recorded native baseline, never a title/type.
        if (added.Length != 1 || window == null || !window.name.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("OwnedNativeIdentityAmbiguous");
        if (!SetWindowPos(added[0], new IntPtr(-1), 0, 0, 0, 0, 0x0013))
            throw new InvalidOperationException("OwnedNativeVisibilityUnavailable");
    }

    private static IntPtr[] NativeEditorWindows()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor) return Array.Empty<IntPtr>();
        var process = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        var handles = new List<IntPtr>();
        NativeWindowCallback callback = (handle, _) =>
        {
            uint owner;
            GetWindowThreadProcessId(handle, out owner);
            if (owner == process) handles.Add(handle);
            return true;
        };
        EnumWindows(callback, IntPtr.Zero);
        return handles.ToArray();
    }

    private delegate bool NativeWindowCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(NativeWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);

    private static void AlignTop(ScrollView scroll, VisualElement target)
    {
        // ScrollTo only makes the heading minimally visible, often leaving its rows below the viewport.
        var desired = scroll.scrollOffset.y + target.worldBound.yMin - scroll.contentViewport.worldBound.yMin;
        scroll.scrollOffset = new Vector2(0, Mathf.Clamp(desired, 0, scroll.verticalScroller.highValue));
    }

    private static int[] WindowIds() => Resources.FindObjectsOfTypeAll<EditorWindow>().Select(value => value.GetInstanceID()).OrderBy(value => value).ToArray();
    private static int[] ContainerIds() => Resources.FindObjectsOfTypeAll(ContainerType).Select(value => value.GetInstanceID()).OrderBy(value => value).ToArray();

    private static void ReleaseOwned(EditorWindow owned, bool showAttempted)
    {
        if (owned == null) return;
        if (!showAttempted) { UnityEngine.Object.DestroyImmediate(owned); return; }
        try { owned.Close(); }
        finally { if (owned != null) UnityEngine.Object.DestroyImmediate(owned); }
        // Native-frame preservation is verified separately, including after fallback destruction.
    }

    private sealed class ExpectedProbe : Exception { }

}
