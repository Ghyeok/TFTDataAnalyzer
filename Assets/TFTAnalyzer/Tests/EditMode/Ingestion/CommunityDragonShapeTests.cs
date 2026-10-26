using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Validation;
using UnityEngine;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class CommunityDragonShapeTests
    {
        internal static readonly PatchKey Patch = new PatchKey("TFTSet18", 18, "18.2", "16.18.1");
        internal static JObject Document(string locale) => JObject.Parse(File.ReadAllText(Path.Combine(
            Application.dataPath, "TFTAnalyzer/Tests/Fixtures/Static/cdragon-shape-" + locale + ".json")));
        internal static JObject Selected(JObject root) => root["setData"].OfType<JObject>().Single(x => x.Value<int>("number") == 18);

        [Test]
        public void Shape_SelectedSetSuppliesRecordsWithoutGuessingTheirNamespace()
        {
            var c = new CommunityDragonAdapter().Parse(Document("ko_kr").ToString(), Document("en_us").ToString(), Patch);
            Assert.That(c.Units.Select(x => x.StableId), Is.EqualTo(new[] { "TFT_Shape_Unit_A" }));
            Assert.That(c.Items.Single().Effects["Power"], Is.Zero);
            Assert.That(c.Augments.Single().StableId, Is.EqualTo("TFT_Shape_Augment_A"));
        }

        [Test]
        public void Shape_ResolvesSameDocumentTraitIdsAndLocalizedNestedDescriptions()
        {
            var c = new CommunityDragonAdapter().Parse(Document("ko_kr").ToString(), Document("en_us").ToString(), Patch);
            var unit = c.Units.Single();
            Assert.That(unit.TraitIds, Is.EqualTo(new[] { "TFT_Shape_Trait_A" }));
            Assert.That(unit.Description.KoKr, Is.EqualTo("중첩 설명"));
            Assert.That(unit.Description.EnUs, Is.EqualTo("Nested description"));
            Assert.That(c.FieldOrigins.Single(x => x.RecordType == "unit" && x.Pointer == "/traitIds").Sources.Single().Url, Does.Contain("en_us"));
            Assert.That(c.FieldOrigins.Single(x => x.RecordType == "unit" && x.Pointer == "/description/koKr").Sources.Single().Url, Does.Contain("ko_kr"));
            Assert.That(new CatalogValidator().Validate(c).CanPublish, Is.True);
        }

        [Test]
        public void Shape_MissingCostIsAnErrorRatherThanAnEmptySuccess()
        {
            var ko = Document("ko_kr"); var en = Document("en_us");
            ((JObject)Selected(en)["champions"][0]).Remove("cost");
            Assert.Throws<JsonSerializationException>(() => new CommunityDragonAdapter().Parse(ko.ToString(), en.ToString(), Patch));
        }

        [Test]
        public void Shape_DuplicateSelectedSetIsRejected()
        {
            var ko = Document("ko_kr"); ((JArray)ko["setData"]).Add(Selected(ko).DeepClone());
            Assert.Throws<JsonSerializationException>(() => new CommunityDragonAdapter().Parse(ko.ToString(), Document("en_us").ToString(), Patch));
        }

        internal static JObject GlobalDocument(string locale)
        {
            var root = Document(locale); var set = Selected(root); var rows = new JArray();
            foreach (var kind in new[] { "items", "augments" })
            {
                foreach (var row in set[kind].OfType<JObject>())
                {
                    var copy = (JObject)row.DeepClone(); copy["isAugment"] = kind == "augments"; rows.Add(copy);
                }
                set[kind] = new JArray();
            }
            rows.Add(new JObject { ["apiName"] = "TFT_Other_Item", ["name"] = "Outside", ["isAugment"] = false });
            root["items"] = rows;
            return root;
        }

        internal sealed class ShapeTransport : IHttpTransport
        {
            public JObject Ko = GlobalDocument("ko_kr"), En = GlobalDocument("en_us");
            public int Requests;
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); Requests++;
                var locale = url.Contains("ko_") ? "ko_kr" : "en_us";
                string body;
                if (url.Contains("/cdragon/tft/")) body = (locale == "ko_kr" ? Ko : En).ToString();
                else
                {
                    var kind = url.EndsWith("tft-champion.json") ? "champions" : url.EndsWith("tft-trait.json") ? "traits" :
                        url.EndsWith("tft-item.json") ? "items" : url.EndsWith("tft-augments.json") ? "augments" : throw new InvalidOperationException("Unexpected fake URL.");
                    body = OfficialBody(kind, locale);
                }
                return Task.FromResult(Response(url, body));
            }
            private static string OfficialBody(string kind, string locale)
            {
                var data = new JObject();
                foreach (var row in Selected(Document(locale))[kind].OfType<JObject>())
                {
                    var id = row.Value<string>("apiName");
                    var copy = new JObject { ["id"] = id, ["name"] = "Official " + row.Value<string>("name") };
                    if (kind == "champions") { copy["cost"] = 1; copy["traits"] = new JArray("TFT_Shape_Trait_A"); }
                    data["/TFTSet18/" + id] = copy;
                }
                return new JObject { ["data"] = data }.ToString();
            }
            private static HttpResponseData Response(string url, string body) => new HttpResponseData(url, 200,
                Encoding.UTF8.GetBytes(body), "", "application/json", DateTime.UnixEpoch);
            internal static StaticCatalog Official()
            {
                DataDragonPayloadPair Pair(DataDragonDocumentKind kind, string name) => new DataDragonPayloadPair(
                    Response(DataDragonEndpoints.Document(Patch.SourceGameVersion, "ko_KR", kind), OfficialBody(name, "ko_kr")),
                    Response(DataDragonEndpoints.Document(Patch.SourceGameVersion, "en_US", kind), OfficialBody(name, "en_us")));
                return new DataDragonAdapter(new ShapeTransport()).Parse(new DataDragonSnapshot(
                    Pair(DataDragonDocumentKind.Champions, "champions"), Pair(DataDragonDocumentKind.Traits, "traits"),
                    Pair(DataDragonDocumentKind.Items, "items"), Pair(DataDragonDocumentKind.Augments, "augments")), Patch);
            }
        }

        [Test]
        public void Shape_GlobalRowsRequireOfficialScope()
        {
            Assert.Throws<JsonSerializationException>(() => new CommunityDragonAdapter().Parse(
                GlobalDocument("ko_kr").ToString(), GlobalDocument("en_us").ToString(), Patch));
        }

        [Test]
        public void Shape_GlobalRowsUseOfficialKindIdsWithoutAuthorizingStatistics()
        {
            var official = ShapeTransport.Official();
            Assert.That(new CatalogValidator().Validate(official).CanPublish, Is.True);
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(GlobalDocument("ko_kr").ToString(), GlobalDocument("en_us").ToString(), Patch, official);
            Assert.That(parsed.Catalog.Items.Select(x => x.StableId), Is.EqualTo(new[] { "TFT_Shape_Item_A" }));
            Assert.That(parsed.Catalog.Augments.Select(x => x.StableId), Is.EqualTo(new[] { "TFT_Shape_Augment_A" }));
            Assert.That(parsed.Issues.Any(x => x.Code == "cdragon_excluded_global_records"), Is.True);
            Assert.That(parsed.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.False);
        }

        [Test]
        public void Shape_GlobalRowsRejectDifferentPatchScope()
        {
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(GlobalDocument("ko_kr").ToString(), GlobalDocument("en_us").ToString(),
                new PatchKey("TFTSet18", 18, "18.2", "16.18.2"), ShapeTransport.Official());
            Assert.That(parsed.Issues.Any(x => x.Code == "cdragon_invalid_official_scope" && x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public async Task Shape_NetworkSourcePassesActualOfficialScopeToRootItems()
        {
            var transport = new ShapeTransport();
            var source = new NetworkStaticCatalogSource(new DataDragonAdapter(transport), new CommunityDragonAdapter(), new CatalogMerger(), transport);
            var loaded = await source.LoadAsync(new StaticImportRequest(new CatalogIdentity(Patch, CatalogSourceProfile.OfficialPlusCommunityDragon)), default);
            Assert.That(loaded.Catalog.Items.Single().Effects["Power"], Is.Zero);
            Assert.That(loaded.Catalog.FieldOrigins.Single(x => x.RecordType == "item" && x.Pointer == "/effects/Power").Sources.Single().IsSupplemental, Is.True);
            Assert.That(transport.Requests, Is.EqualTo(10));
        }

        [TestCase("cost")]
        [TestCase("traits")]
        [TestCase("duplicate")]
        [TestCase("ambiguous")]
        [TestCase("composition")]
        public void Shape_RejectsDamagedSelectedFields(string damage)
        {
            var ko = Document("ko_kr"); var en = Document("en_us"); var set = Selected(en);
            if (damage == "cost") set["champions"][0]["cost"] = "2";
            if (damage == "traits") set["champions"][0]["traits"] = new JArray("Missing trait");
            if (damage == "duplicate") ((JArray)set["champions"]).Add(set["champions"][0].DeepClone());
            if (damage == "ambiguous") { var copy = (JObject)set["traits"][0].DeepClone(); copy["apiName"] = "TFT_Shape_Trait_B"; ((JArray)set["traits"]).Add(copy); }
            if (damage == "composition") set["items"][0]["composition"] = new JArray(7);
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch);
            Assert.That(parsed.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void Shape_PreservesSetNameFlatDescriptionAndKoOnlyOrigins()
        {
            var ko = Document("ko_kr"); var en = Document("en_us");
            Selected(en)["name"] = "Shape Set";
            Selected(ko)["champions"][0]["desc"] = "우선 설명";
            Selected(en)["champions"] = new JArray();
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch);
            Assert.That(parsed.Catalog.Manifest.SetName, Is.EqualTo("Shape Set"));
            var unit = parsed.Catalog.Units.Single();
            Assert.That(unit.Description.KoKr, Is.EqualTo("우선 설명"));
            Assert.That(unit.Description.EnUs, Is.Empty);
            Assert.That(parsed.Catalog.FieldOrigins.Single(x => x.RecordType == "unit" && x.Pointer == "/traitIds").Sources.Single().Url, Does.Contain("ko_kr"));
        }

        [Test]
        public void Shape_ExcludesOutOfDomainUnitsButErrorsIfAnOfficialUnitWouldBeExcluded()
        {
            var ko = Document("ko_kr"); var en = Document("en_us");
            Selected(en)["champions"][0]["cost"] = 0;
            var parser = new CommunityDragonAdapter();
            var excluded = parser.ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch);
            Assert.That(excluded.Catalog.Units, Is.Empty);
            Assert.That(excluded.Issues.Any(x => x.Code == "cdragon_excluded_unit_cost" && x.Severity == ValidationSeverity.Warning), Is.True);
            var official = parser.ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch, ShapeTransport.Official());
            Assert.That(official.Issues.Any(x => x.Code == "cdragon_official_unit_excluded" && x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void Shape_ExcludedUnitWarningRetainsItsStableId()
        {
            var ko = Document("ko_kr"); var en = Document("en_us");
            Selected(en)["champions"][0]["cost"] = 0;
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch);
            var warning = parsed.Issues.Single(x => x.Code == "cdragon_excluded_unit_cost");
            Assert.That(warning.RecordId + " " + warning.Message, Does.Contain("TFT_Shape_Unit_A"));
        }

        [Test]
        public void Shape_ExcludedUnitIdSamplesStayBoundedAndRetainTheTotal()
        {
            var ko = Document("ko_kr"); var en = Document("en_us");
            foreach (var root in new[] { ko, en })
                for (var i = 0; i < 21; i++)
                {
                    var row = (JObject)Selected(root)["champions"][0].DeepClone();
                    row["apiName"] = "EXCLUDED_" + i.ToString("D2") + new string('x', 80);
                    row["cost"] = 0;
                    ((JArray)Selected(root)["champions"]).Add(row);
                }
            var result = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch);
            var warning = result.Issues.Single(x => x.Code == "cdragon_excluded_unit_cost");
            Assert.That(warning.Message, Does.Contain("EXCLUDED_00"));
            Assert.That(warning.Message, Does.Not.Contain("EXCLUDED_20"));
            Assert.That(warning.Message, Does.Contain("21건"));
            Assert.That(warning.Message.Length, Is.LessThanOrEqualTo(512));
        }

        [TestCase("missing-bool")]
        [TestCase("wrong-kind")]
        [TestCase("duplicate")]
        public void Shape_RejectsDamagedInScopeGlobalRows(string damage)
        {
            var ko = GlobalDocument("ko_kr"); var en = GlobalDocument("en_us");
            var row = (JObject)en["items"][0];
            if (damage == "missing-bool") row.Remove("isAugment");
            if (damage == "wrong-kind") row["isAugment"] = true;
            if (damage == "duplicate") ((JArray)en["items"]).Add(row.DeepClone());
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch, ShapeTransport.Official());
            Assert.That(parsed.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void Shape_OutsideGlobalDamageCannotInvalidateKnownRecords()
        {
            var ko = GlobalDocument("ko_kr"); var en = GlobalDocument("en_us");
            ((JArray)en["items"]).Add(new JObject { ["apiName"] = "TFT_Other_Item", ["isAugment"] = "bad" });
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch, ShapeTransport.Official());
            Assert.That(parsed.Catalog.Items.Count, Is.EqualTo(1));
            Assert.That(parsed.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.False);
        }

        [TestCase(17, CatalogSourceProfile.OfficialOnly)]
        [TestCase(18, CatalogSourceProfile.OfficialPlusCommunityDragon)]
        public void Shape_OfficialScopeIncludesSetNumberAndProfile(int number, CatalogSourceProfile profile)
        {
            var c = ShapeTransport.Official(); var m = c.Manifest;
            var manifest = new PatchManifest(m.SchemaVersion, new CatalogIdentity(new PatchKey(Patch.SetId, number, Patch.PatchId, Patch.SourceGameVersion), profile),
                m.SetName, m.CreatedUtc, m.ContentHash, m.Sources, m.PolicyReviewDate, m.PolicyUrls, 0, 0, "", m.CommunityDragonVersion);
            var official = new StaticCatalog(manifest, c.Units, c.Traits, c.Items, c.Augments, c.FieldOrigins);
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(GlobalDocument("ko_kr").ToString(), GlobalDocument("en_us").ToString(), Patch, official);
            Assert.That(parsed.Issues.Any(x => x.Code == "cdragon_invalid_official_scope" && x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void Shape_BlankTraitCannotResolveThroughAnAbsentLocalizedName()
        {
            var ko = Document("ko_kr"); var en = Document("en_us");
            ((JObject)Selected(en)["traits"][0]).Remove("name");
            Selected(en)["champions"][0]["traits"] = new JArray("");
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch);
            Assert.That(parsed.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void Shape_OversizedSetNumberReturnsABoundedDiagnostic()
        {
            var ko = Document("ko_kr"); ko["setData"][0]["number"] = long.MaxValue;
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), Document("en_us").ToString(), Patch);
            Assert.That(parsed.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void Shape_EmptySupportedArraysCannotReturnASilentSuccess()
        {
            const string empty = "{\"setData\":[{\"number\":18,\"champions\":[],\"traits\":[],\"items\":[],\"augments\":[]}]}";
            var parsed = new CommunityDragonAdapter().ParseWithDiagnostics(empty, empty, Patch);
            Assert.That(parsed.Issues.Any(x => x.Code == "cdragon_no_supported_records" && x.Severity == ValidationSeverity.Error), Is.True);
        }
    }
}
