using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.Collection;
using TFTAnalyzer.Editor.UI.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

public static class CollectionScopedVisualValidation
{
    public static Task<object> Run() => Execute(false);
    public static Task<object> RunFailureProbe() => Execute(true);
    public static Task<object> RunInitializationFailureProbe() => Execute(true, true);
    public static Task<object> RunQuarantine() => Execute(false, false, true);

    public static object CleanupLastOwned()
    {
        var path=Path.Combine(Path.GetDirectoryName(Application.dataPath),
            "Library/TftAnalyzerValidation/collection-visual-owned.json");
        if (!File.Exists(path)) return new {Closed=0,OwnedRemaining=0};
        var record=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
        var id=(int)record["Id"];
        var name=(string)record["Name"];
        var owned=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(value =>
            value.GetInstanceID()==id && value.name==name && name.StartsWith("TftAnalyzerValidation_",StringComparison.Ordinal));
        var closed=owned==null ? 0 : 1;
        if (owned!=null) ReleaseOwned(owned, true);
        return new {Closed=closed,OwnedRemaining=Resources.FindObjectsOfTypeAll<EditorWindow>().Count(value =>
            value.GetInstanceID()==id && value.name==name)};
    }

    private static async Task<object> Execute(bool failAfterOpen, bool failBeforeShow=false, bool quarantine=false)
    {
        var previousFocus = EditorWindow.focusedWindow;
        var existing = Resources.FindObjectsOfTypeAll<EditorWindow>();
        var beforeIds = existing.Select(value => value.GetInstanceID()).ToArray();
        var containerType=typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow",true);
        var beforeContainerIds=Resources.FindObjectsOfTypeAll(containerType).Select(value=>value.GetInstanceID()).ToArray();
        var ownedId = 0;
        var shown=false;
        TftDataAnalyzerWindow owned = null;
        var failure = "";
        var stage = "Opening";
        var captures = new System.Collections.Generic.List<object>();
        var root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library/TftAnalyzerValidation");
        var ownershipPath = Path.Combine(root, "collection-visual-owned.json");
        var service = new OfflineStatus(quarantine);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(quarantine ? 8 : 5)))
        {
            try
            {
                owned = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
                ownedId = owned.GetInstanceID();
                owned.name = "TftAnalyzerValidation_" + Guid.NewGuid().ToString("N");
                File.WriteAllText(ownershipPath, JsonConvert.SerializeObject(new { Id=ownedId, Name=owned.name }));
                typeof(TftDataAnalyzerWindow).GetField("_collection", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(owned, new CollectionViewModel(service));
                owned.titleContent = new GUIContent("TFT Data Analyzer");
                if (failBeforeShow) throw new InvalidOperationException("Expected initialization cleanup probe.");
                owned.Show();
                shown=true;
                if (failAfterOpen) throw new InvalidOperationException("Expected cleanup probe.");
                owned.position = new Rect(80, 80, 940, 700);
                owned.Focus();
                await Task.Delay(200, deadline.Token);
                owned.CreateGUI();
                stage="SelectingVerifiedCatalog";
                // UI input events are covered by the real-event tests; this command only verifies rendering.
                var data=(DataManagerViewModel)typeof(TftDataAnalyzerWindow)
                    .GetField("_dataManager",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owned);
                var selected=data.Rows.First(value=>value.IsSelectable);
                if (!data.SelectActiveCatalog(selected.Identity)) throw new InvalidOperationException("Catalog selection failed.");
                typeof(TftDataAnalyzerWindow).GetField("selectedTab",BindingFlags.NonPublic|BindingFlags.Instance)
                    .SetValue(owned,"collection-tab");
                owned.CreateGUI();
                if (quarantine)
                {
                    var model = (CollectionViewModel)typeof(TftDataAnalyzerWindow)
                        .GetField("_collection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owned);
                    foreach (var width in new[] { 940, 500 })
                    {
                        owned.position = new Rect(80, 80, width, 700);
                        foreach (var scenario in new[] { "saved", "failed", "truncated" })
                        {
                            service.Scenario = scenario;
                            await model.StartAsync(summary => true);
                            if (model.Progress.Quarantined != 2) throw new InvalidOperationException("Synthetic run did not settle.");
                            owned.Focus(); owned.Repaint();
                            await Task.Delay(200, deadline.Token);
                            stage = "Quarantine-" + width + "-" + scenario;
                            captures.Add(Capture(owned, root, scenario));
                        }
                    }
                }
                else
                {
                    owned.Focus();
                    owned.Repaint();
                    await Task.Delay(200, deadline.Token);
                    stage="Capture940";
                    captures.Add(Capture(owned, root));
                    owned.position = new Rect(80, 80, 500, 300);
                    owned.Focus();
                    owned.Repaint();
                    await Task.Delay(200, deadline.Token);
                    stage="Capture500";
                    captures.Add(Capture(owned, root));
                }
            }
            catch (OperationCanceledException) { failure = "ValidationDeadlineExceeded"; }
            catch { failure = failAfterOpen ? "ExpectedFailure" : "VisualValidationFailed"; }
            finally
            {
                try { ReleaseOwned(owned,shown); }
                finally { if (previousFocus != null) previousFocus.Focus(); }
            }
        }
        var afterIds = Resources.FindObjectsOfTypeAll<EditorWindow>().Select(value => value.GetInstanceID()).ToArray();
        var afterContainerIds=Resources.FindObjectsOfTypeAll(containerType).Select(value=>value.GetInstanceID()).ToArray();
        var addedContainers=afterContainerIds.Except(beforeContainerIds).ToArray();
        var missingContainers=beforeContainerIds.Except(afterContainerIds).ToArray();
        var leaked = afterIds.Contains(ownedId);
        var preserved = existing.All(value => value != null);
        var restored = previousFocus == null || EditorWindow.focusedWindow == previousFocus;
        var report = new {
            Passed = !leaked && preserved && restored && addedContainers.Length==0 && missingContainers.Length==0 &&
                (failAfterOpen ? failure == "ExpectedFailure" : failure == ""),
            Failure = failure, Stage=stage, OfflineCredentials=true, BeforeIds=beforeIds, OwnedIds=new[] {ownedId}, AfterIds=afterIds,
            BeforeContainerIds=beforeContainerIds,AfterContainerIds=afterContainerIds,
            AddedContainers=addedContainers,MissingContainers=missingContainers,
            OwnedWindowsRemaining=leaked ? 1 : 0, ExistingWindowsPreserved=preserved, OriginalFocusRestored=restored,
            Captures=captures.ToArray(), QuarantineMode=quarantine, SyntheticCollectionCalls=service.Calls, ActualApiCalls=0
        };
        var reportName=failBeforeShow ? "collection-visual-initialization-failure-cleanup.json" :
            failAfterOpen ? "collection-visual-failure-cleanup.json" : quarantine ? "collection-quarantine-visual-final.json" : "collection-visual-final.json";
        File.WriteAllText(Path.Combine(root, reportName),
            JsonConvert.SerializeObject(report, Formatting.Indented));
        return report;
    }

    private static void ReleaseOwned(EditorWindow owned,bool shown)
    {
        if (owned==null) return;
        if (!shown) { UnityEngine.Object.DestroyImmediate(owned); return; }
        try { owned.Close(); }
        finally { if (owned!=null) UnityEngine.Object.DestroyImmediate(owned); }
    }

    private static object Capture(TftDataAnalyzerWindow window, string root, string scenario=null)
    {
        var rect=window.position;
        var width=(int)rect.width;
        var height=(int)window.rootVisualElement.panel.visualTree.worldBound.height;
        File.WriteAllText(Path.Combine(root,"collection-visual-capture-state.json"),JsonConvert.SerializeObject(new {
            Width=width,Height=height,RootBounds=window.rootVisualElement.worldBound.ToString(),
            PanelBounds=window.rootVisualElement.panel.visualTree.worldBound.ToString(),
            CatalogLabel=window.rootVisualElement.Q<Label>("collection-catalog").text
        },Formatting.Indented));
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            image.SetPixels(InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x, rect.y), width, height));
            image.Apply();
            var path=Path.Combine(root, "collection-window-"+width+(scenario==null ? "" : "-quarantine-"+scenario)+".png");
            File.WriteAllBytes(path,image.EncodeToPNG());
            var footer=window.rootVisualElement.Q<Label>("historical-data-disclaimer");
            var scroll=window.rootVisualElement.Q<ScrollView>("collection-scroll");
            var catalogText=window.rootVisualElement.Q<Label>("collection-catalog").text;
            if (!catalogText.Contains("원본 게임 버전"))
                throw new InvalidOperationException("The selected catalog was not rendered.");
            var diagnostic=window.rootVisualElement.Q<Label>("collection-scan-limit");
            if (scenario!=null)
            {
                var expected=scenario=="saved" ? "보고서 저장됨" : scenario=="failed" ? "보고서 저장 실패" : "상한 도달로 일부 누락";
                if (!diagnostic.text.Contains("격리 2") || !diagnostic.text.Contains(expected) || diagnostic.worldBound.yMax>height)
                    throw new InvalidOperationException("Quarantine state was not visible.");
            }
            return new { Path=path, Width=width, Height=height,
                CatalogLabel=catalogText,
                QuarantineLabel=diagnostic.text, QuarantineBounds=diagnostic.worldBound.ToString(),
                FooterBounds=footer.worldBound.ToString(), ScrollMaximum=scroll.verticalScroller.highValue,
                FooterVisible=footer.worldBound.yMax<=height };
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }

    private sealed class OfflineStatus : ICollectionService
    {
        private readonly bool _quarantine;
        internal string Scenario;
        internal int Calls;
        internal OfflineStatus(bool quarantine=false) { _quarantine=quarantine; }
        public bool ApiKeyConfigured=>_quarantine;
        public bool SeedConfigured=>_quarantine;
        public Task<MatchCollectionSummary> CollectAsync(StaticCatalog catalog,MatchCollectionRequest request,
            IProgress<CollectionProgress> progress,CancellationToken token)
        {
            if (!_quarantine) throw new InvalidOperationException("Visual validation never collects.");
            Calls++;
            // The summary constructor is internal; reflection supplies only fabricated numeric status to this isolated UI host.
            var result = (MatchCollectionSummary)Activator.CreateInstance(typeof(MatchCollectionSummary),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] {3,3,1,0,2,0,false,1,false,0,null,2,Scenario!="failed",Scenario=="failed",Scenario=="truncated"}, null);
            return Task.FromResult(result);
        }
    }
}
