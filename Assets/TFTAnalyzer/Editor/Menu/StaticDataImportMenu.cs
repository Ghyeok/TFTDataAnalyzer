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
        private StaticImportPresentation _presentation;
        private bool _isRunning;
        private string _status = "Ready";
        private CancellationTokenSource _cancellation;

        private void OnEnable()
        {
            _presentation = new StaticImportPresentation(
                new StaticCatalogPaths(Path.GetDirectoryName(Application.dataPath)));
        }

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
                _presentation.IncludeCommunityDragon = EditorGUILayout.ToggleLeft(
                    "Use CommunityDragon to fill missing official fields",
                    _presentation.IncludeCommunityDragon);
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

            _isRunning = true;
            _status = "Preparing import confirmation...";
            _cancellation = new CancellationTokenSource();
            Repaint();
            CatalogIdentity identity = null;
            string targetDirectory = "";
            try
            {
                var patch = new PatchKey(_setId, _setNumber, _patchId, _sourceGameVersion);
                identity = _presentation.CreateRequest(patch).Identity;
                targetDirectory = _presentation.TargetDirectory(identity);
                var summary = _presentation.BuildSummary(identity);
                var result = await _presentation.ExecuteAsync(patch,
                    prompt => EditorUtility.DisplayDialog("Import immutable TFT catalog", prompt, "Import", "Cancel"),
                    (request, token) => CreatePipeline().RunAsync(request, token),
                    _cancellation.Token);
                _status = result == null
                    ? $"Import cancelled before download. {summary}"
                    : result.Published
                        ? $"Published {result.Catalog.Units.Count} units at {result.FinalDirectory}. " +
                          $"Profile: {identity.ProfileId}; version: {identity.Patch.SourceGameVersion}; hash: {result.ContentHash}"
                        : $"Validation blocked {identity.ProfileId} at {result.FinalDirectory}: " +
                          $"{result.Validation.ErrorCount} errors, {result.Validation.WarningCount} warnings.";
            }
            catch (OperationCanceledException)
            {
                _status = identity == null
                    ? $"Import cancelled for {_setId} / {_patchId} / {_sourceGameVersion}."
                    : _presentation.CancellationStatus(identity, targetDirectory);
            }
            catch (Exception exception)
            {
                _status = identity == null
                    ? $"Import failed for {_setId} / {_patchId} / {_sourceGameVersion}: {exception.GetType().Name}."
                    : _presentation.FailureStatus(identity, targetDirectory, exception);
                Debug.LogError(_status);
            }
            finally
            {
                _isRunning = false;
                _presentation.IncludeCommunityDragon = false;
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
