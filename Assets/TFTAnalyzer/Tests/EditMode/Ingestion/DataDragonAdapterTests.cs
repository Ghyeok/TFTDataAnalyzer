using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Validation;
using UnityEngine;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class DataDragonAdapterTests
    {
        [Test]
        public async Task FixtureParsesWithoutBlankHiddenItemAndIsolatedStagedIconsRequireTheConfiguredRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "TftIconValidation_" + Guid.NewGuid().ToString("N"));
            try
            {
                var catalog = new DataDragonAdapter(new FakeTransport()).Parse(FixtureSnapshot(), Set18);
                var before = new CatalogValidator().Validate(catalog);
                Assert.That(catalog.Items.Any(item => item.StableId == "TFT_Item_Hidden"), Is.False);
                Assert.That(before.Issues.Where(issue => issue.Severity == ValidationSeverity.Error), Is.Empty);
                var paths = new StaticCatalogPaths(root, "Assets/TftAnalyzerE2E_fixture/Generated", "Library/fixture/cache");
                var staging = paths.CreateStagingDirectory();
                var staged = await new CatalogAssetWriter(paths).StageIconsAsync(catalog, new PngTransport(), staging, default);
                var wrongRoot = new CatalogValidator().Validate(staged);
                Assert.That(wrongRoot.Issues.Where(issue => issue.Severity == ValidationSeverity.Error).Select(issue => issue.Code).Distinct(),
                    Is.EqualTo(new[] { "invalid_icon_metadata" }));
                var configured = new CatalogValidator(paths.GeneratedRelativeDirectory).Validate(staged);
                Assert.That(configured.Issues.Where(issue => issue.Severity == ValidationSeverity.Error), Is.Empty);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static readonly PatchKey Set18 =
            new PatchKey("TFTSet18", 18, "18.2", "16.18.1");

        [TestCase("champions", "ko", false)]
        [TestCase("champions", "ko", true)]
        [TestCase("champions", "en", false)]
        [TestCase("champions", "en", true)]
        [TestCase("traits", "ko", false)]
        [TestCase("traits", "ko", true)]
        [TestCase("traits", "en", false)]
        [TestCase("traits", "en", true)]
        [TestCase("items", "ko", false)]
        [TestCase("items", "ko", true)]
        [TestCase("items", "en", false)]
        [TestCase("items", "en", true)]
        [TestCase("augments", "ko", false)]
        [TestCase("augments", "ko", true)]
        [TestCase("augments", "en", false)]
        [TestCase("augments", "en", true)]
        public void DuplicateSourceId_SelectedRowsRejectEqualAndDifferentValues(string kind, string locale, bool identical)
        {
            var documents = DuplicateDocuments();
            var data = documents[locale][kind]["data"];
            var row = data["TEST18_Record"];
            var duplicate = (JObject)row.DeepClone();
            if (!identical) duplicate["name"] = "different";
            data["other-source-key"] = duplicate;
            var snapshot = DuplicateSnapshot(documents);
            var adapter = new DataDragonAdapter(new FakeTransport());

            var result = adapter.ParseWithDiagnostics(snapshot, Set18);

            Assert.That(result.Issues.Any(x => x.Code == "ddragon_duplicate_source_id" && x.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(result.Catalog.Units, Is.Empty, "Do not expose a partially parsed catalog after any collision error.");
            Assert.That(result.Catalog.Traits, Is.Empty);
            Assert.That(result.Catalog.Items, Is.Empty);
            Assert.That(result.Catalog.Augments, Is.Empty);
            Assert.That(() => adapter.Parse(snapshot, Set18), Throws.InstanceOf<Newtonsoft.Json.JsonException>());
        }

        [TestCase("champions", "ko")]
        [TestCase("champions", "en")]
        [TestCase("traits", "ko")]
        [TestCase("traits", "en")]
        [TestCase("items", "ko")]
        [TestCase("items", "en")]
        [TestCase("augments", "ko")]
        [TestCase("augments", "en")]
        public void DuplicateSourceId_ExplicitIdCollidesWithFallbackKeyBeforeBlankNameFiltering(string kind, string locale)
        {
            var documents = DuplicateDocuments();
            var data = documents[locale][kind]["data"];
            ((JObject)data["TEST18_Record"]).Remove("id");
            data["TEST18_Record"]["name"] = "";
            data["alias"] = new JObject { ["id"] = "TEST18_Record", ["name"] = "" };
            var result = new DataDragonAdapter(new FakeTransport()).ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            Assert.That(result.Issues.Any(x => x.Code == "ddragon_duplicate_source_id" && x.Severity == ValidationSeverity.Error), Is.True);
        }

        [TestCase("champions", "ko")]
        [TestCase("champions", "en")]
        [TestCase("traits", "ko")]
        [TestCase("traits", "en")]
        [TestCase("items", "ko")]
        [TestCase("items", "en")]
        [TestCase("augments", "ko")]
        [TestCase("augments", "en")]
        public void DuplicateSourceId_ConfirmedSingleOtherSetWarnsAndKeepsSelectedRows(string kind, string locale)
        {
            var documents = DuplicateDocuments();
            var data = documents[locale][kind]["data"];
            data["old-a"] = new JObject { ["id"] = "TEST17_Old", ["name"] = "first" };
            data["old-b"] = new JObject { ["id"] = "TEST17_Old", ["name"] = "second" };
            var result = new DataDragonAdapter(new FakeTransport()).ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            Assert.That(result.Issues.Where(x => x.Severity == ValidationSeverity.Error), Is.Empty);
            Assert.That(result.Issues.Any(x => x.Code == "ddragon_duplicate_source_id" && x.Severity == ValidationSeverity.Warning), Is.True);
            Assert.That(result.Catalog.Units.Single().StableId, Is.EqualTo("TEST18_Record"));
            Assert.That(result.Catalog.Traits.Single().StableId, Is.EqualTo("TEST18_Record"));
            Assert.That(result.Catalog.Items.Single().StableId, Is.EqualTo("TEST18_Record"));
            Assert.That(result.Catalog.Augments.Single().StableId, Is.EqualTo("TEST18_Record"));
        }

        [TestCase("selected-other-locale")]
        [TestCase("selected-source-key")]
        [TestCase("unknown-boundary")]
        [TestCase("two-old-sets")]
        [TestCase("conflicting-id-and-key")]
        [TestCase("unknown-other-locale")]
        public void DuplicateSourceId_BothLocalesAndAllRowsMustProveOneUnselectedSet(string scenario)
        {
            var documents = DuplicateDocuments();
            var ko = documents["ko"]["champions"]["data"];
            var en = documents["en"]["champions"]["data"];
            var id = scenario == "conflicting-id-and-key" ? "TEST17_Old" : "Alias_Old";
            ko["/TFTSet17/first"] = new JObject { ["id"] = id, ["name"] = "" };
            var secondKey = scenario == "selected-source-key" ? "/TFTSet18/second" :
                scenario == "two-old-sets" || scenario == "conflicting-id-and-key" ? "/TFTSet16/second" :
                scenario == "unknown-boundary" ? "unknown" : "/TFTSet17/second";
            ko[secondKey] = new JObject { ["id"] = id, ["name"] = "" };
            if (scenario == "selected-other-locale" || scenario == "unknown-other-locale")
                en[scenario == "selected-other-locale" ? "/TFTSet18/en" : "unknown-en"] =
                    new JObject { ["id"] = id, ["name"] = "English" };
            var result = new DataDragonAdapter(new FakeTransport()).ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            Assert.That(result.Issues.Any(x => x.Code == "ddragon_duplicate_source_id" && x.Severity == ValidationSeverity.Error), Is.True, scenario);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DuplicateSourceId_ChampionPrefixDiscoveryCannotLoseSelectedOrOutsideRows(bool reverse)
        {
            var documents = DuplicateDocuments();
            var ko = (JObject)documents["ko"]["champions"]["data"];
            ko["/TFTSet18/alias"] = new JObject { ["id"] = "Alias_Unit", ["name"] = "selected" };
            ko["/TFTSet17/alias"] = new JObject { ["id"] = "Alias_Unit", ["name"] = "outside" };
            documents["en"]["traits"]["data"]["Alias_Trait"] = new JObject { ["id"] = "Alias_Trait", ["name"] = "trait" };
            if (reverse) documents["ko"]["champions"]["data"] = ReverseProperties(ko);
            var result = new DataDragonAdapter(new FakeTransport()).ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            Assert.That(result.Issues.Any(x => x.Code == "ddragon_duplicate_source_id" && x.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(result.Catalog.Traits, Is.Empty);
        }

        [Test]
        public void DuplicateSourceId_OtherSetCollisionTouchingSelectedChampionPrefixIsRejected()
        {
            var documents = DuplicateDocuments();
            documents["en"]["champions"]["data"]["/TFTSet18/seed"] = new JObject { ["id"] = "Alias_Selected", ["name"] = "selected" };
            var data = documents["ko"]["champions"]["data"];
            data["/TFTSet17/a"] = new JObject { ["id"] = "Alias_Old", ["name"] = "old-a" };
            data["/TFTSet17/b"] = new JObject { ["id"] = "Alias_Old", ["name"] = "old-b" };
            var result = new DataDragonAdapter(new FakeTransport()).ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            Assert.That(result.Issues.Any(x => x.Code == "ddragon_duplicate_source_id" && x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void DuplicateSourceId_NormalLocaleAndKindJoinsRemainIndependentAndKeepRawHashes()
        {
            var snapshot = DuplicateSnapshot(DuplicateDocuments());
            var result = new DataDragonAdapter(new FakeTransport()).ParseWithDiagnostics(snapshot, Set18);
            Assert.That(result.Issues.Where(x => x.Severity == ValidationSeverity.Error), Is.Empty);
            Assert.That(result.Catalog.Units.Single().Name.KoKr, Is.EqualTo("ko name"));
            Assert.That(result.Catalog.Units.Single().Name.EnUs, Is.EqualTo("en name"));
            Assert.That(result.Catalog.Manifest.Sources.Select(x => x.Sha256),
                Is.EqualTo(snapshot.AllResponses.Select(x => TFTAnalyzer.Serialization.ContentHasher.Sha256Hex(x.Body))));
        }

        [Test]
        public void DuplicateSourceId_WarningsAreSortedBoundedAndSafeForReportDisplay()
        {
            var documents = DuplicateDocuments();
            foreach (var kind in new[] { "traits", "items", "champions", "augments" })
            foreach (var locale in new[] { "ko", "en" })
            for (var i = 3; i >= 0; i--)
            {
                var data = documents[locale][kind]["data"];
                var id = "TEST17_" + i.ToString("D2") + "<b>\n\u202e" + new string('x', 160);
                for (var key = 5; key >= 0; key--)
                    data["key-" + key + "-" + i + "<b>\n\u202e" + new string('y', 160)] = new JObject { ["id"] = id, ["name"] = "old" };
            }
            var adapter = new DataDragonAdapter(new FakeTransport());
            var first = adapter.ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            foreach (var d in documents.Values)
            foreach (var section in d.Properties()) section.Value["data"] = ReverseProperties((JObject)section.Value["data"]);
            var second = adapter.ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            var details = first.Issues.Where(x => x.Code == "ddragon_duplicate_source_id").ToArray();
            Assert.That(first.Issues.Select(x => x.Message), Is.EqualTo(second.Issues.Select(x => x.Message)));
            Assert.That(details.Length, Is.InRange(1, 20));
            Assert.That(details.First().Message, Does.Contain("augment").And.Contain("en_US"));
            Assert.That(first.Issues.Single(x => x.Code == "ddragon_duplicate_source_id_summary").Message, Does.Contain("32"));
            Assert.That(details.All(x => x.Message.Length <= 1000 && x.RecordId.Length <= 128), Is.True);
            Assert.That(details.All(x => !x.Message.Contains("<") && !x.Message.Contains(">") &&
                !x.Message.Any(char.IsControl) && !x.Message.Contains('\u202e')), Is.True);
            Assert.That(details.First().Message, Does.Contain("\\u003c").And.Contain("\\u000a").And.Contain("\\u202e"));
        }

        [Test]
        public void DuplicateSourceId_LateErrorDetailsRemainVisibleWhenEarlierWarningsExceedTheLimit()
        {
            var documents = DuplicateDocuments();
            var warnings = documents["en"]["augments"]["data"];
            for (var i = 0; i < 25; i++)
            {
                warnings["a" + i] = new JObject { ["id"] = "TEST17_" + i, ["name"] = "old" };
                warnings["b" + i] = new JObject { ["id"] = "TEST17_" + i, ["name"] = "old" };
            }
            documents["ko"]["champions"]["data"]["duplicate"] = new JObject { ["id"] = "TEST18_Record", ["name"] = "collision" };
            var result = new DataDragonAdapter(new FakeTransport()).ParseWithDiagnostics(DuplicateSnapshot(documents), Set18);
            var details = result.Issues.Where(x => x.Code == "ddragon_duplicate_source_id").ToArray();
            Assert.That(details.Length, Is.InRange(1, 20));
            Assert.That(details.Any(x => x.Severity == ValidationSeverity.Error && x.RecordId == "TEST18_Record" &&
                x.Message.Contains("unit") && x.Message.Contains("ko_KR")), Is.True);
            Assert.That(result.Catalog.Units, Is.Empty);
            Assert.That(result.Issues.Single(x => x.Code == "ddragon_duplicate_source_id_summary").Message, Does.Contain("26"));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task DuplicateSourceId_FailedOfficialImportPreservesExistingPublicationAndSkipsSupplement(bool supplement, bool existing)
        {
            var root = Path.Combine(Path.GetTempPath(), "TftDuplicateIds_" + Guid.NewGuid().ToString("N"));
            try
            {
                var transport = new DuplicateTransport();
                var paths = new StaticCatalogPaths(root);
                var index = new DuplicateIndexWriter();
                var source = new NetworkStaticCatalogSource(new DataDragonAdapter(transport), new CommunityDragonAdapter(), new CatalogMerger(), transport);
                var pipeline = new StaticCatalogPipeline(source, transport, paths, new CatalogAssetWriter(paths), new CatalogValidator(), index, null);
                var request = new StaticImportRequest(new CatalogIdentity(Set18, supplement ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly));
                string finalDirectory = paths.CanonicalFinalDirectory(request.Identity);
                Dictionary<string, byte[]> before = null;
                if (existing)
                {
                    var published = await pipeline.RunAsync(request, default);
                    Assert.That(published.Published, Is.True);
                    before = Directory.GetFiles(finalDirectory, "*", SearchOption.AllDirectories)
                        .ToDictionary(x => x, File.ReadAllBytes);
                }
                transport.Documents["ko"]["items"]["data"]["duplicate"] = transport.Documents["ko"]["items"]["data"]["TEST18_Record"].DeepClone();
                var writes = index.Writes;
                var supplementRequests = transport.SupplementRequests;
                var result = await pipeline.RunAsync(request, default);
                Assert.That(result.Published, Is.False);
                Assert.That(result.Validation.Issues.Any(x => x.Code == "ddragon_duplicate_source_id"), Is.True);
                Assert.That(transport.SupplementRequests, Is.EqualTo(supplementRequests));
                Assert.That(index.Writes, Is.EqualTo(writes));
                Assert.That(Directory.Exists(result.FinalDirectory), Is.EqualTo(existing));
                if (before != null)
                {
                    Assert.That(Directory.GetFiles(finalDirectory, "*", SearchOption.AllDirectories), Is.EquivalentTo(before.Keys));
                    foreach (var file in before) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value));
                }
                Assert.That(Directory.GetDirectories(paths.StagingDirectory), Is.Empty);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static Dictionary<string, JObject> DuplicateDocuments()
        {
            return new[] { "ko", "en" }.ToDictionary(locale => locale, locale => new JObject(
                new[] { "champions", "traits", "items", "augments" }.Select(kind => new JProperty(kind,
                    new JObject { ["data"] = new JObject { ["TEST18_Record"] = new JObject { ["id"] = "TEST18_Record", ["name"] = locale + " name", ["cost"] = 1 } } }))));
        }

        private static JObject ReverseProperties(JObject value) => new JObject(value.Properties().Reverse()
            .Select(x => new JProperty(x.Name, x.Value.DeepClone())));

        private static DataDragonSnapshot DuplicateSnapshot(Dictionary<string, JObject> documents) => new DataDragonSnapshot(
            Pair(documents["ko"], documents["en"], "champions"), Pair(documents["ko"], documents["en"], "traits"),
            Pair(documents["ko"], documents["en"], "items"), Pair(documents["ko"], documents["en"], "augments"));

        private sealed class DuplicateTransport : IHttpTransport
        {
            internal readonly Dictionary<string, JObject> Documents = DuplicateDocuments();
            internal int SupplementRequests;
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                if (url.Contains("/cdragon/"))
                {
                    SupplementRequests++;
                    var set = new JObject { ["number"] = 18, ["champions"] = new JArray(new JObject {
                        ["apiName"] = "TEST18_Record", ["name"] = "supplement name", ["cost"] = 1,
                        ["desc"] = "Supplementary description" }) };
                    return Task.FromResult(Response(url, new JObject { ["setData"] = new JArray(set) }.ToString()));
                }
                var kind = url.EndsWith("tft-champion.json") ? "champions" : url.EndsWith("tft-trait.json") ? "traits" :
                    url.EndsWith("tft-item.json") ? "items" : "augments";
                return Task.FromResult(Response(url, Documents[url.Contains("ko_KR") ? "ko" : "en"][kind].ToString()));
            }
        }

        private sealed class DuplicateIndexWriter : ICatalogIndexWriter
        {
            internal int Writes;
            public void Upsert(CatalogIdentity identity, string contentHash, string catalogAssetPath) { Writes++; }
        }

        [Test]
        public void Parse_JoinsLocalesAndFiltersRequestedSetPrefix()
        {
            var adapter = new DataDragonAdapter(new FakeTransport());

            var catalog = adapter.Parse(FixtureSnapshot(), Set18);

            var primaryUnit = catalog.Units.Single(unit => unit.StableId == "TEST18_Unit_A");
            Assert.That(primaryUnit.Name.KoKr, Is.EqualTo("시험 기물"));
            Assert.That(primaryUnit.Name.EnUs, Is.EqualTo("Test Unit"));
            Assert.That(primaryUnit.Cost, Is.EqualTo(4));
            Assert.That(catalog.Units,
                Has.None.Matches<UnitRecord>(unit => unit.StableId.StartsWith("TEST17_")));
            Assert.That(catalog.Units.Select(unit => unit.StableId),
                Does.Contain("DA_18_Unit_A"));
            Assert.That(catalog.Traits.Select(trait => trait.StableId),
                Is.EquivalentTo(new[] { "TEST18_Trait_A", "DA_18_Trait_A" }));
            Assert.That(catalog.Items.Select(item => item.StableId),
                Is.EquivalentTo(new[] { "TEST18_Item_A", "TFT_Item_Generic", "DA_Item_A" }));
            Assert.That(catalog.Augments.Select(augment => augment.StableId),
                Is.EquivalentTo(new[] { "TEST18_Augment_A", "DA_Augment_A" }));
        }

        [Test]
        public void Parse_UsesEnglishFallbackWhenKoreanEntryIsBlank()
        {
            var catalog = new DataDragonAdapter(new FakeTransport()).Parse(FixtureSnapshot(), Set18);

            var augment = catalog.Augments.Single(value => value.StableId == "TEST18_Augment_A");
            Assert.That(augment.Name.DisplayValue, Is.EqualTo("Test Augment"));
            Assert.That(augment.Name.UsedEnglishFallback, Is.True);
        }

        [Test]
        public void Endpoints_PinVersionAndLocale()
        {
            Assert.That(
                DataDragonEndpoints.Document("16.18.1", "ko_KR", DataDragonDocumentKind.Champions),
                Is.EqualTo("https://ddragon.leagueoflegends.com/cdn/16.18.1/data/ko_KR/tft-champion.json"));
        }

        [Test]
        public async Task FetchAsync_RequestsAllFourDocumentsForBothLocales()
        {
            var transport = new FakeTransport();
            var adapter = new DataDragonAdapter(transport);

            await adapter.FetchAsync(new DataDragonRequest(Set18), CancellationToken.None);

            Assert.That(transport.RequestedUrls.Count, Is.EqualTo(8));
            Assert.That(transport.RequestedUrls,
                Has.Some.Contains("/ko_KR/tft-item.json"));
            Assert.That(transport.RequestedUrls,
                Has.Some.Contains("/en_US/tft-augments.json"));
        }

        [Test]
        public void HttpTransportException_DoesNotExposeResponseBody()
        {
            var exception = new HttpTransportException(
                "https://example.test/data.json",
                500,
                "Internal Server Error");

            Assert.That(exception.Message, Does.Not.Contain("secret response body"));
            Assert.That(exception.StatusCode, Is.EqualTo(500));
        }

        private static DataDragonSnapshot FixtureSnapshot()
        {
            var en = JObject.Parse(ReadFixture("ddragon-en_US.json"));
            var ko = JObject.Parse(ReadFixture("ddragon-ko_KR.json"));
            return new DataDragonSnapshot(
                Pair(ko, en, "champions"),
                Pair(ko, en, "traits"),
                Pair(ko, en, "items"),
                Pair(ko, en, "augments"));
        }

        private static DataDragonPayloadPair Pair(JObject ko, JObject en, string section)
        {
            return new DataDragonPayloadPair(
                Response($"https://example.test/ko/{section}.json", ko[section].ToString()),
                Response($"https://example.test/en/{section}.json", en[section].ToString()));
        }

        private static HttpResponseData Response(string url, string body)
        {
            return new HttpResponseData(
                url,
                200,
                Encoding.UTF8.GetBytes(body),
                "fixture-etag",
                "application/json",
                DateTime.UnixEpoch);
        }

        private static string ReadFixture(string name)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "TFTAnalyzer",
                "Tests",
                "Fixtures",
                "Static",
                name));
        }

        private sealed class FakeTransport : IHttpTransport
        {
            public List<string> RequestedUrls { get; } = new List<string>();

            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                RequestedUrls.Add(url);
                return Task.FromResult(Response(url, "{\"data\":{}}"));
            }
        }

        private sealed class PngTransport : IHttpTransport
        {
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
                return Task.FromResult(new HttpResponseData(url, 200, png, "", "image/png", DateTime.UnixEpoch));
            }
        }
    }
}
