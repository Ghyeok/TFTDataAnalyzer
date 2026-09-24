using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TFTAnalyzer.Domain;
using UnityEditor;
using UnityEngine;

namespace TFTAnalyzer.Editor.Persistence
{
    public sealed class CatalogIndexRollbackException : InvalidOperationException
    {
        public string IndexFilePath { get; }

        public CatalogIndexRollbackException(string indexFilePath, Exception innerException)
            : base($"Index update and rollback both failed at {indexFilePath}.", innerException)
        {
            IndexFilePath = indexFilePath;
        }
    }

    public interface ICatalogIndexWriter
    {
        void Upsert(CatalogIdentity identity, string contentHash, string catalogAssetPath);
    }

    [Serializable]
    public sealed class CatalogIndexEntry
    {
        [SerializeField] private string setId;
        [SerializeField] private int setNumber;
        [SerializeField] private string patchId;
        [SerializeField] private string sourceGameVersion;
        [SerializeField] private string sourceProfileId;
        [SerializeField] private string contentHash;
        [SerializeField] private string catalogAssetPath;

        public string SetId => setId;
        public int SetNumber => setNumber;
        public string PatchId => patchId;
        public string SourceGameVersion => sourceGameVersion;
        public string SourceProfileId => sourceProfileId;
        public string ContentHash => contentHash;
        public string CatalogAssetPath => catalogAssetPath;

        public CatalogIndexEntry(CatalogIdentity identity, string hash, string assetPath)
        {
            setId = identity.Patch.SetId;
            setNumber = identity.Patch.SetNumber;
            patchId = identity.Patch.PatchId;
            sourceGameVersion = identity.Patch.SourceGameVersion;
            sourceProfileId = identity.ProfileId;
            contentHash = hash;
            catalogAssetPath = assetPath;
        }

        public CatalogIdentity ResolveIdentity(LegacyCatalogResolver resolver)
        {
            var patch = new PatchKey(setId, setNumber, patchId, sourceGameVersion);
            if (!string.IsNullOrEmpty(sourceProfileId))
            {
                return new CatalogIdentity(patch, CatalogSourceProfileIds.Parse(sourceProfileId));
            }

            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            var identity = new CatalogIdentity(patch, CatalogSourceProfile.OfficialOnly);
            var resolution = resolver.Resolve(identity);
            if (resolution.Status != LegacyCatalogStatus.CompatibleOfficialLegacy ||
                !string.Equals(resolution.CatalogAssetPath, catalogAssetPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unprofiled index entry is not verified legacy: {catalogAssetPath}");
            }

            return identity;
        }
    }

    public sealed class CatalogIndex : ScriptableObject
    {
        [SerializeField] private List<CatalogIndexEntry> entries = new List<CatalogIndexEntry>();

        public IReadOnlyList<CatalogIndexEntry> Entries => entries;

        internal void RestoreEntries(IEnumerable<CatalogIndexEntry> previous)
        {
            entries = previous.ToList();
        }

        public void Upsert(CatalogIdentity identity, string contentHash, string catalogAssetPath,
            bool verifiedLegacy = false)
        {
            entries.RemoveAll(entry =>
                string.Equals(entry.SetId, identity.Patch.SetId, StringComparison.Ordinal) &&
                string.Equals(entry.PatchId, identity.Patch.PatchId, StringComparison.Ordinal) &&
                string.Equals(entry.SourceGameVersion, identity.Patch.SourceGameVersion, StringComparison.Ordinal) &&
                (string.Equals(entry.SourceProfileId, identity.ProfileId, StringComparison.Ordinal) ||
                 (verifiedLegacy && identity.SourceProfile == CatalogSourceProfile.OfficialOnly &&
                  string.IsNullOrEmpty(entry.SourceProfileId) &&
                  string.Equals(entry.CatalogAssetPath, catalogAssetPath, StringComparison.Ordinal))));
            entries.Add(new CatalogIndexEntry(identity, contentHash, catalogAssetPath));
            entries = entries
                .OrderBy(entry => entry.SetNumber)
                .ThenBy(entry => entry.PatchId, StringComparer.Ordinal)
                .ThenBy(entry => entry.SourceGameVersion, StringComparer.Ordinal)
                .ThenBy(entry => entry.SourceProfileId, StringComparer.Ordinal)
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

        public void Upsert(CatalogIdentity identity, string contentHash, string catalogAssetPath)
        {
            var legacyPath = _paths.LegacyCatalogAssetPath(identity.Patch);
            var isLegacy = string.Equals(catalogAssetPath, legacyPath, StringComparison.Ordinal);
            if (!isLegacy && !string.Equals(catalogAssetPath,
                    _paths.CatalogAssetPath(identity), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Index target path does not match identity: {catalogAssetPath}");
            }

            if (isLegacy && new LegacyCatalogResolver(_paths).Resolve(identity).Status !=
                LegacyCatalogStatus.CompatibleOfficialLegacy)
            {
                throw new InvalidOperationException($"Legacy index target is not verified: {catalogAssetPath}");
            }

            var finalDirectory = Path.GetDirectoryName(_paths.AbsoluteAssetPath(catalogAssetPath));
            var actualHash = CatalogAssetWriter.VerifyPublishedHash(finalDirectory, identity, isLegacy);
            if (!string.Equals(actualHash, contentHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Index hash does not match catalog: {catalogAssetPath}");
            }

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

            SaveWithRollback(index, identity, contentHash, catalogAssetPath, isLegacy,
                _paths.AbsoluteAssetPath(StaticCatalogPaths.CatalogIndexAssetPath), () =>
                {
                    EditorUtility.SetDirty(index);
                    AssetDatabase.SaveAssets();
                });
        }

        public static void SaveWithRollback(CatalogIndex index, CatalogIdentity identity,
            string contentHash, string catalogAssetPath, bool verifiedLegacy,
            string indexFilePath, Action save)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (save == null) throw new ArgumentNullException(nameof(save));
            var previousEntries = index.Entries.ToArray();
            var previousBytes = File.Exists(indexFilePath) ? File.ReadAllBytes(indexFilePath) : null;
            try
            {
                index.Upsert(identity, contentHash, catalogAssetPath, verifiedLegacy);
                save();
            }
            catch (Exception error)
            {
                index.RestoreEntries(previousEntries);
                try
                {
                    if (previousBytes != null) File.WriteAllBytes(indexFilePath, previousBytes);
                }
                catch (Exception rollbackError)
                {
                    throw new CatalogIndexRollbackException(indexFilePath,
                        new AggregateException(error, rollbackError));
                }

                throw;
            }
        }
    }
}
