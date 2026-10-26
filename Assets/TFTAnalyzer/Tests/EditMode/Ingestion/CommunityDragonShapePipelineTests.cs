using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class CommunityDragonShapePipelineTests
    {
        private string _root;
        private sealed class Index : ICatalogIndexWriter { public void Upsert(CatalogIdentity identity, string hash, string path) { } }
        [SetUp] public void Before() { _root = Path.Combine(Path.GetTempPath(), "TftShape_" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void After() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        private StaticCatalogPipeline Pipeline(CommunityDragonShapeTests.ShapeTransport transport, StaticCatalogPaths paths) =>
            new StaticCatalogPipeline(new NetworkStaticCatalogSource(new DataDragonAdapter(transport), new CommunityDragonAdapter(), new CatalogMerger(), transport),
                transport, paths, new CatalogAssetWriter(paths), new CatalogValidator(), new Index(), null);
        private static StaticImportRequest Request() => new StaticImportRequest(new CatalogIdentity(CommunityDragonShapeTests.Patch, CatalogSourceProfile.OfficialPlusCommunityDragon));

        [TestCase(false)]
        [TestCase(true)]
        public async Task ShapePipeline_ReportsDiagnosticsAndNeverPublishesAnError(bool damaged)
        {
            var transport = new CommunityDragonShapeTests.ShapeTransport();
            if (damaged) transport.En["items"][0]["isAugment"] = true;
            var paths = new StaticCatalogPaths(_root);
            var result = await Pipeline(transport, paths).RunAsync(Request(), default);
            var report = StaticImportReportViewModel.FromResult(Request().Identity, true, DateTimeOffset.UnixEpoch, result);
            Assert.That(result.Published, Is.EqualTo(!damaged));
            Assert.That(report.ValidationRows.Any(x => x.Contains(damaged ? "cdragon_record_kind_conflict" : "cdragon_unsupported_trait_tiers")), Is.True);
            if (damaged) Assert.That(Directory.Exists(result.FinalDirectory), Is.False);
            else
            {
                var metadata = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(result.FinalDirectory, "manifest.json")));
                Assert.That(metadata.Value<int>("validationWarningCount"), Is.EqualTo(result.Validation.WarningCount));
                Assert.That(result.Catalog.Units.Single().Cost, Is.EqualTo(1));
                Assert.That(result.Catalog.Units.Single().Description.EnUs, Is.EqualTo("Nested description"));
            }
        }

        [Test]
        public async Task ShapePipeline_RoundTripsLineageBrowserAndImmutablePack()
        {
            var transport = new CommunityDragonShapeTests.ShapeTransport(); var paths = new StaticCatalogPaths(_root);
            var pipeline = Pipeline(transport, paths); var first = await pipeline.RunAsync(Request(), default);
            var before = File.ReadAllBytes(Path.Combine(first.FinalDirectory, "catalog.json"));
            var second = await pipeline.RunAsync(Request(), default);
            Assert.That(second.Idempotent, Is.True); Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));
            var index = UnityEngine.ScriptableObject.CreateInstance<CatalogIndex>();
            try
            {
                index.Upsert(Request().Identity, first.ContentHash, paths.CatalogAssetPath(Request().Identity));
                var row = new PublishedCatalogLoader(paths).Load(index).Single();
                Assert.That(row.IsSelectable, Is.True);
                using (var browser = new CatalogBrowserViewModel())
                {
                    browser.SetCatalog(row.Catalog, row.Identity); browser.SetFilter(CatalogRecordKind.Item, "TFT_Shape_Item_A"); browser.Select("TFT_Shape_Item_A");
                    Assert.That(browser.Selected.DetailsText, Does.Contain("/effects/Power"));
                    Assert.That(browser.Selected.DetailsText, Does.Contain("CommunityDragon"));
                }
                var participants = Enumerable.Range(1, 8).Select(place => new AnonymousParticipant(place, 30,
                    new[] { new AnonymousUnit("TFT_Shape_Unit_A", 2, null) }, null, place == 1 ? new[] { "TFT_Shape_Augment_A" } : null)).ToArray();
                var scope = new MetricScope(Request().Identity, "asia", "1100", "MASTER+");
                var metrics = AggregateBuilder.Build(Enumerable.Range(0, 100).Select(i => new AnonymousMatchObservation(
                    ContentHasher.Sha256Hex("shape-" + i), scope, DateTimeOffset.UnixEpoch, participants))).Scopes.Single().Metrics;
                var policy = new MetricPolicy().Evaluate(metrics.Single(x => x.AugmentIds.Count == 1), row.Catalog);
                Assert.That(policy.ReasonCode, Is.EqualTo("unclassified_augment")); Assert.That(policy.Ratios, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(index); }
            transport.En["items"][0]["effects"]["Power"] = 12;
            Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.RunAsync(Request(), default));
            Assert.That(File.ReadAllBytes(Path.Combine(first.FinalDirectory, "catalog.json")), Is.EqualTo(before));
            Assert.That(Directory.GetDirectories(paths.StagingDirectory), Is.Empty);
        }
    }
}
