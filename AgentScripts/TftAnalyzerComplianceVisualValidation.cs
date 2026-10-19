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

// Loaded only by run_script. One exact owned frame hosts fabricated compliance UI.
public static class ComplianceScopedVisualValidation
{
    private static IntPtr[] _nativeBaseline = Array.Empty<IntPtr>();
    private const string Prefix = "TftAnalyzerComplianceValidation_";
    private static string Root => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library/TftAnalyzerValidation");
    private static string OwnershipPath => Path.Combine(Root, "compliance-visual-owned.json");
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
        var before = WindowIds();
        var containers = ContainerIds();
        _nativeBaseline = NativeEditorWindows();
        var name = Prefix + Guid.NewGuid().ToString("N");
        var fixture = Path.GetFullPath(Path.Combine(Root, "compliance-fixture-" + Guid.NewGuid().ToString("N")));
        var id = 0;
        var shown = false;
        var expected = false;
        var failure = "";
        var cleanupFailure = "";
        var stage = "BeforeCreate";
        var captures = new List<object>();
        TftDataAnalyzerWindow window = null;
        ComplianceSettingsProvider provider = null;
        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        WriteOwnership(id, name, before, containers, focusId, false);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
        {
            try
            {
                var store = new ComplianceSettingsStore(fixture);
                store.Save(new ComplianceSettings { policyReviewedOn = "2026-10-19", policyUrls = PolicyCopy.Urls,
                    registrationStatus = "Unverified", acknowledgedAt = null });
                window = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
                id = window.GetInstanceID(); window.name = name;
                WriteOwnership(id, name, before, containers, focusId, false);
                Inject(window, "_dataManager", new DataManagerViewModel(new OfflineCatalogs()));
                Inject(window, "_collection", new CollectionViewModel(new OfflineCollection()));
                Inject(window, "_analysis", new AnalysisViewModel(new OfflineAnalysis()));
                Inject(window, "_about", new AboutViewModel(store));
                Inject(window, "selectedTab", "about-tab");
                window.titleContent = new GUIContent("TFT Policy Validation");
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
                var root = window.rootVisualElement;
                if (!root.Q<Label>("about-compliance-state").text.Contains("등록 미확인"))
                    throw new InvalidOperationException("UnverifiedAboutStateMissing");
                foreach (var width in new[] { 940, 500 })
                {
                    window.position = new Rect(80, 80, width, width == 940 ? 700 : 300);
                    var scroll = root.Q<ScrollView>("about-scroll");
                    scroll.scrollOffset = Vector2.zero;
                    stage = "About" + width + "Top";
                    await Settle(window, id, name, deadline.Token);
                    captures.Add(Capture(window, width, "about-top", scroll));
                    scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
                    stage = "About" + width + "Bottom";
                    await Settle(window, id, name, deadline.Token);
                    captures.Add(Capture(window, width, "about-bottom", scroll));
                }
                // Host the real provider on this exact owned frame; never open/reuse Project Settings.
                stage = "SettingsProvider";
                window.position = new Rect(80, 80, 500, 300);
                provider = new ComplianceSettingsProvider(store, () => now);
                provider.OnActivate("", root);
                await Settle(window, id, name, deadline.Token);
                var settingsScroll = root.Q<ScrollView>("compliance-scroll");
                captures.Add(Capture(window, 500, "settings-top", settingsScroll));
                root.Q<TextField>("compliance-review-date").value = "2026-10-19";
                root.Q<DropdownField>("compliance-registration").value = "ApprovedOrAcknowledged";
                root.Q<Toggle>("compliance-portal-confirmed").value = true;
                Submit(root, "compliance-save");
                if (store.Load().State != ComplianceReadState.Valid || !CompliancePolicy.CanPublish(store.Load().Settings, now))
                    throw new InvalidOperationException("SyntheticAcknowledgementNotSaved");
                settingsScroll.scrollOffset = new Vector2(0, settingsScroll.verticalScroller.highValue);
                stage = "SettingsSaved";
                await Settle(window, id, name, deadline.Token);
                captures.Add(Capture(window, 500, "settings-saved", settingsScroll));
            }
            catch (ExpectedProbe) { expected = true; failure = "ExpectedProbeFailure"; }
            catch (OperationCanceledException) { failure = "ValidationDeadlineOrAbort"; }
            catch (Exception exception) { failure = "VisualValidationFailed:" + exception.GetType().Name; }
            finally
            {
                try { provider?.OnDeactivate(); } catch { cleanupFailure = "ProviderDeactivateFailed"; }
                try { ReleaseOwned(window, shown); } catch { cleanupFailure += " OwnedWindowCloseFailed"; }
                finally
                {
                    try { if (focus != null) focus.Focus(); } catch { cleanupFailure += " OriginalFocusRestoreFailed"; }
                    try
                    {
                        // Only the unique fixture created by this invocation may be removed recursively.
                        var allowed = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        if (!fixture.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("FixtureOutsideValidationRoot");
                        if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
                    }
                    catch { cleanupFailure += " FixtureCleanupFailed"; }
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
            SyntheticSettingsOnly = true, OfflineServices = true, UserSettingsWritten = false,
            BeforeIds = before, AfterIds = after, BeforeContainerIds = containers, AfterContainerIds = afterContainers,
            OwnedIds = new[] { id }, OwnedWindowsRemaining = remaining,
            MissingWindowIds = missing, AddedWindowIds = added, MissingContainers = missingContainers, AddedContainers = addedContainers,
            ExistingWindowsPreserved = missing.Length == 0, OriginalFocusRestored = restored,
            PixelVisualReviewRequired = true, Captures = captures.ToArray()
        };
        var file = failBeforeShow ? "compliance-visual-initialization-failure-cleanup.json" :
            failAfterShow ? "compliance-visual-failure-cleanup.json" : "compliance-visual-final.json";
        File.WriteAllText(Path.Combine(Root, file), JsonConvert.SerializeObject(report, Formatting.Indented));
        return report;
    }

    private static object Capture(TftDataAnalyzerWindow window, int width, string area, ScrollView scroll)
    {
        var root = window.rootVisualElement;
        if (root.panel == null || scroll == null) throw new InvalidOperationException("RenderedComplianceContractMissing");
        var rect = window.position;
        var height = (int)root.panel.visualTree.worldBound.height;
        if (height <= 0 || (int)rect.width != width) throw new InvalidOperationException("CaptureGeometryInvalid");
        var path = Path.Combine(Root, "compliance-window-" + width + "-" + area + ".png");
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

    private static void Submit(VisualElement root, string name)
    {
        var button = root.Q<Button>(name); button.Focus();
        using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
    }

    private sealed class OfflineCatalogs : IDataManagerService
    {
        public IReadOnlyList<PatchRowViewModel> LoadLocalCatalogs() => Array.Empty<PatchRowViewModel>();
        public string ResolveDestination(StaticImportRequest request) => throw new InvalidOperationException("ValidationNeverImports");
        public Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token) => throw new InvalidOperationException("ValidationNeverRequestsVersions");
        public Task<StaticImportResult> ImportAsync(StaticImportRequest request, CancellationToken token) => throw new InvalidOperationException("ValidationNeverImports");
    }
    private sealed class OfflineAnalysis : IAnalysisService
    {
        public Task<AnalysisSnapshot> LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token) =>
            throw new InvalidOperationException("ValidationNeverLoadsObservations");
    }
    private sealed class OfflineCollection : ICollectionService
    {
        public bool ApiKeyConfigured => false;
        public bool SeedConfigured => false;
        public Task<MatchCollectionSummary> CollectAsync(StaticCatalog catalog, MatchCollectionRequest request,
            IProgress<CollectionProgress> progress, CancellationToken token) => throw new InvalidOperationException("ValidationNeverCollects");
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
