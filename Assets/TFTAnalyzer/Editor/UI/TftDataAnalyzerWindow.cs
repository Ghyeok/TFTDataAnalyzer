using System.IO;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Editor.UI.Collection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI
{
    public sealed class TftDataAnalyzerWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml";
        private const string UssPath = "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss";
        private const string Disclaimer = "과거 관측 결과이며 전투 예측이나 플레이 권고가 아닙니다.";
        private static readonly string[] TabNames = { "data-tab", "collection-tab", "analysis-tab", "about-tab" };

        [SerializeField] private string selectedTab = "data-tab";
        private DataManagerViewModel _dataManager;
        private DataManagerTab _dataBinding;
        private CollectionViewModel _collection;
        private CollectionTab _collectionBinding;

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
                summary => EditorUtility.DisplayDialog("카탈로그 가져오기", summary, "가져오기", "취소"));
            if (_collection == null)
                _collection = new CollectionViewModel(new CollectionService(new ObservationStore(
                    new AnalyzerStorePaths(Path.GetDirectoryName(Application.dataPath)))));
            _dataManager.Changed -= SyncCollectionCatalog;
            _dataManager.Changed += SyncCollectionCatalog;
            SyncCollectionCatalog();
            _collectionBinding = new CollectionTab(tree.Q("collection-tab"), _collection,
                summary => EditorUtility.DisplayDialog("과거 경기 수집 조건 확인", summary, "수집", "취소"));
            SelectTab(selectedTab);
        }

        private void SyncCollectionCatalog()
        {
            var row = _dataManager.Rows.FirstOrDefault(value => value.IsSelectable &&
                value.Identity.Equals(_dataManager.ActiveIdentity));
            _collection?.SetCatalog(row?.Catalog, row?.Identity);
        }

        private void OnDisable()
        {
            _dataBinding?.Dispose();
            _dataBinding = null;
            _collectionBinding?.Dispose();
            _collectionBinding = null;
            _collection?.Dispose();
            _collection = null;
            if (_dataManager != null) _dataManager.Changed -= SyncCollectionCatalog;
            _dataManager?.Dispose();
            _dataManager = null;
        }

        private void SelectTab(string tabName)
        {
            selectedTab = System.Array.IndexOf(TabNames, tabName) >= 0 ? tabName : "data-tab";
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
