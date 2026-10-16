using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.UI;
using TFTAnalyzer.Validation;
using UnityEngine;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class PublishedCatalogLoaderTests
    {
        private string _root;
        private StaticCatalogPaths _paths;
        private CatalogIndex _index;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftPublishedLoaderTests", Guid.NewGuid().ToString("N"));
            _paths = new StaticCatalogPaths(_root);
            _index = ScriptableObject.CreateInstance<CatalogIndex>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_index);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void LocalRowsKeepAllFourVersionProfileIdentitiesAndStandaloneMetadataWithoutWriting()
        {
            foreach (var version in new[] { "16.18.1", "16.18.2" })
            foreach (var profile in new[] { CatalogSourceProfile.OfficialOnly, CatalogSourceProfile.OfficialPlusCommunityDragon })
                Publish(DataManagerTestSupport.Identity(version, profile));
            var before = Snapshot();

            var rows = new PublishedCatalogLoader(_paths).Load(_index);

            Assert.That(rows, Has.Count.EqualTo(4));
            Assert.That(rows.Select(row => row.Identity).Distinct().Count(), Is.EqualTo(4));
            Assert.That(rows.All(row => row.IsSelectable), Is.True);
            foreach (var row in rows)
            {
                Assert.That(row.RetrievedUtc, Is.EqualTo(new DateTime(2026, 10, 15, 9, 23, 0, DateTimeKind.Utc)));
                Assert.That(row.ValidationErrorCount, Is.Zero);
                Assert.That(row.ValidationWarningCount, Is.EqualTo(1));
                Assert.That(row.Directory, Is.EqualTo(_paths.CanonicalFinalDirectory(row.Identity)));
                Assert.That(row.ContentHashPrefix.Length, Is.InRange(8, 16));
                Assert.That(row.Entry.ContentHash, Does.StartWith(row.ContentHashPrefix));
                Assert.That(row.SourceBadge, Is.Not.Empty);
                // The writer's normalized snapshot deliberately differs from the display metadata.
                Assert.That(row.Catalog.Manifest.CreatedUtc, Is.EqualTo(DateTime.UnixEpoch));
                Assert.That(row.Catalog.Manifest.ValidationWarningCount, Is.Zero);
            }
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        [TestCase("bytes")]
        [TestCase("index-hash")]
        [TestCase("provenance")]
        [TestCase("wrong-path")]
        [TestCase("validator")]
        [TestCase("missing-metadata")]
        [TestCase("negative-validation")]
        public void InvalidLocalEntryCannotHideTheHealthyRowOrBecomeSelectable(string mutation)
        {
            var healthy = DataManagerTestSupport.Identity();
            var broken = DataManagerTestSupport.Identity("16.18.2");
            Publish(healthy);
            var hash = Publish(broken, invalidCatalog: mutation == "validator");
            var directory = _paths.CanonicalFinalDirectory(broken);
            if (mutation == "bytes") File.AppendAllText(Path.Combine(directory, "catalog.json"), " ");
            if (mutation == "index-hash") _index.Upsert(broken, new string('f', 64), _paths.CatalogAssetPath(broken));
            if (mutation == "wrong-path")
            {
                var wrong = Path.Combine(_root, "Assets", "elsewhere");
                Directory.CreateDirectory(wrong);
                File.Copy(Path.Combine(directory, "catalog.json"), Path.Combine(wrong, "catalog.json"));
                File.Copy(Path.Combine(directory, "manifest.json"), Path.Combine(wrong, "manifest.json"));
                _index.Upsert(broken, hash, "Assets/elsewhere/catalog.json");
            }
            if (mutation == "provenance")
            {
                var path = Path.Combine(directory, "manifest.json");
                var manifest = JObject.Parse(File.ReadAllText(path));
                manifest["sources"][0]["IsSupplemental"] = true;
                File.WriteAllText(path, manifest.ToString());
            }
            if (mutation == "missing-metadata" || mutation == "negative-validation")
            {
                var path = Path.Combine(directory, "manifest.json");
                var manifest = JObject.Parse(File.ReadAllText(path));
                if (mutation == "missing-metadata") manifest.Remove("createdUtc");
                else manifest["validationWarningCount"] = -1;
                File.WriteAllText(path, manifest.ToString());
            }
            var before = Snapshot();

            var rows = new PublishedCatalogLoader(_paths).Load(_index);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.1").IsSelectable, Is.True);
            var failed = rows.Single(row => row.Entry.SourceGameVersion == "16.18.2");
            Assert.That(failed.IsSelectable, Is.False);
            Assert.That(failed.ErrorCode, Is.Not.Empty);
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VerifiedLegacyCanBeReadWithUnprofiledOrExplicitOfficialEntryWithoutMigration(bool unprofiled)
        {
            var identity = DataManagerTestSupport.Identity();
            var hash = Publish(identity, legacy: true);
            var entry = new CatalogIndexEntry(identity, hash, _paths.LegacyCatalogAssetPath(identity.Patch));
            if (unprofiled) typeof(CatalogIndexEntry).GetField("sourceProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(entry, "");
            _index.RestoreEntries(new[] { entry });
            var before = Snapshot();

            var row = new PublishedCatalogLoader(_paths).Load(_index).Single();

            Assert.That(row.IsSelectable, Is.True);
            Assert.That(row.Identity.ProfileId, Is.EqualTo("official-only"));
            Assert.That(row.Directory, Is.EqualTo(_paths.LegacyFinalDirectory(identity.Patch)));
            Assert.That(Directory.Exists(_paths.CanonicalFinalDirectory(identity)), Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        [Test]
        public void UnprofiledCanonicalEntryIsNotAssumedToBeOfficialLegacy()
        {
            var identity = DataManagerTestSupport.Identity();
            Publish(identity);
            typeof(CatalogIndexEntry).GetField("sourceProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_index.Entries.Single(), "");
            var row = new PublishedCatalogLoader(_paths).Load(_index).Single();
            Assert.That(row.IsSelectable, Is.False);
            Assert.That(row.ErrorCode, Is.Not.Empty);
        }

        private string Publish(CatalogIdentity identity, bool legacy = false, bool invalidCatalog = false)
        {
            var catalog = DataManagerTestSupport.Catalog(identity, legacy: legacy);
            if (invalidCatalog) catalog = new StaticCatalog(catalog.Manifest,
                catalog.Units.Concat(catalog.Units), null, null, null);
            var writer = new CatalogAssetWriter(_paths);
            var staging = _paths.CreateStagingDirectory();
            var staged = writer.WriteStagedCatalog(catalog, new CatalogValidator().Validate(catalog), staging);
            writer.Publish(staged, legacy ? _paths.LegacyFinalDirectory(identity.Patch) : _paths.CanonicalFinalDirectory(identity));
            writer.CleanupStaging(staging);
            _index.Upsert(identity, staged.ContentHash, legacy ? _paths.LegacyCatalogAssetPath(identity.Patch) : _paths.CatalogAssetPath(identity));
            return staged.ContentHash;
        }

        private string[] Snapshot() => Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => path + ":" + ContentHasher.Sha256Hex(File.ReadAllBytes(path))).ToArray();
    }
}
