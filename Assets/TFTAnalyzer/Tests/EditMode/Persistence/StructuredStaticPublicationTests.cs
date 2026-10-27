using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.Ingestion;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class StructuredStaticPublicationTests
    {
        private string _root; private StaticCatalogPaths _paths;
        private sealed class Index : ICatalogIndexWriter
        {
            internal int Writes; internal bool Fail;
            public void Upsert(CatalogIdentity identity, string hash, string path) { if (Fail) throw new IOException("Synthetic index failure."); Writes++; }
        }
        private sealed class Source : IStaticCatalogSource, IHttpTransport
        {
            internal StaticCatalog Catalog;
            public Task<StaticSourceLoadResult> LoadAsync(StaticImportRequest request, CancellationToken token) => Task.FromResult(new StaticSourceLoadResult(Catalog, null, null));
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token) => throw new InvalidOperationException("No network in model publication fixture.");
        }
        [SetUp] public void Before() { _root = Path.Combine(Path.GetTempPath(), "TftStructured_" + Guid.NewGuid().ToString("N")); _paths = new StaticCatalogPaths(_root); }
        [TearDown] public void After()
        {
            var prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Directory.Exists(_root) && Path.GetFullPath(_root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) Directory.Delete(_root, true);
        }
        private StaticCatalogPipeline NetworkPipeline(StructuredStaticIngestionTests.StructuredTransport t, Index index, Action refresh = null) =>
            new StaticCatalogPipeline(new NetworkStaticCatalogSource(new DataDragonAdapter(t), new CommunityDragonAdapter(), new CatalogMerger(), t),
                t, _paths, new CatalogAssetWriter(_paths), new CatalogValidator(), index, refresh);
        private static StaticImportRequest Request(bool supplement) => new StaticImportRequest(new CatalogIdentity(CommunityDragonShapeTests.Patch,
            supplement ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly));
        private static StructuredStaticIngestionTests.StructuredTransport Transport()
        {
            var t = new StructuredStaticIngestionTests.StructuredTransport { AllowSupplement = true };
            foreach (var d in new[] { t.Ko, t.En })
            {
                StructuredStaticIngestionTests.Selected(d)["champions"][0]["image"] = new JObject { ["full"] = "structured.png" };
                StructuredStaticIngestionTests.Selected(d)["augments"][0]["unmodeled"] = false;
            }
            return t;
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task StructuredStatic_NetworkPipelinePreservesValuesOriginsAndStagedIconThroughLoader(bool supplement)
        {
            var t = Transport(); var index = new Index(); var request = Request(supplement);
            var result = await NetworkPipeline(t, index).RunAsync(request, default);
            Assert.That(result.Published, Is.True, string.Join(",", result.Validation.Issues.Select(x => x.Code)));
            Assert.That(t.IconRequests, Is.EqualTo(1)); Assert.That(index.Writes, Is.EqualTo(1));
            var catalog = result.Catalog;
            Assert.That(catalog.Manifest.SchemaVersion, Is.EqualTo(4)); Assert.That(catalog.Units.Single().BaseStats["hp"], Is.EqualTo(100));
            Assert.That(catalog.Units.Single().AbilityVariables.Single().Values, Is.EqualTo(new double?[] { 0, 10, null, 30 }));
            Assert.That(catalog.Traits.Single().Breakpoints.Single().MinUnits, Is.Null); Assert.That(catalog.Items.Single().Unique, Is.False);
            Assert.That(catalog.Augments.Single().Extensions.Any(x => x.Pointer == "/unmodeled"), Is.True);
            Assert.That(catalog.Units.Single().IconPath, Does.StartWith("Assets/TFTAnalyzer/Data/Generated/Icons/"));
            Assert.That(catalog.FieldOrigins.Single(x => x.RecordType == "unit" && x.Pointer == "/baseStats/hp").Sources.Single().IsSupplemental, Is.False);
            var metadata = JObject.Parse(File.ReadAllText(Path.Combine(result.FinalDirectory, "manifest.json")));
            Assert.That(metadata.Value<int>("validationWarningCount"), Is.EqualTo(result.Validation.WarningCount));
            Assert.That(CatalogAssetWriter.VerifyPublishedHash(result.FinalDirectory, request.Identity, false), Is.EqualTo(result.ContentHash));
            var asset = UnityEngine.ScriptableObject.CreateInstance<CatalogIndex>();
            try
            {
                asset.Upsert(request.Identity, result.ContentHash, _paths.CatalogAssetPath(request.Identity));
                var row = new PublishedCatalogLoader(_paths).Load(asset).Single(); Assert.That(row.IsSelectable, Is.True);
                Assert.That(row.Catalog.Units.Single().BaseStats["mana"], Is.Null);
                using (var browser = new CatalogBrowserViewModel())
                {
                    browser.SetCatalog(row.Catalog, row.Identity); browser.SetFilter(CatalogRecordKind.Unit, "TFT_Shape_Unit_A"); browser.Select("TFT_Shape_Unit_A");
                    Assert.That(browser.Selected.DetailsText, Does.Contain("/cost"));
                    Assert.That(browser.StructuredRows.Single(x => x.Field == "/baseStats/hp").Sources.Single().IsSupplemental, Is.False);
                }
                if (supplement)
                {
                    var participants = Enumerable.Range(1, 8).Select(place => new AnonymousParticipant(place, 30,
                        new[] { new AnonymousUnit("TFT_Shape_Unit_A", 2, null) }, null, place == 1 ? new[] { "TFT_Shape_Augment_A" } : null)).ToArray();
                    var scope = new MetricScope(request.Identity, "asia", "1100", "MASTER+");
                    var metrics = AggregateBuilder.Build(Enumerable.Range(0, 100).Select(i => new AnonymousMatchObservation(
                        ContentHasher.Sha256Hex("structured-" + i), scope, DateTimeOffset.UnixEpoch, participants))).Scopes.Single().Metrics;
                    var policy = new MetricPolicy().Evaluate(metrics.Single(x => x.AugmentIds.Count == 1), row.Catalog);
                    Assert.That(policy.ReasonCode, Is.EqualTo("unclassified_augment")); Assert.That(policy.Ratios, Is.Empty);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
            Assert.That(Directory.GetDirectories(_paths.StagingDirectory), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task StructuredStatic_OfficialParseErrorReachesReportWithoutPromotionOrPublication(bool supplement)
        {
            var t = Transport(); StructuredStaticIngestionTests.Selected(t.En)["champions"][0]["stats"]["hp"] = "damaged";
            var index = new Index(); var result = await NetworkPipeline(t, index).RunAsync(Request(supplement), default);
            Assert.That(result.Published, Is.False); Assert.That(result.Validation.Issues.Any(x => x.Code == "ddragon_invalid_static_data"), Is.True);
            Assert.That(t.CommunityRequests, Is.Zero); Assert.That(t.IconRequests, Is.Zero); Assert.That(index.Writes, Is.Zero);
            Assert.That(Directory.Exists(result.FinalDirectory), Is.False); Assert.That(Directory.GetDirectories(_paths.StagingDirectory), Is.Empty);
        }

        [Test]
        public async Task StructuredStatic_CombinedExtensionBudgetReportsErrorWithoutPublishing()
        {
            var t = Transport(); StructuredStaticIngestionTests.Selected(t.En)["champions"][0]["largeUnknown"] = new string('x',40000);
            var official = new DataDragonAdapter(t).ParseWithDiagnostics(StructuredStaticIngestionTests.OfficialSnapshot(t.Ko,t.En),Request(true).Patch);
            var supplement = StructuredStaticIngestionTests.Parse(t.Ko,t.En);
            Assert.That(official.Issues.Any(x => x.Severity == ValidationSeverity.Error),Is.False);
            Assert.That(supplement.Issues.Any(x => x.Severity == ValidationSeverity.Error),Is.False);
            Assert.That(official.Catalog.Units.Single().Extensions.Sum(x => System.Text.Encoding.UTF8.GetByteCount(x.CanonicalJson)), Is.LessThan(65536));
            Assert.That(supplement.Catalog.Units.Single().Extensions.Sum(x => System.Text.Encoding.UTF8.GetByteCount(x.CanonicalJson)), Is.LessThan(65536));
            var index = new Index(); var result = await NetworkPipeline(t,index).RunAsync(Request(true),default);
            Assert.That(result.Published,Is.False);
            Assert.That(result.Validation.Issues.Any(x => x.Code == "structured_extension_budget_exceeded"),Is.True);
            Assert.That(t.IconRequests,Is.Zero); Assert.That(index.Writes,Is.Zero);
            Assert.That(Directory.Exists(result.FinalDirectory),Is.False); Assert.That(Directory.GetDirectories(_paths.StagingDirectory),Is.Empty);
        }

        private static StaticCatalog NewCatalog(bool supplement, double stat = 100)
        {
            var left = StructuredStaticMergeTests.Catalog(false, sourceName: "Riot Data Dragon", sourceUrl: "https://ddragon.leagueoflegends.com/cdn/test.json");
            var data = StructuredStaticMergeTests.Catalog(supplement, new Dictionary<string, double?> { ["hp"] = stat },
                new[] { new StaticNumericSeries("D", new double?[] { 0, null, 2 }) }, new[] { new TraitBreakpoint(null, 9, 1, null) }, false,
                sourceName: supplement ? "CommunityDragon" : "Riot Data Dragon", sourceUrl: supplement ? "https://raw.communitydragon.org/16.18/cdragon/tft/en_us.json" : "https://ddragon.leagueoflegends.com/cdn/test.json");
            return supplement ? new CatalogMerger().Merge(left, data).Catalog : data;
        }
        // Independent legacy fixture definition; the immutable baseline golden was generated
        // separately with the ORIGINAL serializer, never regenerated by this helper.
        private static StaticCatalog Legacy(StaticCatalog c, int schema)
        {
            var m = c.Manifest;
            var old = schema == 1 ? new PatchManifest(1, m.Patch, m.SetName, m.CreatedUtc, "", m.Sources, m.PolicyReviewDate, m.PolicyUrls, 0, 0, "", "") :
                new PatchManifest(schema, m.Identity, m.SetName, m.CreatedUtc, "", m.Sources, m.PolicyReviewDate, m.PolicyUrls, 0, 0, "", m.CommunityDragonVersion);
            return new StaticCatalog(old,
                c.Units.Select(x => new UnitRecord(x.StableId, x.Name, x.Cost, x.TraitIds, x.IconPath, x.Sources, x.Role, x.Description, x.Effects)),
                c.Traits.Select(x => new TraitRecord(x.StableId, x.Name, x.Description, x.Effects, x.IconPath, x.Sources)),
                c.Items.Select(x => new ItemRecord(x.StableId, x.Name, x.Description, x.ComponentIds, x.Effects, x.IconPath, x.Sources)),
                c.Augments.Select(x => new AugmentRecord(x.StableId, x.Name, x.Tier, x.Description, x.RelatedIds, x.Effects, x.IconPath, x.Sources)),
                schema == 3 ? c.FieldOrigins.Where(x => x.Pointer == "/stableId" || x.Pointer == "/name/enUs" || x.Pointer == "/cost") : null);
        }
        private string PublishOld(StaticCatalog c, int schema)
        {
            var writer = new CatalogAssetWriter(_paths); var stage = _paths.CreateStagingDirectory();
            try
            {
                var old = Legacy(c, schema); var staged = writer.WriteStagedCatalog(old, new CatalogValidator().Validate(old), stage);
                var directory = schema == 1 ? _paths.LegacyFinalDirectory(c.Manifest.Patch) : _paths.CanonicalFinalDirectory(c.Manifest.Identity);
                writer.Publish(staged, directory); return directory;
            }
            finally { writer.CleanupStaging(stage); }
        }
        private StaticCatalogPipeline ModelPipeline(Source source, Index index) => new StaticCatalogPipeline(source, source, _paths,
            new CatalogAssetWriter(_paths), new CatalogValidator(), index, null);

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(3, true)]
        public async Task StructuredStatic_OldPackKeepsBytesHashSourcesAndReturnsItsActualSchema(int schema, bool supplement)
        {
            var c = NewCatalog(supplement); var directory = PublishOld(c, schema);
            var before = File.ReadAllBytes(Path.Combine(directory, "catalog.json")); var source = new Source { Catalog = c };
            var result = await ModelPipeline(source, new Index()).RunAsync(new StaticImportRequest(c.Manifest.Identity), default);
            Assert.That(result.Published && result.Idempotent, Is.True); Assert.That(result.Catalog.Manifest.SchemaVersion, Is.EqualTo(schema));
            Assert.That(result.Catalog.Units.Single().BaseStats, Is.Empty); Assert.That(result.Catalog.Units.Single().AbilityVariables, Is.Empty);
            Assert.That(result.Catalog.Items.Single().Unique, Is.Null); Assert.That(result.Catalog.Manifest.Sources.Count, Is.EqualTo(c.Manifest.Sources.Count));
            Assert.That(result.Catalog.Units.Single().Sources.Count, Is.EqualTo(c.Units.Single().Sources.Count));
            Assert.That(result.Catalog.FieldOrigins.Count, Is.EqualTo(schema == 3 ? 7 : 0));
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "catalog.json")), Is.EqualTo(before));
            Assert.That(result.ContentHash, Is.EqualTo(ContentHasher.Sha256Hex(before)));
        }

        [Test]
        public void StructuredStatic_NormalizedContentHashIncludesStructuredValues()
        {
            string Hash(StaticCatalog c)
            {
                var writer = new CatalogAssetWriter(_paths); var stage = _paths.CreateStagingDirectory();
                try { return writer.WriteStagedCatalog(c, new CatalogValidator().Validate(c), stage).ContentHash; }
                finally { writer.CleanupStaging(stage); }
            }
            Assert.That(Hash(NewCatalog(false, 100)), Is.Not.EqualTo(Hash(NewCatalog(false, 200))));
        }

        [Test]
        public async Task StructuredStatic_SchemaFourDifferentValueCannotOverwritePack()
        {
            var source = new Source { Catalog = NewCatalog(false) }; var pipeline = ModelPipeline(source, new Index());
            var first = await pipeline.RunAsync(new StaticImportRequest(source.Catalog.Manifest.Identity), default);
            var path = Path.Combine(first.FinalDirectory, "catalog.json"); var before = File.ReadAllBytes(path);
            source.Catalog = NewCatalog(false, 200);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.RunAsync(new StaticImportRequest(source.Catalog.Manifest.Identity), default));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before)); Assert.That(Directory.GetDirectories(_paths.StagingDirectory), Is.Empty);
        }

        [TestCase("source")]
        [TestCase("origin")]
        public void StructuredStatic_OldSchemaThreeProjectionRetainsDocumentAndLegacyOriginChanges(string change)
        {
            var c = NewCatalog(true); var directory = PublishOld(c, 3); var before = File.ReadAllBytes(Path.Combine(directory, "catalog.json"));
            var doc = JObject.Parse(CanonicalCatalogSerializer.Serialize(c));
            if (change == "source") foreach (var row in doc.Descendants().OfType<JObject>().Where(x => x.Value<bool?>("isSupplemental") == true && x["sha256"] != null)) row["sha256"] = "changed";
            else doc["fieldOrigins"].First(x => x.Value<string>("recordType") == "unit" && x.Value<string>("pointer") == "/name/enUs")["sources"] =
                new JArray(doc["manifest"]["sources"].First(x => x.Value<bool>("isSupplemental")).DeepClone());
            var changed = CanonicalCatalogSerializer.Deserialize(doc.ToString()); var source = new Source { Catalog = changed };
            Assert.ThrowsAsync<InvalidOperationException>(async () => await ModelPipeline(source, new Index()).RunAsync(new StaticImportRequest(changed.Manifest.Identity), default));
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "catalog.json")), Is.EqualTo(before));
        }

        [Test]
        public async Task StructuredStatic_PublishedIndexFailureRecoversSameSchemaFourPack()
        {
            var t = Transport(); var index = new Index { Fail = true }; var pipeline = NetworkPipeline(t, index); var request = Request(true);
            var error = Assert.ThrowsAsync<StaticImportRecoveryException>(async () => await pipeline.RunAsync(request, default));
            Assert.That(error.RecoveryState, Is.EqualTo(StaticImportRecoveryState.PublishedUnindexed));
            var before = File.ReadAllBytes(Path.Combine(error.FinalDirectory, "catalog.json")); index.Fail = false;
            var result = await pipeline.RunAsync(request, default);
            Assert.That(result.Published && result.Idempotent, Is.True); Assert.That(result.Catalog.Manifest.SchemaVersion, Is.EqualTo(4));
            Assert.That(File.ReadAllBytes(Path.Combine(result.FinalDirectory, "catalog.json")), Is.EqualTo(before));
        }

        [Test]
        public void StructuredStatic_CancelBeforeLoadNeverPublishesOrIndexes()
        {
            var t = Transport(); var index = new Index(); var pipeline = NetworkPipeline(t, index);
            Assert.CatchAsync<OperationCanceledException>(async () => await pipeline.RunAsync(Request(true), new CancellationToken(true)));
            Assert.That(index.Writes, Is.Zero); Assert.That(t.Requests, Is.Zero); Assert.That(Directory.GetDirectories(_paths.StagingDirectory), Is.Empty);
        }
    }
}
