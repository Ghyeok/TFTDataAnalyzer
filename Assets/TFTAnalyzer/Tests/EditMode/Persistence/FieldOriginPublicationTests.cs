using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.Ingestion;
using TFTAnalyzer.Validation;
using Newtonsoft.Json.Linq;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class FieldOriginPublicationTests
    {
        private string _root;
        private StaticCatalogPaths _paths;
        private sealed class Source : IStaticCatalogSource, IHttpTransport, ICatalogIndexWriter
        {
            public StaticCatalog Catalog;
            public Task<StaticSourceLoadResult> LoadAsync(StaticImportRequest request, CancellationToken token) =>
                Task.FromResult(new StaticSourceLoadResult(Catalog, null, null));
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token) => throw new InvalidOperationException("No network.");
            public void Upsert(CatalogIdentity identity, string hash, string path) { }
        }
        [SetUp] public void Before() { _root = Path.Combine(Path.GetTempPath(), "TftOrigins_" + Guid.NewGuid().ToString("N")); _paths = new StaticCatalogPaths(_root); }
        [TearDown] public void After() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestCase(false)]
        [TestCase(true)]
        public async Task FieldOriginPublication_PipelineReturnsActuallyStoredCurrentSchemaAndOrigins(bool supplemental)
        {
            var c = supplemental ? new CatalogMerger().Merge(FieldOriginIngestionTests.Official(), FieldOriginIngestionTests.Supplemental()).Catalog
                : FieldOriginIngestionTests.Official();
            var source = new Source { Catalog = c };
            var p = new StaticCatalogPipeline(source, source, _paths, new CatalogAssetWriter(_paths), new CatalogValidator(), source, null);
            var result = await p.RunAsync(new StaticImportRequest(c.Manifest.Identity), default);
            Assert.That(result.Published, Is.True);
            var disk = CanonicalCatalogSerializer.Deserialize(File.ReadAllText(Path.Combine(result.FinalDirectory, "catalog.json")));
            Assert.That(disk.Manifest.SchemaVersion, Is.EqualTo(4));
            Assert.That(disk.FieldOrigins.Count, Is.EqualTo(c.FieldOrigins.Count));
            Assert.That(new CatalogValidator().Validate(disk).CanPublish, Is.True);
            Assert.That(result.Catalog.Manifest.SchemaVersion, Is.EqualTo(disk.Manifest.SchemaVersion));
            Assert.That(CanonicalCatalogSerializer.Serialize(result.Catalog), Is.EqualTo(CanonicalCatalogSerializer.Serialize(disk)));
            Assert.That(CatalogAssetWriter.VerifyPublishedHash(result.FinalDirectory, c.Manifest.Identity, false), Is.EqualTo(result.ContentHash));
            var index = UnityEngine.ScriptableObject.CreateInstance<CatalogIndex>();
            try
            {
                index.Upsert(c.Manifest.Identity, result.ContentHash, _paths.CatalogAssetPath(c.Manifest.Identity));
                var row = new PublishedCatalogLoader(_paths).Load(index).Single();
                Assert.That(row.IsSelectable, Is.True);
                Assert.That(row.Catalog.FieldOrigins.Count, Is.EqualTo(c.FieldOrigins.Count));
                // Updating both outer hashes must not bypass the semantic completeness check.
                var catalogPath = Path.Combine(result.FinalDirectory, "catalog.json");
                var altered = JObject.Parse(File.ReadAllText(catalogPath));
                altered["fieldOrigins"].Single(x => x.Value<string>("pointer") == "/cost").Remove();
                File.WriteAllText(catalogPath, altered.ToString());
                var alteredHash = ContentHasher.Sha256Hex(File.ReadAllBytes(catalogPath));
                var manifestPath = Path.Combine(result.FinalDirectory, "manifest.json");
                var metadata = JObject.Parse(File.ReadAllText(manifestPath)); metadata["contentHash"] = alteredHash;
                File.WriteAllText(manifestPath, metadata.ToString());
                index.Upsert(c.Manifest.Identity, alteredHash, _paths.CatalogAssetPath(c.Manifest.Identity));
                Assert.That(new PublishedCatalogLoader(_paths).Load(index).Single().IsSelectable, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(index); }
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public async Task FieldOriginPublication_ReusesOldBytesAndReturnsOldCatalog(int schema, bool supplemental)
        {
            var c = supplemental ? new CatalogMerger().Merge(FieldOriginIngestionTests.Official(), FieldOriginIngestionTests.Supplemental()).Catalog
                : FieldOriginIngestionTests.Official(); var m = c.Manifest;
            var oldManifest = schema == 1 ? new PatchManifest(1, m.Patch, m.SetName, m.CreatedUtc, "", m.Sources, m.PolicyReviewDate,
                m.PolicyUrls, 0, 0, "", "") : new PatchManifest(2, m.Identity, m.SetName, m.CreatedUtc, "", m.Sources, m.PolicyReviewDate, m.PolicyUrls, 0, 0, "", m.CommunityDragonVersion);
            // These are explicit old-format fixture values. Current adapters now return
            // schema4 structures, which must not be smuggled into a schema1/2 fixture.
            var old = new StaticCatalog(oldManifest,
                c.Units.Select(x => new UnitRecord(x.StableId, x.Name, x.Cost, x.TraitIds, x.IconPath, x.Sources, x.Role, x.Description, x.Effects)),
                c.Traits.Select(x => new TraitRecord(x.StableId, x.Name, x.Description, x.Effects, x.IconPath, x.Sources)),
                c.Items.Select(x => new ItemRecord(x.StableId, x.Name, x.Description, x.ComponentIds, x.Effects, x.IconPath, x.Sources)),
                c.Augments.Select(x => new AugmentRecord(x.StableId, x.Name, x.Tier, x.Description, x.RelatedIds, x.Effects, x.IconPath, x.Sources)));
            var writer = new CatalogAssetWriter(_paths); var staging = _paths.CreateStagingDirectory();
            var staged = writer.WriteStagedCatalog(old, new CatalogValidator().Validate(old), staging);
            var directory = schema == 1 ? _paths.LegacyFinalDirectory(m.Patch) : _paths.CanonicalFinalDirectory(m.Identity);
            writer.Publish(staged, directory); writer.CleanupStaging(staging);
            var before = File.ReadAllBytes(Path.Combine(directory, "catalog.json"));
            var source = new Source { Catalog = c };
            var pipeline = new StaticCatalogPipeline(source, source, _paths, writer, new CatalogValidator(), source, null);
            var result = await pipeline.RunAsync(new StaticImportRequest(c.Manifest.Identity), default);
            Assert.That(result.Published && result.Idempotent, Is.True);
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "catalog.json")), Is.EqualTo(before));
            Assert.That(result.Catalog.Manifest.SchemaVersion, Is.EqualTo(schema));
            Assert.That(result.Catalog.FieldOrigins, Is.Empty);
            Assert.That(result.ContentHash, Is.EqualTo(ContentHasher.Sha256Hex(before)));
        }

        private string SnapshotHash(StaticCatalog catalog)
        {
            var stage = _paths.CreateStagingDirectory(); var writer = new CatalogAssetWriter(_paths);
            try { return writer.WriteStagedCatalog(catalog, new CatalogValidator().Validate(catalog), stage).ContentHash; }
            finally { writer.CleanupStaging(stage); }
        }

        [Test]
        public void FieldOriginPublication_NormalizesVolatileSourceFieldsButHashesSelectedOrigin()
        {
            var c = FieldOriginIngestionTests.Official(); var original = SnapshotHash(c);
            var json = JObject.Parse(CanonicalCatalogSerializer.Serialize(c));
            foreach (var property in json.Descendants().OfType<JProperty>().ToArray())
            {
                if (property.Name == "retrievedUtc") property.Value = DateTime.UnixEpoch.AddDays(5);
                if (property.Name == "etag") property.Value = "changed-etag";
            }
            Assert.That(SnapshotHash(CanonicalCatalogSerializer.Deserialize(json.ToString())), Is.EqualTo(original));
            var name = json["fieldOrigins"].Single(x => x.Value<string>("pointer") == "/name/enUs");
            name["sources"] = new JArray(json["units"][0]["sources"][1].DeepClone());
            var changed = CanonicalCatalogSerializer.Deserialize(json.ToString());
            Assert.That(new CatalogValidator().Validate(changed).CanPublish, Is.True);
            Assert.That(SnapshotHash(changed), Is.Not.EqualTo(original));
        }

        [Test]
        public async Task FieldOriginPublication_ChangedLineageCannotOverwriteAnExistingSchemaThreePack()
        {
            var c = FieldOriginIngestionTests.Official(); var source = new Source { Catalog = c };
            var pipeline = new StaticCatalogPipeline(source, source, _paths, new CatalogAssetWriter(_paths), new CatalogValidator(), source, null);
            var result = await pipeline.RunAsync(new StaticImportRequest(c.Manifest.Identity), default);
            var path = Path.Combine(result.FinalDirectory, "catalog.json"); var before = File.ReadAllBytes(path);
            var json = JObject.Parse(CanonicalCatalogSerializer.Serialize(c));
            json["fieldOrigins"].Single(x => x.Value<string>("pointer") == "/name/enUs")["sources"] = new JArray(json["units"][0]["sources"][1].DeepClone());
            source.Catalog = CanonicalCatalogSerializer.Deserialize(json.ToString());
            Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.RunAsync(new StaticImportRequest(c.Manifest.Identity), default));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(Directory.GetDirectories(_paths.StagingDirectory), Is.Empty);
        }
    }
}
