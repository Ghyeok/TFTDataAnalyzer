using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Serialization;
using UnityEngine;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class LegacyCatalogResolverTests
    {
        private string _root;
        private StaticCatalogPaths _paths;
        private CatalogIdentity _identity;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftLegacyTests", Guid.NewGuid().ToString("N"));
            _paths = new StaticCatalogPaths(_root);
            _identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void OfficialSchemaOneWithPascalCaseSourcesResolvesExistingPath()
        {
            WriteLegacy(Manifest());
            var result = new LegacyCatalogResolver(_paths).Resolve(_identity);
            Assert.That(result.Status, Is.EqualTo(LegacyCatalogStatus.CompatibleOfficialLegacy));
            Assert.That(result.FinalDirectory, Is.EqualTo(_paths.LegacyFinalDirectory(_identity.Patch)));
            Assert.That(result.CatalogAssetPath, Is.EqualTo("Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/catalog.json"));
        }

        [Test]
        public void SupplementalSchemaOneIsAmbiguous()
        {
            var manifest = Manifest();
            manifest["sources"][0]["IsSupplemental"] = true;
            WriteLegacy(manifest);
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(_identity).Status, Is.EqualTo(LegacyCatalogStatus.AmbiguousLegacy));
        }

        [Test]
        public void EmbeddedSupplementalManifestCannotBeClaimedAsOfficial()
        {
            var manifest = Manifest();
            var embedded = (JObject)manifest.DeepClone();
            embedded["sources"][0]["IsSupplemental"] = true;
            WriteLegacy(manifest, embedded);
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(_identity).Status,
                Is.EqualTo(LegacyCatalogStatus.AmbiguousLegacy));
        }

        [Test]
        public void MissingEmbeddedManifestIsAmbiguous()
        {
            WriteLegacy(Manifest(), new JObject());
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(_identity).Status,
                Is.EqualTo(LegacyCatalogStatus.AmbiguousLegacy));
        }

        [Test]
        public void CommunityVersionInSchemaOneIsAmbiguous()
        {
            var manifest = Manifest();
            manifest["communityDragonVersion"] = "16.18";
            WriteLegacy(manifest);
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(_identity).Status, Is.EqualTo(LegacyCatalogStatus.AmbiguousLegacy));
        }

        [Test]
        public void MissingManifestIsAmbiguous()
        {
            var directory = _paths.LegacyFinalDirectory(_identity.Patch);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "catalog.json"), "{}");
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(_identity).Status, Is.EqualTo(LegacyCatalogStatus.AmbiguousLegacy));
        }

        [Test]
        public void DifferentSourceVersionDoesNotClaimLegacy()
        {
            WriteLegacy(Manifest());
            var other = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.2"), CatalogSourceProfile.OfficialOnly);
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(other).Status, Is.EqualTo(LegacyCatalogStatus.NotFound));
        }

        [Test]
        public void MalformedManifestIsAmbiguous()
        {
            var directory = _paths.LegacyFinalDirectory(_identity.Patch);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), "not json");
            File.WriteAllText(Path.Combine(directory, "catalog.json"), "{}");
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(_identity).Status, Is.EqualTo(LegacyCatalogStatus.AmbiguousLegacy));
        }

        [Test]
        public void ModifiedLegacyCatalogBytesCannotBeClassifiedCompatible()
        {
            WriteLegacy(Manifest());
            File.WriteAllText(Path.Combine(_paths.LegacyFinalDirectory(_identity.Patch), "catalog.json"),
                "{\"tampered\":true}");
            Assert.That(new LegacyCatalogResolver(_paths).Resolve(_identity).Status,
                Is.EqualTo(LegacyCatalogStatus.AmbiguousLegacy));
        }

        [Test]
        public void CommittedSet18LegacyCatalogResolvesWithoutChangingItsBytes()
        {
            var paths = new StaticCatalogPaths(Path.GetDirectoryName(Application.dataPath));
            var catalogPath = Path.Combine(paths.LegacyFinalDirectory(_identity.Patch), "catalog.json");
            var before = ContentHasher.Sha256Hex(File.ReadAllBytes(catalogPath));
            Assert.That(before, Is.EqualTo("b306b524fad37119ea88fe28f2f7241423ee997f06bddc563890adb35f0c27f9"));
            Assert.That(new LegacyCatalogResolver(paths).Resolve(_identity).Status,
                Is.EqualTo(LegacyCatalogStatus.CompatibleOfficialLegacy));
            Assert.That(ContentHasher.Sha256Hex(File.ReadAllBytes(catalogPath)), Is.EqualTo(before));
        }

        private void WriteLegacy(JObject manifest, JObject embeddedManifest = null)
        {
            var directory = _paths.LegacyFinalDirectory(_identity.Patch);
            Directory.CreateDirectory(directory);
            var catalog = new JObject
            {
                ["manifest"] = embeddedManifest ?? (JObject)manifest.DeepClone(),
                ["units"] = new JArray(),
                ["traits"] = new JArray(),
                ["items"] = new JArray(),
                ["augments"] = new JArray()
            };
            var catalogJson = JsonConvert.SerializeObject(catalog, Formatting.Indented);
            manifest["contentHash"] = ContentHasher.Sha256Hex(catalogJson);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToString());
            File.WriteAllText(Path.Combine(directory, "catalog.json"), catalogJson);
        }

        private static JObject Manifest() => JObject.Parse(File.ReadAllText(Path.Combine(
            Application.dataPath, "TFTAnalyzer", "Tests", "Fixtures", "Static",
            "legacy-official-manifest.json")));
    }
}
