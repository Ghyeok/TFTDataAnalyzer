using System.IO;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Editor.UI.Collection;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Editor.UI.About;
using TFTAnalyzer.Editor.Compliance;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI
{
    public sealed class TftDataAnalyzerWindow : EditorWindow
    {
        internal void Configure(DataManagerViewModel data, CollectionViewModel collection,
            AnalysisViewModel analysis, AboutViewModel about, System.Func<string, bool> confirmImport,
            System.Func<string, bool> confirmCollection, StaticVersionCheckScheduler versionCheck = null)
        {
            if (rootVisualElement.childCount != 0 || _dataManager != null || _collection != null || _analysis != null || _about != null)
                throw new System.InvalidOperationException("Configure the window before GUI creation.");
            _dataManager = data ?? throw new System.ArgumentNullException(nameof(data));
            _collection = collection ?? throw new System.ArgumentNullException(nameof(collection));
            _analysis = analysis ?? throw new System.ArgumentNullException(nameof(analysis));
            _about = about ?? throw new System.ArgumentNullException(nameof(about));
            _confirmImport = confirmImport ?? throw new System.ArgumentNullException(nameof(confirmImport));
            _confirmCollection = confirmCollection ?? throw new System.ArgumentNullException(nameof(confirmCollection));
            _versionCheck = versionCheck;
            _configured = true;
        }
        private const string UxmlPath = "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml";
        private const string UssPath = "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss";
        private const string Disclaimer = "과거 관측 결과이며 전투 예측이나 플레이 권고가 아닙니다.";
        private static readonly string[] TabNames = { "data-tab", "collection-tab", "analysis-tab", "about-tab" };

        [SerializeField] private string selectedTab = "data-tab";
        private DataManagerViewModel _dataManager;
        private DataManagerTab _dataBinding;
        private CollectionViewModel _collection;
        private CollectionTab _collectionBinding;
        private AnalysisViewModel _analysis;
        private AnalysisTab _analysisBinding;
        private AboutViewModel _about;
        private AboutTab _aboutBinding;
        private StaticVersionCheckScheduler _versionCheck;
        private System.Func<string, bool> _confirmImport, _confirmCollection;
        private bool _configured;

        [MenuItem("TFT Analyzer/Open Data Analyzer")]
        private static void Open()
        {
            GetWindow<TftDataAnalyzerWindow>("TFT Data Analyzer").Show();
        }

        public void CreateGUI()
        {
            minSize = new Vector2(500, 300);
            RebuildUI(UxmlPath, UssPath);
        }

        internal void RebuildUI(string uxmlPath, string ussPath)
        {
            _dataBinding?.Dispose();
            _dataBinding = null;
            _collectionBinding?.Dispose();
            _collectionBinding = null;
            _analysisBinding?.Dispose();
            _analysisBinding = null;
            _aboutBinding?.Dispose();
            _aboutBinding = null;
            if (_versionCheck != null) _versionCheck.Changed -= RenderVersionNotice;
            rootVisualElement.Clear();
            var treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            var missingPaths = new List<string>();
            if (treeAsset == null)
                missingPaths.Add(uxmlPath);
            if (stylesheet == null)
                missingPaths.Add(ussPath);
            if (missingPaths.Count > 0)
            {
                ShowLoadError(missingPaths);
                return;
            }

            var tree = treeAsset.CloneTree();
            tree.AddToClassList("tft-analyzer-tree");
            rootVisualElement.Add(tree);
            foreach (var tab in TabNames)
            {
                var tabName = tab;
                tree.Q<Button>(tabName + "-button").clicked += () => SelectTab(tabName);
            }
            if (_dataManager == null)
            {
                var paths = new StaticCatalogPaths(Path.GetDirectoryName(Application.dataPath));
                _dataManager = new DataManagerViewModel(new DataManagerService(paths,
                    new UnityWebRequestTransport(), () => AssetDatabase.LoadAssetAtPath<CatalogIndex>(
                        StaticCatalogPaths.CatalogIndexAssetPath)));
            }
            _dataBinding = new DataManagerTab(tree.Q("data-tab"), _dataManager,
                _confirmImport ?? (summary => EditorUtility.DisplayDialog("카탈로그 가져오기", summary, "가져오기", "취소")));
            if (_collection == null)
                _collection = new CollectionViewModel(new CollectionService(new ObservationStore(
                    new AnalyzerStorePaths(Path.GetDirectoryName(Application.dataPath)))));
            if (_analysis == null)
            {
                var paths = new AnalyzerStorePaths(Path.GetDirectoryName(Application.dataPath));
                _analysis = new AnalysisViewModel(new AnalysisService(new ObservationStore(paths), new AggregateStore(paths),
                    provider: new ReviewedAugmentPolicyProvider(paths.ProjectRoot)));
            }
            _dataManager.Changed -= SyncCollectionCatalog;
            _dataManager.Changed += SyncCollectionCatalog;
            _dataManager.Changed -= RenderVersionNotice;
            _dataManager.Changed += RenderVersionNotice;
            SyncCollectionCatalog();
            _collectionBinding = new CollectionTab(tree.Q("collection-tab"), _collection,
                _confirmCollection ?? (summary => EditorUtility.DisplayDialog("과거 경기 수집 조건 확인", summary, "수집", "취소")));
            var exporter = new AnalysisCsvExporter(Path.GetDirectoryName(Application.dataPath));
            _analysisBinding = new AnalysisTab(tree.Q("analysis-tab"), _analysis,
                () => EditorUtility.SaveFilePanel("개인 연구용 과거 관측 CSV 저장", "", "tft-historical-observations", "csv"),
                (path, rows) => exporter.Save(path, rows));
            if (_about == null)
                _about = new AboutViewModel(new ComplianceSettingsStore(Path.GetDirectoryName(Application.dataPath)));
            _aboutBinding = new AboutTab(tree.Q("about-tab"), _about,
                () => SettingsService.OpenProjectSettings(ComplianceSettingsProvider.SettingsPath), Application.OpenURL);
            if (!_configured) _versionCheck = StaticVersionCheckUnityConnection.Scheduler;
            if (_versionCheck != null) _versionCheck.Changed += RenderVersionNotice;
            RenderVersionNotice();
            SelectTab(selectedTab);
        }

        private void RenderVersionNotice()
        {
            var label = rootVisualElement.Q<Label>("startup-version-notice");
            if (label == null) return;
            label.text = _versionCheck?.NoticeFor(LastImportedSourceVersion.Select(_dataManager.Rows)) ?? "";
            label.EnableInClassList("tft-tab-hidden", string.IsNullOrEmpty(label.text));
        }

        private void SyncCollectionCatalog()
        {
            var row = _dataManager.Rows.FirstOrDefault(value => value.IsSelectable &&
                value.Identity.Equals(_dataManager.ActiveIdentity));
            _collection?.SetCatalog(row?.Catalog, row?.Identity);
            _analysis?.SetCatalog(row?.Catalog, row?.Identity);
        }

        private void OnDisable()
        {
            if (_versionCheck != null) _versionCheck.Changed -= RenderVersionNotice;
            _dataBinding?.Dispose();
            _dataBinding = null;
            _collectionBinding?.Dispose();
            _collectionBinding = null;
            _analysisBinding?.Dispose();
            _analysisBinding = null;
            _aboutBinding?.Dispose();
            _aboutBinding = null;
            _about = null;
            _analysis?.Dispose();
            _analysis = null;
            _collection?.Dispose();
            _collection = null;
            if (_dataManager != null) _dataManager.Changed -= SyncCollectionCatalog;
            if (_dataManager != null) _dataManager.Changed -= RenderVersionNotice;
            _dataManager?.Dispose();
            _dataManager = null;
            _versionCheck = null;
            _confirmImport = _confirmCollection = null;
            _configured = false;
        }

        private void SelectTab(string tabName)
        {
            selectedTab = System.Array.IndexOf(TabNames, tabName) >= 0 ? tabName : "data-tab";
            if (selectedTab == "about-tab") _aboutBinding?.Refresh();
            foreach (var tab in TabNames)
            {
                var selected = tab == selectedTab;
                rootVisualElement.Q(tab).EnableInClassList("tft-tab-hidden", !selected);
                rootVisualElement.Q<Button>(tab + "-button").EnableInClassList("tft-tab-selected", selected);
            }
        }

        private void ShowLoadError(IEnumerable<string> missingPaths)
        {
            var error = new Label
            {
                name = "ui-load-error",
                text = "화면 자산을 불러올 수 없습니다. 다음 경로를 확인해 주세요:\n" + string.Join("\n", missingPaths)
            };
            error.style.whiteSpace = WhiteSpace.Normal;
            error.style.flexGrow = 1;
            rootVisualElement.Add(error);
            var disclaimer = new Label(Disclaimer) { name = "historical-data-disclaimer" };
            disclaimer.style.whiteSpace = WhiteSpace.Normal;
            disclaimer.style.flexShrink = 0;
            rootVisualElement.Add(disclaimer);
        }
    }
}
