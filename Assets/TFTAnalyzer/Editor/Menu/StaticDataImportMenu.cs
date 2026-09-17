using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Validation;
using UnityEditor;
using UnityEngine;

namespace TFTAnalyzer.Editor.Menu
{
    public sealed class StaticDataImportWindow : EditorWindow
    {
        private string _setId = "TFTSet18";
        private int _setNumber = 18;
        private string _patchId = "18.2";
        private string _sourceGameVersion = "16.18.1";
        private bool _includeCommunityDragon;
        private bool _isRunning;
        private string _status = "Ready";
        private CancellationTokenSource _cancellation;

        [MenuItem("TFT Analyzer/Data/Import Set 18 Patch...")]
        private static void Open()
        {
            var window = GetWindow<StaticDataImportWindow>(true, "TFT Static Data Import");
            window.minSize = new Vector2(460, 260);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Immutable patch import", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_isRunning))
            {
                _setId = EditorGUILayout.TextField("Set ID", _setId);
                _setNumber = EditorGUILayout.IntField("Set number", _setNumber);
                _patchId = EditorGUILayout.TextField("TFT patch ID", _patchId);
                _sourceGameVersion = EditorGUILayout.TextField(
                    "Data Dragon version",
                    _sourceGameVersion);
                _includeCommunityDragon = EditorGUILayout.ToggleLeft(
                    "Use CommunityDragon to fill missing official fields",
                    _includeCommunityDragon);
                EditorGUILayout.HelpBox(
                    "CommunityDragon is disabled by default. Existing patch outputs are never overwritten.",
                    MessageType.Info);

                if (GUILayout.Button("Import patch"))
                {
                    BeginImport();
                }
            }

            if (_isRunning && GUILayout.Button("Cancel"))
            {
                _cancellation?.Cancel();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", _status, EditorStyles.wordWrappedLabel);
        }

        private async void BeginImport()
        {
            if (_isRunning)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Import immutable TFT patch",
                    $"Import {_setId} / {_patchId} from Data Dragon {_sourceGameVersion}?",
                    "Import",
                    "Cancel"))
            {
                return;
            }

            _isRunning = true;
            _status = "Importing...";
            _cancellation = new CancellationTokenSource();
            Repaint();
            try
            {
                var result = await CreatePipeline().RunAsync(
                    new StaticImportRequest(
                        new PatchKey(_setId, _setNumber, _patchId, _sourceGameVersion),
                        _includeCommunityDragon),
                    _cancellation.Token);
                _status = result.Published
                    ? $"Published {result.Catalog.Units.Count} units. Hash: {result.ContentHash}"
                    : $"Validation blocked publication: {result.Validation.ErrorCount} errors, " +
                      $"{result.Validation.WarningCount} warnings.";
            }
            catch (OperationCanceledException)
            {
                _status = "Import cancelled. Staging was cleaned.";
            }
            catch (Exception exception)
            {
                _status = $"Import failed: {exception.Message}";
                Debug.LogException(exception);
            }
            finally
            {
                _isRunning = false;
                _cancellation.Dispose();
                _cancellation = null;
                Repaint();
            }
        }

        private static StaticCatalogPipeline CreatePipeline()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var paths = new StaticCatalogPaths(projectRoot);
            var transport = new UnityWebRequestTransport();
            var dataDragon = new DataDragonAdapter(transport);
            var communityDragon = new CommunityDragonAdapter();
            var source = new NetworkStaticCatalogSource(
                dataDragon,
                communityDragon,
                new CatalogMerger(),
                transport);
            return new StaticCatalogPipeline(
                source,
                transport,
                paths,
                new CatalogAssetWriter(paths),
                new CatalogValidator(),
                new CatalogIndexWriter(paths),
                AssetDatabase.Refresh);
        }

        private void OnDisable()
        {
            _cancellation?.Cancel();
        }
    }
}
