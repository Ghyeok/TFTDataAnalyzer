using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;
using UnityEditor;
using UnityEngine;

namespace TFTAnalyzer.Editor.Persistence
{
    public interface ICatalogIndexWriter
    {
        void Upsert(PatchKey patch, string contentHash, string catalogAssetPath);
    }

    [Serializable]
    public sealed class CatalogIndexEntry
    {
        [SerializeField] private string setId;
        [SerializeField] private int setNumber;
        [SerializeField] private string patchId;
        [SerializeField] private string sourceGameVersion;
        [SerializeField] private string contentHash;
        [SerializeField] private string catalogAssetPath;

        public string SetId => setId;
        public int SetNumber => setNumber;
        public string PatchId => patchId;
        public string SourceGameVersion => sourceGameVersion;
        public string ContentHash => contentHash;
        public string CatalogAssetPath => catalogAssetPath;

        public CatalogIndexEntry(PatchKey patch, string hash, string assetPath)
        {
            setId = patch.SetId;
            setNumber = patch.SetNumber;
            patchId = patch.PatchId;
            sourceGameVersion = patch.SourceGameVersion;
            contentHash = hash;
            catalogAssetPath = assetPath;
        }
    }

    public sealed class CatalogIndex : ScriptableObject
    {
        [SerializeField] private List<CatalogIndexEntry> entries = new List<CatalogIndexEntry>();

        public IReadOnlyList<CatalogIndexEntry> Entries => entries;

        public void Upsert(PatchKey patch, string contentHash, string catalogAssetPath)
        {
            entries.RemoveAll(entry =>
                string.Equals(entry.SetId, patch.SetId, StringComparison.Ordinal) &&
                string.Equals(entry.PatchId, patch.PatchId, StringComparison.Ordinal));
            entries.Add(new CatalogIndexEntry(patch, contentHash, catalogAssetPath));
            entries = entries
                .OrderBy(entry => entry.SetNumber)
                .ThenBy(entry => entry.PatchId, StringComparer.Ordinal)
                .ToList();
        }
    }

    public sealed class CatalogIndexWriter : ICatalogIndexWriter
    {
        private readonly StaticCatalogPaths _paths;

        public CatalogIndexWriter(StaticCatalogPaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public void Upsert(PatchKey patch, string contentHash, string catalogAssetPath)
        {
            var directory = _paths.AbsoluteAssetPath(StaticCatalogPaths.GeneratedAssetDirectory);
            System.IO.Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            var index = AssetDatabase.LoadAssetAtPath<CatalogIndex>(
                StaticCatalogPaths.CatalogIndexAssetPath);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<CatalogIndex>();
                AssetDatabase.CreateAsset(index, StaticCatalogPaths.CatalogIndexAssetPath);
            }

            index.Upsert(patch, contentHash, catalogAssetPath);
            EditorUtility.SetDirty(index);
            AssetDatabase.SaveAssets();
        }
    }
}
