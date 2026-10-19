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

// Loaded only by run_script; outside Assets so it cannot become a product feature.
public static class AnalysisScopedVisualValidation
{
    private static IntPtr[] _nativeBaseline = Array.Empty<IntPtr>();
    private const string Prefix = "TftAnalyzerAnalysisValidation_";
    private static string Root => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library/TftAnalyzerValidation");
    private static string OwnershipPath => Path.Combine(Root, "analysis-visual-owned.json");
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
        var beforeContainers = ContainerIds();
        _nativeBaseline = NativeEditorWindows();
        var id = 0;
        var name = Prefix + Guid.NewGuid().ToString("N");
        var showAttempted = false;
        TftDataAnalyzerWindow window = null;
        DataManagerViewModel data = null;
        AnalysisViewModel analysis = null;
        CollectionViewModel collection = null;
        var captures = new List<object>();
        var failure = "";
        var cleanupFailure = "";
        var stage = "BeforeCreate";
        var expectedFailureObserved = false;
        OfflineCollection.Calls = 0;
        var fake = new SyntheticAnalysis();
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
        {
            try
            {
                window = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
                id = window.GetInstanceID();
                window.name = name;
                WriteOwnership(id, name, before, beforeContainers, focusId, false);
                data = new DataManagerViewModel(new SyntheticCatalogs(fake.Catalog, fake.Identity));
                analysis = new AnalysisViewModel(fake);
                collection = new CollectionViewModel(new OfflineCollection());
                Inject(window, "_dataManager", data);
                Inject(window, "_analysis", analysis);
                Inject(window, "_collection", collection);
                Inject(window, "selectedTab", "analysis-tab");
                window.titleContent = new GUIContent("TFT Analysis Validation");
                stage = "InjectedBeforeShow";
                if (failBeforeShow) throw new ExpectedProbe();
                showAttempted = true;
                WriteOwnership(id, name, before, beforeContainers, focusId, true);
                window.Show();
                stage = "Shown";
                if (failAfterShow) throw new ExpectedProbe();
                window.position = new Rect(80, 80, 940, 700);
                window.Focus();
                await Settle(window, id, name, deadline.Token);
                window.CreateGUI();
                stage = "SelectingVerifiedSyntheticCatalog";
                window.rootVisualElement.Q<ListView>("catalog-list").SetSelection(0);
                await Settle(window, id, name, deadline.Token);
                if (analysis.ActiveIdentity == null || !analysis.ActiveIdentity.Equals(fake.Identity) ||
                    analysis.ActiveIdentity.Patch.SetNumber != fake.Identity.Patch.SetNumber ||
                    !ReferenceEquals(analysis.Catalog, fake.Catalog))
                    throw new InvalidOperationException("VerifiedSelectionNotTransferred");
                stage = "ExplicitLocalRefresh";
                await analysis.RefreshAsync();
                await Settle(window, id, name, deadline.Token);
                if (fake.LoadCalls != 1 || analysis.IsBusy || !analysis.CanExport ||
                    !analysis.Rows.Any(row => row.Presentation.State == MetricVisibility.Visible) ||
                    !analysis.Rows.Any(row => row.Presentation.State == MetricVisibility.InsufficientSample) ||
                    analysis.Pairs.Count == 0)
                    throw new InvalidOperationException("SyntheticAnalysisStateInvalid");
                foreach (var width in new[] { 940, 500 })
                {
                    window.position = new Rect(80, 80, width, width == 940 ? 700 : 300);
                    var scroll = window.rootVisualElement.Q<ScrollView>("analysis-scroll");
                    scroll.scrollOffset = Vector2.zero;
                    stage = "Capture" + width + "Top";
                    await Settle(window, id, name, deadline.Token);
                    captures.Add(Capture(window, analysis, width, "top"));
                    stage = "Capture" + width + "Rows";
                    var rows = window.rootVisualElement.Q("analysis-rows");
                    AlignTop(scroll, rows.Children().FirstOrDefault() ?? rows);
                    await Settle(window, id, name, deadline.Token);
                    captures.Add(Capture(window, analysis, width, "rows"));
                    stage = "Capture" + width + "Pairs";
                    AlignTop(scroll, window.rootVisualElement.Q("analysis-pairwise-heading"));
                    await Settle(window, id, name, deadline.Token);
                    captures.Add(Capture(window, analysis, width, "pairs"));
                }
            }
            catch (ExpectedProbe) { expectedFailureObserved = true; failure = "ExpectedProbeFailure"; }
            catch (OperationCanceledException) { failure = "ValidationDeadlineOrAbort"; }
            catch (Exception exception) { failure = "VisualValidationFailed:" + exception.GetType().Name; }
            finally
            {
                // Each exact owner still receives cleanup if another cleanup step throws.
                try { analysis?.Dispose(); } catch { cleanupFailure = "AnalysisDisposeFailed"; }
                try { collection?.Dispose(); } catch { cleanupFailure += " CollectionDisposeFailed"; }
                try { data?.Dispose(); } catch { cleanupFailure += " DataDisposeFailed"; }
                try { ReleaseOwned(window, showAttempted); } catch { cleanupFailure += " OwnedWindowCloseFailed"; }
                finally
                {
                    try { if (focus != null) focus.Focus(); }
                    catch { cleanupFailure += " OriginalFocusRestoreFailed"; }
                }
            }
        }
        var after = WindowIds();
        var afterContainers = ContainerIds();
        var added = after.Except(before).ToArray();
        var missing = before.Except(after).ToArray();
        var addedContainers = afterContainers.Except(beforeContainers).ToArray();
        var missingContainers = beforeContainers.Except(afterContainers).ToArray();
        var remaining = after.Contains(id) ? 1 : 0;
        var restored = focusId == 0 || (focus != null && EditorWindow.focusedWindow == focus);
        var report = new
        {
            Passed = cleanupFailure == "" && OfflineCollection.Calls == 0 && remaining == 0 && missing.Length == 0 && added.Length == 0 && restored &&
                addedContainers.Length == 0 && missingContainers.Length == 0 &&
                ((failBeforeShow || failAfterShow) ? expectedFailureObserved : failure == "" && captures.Count == 6),
            Failure = failure, CleanupFailure = cleanupFailure, Stage = stage,
            SyntheticDataOnly = true, NoObservationStore = true, OfflineCredentials = true,
            LoadCalls = fake.LoadCalls, CollectionCalls = OfflineCollection.Calls,
            BeforeIds = before, OwnedIds = new[] { id }, AfterIds = after,
            AddedWindowIds = added, MissingWindowIds = missing,
            BeforeContainerIds = beforeContainers, AfterContainerIds = afterContainers,
            AddedContainers = addedContainers, MissingContainers = missingContainers,
            OwnedWindowsRemaining = remaining, ExistingWindowsPreserved = missing.Length == 0,
            OriginalFocusRestored = restored, PixelVisualReviewRequired = true, Captures = captures.ToArray()
        };
        var file = failBeforeShow ? "analysis-visual-initialization-failure-cleanup.json" :
            failAfterShow ? "analysis-visual-failure-cleanup.json" : "analysis-visual-final.json";
        File.WriteAllText(Path.Combine(Root, file), JsonConvert.SerializeObject(report, Formatting.Indented));
        return report;
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

    private static object Capture(TftDataAnalyzerWindow window, AnalysisViewModel analysis, int width, string area)
    {
        var root = window.rootVisualElement;
        var tab = root.Q("analysis-tab");
        var scroll = root.Q<ScrollView>("analysis-scroll");
        var footer = root.Q<Label>("historical-data-disclaimer");
        var catalog = root.Q<Label>("analysis-catalog");
        var status = root.Q<Label>("analysis-status");
        var period = root.Q<Label>("analysis-period");
        var heading = root.Q<Label>("analysis-pairwise-heading");
        if (tab.resolvedStyle.display == DisplayStyle.None || root.panel == null ||
            string.IsNullOrEmpty(catalog.text) || string.IsNullOrEmpty(period.text) ||
            heading.text != MetricRowViewModel.PairwiseLabel || heading.tooltip != MetricRowViewModel.PairwiseTooltip)
            throw new InvalidOperationException("RenderedAnalysisContractMissing");
        var rect = window.position;
        var height = (int)root.panel.visualTree.worldBound.height;
        if (height <= 0 || (int)rect.width != width) throw new InvalidOperationException("CaptureGeometryInvalid");
        var path = Path.Combine(Root, "analysis-window-" + width + "-" + area + ".png");
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            texture.SetPixels(InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x, rect.y), width, height));
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        return new
        {
            Path = path, Width = width, Height = height, Area = area,
            RootBounds = root.worldBound.ToString(), PanelBounds = root.panel.visualTree.worldBound.ToString(),
            CatalogLabel = catalog.text, StatusLabel = status.text, ObservedPeriodLabel = period.text,
            AggregateCount = analysis.Rows.Count, PairCount = analysis.Pairs.Count,
            VisibleCount = analysis.Rows.Count(row => row.Presentation.State == MetricVisibility.Visible),
            SuppressedCount = analysis.Rows.Count(row => row.Presentation.State == MetricVisibility.InsufficientSample),
            Labels = tab.Query<Label>().ToList().Select(label => new { label.name, label.text }).ToArray(),
            ScrollOffset = scroll.scrollOffset.ToString(), ScrollMaximum = scroll.verticalScroller.highValue,
            FooterText = footer.text, FooterBounds = footer.worldBound.ToString(),
            FooterVisible = footer.worldBound.yMin >= 0 && footer.worldBound.yMax <= root.panel.visualTree.worldBound.yMax,
            PixelVisualReviewRequired = true
        };
    }

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

    private sealed class SyntheticAnalysis : IAnalysisService
    {
        public readonly CatalogIdentity Identity;
        public readonly StaticCatalog Catalog;
        public int LoadCalls;
        public SyntheticAnalysis()
        {
            Identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var time = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var sources = new[] { new SourceStamp("Riot Data Dragon", "https://ddragon.leagueoflegends.com/fabricated.json", time, "", new string('a', 64)) };
            Catalog = new StaticCatalog(new PatchManifest(2, Identity, "화면 검증용 가상 세트", time,
                new string('a', 64), sources, time, null, 0, 0, "", ""),
                Enumerable.Range(1, 9).Select(i => new UnitRecord("VISUAL_Unit" + i, new LocalizedText("검증 기물 " + i, "Visual unit " + i), 2,
                    new[] { "VISUAL_Trait" }, "", sources)),
                new[] { new TraitRecord("VISUAL_Trait", new LocalizedText("검증 시너지", "Visual trait"), null, null, "", sources) },
                new[] { new ItemRecord("VISUAL_Item", new LocalizedText("검증 아이템", "Visual item"), null, null, null, "", sources) },
                new[] { new AugmentRecord("VISUAL_Standard", new LocalizedText("검증 증강", "Visual augment"), "1", null, null, null, "", sources) });
        }
        public Task<AnalysisSnapshot> LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            LoadCalls++;
            if (!ReferenceEquals(catalog, Catalog) || !Identity.Equals(identity) || Identity.Patch.SetNumber != identity.Patch.SetNumber)
                throw new InvalidOperationException("SyntheticSelectionMismatch");
            var observations = Enumerable.Range(0, 100).Select(index => new AnonymousMatchObservation(
                ContentHasher.Sha256Hex("fabricated-analysis-visual-" + index), new MetricScope(identity, "asia", "1100", "ALL"),
                new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddHours(index),
                Enumerable.Range(1, 8).Select(place => new AnonymousParticipant(place, 30,
                    new[] { new AnonymousUnit("VISUAL_Unit" + (index == 99 && place == 1 ? 9 : place), 2, new[] { "VISUAL_Item" }) },
                    new[] { new AnonymousTrait("VISUAL_Trait", 1) }, new[] { "VISUAL_Standard" })).ToArray(), "2", "16.18.1"));
            var policy = new MetricPolicy(100, new MetricPolicyMetadata(identity,
                new Dictionary<string, AugmentPolicyKind> { ["VISUAL_Standard"] = AugmentPolicyKind.Standard }));
            return Task.FromResult(AnalysisService.BuildSnapshot(catalog, identity, observations, policy));
        }
    }

    private sealed class SyntheticCatalogs : IDataManagerService
    {
        private readonly PatchRowViewModel[] _rows;
        public SyntheticCatalogs(StaticCatalog catalog, CatalogIdentity identity)
        {
            _rows = new[] { new PatchRowViewModel(new CatalogIndexEntry(identity, new string('a', 64), "visual-only/catalog.json"),
                identity, "가상 검증 자료 · 실제 파일 없음", catalog, catalog.Manifest.CreatedUtc, 0, 0, "") };
        }
        public IReadOnlyList<PatchRowViewModel> LoadLocalCatalogs() => _rows;
        public string ResolveDestination(StaticImportRequest request) => throw new InvalidOperationException("VisualValidationNeverImports");
        public Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token) => throw new InvalidOperationException("VisualValidationNeverRequestsVersions");
        public Task<StaticImportResult> ImportAsync(StaticImportRequest request, CancellationToken token) => throw new InvalidOperationException("VisualValidationNeverImports");
    }

    private sealed class OfflineCollection : ICollectionService
    {
        public static int Calls;
        public bool ApiKeyConfigured => false;
        public bool SeedConfigured => false;
        public Task<MatchCollectionSummary> CollectAsync(StaticCatalog catalog, MatchCollectionRequest request,
            IProgress<CollectionProgress> progress, CancellationToken token)
        { Calls++; throw new InvalidOperationException("VisualValidationNeverCollects"); }
    }
}
