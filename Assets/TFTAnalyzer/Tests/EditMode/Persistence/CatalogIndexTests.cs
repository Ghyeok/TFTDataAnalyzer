using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using UnityEngine;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class CatalogIndexTests
    {
        [Test]
        public void FourIdentitiesForOnePatchCoexistAndExactUpsertReplacesOnlyOne()
        {
            var index = ScriptableObject.CreateInstance<CatalogIndex>();
            foreach (var version in new[] { "16.18.1", "16.18.2" })
            foreach (var profile in new[] { CatalogSourceProfile.OfficialOnly, CatalogSourceProfile.OfficialPlusCommunityDragon })
            {
                var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", version), profile);
                index.Upsert(identity, "initial", "Assets/" + version + "/" + identity.ProfileId + "/catalog.json");
            }

            Assert.That(index.Entries.Count, Is.EqualTo(4));
            Assert.That(index.Entries.Select(entry => entry.SourceGameVersion + "/" + entry.SourceProfileId),
                Is.EqualTo(new[] {
                    "16.18.1/official-only", "16.18.1/official-plus-communitydragon",
                    "16.18.2/official-only", "16.18.2/official-plus-communitydragon" }));

            index.Upsert(new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                CatalogSourceProfile.OfficialOnly), "replacement", "Assets/replacement/catalog.json");

            Assert.That(index.Entries.Count, Is.EqualTo(4));
            Assert.That(index.Entries.Single(entry => entry.SourceGameVersion == "16.18.1" &&
                entry.SourceProfileId == "official-only").ContentHash, Is.EqualTo("replacement"));
            Assert.That(index.Entries.Count(entry => entry.ContentHash == "initial"), Is.EqualTo(3));
        }

        [Test]
        public void CanonicalPathsSeparateVersionAndProfile()
        {
            var root = Path.Combine(Path.GetTempPath(), "catalog-path-tests");
            var paths = new StaticCatalogPaths(root);
            var official = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var supplemented = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialPlusCommunityDragon);
            var newer = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.2"), CatalogSourceProfile.OfficialOnly);

            Assert.That(paths.CatalogAssetPath(official), Is.EqualTo("Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/16.18.1/official-only/catalog.json"));
            Assert.That(paths.CatalogAssetPath(supplemented), Is.EqualTo("Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/16.18.1/official-plus-communitydragon/catalog.json"));
            Assert.That(paths.CatalogAssetPath(newer), Is.EqualTo("Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/16.18.2/official-only/catalog.json"));
            Assert.That(paths.CanonicalFinalDirectory(official), Is.EqualTo(Path.Combine(root, "Assets", "TFTAnalyzer", "Data", "Generated", "TFTSet18", "18.2", "16.18.1", "official-only")));
        }

        [Test]
        public void VerifiedLegacyUpsertReplacesOldUnprofiledEntryWithoutRemovingSibling()
        {
            var index = ScriptableObject.CreateInstance<CatalogIndex>();
            var official = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var plus = new CatalogIdentity(official.Patch, CatalogSourceProfile.OfficialPlusCommunityDragon);
            var legacyPath = "Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/catalog.json";
            index.Upsert(official, "old", legacyPath);
            index.Upsert(plus, "sibling", "Assets/sibling/catalog.json");
            typeof(CatalogIndexEntry).GetField("sourceProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(index.Entries[0], null); // Simulates the committed pre-profile Unity serialized entry.

            index.Upsert(official, "new", legacyPath, true);

            Assert.That(index.Entries.Count, Is.EqualTo(2));
            Assert.That(index.Entries.Single(entry => entry.SourceProfileId == "official-only").ContentHash, Is.EqualTo("new"));
            Assert.That(index.Entries.Single(entry => entry.SourceProfileId == "official-plus-communitydragon").ContentHash, Is.EqualTo("sibling"));
        }

        [Test]
        public void UnprofiledEntryCannotBeSilentlyReadAsOfficial()
        {
            var official = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var entry = new CatalogIndexEntry(official, "hash", "Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/catalog.json");
            typeof(CatalogIndexEntry).GetField("sourceProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(entry, null);
            var paths = new StaticCatalogPaths(Path.Combine(Path.GetTempPath(), "missing-legacy"));
            Assert.Throws<InvalidOperationException>(() => entry.ResolveIdentity(new LegacyCatalogResolver(paths)));
        }

        [Test]
        public void SaveFailureRestoresExistingIndexBytesAndEntries()
        {
            var index = ScriptableObject.CreateInstance<CatalogIndex>();
            var sibling = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                CatalogSourceProfile.OfficialPlusCommunityDragon);
            var official = new CatalogIdentity(sibling.Patch, CatalogSourceProfile.OfficialOnly);
            index.Upsert(sibling, "sibling-hash", "Assets/sibling/catalog.json");
            var file = Path.Combine(Path.GetTempPath(), "catalog-index-rollback-" + Guid.NewGuid().ToString("N") + ".asset");
            try
            {
                File.WriteAllText(file, "original index bytes");
                Assert.Throws<IOException>(() => CatalogIndexWriter.SaveWithRollback(index, official,
                    "new-hash", "Assets/new/catalog.json", false, file,
                    () => { File.WriteAllText(file, "partial save"); throw new IOException("save failed"); }));
                Assert.That(File.ReadAllText(file), Is.EqualTo("original index bytes"));
                Assert.That(index.Entries.Count, Is.EqualTo(1));
                Assert.That(index.Entries[0].ContentHash, Is.EqualTo("sibling-hash"));
            }
            finally
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }

        [Test]
        public void IndexWriterRejectsMissingCatalogTarget()
        {
            var root = Path.Combine(Path.GetTempPath(), "missing-catalog-" + Guid.NewGuid().ToString("N"));
            var paths = new StaticCatalogPaths(root);
            var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                CatalogSourceProfile.OfficialOnly);
            Assert.Throws<InvalidDataException>(() => new CatalogIndexWriter(paths).Upsert(
                identity, "hash", paths.CatalogAssetPath(identity)));
        }

        [TestCase("../escape", "18.2", "16.18.1")]
        [TestCase("TFTSet18", "a/b", "16.18.1")]
        [TestCase("TFTSet18", "18.2", "../version")]
        [TestCase("TFTSet18", "18.2", "16/18/1")]
        public void UnsafeIdentitySegmentCannotBecomePath(string set, string patch, string version)
        {
            var paths = new StaticCatalogPaths(Path.GetTempPath());
            var identity = new CatalogIdentity(new PatchKey(set, 18, patch, version), CatalogSourceProfile.OfficialOnly);
            Assert.Throws<ArgumentException>(() => paths.CatalogAssetPath(identity));
        }
    }
}
