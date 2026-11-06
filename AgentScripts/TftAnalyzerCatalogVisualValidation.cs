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
public static class CatalogScopedVisualValidation
{
    private static IntPtr[] _nativeBaseline = Array.Empty<IntPtr>();
    private const string Prefix = "TftAnalyzerCatalogValidation_";
    private static string Root => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library/TftAnalyzerValidation");
    private static string OwnershipPath => Path.Combine(Root, "catalog-visual-owned.json");
    private static Type ContainerType => typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow", true);

    public static Task<object> RunStructured() => Execute(false, false, false, true);
    public static Task<object> RunStructuredFailureProbe() => Execute(true, false, false, true);
    public static Task<object> RunStructuredInitializationFailureProbe() => Execute(false, true, false, true);
    public static Task<object> RunComparison() => Execute(false, false, false, false, true);
    public static Task<object> RunComparisonFailureProbe() => Execute(true, false, false, false, true);
    public static Task<object> RunComparisonInitializationFailureProbe() => Execute(false, true, false, false, true);
    public static Task<object> RunCollisionComparison() => Execute(false, false, false, false, false, true);
    public static Task<object> RunCollisionComparisonFailureProbe() => Execute(true, false, false, false, false, true);
    public static Task<object> RunCollisionComparisonInitializationFailureProbe() => Execute(false, true, false, false, false, true);
    public static Task<object> Run() => Execute(false, false);
    public static Task<object> RunFieldOrigins() => Execute(false, false, true);
    public static Task<object> RunFieldOriginFailureProbe() => Execute(true, false, true);
    public static Task<object> RunFieldOriginInitializationFailureProbe() => Execute(false, true, true);
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

    private static async Task<object> Execute(bool failAfterShow, bool failBeforeShow, bool synthetic = false, bool structured = false, bool comparison = false, bool collision = false)
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
        var local = new LocalCatalogs(synthetic, structured, comparison, collision);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(structured || comparison || collision ? 20 : 8)))
        {
            try
            {
                window = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
                id = window.GetInstanceID();
                window.name = name;
                WriteOwnership(id, name, before, beforeContainers, focusId, false);
                data = new DataManagerViewModel(local);
                analysis = new AnalysisViewModel(fake);
                collection = new CollectionViewModel(new OfflineCollection());
                Inject(window, "_dataManager", data);
                Inject(window, "_analysis", analysis);
                Inject(window, "_collection", collection);
                Inject(window, "selectedTab", "data-tab");
                window.titleContent = new GUIContent("TFT Catalog Validation");
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
                stage = "SelectingVerifiedLocalCatalog";
                if (collision)
                    await CaptureCollisionComparison(window, data, id, name, deadline.Token, captures);
                else if (comparison)
                    await CaptureComparison(window, data, id, name, deadline.Token, captures);
                else if (structured)
                    await CaptureStructured(window, data, id, name, deadline.Token, captures);
                else if (synthetic)
                    await CaptureFieldOrigins(window, data, id, name, deadline.Token, captures);
                else
                {
                var row = data.Rows.FirstOrDefault(value => value.IsSelectable);
                if (row == null || !data.SelectActiveCatalog(row.Identity))
                    throw new InvalidOperationException("NoVerifiedLocalCatalog");
                var browser = window.rootVisualElement.Q<Foldout>("catalog-browser");
                browser.value = true;
                window.rootVisualElement.Q<DropdownField>("browser-kind").index = 3;
                await Settle(window, id, name, deadline.Token);
                window.rootVisualElement.Q<ListView>("browser-results").SetSelection(0);
                await Settle(window, id, name, deadline.Token);
                if (string.IsNullOrEmpty(window.rootVisualElement.Q<Label>("browser-detail-text").text))
                    throw new InvalidOperationException("CatalogDetailsNotRendered");
                foreach (var width in new[] { 940, 500 })
                {
                    window.position = new Rect(80, 80, width, width == 940 ? 760 : 700);
                    var scroll = window.rootVisualElement.Q<ScrollView>("data-scroll");
                    stage = "Capture" + width + "Browser";
                    await Settle(window, id, name, deadline.Token);
                    AlignTop(scroll, browser);
                    await Settle(window, id, name, deadline.Token);
                    captures.Add(Capture(window, width, "browser"));
                    stage = "Capture" + width + "Details";
                    AlignTop(scroll, window.rootVisualElement.Q("browser-details"));
                    await Settle(window, id, name, deadline.Token);
                    captures.Add(Capture(window, width, "details"));
                }
                }
            }
            catch (ExpectedProbe) { expectedFailureObserved = true; failure = "ExpectedProbeFailure"; }
            catch (OperationCanceledException) { failure = "ValidationDeadlineOrAbort"; }
            catch (Exception exception)
            {
                var known = new[] { "FieldOriginDetailsMissing", "OwnedNativeIdentityAmbiguous", "OwnedNativeVisibilityUnavailable", "SyntheticCatalogInvalid",
                    "SyntheticComparisonMissing", "ComparisonPlainTextContractMissing", "ComparisonTargetOutsideViewport",
                    "CollisionChangeContractMissing", "CollisionDisplayContractMissing", "CollisionLongPageContractMissing" };
                failure = "VisualValidationFailed:" + exception.GetType().Name +
                    (known.Contains(exception.Message) ? ":" + exception.Message : "");
            }
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
            Passed = cleanupFailure == "" && OfflineCollection.Calls == 0 && fake.LoadCalls == 0 && local.ForbiddenServiceCalls == 0 && remaining == 0 && missing.Length == 0 && added.Length == 0 && restored &&
                addedContainers.Length == 0 && missingContainers.Length == 0 &&
                ((failBeforeShow || failAfterShow) ? expectedFailureObserved : failure == "" && captures.Count == (collision ? 10 : structured || comparison ? 8 : 4)),
            Failure = failure, CleanupFailure = cleanupFailure, Stage = stage,
            ReadOnlyLocalCatalogs = !synthetic && !structured && !comparison && !collision, SyntheticCatalogs = synthetic || structured || comparison || collision,
            StructuredStatic = structured, StaticComparison = comparison, CollisionComparison = collision, NoObservationStore = true, OfflineCredentials = true,
            ActualApiCalls = 0, ForbiddenServiceCalls = local.ForbiddenServiceCalls,
            ComparisonCatalogValidation = comparison || collision ? data?.Rows.Select(row => new
                { SourceGameVersion = row.Identity.Patch.SourceGameVersion, row.ValidationErrorCount, row.ValidationWarningCount }).ToArray() : null,
            UnusedSyntheticAnalysisLoadCalls = fake.LoadCalls, CollectionCalls = OfflineCollection.Calls,
            BeforeIds = before, OwnedIds = new[] { id }, AfterIds = after,
            AddedWindowIds = added, MissingWindowIds = missing,
            BeforeContainerIds = beforeContainers, AfterContainerIds = afterContainers,
            AddedContainers = addedContainers, MissingContainers = missingContainers,
            OwnedWindowsRemaining = remaining, ExistingWindowsPreserved = missing.Length == 0,
            OriginalFocusRestored = restored, PixelVisualReviewRequired = true, Captures = captures.ToArray()
        };
        var file = failBeforeShow ? "catalog-visual-initialization-failure-cleanup.json" :
            failAfterShow ? "catalog-visual-failure-cleanup.json" : "catalog-visual-final.json";
        File.WriteAllText(Path.Combine(Root, collision ? "collision-" + file : comparison ? "comparison-" + file : structured ? "structured-" + file : synthetic ? "field-origin-" + file : file), JsonConvert.SerializeObject(report, Formatting.Indented));
        return report;
    }

    private static async Task CaptureComparison(TftDataAnalyzerWindow window, DataManagerViewModel data,
        int id, string name, CancellationToken token, List<object> captures)
    {
        var root = window.rootVisualElement;
        root.Q<DropdownField>("diff-before").index = 0;
        root.Q<DropdownField>("diff-after").index = 1;
        Submit(root.Q<Button>("compare-button"));
        await Settle(window, id, name, token);
        if (data.Diff == null || data.Diff.Units.Changed.Count != 1 || data.Diff.Units.Changed[0].Fields.Count != 3)
            throw new InvalidOperationException("SyntheticComparisonMissing");
        var diff = root.Q("catalog-diff");
        var group = diff.Query<Foldout>().ToList().First();
        var record = group.Query<Foldout>().ToList().First(x => x.text == ComparisonId);
        group.value = true;
        record.value = true;
        await Settle(window, id, name, token);
        // Include the Foldout's generated Toggle label as well as all data labels.
        if (diff.Query<Label>().ToList().Any(x => x.enableRichText) ||
            !record.Q<Toggle>().Query<Label>().ToList().Any(x => x.text == ComparisonId && !x.enableRichText))
            throw new InvalidOperationException("ComparisonPlainTextContractMissing");
        var fields = data.Diff.Units.Changed[0].Fields;
        foreach (var width in new[] { 940, 500 })
        {
            window.position = new Rect(80, 80, width, 760);
            await Settle(window, id, name, token);
            var scroll = root.Q<ScrollView>("data-scroll");
            AlignTop(scroll, group);
            await Settle(window, id, name, token);
            captures.Add(CaptureComparisonPixels(window, width, "titles", new[] { group.Q<Toggle>(), record.Q<Toggle>() }));
            foreach (var field in fields)
            {
                var expected = $"{field.FieldName}\n이전: {field.Before}\n이후: {field.After}";
                var label = record.Query<Label>().ToList().Single(x => x.text == expected);
                if (label.enableRichText) throw new InvalidOperationException("ComparisonPlainTextContractMissing");
                AlignTop(scroll, label);
                await Settle(window, id, name, token);
                captures.Add(CaptureComparisonPixels(window, width, field.FieldName.Replace('.', '-'), new VisualElement[] { label }));
            }
        }
    }

    private static async Task CaptureCollisionComparison(TftDataAnalyzerWindow window, DataManagerViewModel data,
        int id, string name, CancellationToken token, List<object> captures)
    {
        var root = window.rootVisualElement;
        root.Q<DropdownField>("diff-before").index = 0;
        root.Q<DropdownField>("diff-after").index = 1;
        Submit(root.Q<Button>("compare-button"));
        await Settle(window, id, name, token);
        var cases = new[]
        {
            (Id: CollisionEffectsId, Field: "effects", Before: "{\"A\":1,\"B\":2}", After: "{\"A=1\\u001fB\":2}"),
            (Id: CollisionRelationsId, Field: "traitIds", Before: "[\"A\",\"B\"]", After: "[\"A\\u001fB\"]"),
            (Id: CollisionSourcesId, Field: "sources", Before: CollisionSourceDisplay(false), After: CollisionSourceDisplay(true))
        };
        if (data.Diff == null || data.Diff.Units.Changed.Count != 4 || data.Diff.Units.AddedIds.Count != 0 ||
            data.Diff.Units.RemovedIds.Count != 0)
            throw new InvalidOperationException("CollisionChangeContractMissing");
        foreach (var item in cases)
        {
            var change = data.Diff.Units.Changed.Single(x => x.StableId == item.Id);
            // This assertion concerns the public legacy strings, independently of the rendered structure below.
            if (change.Fields.Count != 1 || change.Fields[0].FieldName != item.Field || change.Fields[0].Before != change.Fields[0].After)
                throw new InvalidOperationException("CollisionChangeContractMissing");
        }
        var diff = root.Q("catalog-diff");
        var group = diff.Query<Foldout>().ToList().First();
        group.value = true;
        var scroll = root.Q<ScrollView>("data-scroll");
        foreach (var width in new[] { 940, 500 })
        {
            window.position = new Rect(80, 80, width, 760);
            foreach (var item in cases)
            {
                foreach (var previous in group.Query<Foldout>().ToList().Where(x => x != group)) previous.value = false;
                var record = group.Query<Foldout>().ToList().Single(x => x.text == item.Id);
                record.value = true;
                await Settle(window, id, name, token);
                var label = record.Q("diff-field-" + item.Field).Q<Label>("diff-field-summary");
                if (item.Before == item.After || label.text != $"{item.Field}\n이전: {item.Before}\n이후: {item.After}" ||
                    diff.Query<TextElement>().ToList().Any(x => x.enableRichText))
                    throw new InvalidOperationException("CollisionDisplayContractMissing");
                AlignTop(scroll, record);
                await Settle(window, id, name, token);
                captures.Add(CaptureComparisonPixels(window, width, item.Field,
                    new VisualElement[] { record.Q<Toggle>(), label }, "collision"));
            }

            foreach (var previous in group.Query<Foldout>().ToList().Where(x => x != group)) previous.value = false;
            var longRecord = group.Query<Foldout>().ToList().Single(x => x.text == CollisionLongId);
            longRecord.value = true;
            var field = longRecord.Q("diff-field-effects");
            var summary = field.Q<Label>("diff-field-summary");
            var full = field.Q<Foldout>("diff-field-full");
            if (!summary.text.Contains("일부 생략") || full == null)
                throw new InvalidOperationException("CollisionLongPageContractMissing");
            full.value = true;
            var sides = new[] { full.Q("diff-before-full"), full.Q("diff-after-full") };
            var firstPages = new List<string>();
            for (var side = 0; side < sides.Length; side++)
            {
                var valueRoot = sides[side];
                if (valueRoot.Q<Button>("diff-value-previous").enabledSelf) Submit(valueRoot.Q<Button>("diff-value-previous"));
                var text = valueRoot.Q<TextField>("diff-value-text");
                var first = text.value;
                if (!text.isReadOnly || first.Length != 4095 || HasUnpairedSurrogate(first) ||
                    !first.Contains(side == 0 ? "BEFORE-BEGIN 😀🚀" : "AFTER-BEGIN 😀🚀") ||
                    !first.Contains("<b>literal</b>") || !first.Contains("\\u001f") || !first.Contains("\\\\u001f") ||
                    !first.Contains("\\n") || !first.Contains("\\\\n") ||
                    valueRoot.Q<Label>("diff-value-page").text != "1 / 2 페이지")
                    throw new InvalidOperationException("CollisionLongPageContractMissing");
                firstPages.Add(first);
                var textScroll = text.Q<ScrollView>();
                if (textScroll != null) textScroll.scrollOffset = Vector2.zero;
            }
            await Settle(window, id, name, token);
            AlignTop(scroll, full);
            await Settle(window, id, name, token);
            captures.Add(CaptureComparisonPixels(window, width, "long-first",
                new VisualElement[] { full.Q<Toggle>(), sides[0], sides[1] }, "collision"));
            for (var side = 0; side < sides.Length; side++)
            {
                var valueRoot = sides[side];
                Submit(valueRoot.Q<Button>("diff-value-next"));
                var last = valueRoot.Q<TextField>("diff-value-text").value;
                var expected = "{" + JsonConvert.SerializeObject(CollisionLongKey(side == 1)) + ":1}";
                if (HasUnpairedSurrogate(last) || !last.StartsWith("😀", StringComparison.Ordinal) ||
                    !last.Contains(side == 0 ? "BEFORE-END 🚀" : "AFTER-END 🚀") || firstPages[side] + last != expected ||
                    valueRoot.Q<Label>("diff-value-page").text != "2 / 2 페이지" ||
                    valueRoot.Q<Button>("diff-value-next").enabledSelf)
                    throw new InvalidOperationException("CollisionLongPageContractMissing");
            }
            await Settle(window, id, name, token);
            AlignTop(scroll, full);
            await Settle(window, id, name, token);
            captures.Add(CaptureComparisonPixels(window, width, "long-last",
                new VisualElement[] { full.Q<Toggle>(), sides[0], sides[1] }, "collision"));
        }
    }

    private static bool HasUnpairedSurrogate(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[++index])) return true;
            }
            else if (char.IsLowSurrogate(value[index])) return true;
        }
        return false;
    }

    private static object CaptureComparisonPixels(TftDataAnalyzerWindow window, int width, string area, VisualElement[] targets, string mode = "comparison")
    {
        var root = window.rootVisualElement;
        var viewport = root.Q<ScrollView>("data-scroll").contentViewport.worldBound;
        if (root.Q("data-tab").resolvedStyle.display == DisplayStyle.None || root.panel == null ||
            targets.Any(x => x == null || x.worldBound.yMin < viewport.yMin - 1 || x.worldBound.yMax > viewport.yMax + 1 ||
                x.worldBound.xMin < viewport.xMin - 1 || x.worldBound.xMax > viewport.xMax + 1))
            throw new InvalidOperationException("ComparisonTargetOutsideViewport");
        var rect = window.position;
        var height = (int)root.panel.visualTree.worldBound.height;
        if (height <= 0 || (int)rect.width != width) throw new InvalidOperationException("CaptureGeometryInvalid");
        if (root.Q("catalog-diff").Query<TextElement>().ToList().Any(x => x.enableRichText))
            throw new InvalidOperationException("ComparisonPlainTextContractMissing");
        var path = Path.Combine(Root, "catalog-window-" + width + "-" + mode + "-" + area + ".png");
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            texture.SetPixels(InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x, rect.y), width, height));
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        return new
        {
            Path = path, Width = width, Height = height, Area = area,
            FoldoutTitles = root.Q("catalog-diff").Query<Foldout>().ToList().Select(x => x.text).ToArray(),
            TargetText = targets.SelectMany(x => x is Label label ? new[] { label } : x.Query<Label>().ToList().ToArray()).Select(x => x.text).ToArray(),
            TargetBounds = targets.Select(x => x.worldBound.ToString()).ToArray(), ViewportBounds = viewport.ToString(),
            AllComparisonLabelsPlainText = root.Q("catalog-diff").Query<Label>().ToList().All(x => !x.enableRichText),
            AllComparisonTextPlainText = root.Q("catalog-diff").Query<TextElement>().ToList().All(x => !x.enableRichText),
            FullValues = targets.SelectMany(x => x.Query<TextField>().ToList()).Select(x => new
                { x.value, x.isReadOnly, Utf16Length = x.value.Length, HasUnpairedSurrogate = HasUnpairedSurrogate(x.value) }).ToArray(),
            ScrollOffset = root.Q<ScrollView>("data-scroll").scrollOffset.ToString(),
            ScrollMaximum = root.Q<ScrollView>("data-scroll").verticalScroller.highValue,
            PixelVisualReviewRequired = true
        };
    }

    private const string ComparisonId = "TEST18_<b>Literal</b>";
    private const string CollisionEffectsId = "COLLISION_<b>effects</b>";
    private const string CollisionRelationsId = "COLLISION_<b>relations</b>";
    private const string CollisionSourcesId = "COLLISION_<b>sources</b>";
    private const string CollisionLongId = "COLLISION_<b>long 😀🚀</b>";

    private static string CollisionLongKey(bool after)
    {
        var prefix = (after ? "AFTER-BEGIN 😀🚀" : "BEFORE-BEGIN 😀🚀") + " <b>literal</b> actual:\u001f literal:\\u001f newline:\n literal:\\n quote:\" slash:\\ ";
        // The JSON object's opening brace and key quote place this pair across the 4096-unit page boundary.
        var padding = 4095 - JsonConvert.SerializeObject(prefix).Length;
        return prefix + new string('x', padding) + "😀 " + new string('z', 80) + (after ? " AFTER-END 🚀" : " BEFORE-END 🚀");
    }

    private static SourceStamp CollisionSource(bool after) => new SourceStamp("Riot Data Dragon",
        "https://ddragon.leagueoflegends.com/validation.json", DateTime.UnixEpoch,
        after ? "a" : "a\u001fb", after ? "b\u001fc" : "c");

    private static string CollisionSourceDisplay(bool after)
    {
        var source = CollisionSource(after);
        return JsonConvert.SerializeObject(new[] { new { source.SourceName, source.Url, source.ETag, source.Sha256, source.IsSupplemental } });
    }

    private static IReadOnlyList<PatchRowViewModel> CollisionCatalogRows()
    {
        var rows = new List<PatchRowViewModel>();
        var label = new LocalizedText("창작 충돌 검증", "Synthetic collision validation");
        var traits = new[] { "A", "B", "A\u001fB" }.Select(trait => new TraitRecord(trait, label, null, null, "", null)).ToArray();
        foreach (var after in new[] { false, true })
        {
            var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", after ? "16.18.2" : "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var official = new SourceStamp("Riot Data Dragon", "https://ddragon.leagueoflegends.com/validation.json", DateTime.UnixEpoch, "", new string('a', 64));
            var sources = new[] { official };
            var units = new[]
            {
                new UnitRecord(CollisionEffectsId, label, 1, null, "", sources, effects: after
                    ? new Dictionary<string, double> { ["A=1\u001fB"] = 2 }
                    : new Dictionary<string, double> { ["A"] = 1, ["B"] = 2 }),
                new UnitRecord(CollisionRelationsId, label, 1, after ? new[] { "A\u001fB" } : new[] { "A", "B" }, "", sources),
                new UnitRecord(CollisionSourcesId, label, 1, null, "", new[] { CollisionSource(after) }),
                new UnitRecord(CollisionLongId, label, 1, null, "", sources,
                    effects: new Dictionary<string, double> { [CollisionLongKey(after)] = 1 })
            };
            var manifest = new PatchManifest(2, identity, "창작 검증 자료", DateTime.UnixEpoch, "", sources, DateTime.UnixEpoch, null, 0, 0, "", "");
            var catalog = new StaticCatalog(manifest, units, traits, null, null);
            var validation = new TFTAnalyzer.Validation.CatalogValidator().Validate(catalog);
            if (!validation.CanPublish) throw new InvalidOperationException("SyntheticCatalogInvalid");
            rows.Add(new PatchRowViewModel(new CatalogIndexEntry(identity, "synthetic-only", "Assets/SyntheticValidation/catalog.json"),
                identity, "synthetic-only", catalog, DateTime.UnixEpoch, validation.ErrorCount, validation.WarningCount, ""));
        }
        return rows.AsReadOnly();
    }

    private static string ComparisonDescription(bool after)
    {
        // Long enough to exercise multiple lines at 500px while keeping the complete before/after field capturable.
        // Supplementary characters remain surrogate pairs in the exact-text assertion and serialized evidence.
        return (after ? "<b>이후 설명 🚀</b> " : "<color=blue>이전 설명 😀</color> ") +
            string.Concat(Enumerable.Repeat("긴 문자열·비BMP 보존 😀🚀 확인. ", 10)) +
            (after ? "<i>AFTER-END 🚀</i>" : "<i>BEFORE-END 😀</i>");
    }

    private static IReadOnlyList<PatchRowViewModel> ComparisonCatalogRows()
    {
        var rows = new List<PatchRowViewModel>();
        foreach (var after in new[] { false, true })
        {
            var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", after ? "16.18.2" : "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var source = new SourceStamp("Riot Data Dragon", "https://ddragon.leagueoflegends.com/validation.json", DateTime.UnixEpoch, "", new string('a', 64));
            var unit = new UnitRecord(ComparisonId,
                new LocalizedText(after ? "<color=red>이후 기물</color>" : "<b>이전 기물</b>", after ? "<i>After unit</i>" : "<size=28>Before unit</size>"),
                3, null, "", new[] { source }, description: new LocalizedText(ComparisonDescription(after), "Unchanged description"));
            var manifest = new PatchManifest(2, identity, "창작 비교 검증 자료", DateTime.UnixEpoch, "", new[] { source }, DateTime.UnixEpoch, null, 0, 0, "", "");
            var catalog = new StaticCatalog(manifest, new[] { unit }, null, null, null);
            var validation = new TFTAnalyzer.Validation.CatalogValidator().Validate(catalog);
            if (!validation.CanPublish)
                throw new InvalidOperationException("SyntheticCatalogInvalid");
            var entry = new CatalogIndexEntry(identity, "synthetic-only", "Assets/SyntheticValidation/catalog.json");
            rows.Add(new PatchRowViewModel(entry, identity, "synthetic-only", catalog, DateTime.UnixEpoch,
                validation.ErrorCount, validation.WarningCount, ""));
        }
        return rows.AsReadOnly();
    }

    private static async Task CaptureFieldOrigins(TftDataAnalyzerWindow window, DataManagerViewModel data,
        int id, string name, CancellationToken token, List<object> captures)
    {
        foreach (var width in new[] { 940, 500 })
        foreach (var row in data.Rows)
        {
            data.SelectActiveCatalog(row.Identity);
            var root = window.rootVisualElement;
            root.Q<Foldout>("catalog-browser").value = true;
            root.Q<DropdownField>("browser-kind").index = 0;
            window.position = new Rect(80, 80, width, width == 940 ? 760 : 700);
            await Settle(window, id, name, token);
            root.Q<ListView>("browser-results").SetSelection(0);
            await Settle(window, id, name, token);
            var text = root.Q<Label>("browser-detail-text").text;
            var modern = row.Catalog.Manifest.SchemaVersion == 3;
            File.WriteAllText(Path.Combine(Root, "field-origin-capture-state.json"), JsonConvert.SerializeObject(new
            { Schema = row.Catalog.Manifest.SchemaVersion, Active = data.ActiveIdentity?.ToString(),
                DetailLength = text?.Length ?? 0, HasCost = text?.Contains("/cost"), HasSupplement = text?.Contains("보완"),
                HasLegacyAbsence = text?.Contains("필드별 출처 정보 없음"), Rows = root.Q<ListView>("browser-results").itemsSource?.Count,
                SelectedIndex = root.Q<ListView>("browser-results").selectedIndex }, Formatting.Indented));
            if (modern ? !text.Contains("/cost") || !text.Contains("보완") : !text.Contains("필드별 출처 정보 없음"))
                throw new InvalidOperationException("FieldOriginDetailsMissing");
            AlignTop(root.Q<ScrollView>("data-scroll"), root.Q("browser-details"));
            await Settle(window, id, name, token);
            captures.Add(Capture(window, width, modern ? "field-origins" : "field-origins-legacy"));
        }
    }

    private static void Submit(Button button)
    {
        using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
    }

    private static async Task CaptureStructured(TftDataAnalyzerWindow window, DataManagerViewModel data,
        int id, string name, CancellationToken token, List<object> captures)
    {
        foreach (var width in new[] { 940, 500 })
        {
            var root = window.rootVisualElement;
            data.SelectActiveCatalog(data.Rows.First(x => x.Catalog.Manifest.SchemaVersion == 4).Identity);
            root.Q<Foldout>("catalog-browser").value = true;
            window.position = new Rect(80, 80, width, 850);
            root.Q<DropdownField>("browser-kind").index = 0;
            await Settle(window, id, name, token);
            root.Q<ListView>("browser-results").SetSelection(0);
            root.Q<Foldout>("browser-structured").value = true;
            await Settle(window, id, name, token);
            Submit(root.Q<Button>("browser-structured-next"));
            await Settle(window, id, name, token);
            if (!root.Q<Label>("browser-structured-status").text.Contains("2/2")) throw new InvalidOperationException("StructuredPageMissing");
            AlignTop(root.Q<ScrollView>("data-scroll"), root.Q("browser-structured-notice"));
            await Settle(window, id, name, token);
            captures.Add(Capture(window, width, "structured-unit-last"));

            root.Q<DropdownField>("browser-kind").index = 1;
            await Settle(window, id, name, token);
            root.Q<ListView>("browser-results").SetSelection(0);
            await Settle(window, id, name, token);
            AlignTop(root.Q<ScrollView>("data-scroll"), root.Q("browser-structured-notice"));
            await Settle(window, id, name, token);
            captures.Add(Capture(window, width, "structured-trait"));

            root.Q<DropdownField>("browser-kind").index = 2;
            await Settle(window, id, name, token);
            root.Q<ListView>("browser-results").SetSelection(0);
            root.Q<Foldout>("browser-extensions").value = true;
            await Settle(window, id, name, token);
            Submit(root.Q("browser-extension-rows").Q<Button>());
            await Settle(window, id, name, token);
            Submit(root.Q<Button>("browser-extension-text-next"));
            await Settle(window, id, name, token);
            Submit(root.Q<Button>("browser-extension-text-next"));
            await Settle(window, id, name, token);
            if (!root.Q<TextField>("browser-extension-text").value.Contains("TAIL-END")) throw new InvalidOperationException("ExtensionTailMissing");
            AlignTop(root.Q<ScrollView>("data-scroll"), root.Q("browser-structured-notice"));
            await Settle(window, id, name, token);
            captures.Add(Capture(window, width, "structured-item-raw-tail"));

            data.SelectActiveCatalog(data.Rows.First(x => x.Catalog.Manifest.SchemaVersion == 2).Identity);
            root.Q<DropdownField>("browser-kind").index = 0;
            await Settle(window, id, name, token);
            root.Q<ListView>("browser-results").SetSelection(0);
            await Settle(window, id, name, token);
            AlignTop(root.Q<ScrollView>("data-scroll"), root.Q("browser-structured-notice"));
            await Settle(window, id, name, token);
            captures.Add(Capture(window, width, "structured-legacy"));
        }
    }

    private static IReadOnlyList<PatchRowViewModel> StructuredCatalogRows()
    {
        var source = new SourceStamp("Riot Data Dragon", "https://ddragon.leagueoflegends.com/validation.json", DateTime.UnixEpoch, "", new string('a',64));
        var sources = new[] { source };
        var identity = new CatalogIdentity(new PatchKey("TFTSet18",18,"18.2","16.18.1"), CatalogSourceProfile.OfficialOnly);
        var stats = Enumerable.Range(0,21).ToDictionary(x => "Stat" + x.ToString("D2"), x => x == 1 ? (double?)null : x);
        var unit = new UnitRecord("TEST18_Validation", new LocalizedText("검증 기물","Validation unit"),3,null,"",sources,
            baseStats: stats, abilityVariables: new[] { new StaticNumericSeries("Damage", new double?[] { 0, 10, null, 30 }) });
        var trait = new TraitRecord("TEST18_Trait",new LocalizedText("검증 시너지","Validation trait"),null,null,"",sources,
            breakpoints: new[] { new TraitBreakpoint(null, 9, 1, new Dictionary<string,double?> { ["Power"] = 0 }) });
        var item = new ItemRecord("TEST18_Item",new LocalizedText("검증 아이템","Validation item"),null,null,null,"",sources,
            unique:false, extensions: new[] { new StaticExtensionField(source.SourceName,"enUs","/incompatibleTokens",
                JsonConvert.SerializeObject(new string('a',4094) + "😀" + new string('b',4500) + "TAIL-END")) });
        var manifest = new PatchManifest(4,identity,"창작 검증 자료",DateTime.UnixEpoch,"",sources,DateTime.UnixEpoch,null,0,0,"","");
        var origins = new List<FieldOrigin>();
        foreach (var pair in new[] { (Type:"unit",Record:(object)unit), (Type:"trait",Record:(object)trait), (Type:"item",Record:(object)item) })
        foreach (var field in FieldOriginFields.Required(pair.Record,4))
            origins.Add(new FieldOrigin(pair.Type, pair.Record is UnitRecord ? unit.StableId : pair.Record is TraitRecord ? trait.StableId : item.StableId, field,sources));
        var catalog = new StaticCatalog(manifest,new[] { unit },new[] { trait },new[] { item },null,origins);
        if (!new TFTAnalyzer.Validation.CatalogValidator().Validate(catalog).CanPublish) throw new InvalidOperationException("SyntheticCatalogInvalid");
        var rows = new List<PatchRowViewModel> { new PatchRowViewModel(new CatalogIndexEntry(identity,"synthetic-only","Assets/SyntheticValidation/catalog.json"), identity,"synthetic-only",catalog,DateTime.UnixEpoch,0,0,"") };
        rows.Add(SyntheticCatalogRows().Single(x => x.Catalog.Manifest.SchemaVersion == 2));
        return rows.AsReadOnly();
    }

    private static IReadOnlyList<PatchRowViewModel> SyntheticCatalogRows()
    {
        var rows = new List<PatchRowViewModel>();
        foreach (var schema in new[] { 3, 2 })
        {
            var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", schema == 3 ? "16.18.1" : "16.18.2"),
                schema == 3 ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly);
            var official = new SourceStamp("Riot Data Dragon", "https://ddragon.leagueoflegends.com/validation.json", DateTime.UnixEpoch, "", new string('a', 64));
            var supplemental = new SourceStamp("CommunityDragon", "https://raw.communitydragon.org/16.18/validation.json", DateTime.UnixEpoch, "", new string('b', 64), true);
            var sources = schema == 3 ? new[] { official, supplemental } : new[] { official };
            var unit = new UnitRecord("TEST18_Validation", new LocalizedText("검증 기물", "Validation unit"), 3, null, "", sources,
                description: new LocalizedText("보완 자료의 설명", "Supplemental description"));
            var manifest = new PatchManifest(schema, identity, "창작 검증 자료", DateTime.UnixEpoch, "", sources,
                DateTime.UnixEpoch, null, 0, 0, "", schema == 3 ? "16.18" : "");
            var origins = schema == 3 ? FieldOriginFields.Required(unit).Select(pointer => new FieldOrigin("unit", unit.StableId,
                pointer, new[] { pointer.StartsWith("/description/", StringComparison.Ordinal) ? supplemental : official })) : null;
            var catalog = new StaticCatalog(manifest, new[] { unit }, null, null, null, origins);
            if (!new TFTAnalyzer.Validation.CatalogValidator().Validate(catalog).CanPublish)
                throw new InvalidOperationException("SyntheticCatalogInvalid");
            var entry = new CatalogIndexEntry(identity, "synthetic-only", "Assets/SyntheticValidation/catalog.json");
            rows.Add(new PatchRowViewModel(entry, identity, "synthetic-only", catalog, DateTime.UnixEpoch, 0, 0, ""));
        }
        return rows.AsReadOnly();
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

    private static object Capture(TftDataAnalyzerWindow window, int width, string area)
    {
        var root = window.rootVisualElement;
        var browser = root.Q<Foldout>("catalog-browser");
        var scroll = root.Q<ScrollView>("data-scroll");
        var footer = root.Q<Label>("historical-data-disclaimer");
        var catalog = root.Q<Label>("browser-catalog");
        var status = root.Q<Label>("browser-status");
        var details = root.Q<Label>("browser-detail-text");
        if (root.Q("data-tab").resolvedStyle.display == DisplayStyle.None || root.panel == null || !browser.value ||
            string.IsNullOrEmpty(catalog.text) || string.IsNullOrEmpty(details.text) || details.enableRichText)
            throw new InvalidOperationException("RenderedCatalogContractMissing");
        var rect = window.position;
        var height = (int)root.panel.visualTree.worldBound.height;
        if (height <= 0 || (int)rect.width != width) throw new InvalidOperationException("CaptureGeometryInvalid");
        var path = Path.Combine(Root, "catalog-window-" + width + "-" + area + ".png");
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            texture.SetPixels(InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x, rect.y), width, height));
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        return new
        {
            Path = path, Width = width, Height = height, Area = area,
            CatalogLabel = catalog.text, StatusLabel = status.text, DetailText = details.text,
            RootBounds = root.worldBound.ToString(), PanelBounds = root.panel.visualTree.worldBound.ToString(),
            PageRows = root.Q<ListView>("browser-results").itemsSource.Count,
            IconVisible = root.Q<Image>("browser-icon").image != null,
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
        public int LoadCalls;
        public Task<AnalysisSnapshot> LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token, int? minimumSamples = null)
        { LoadCalls++; throw new InvalidOperationException("CatalogValidationNeverLoadsAnalysis"); }
    }

    private sealed class LocalCatalogs : IDataManagerService
    {
        private readonly bool _synthetic, _structured, _comparison, _collision;
        public int ForbiddenServiceCalls;
        public LocalCatalogs(bool synthetic = false, bool structured = false, bool comparison = false, bool collision = false)
        { _synthetic = synthetic; _structured = structured; _comparison = comparison; _collision = collision; }
        public IReadOnlyList<PatchRowViewModel> LoadLocalCatalogs()
        {
            if (_collision) return CollisionCatalogRows();
            if (_comparison) return ComparisonCatalogRows();
            if (_structured) return StructuredCatalogRows();
            if (_synthetic) return SyntheticCatalogRows();
            var paths = new StaticCatalogPaths(Path.GetDirectoryName(Application.dataPath));
            return new PublishedCatalogLoader(paths).Load(AssetDatabase.LoadAssetAtPath<CatalogIndex>(StaticCatalogPaths.CatalogIndexAssetPath));
        }
        public string ResolveDestination(StaticImportRequest request) => throw new InvalidOperationException("ValidationNeverImports");
        public Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token)
        { ForbiddenServiceCalls++; throw new InvalidOperationException("ValidationNeverRequestsVersions"); }
        public Task<StaticImportResult> ImportAsync(StaticImportRequest request, CancellationToken token)
        { ForbiddenServiceCalls++; throw new InvalidOperationException("ValidationNeverImports"); }
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
