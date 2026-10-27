using System;
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

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class StructuredStaticIngestionTests
    {
        internal static JObject Document(string locale) => JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
            "TFTAnalyzer/Tests/Fixtures/Static/structured-" + locale + ".json")));
        internal static JObject Selected(JObject d) => d["setData"].OfType<JObject>().Single(x => x.Value<int>("number") == 18);
        private static readonly PatchKey Patch = CommunityDragonShapeTests.Patch;
        internal static CommunityDragonParseResult Parse(JObject ko, JObject en) => new CommunityDragonAdapter().ParseWithDiagnostics(ko.ToString(), en.ToString(), Patch);

        [Test]
        public void StructuredStatic_CommunitySuppliesSourceStructuresAndKeepsUnknownMeaningInert()
        {
            var result = Parse(Document("ko_kr"), Document("en_us")); var c = result.Catalog;
            Assert.That(c.Manifest.SchemaVersion, Is.EqualTo(4));
            Assert.That(c.Units.Single().BaseStats["hp"], Is.EqualTo(100));
            Assert.That(c.Units.Single().BaseStats["zero/~"], Is.Zero); Assert.That(c.Units.Single().BaseStats.ContainsKey("mana"), Is.True);
            Assert.That(c.Units.Single().AbilityVariables.Single().Values, Is.EqualTo(new double?[] { 0, 10, null, 30 }));
            Assert.That(c.Traits.Single().Breakpoints.Single().MinUnits, Is.Null);
            Assert.That(c.Traits.Single().Breakpoints.Single().Effects["power"], Is.Null);
            Assert.That(c.Items.Single().Unique, Is.False);
            Assert.That(c.Items.Single().Extensions.Any(x => x.Pointer == "/incompatibleTraits" && x.CanonicalJson.Contains("UNRESOLVED_SOURCE_TOKEN")), Is.True);
            Assert.That(result.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.False);
            Assert.That(result.Issues.Any(x => x.Code.StartsWith("cdragon_unsupported_", StringComparison.Ordinal)), Is.False);
            Assert.That(new CatalogValidator().Validate(c).CanPublish, Is.True);
        }

        [Test]
        public void StructuredStatic_UnknownChildrenKeepBothLocalesWithoutDuplicatingKnownFields()
        {
            var c = Parse(Document("ko_kr"), Document("en_us")).Catalog; var u = c.Units.Single();
            Assert.That(u.Extensions.Count(x => x.Pointer == "/ability/other"), Is.EqualTo(2));
            Assert.That(u.Extensions.Any(x => x.Pointer == "/stats" || x.Pointer.StartsWith("/ability/variables", StringComparison.Ordinal)), Is.False);
            Assert.That(u.Extensions.Single(x => x.Pointer == "/unmodeled" && x.Locale == "enUs").CanonicalJson,
                Is.EqualTo("{\"$type\":\"inert text\",\"a\":1,\"z\":2}"));
            Assert.That(c.FieldOrigins.Single(x => x.RecordType == "unit" && x.Pointer == "/baseStats/mana").Sources.Single().Url, Does.Contain("en_us"));
            Assert.That(c.FieldOrigins.Single(x => x.RecordType == "trait" && x.Pointer == "/breakpoints/0/minUnits").Sources.Single().Url, Does.Contain("en_us"));
        }

        [TestCase("stat")]
        [TestCase("series")]
        [TestCase("duplicateVariable")]
        [TestCase("missingBound")]
        [TestCase("unique")]
        [TestCase("depth")]
        [TestCase("budget")]
        public void StructuredStatic_KnownDamageOrExtensionOverflowBlocksCommunityPublication(string change)
        {
            var ko = Document("ko_kr"); var en = Document("en_us"); var s = Selected(en); var u = s["champions"][0];
            if (change == "stat") u["stats"]["hp"] = "100";
            if (change == "series") u["ability"]["variables"][0]["value"][1] = true;
            if (change == "duplicateVariable") ((JArray)u["ability"]["variables"]).Add(u["ability"]["variables"][0].DeepClone());
            if (change == "missingBound") ((JObject)s["traits"][0]["effects"][0]).Remove("minUnits");
            if (change == "unique") s["items"][0]["unique"] = "false";
            if (change == "depth") { JToken value = 0; for (var i = 0; i < 20; i++) value = new JArray(value); u["unmodeled"] = value; }
            if (change == "budget") u["unmodeled"] = new string('x', 65537);
            var result = Parse(ko, en);
            Assert.That(result.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(() => new CommunityDragonAdapter().Parse(ko.ToString(), en.ToString(), Patch), Throws.InstanceOf<JsonException>());
        }

        [Test]
        public void StructuredStatic_DuplicateSourceJsonKeyIsAnError()
        {
            var en = Document("en_us").ToString(Formatting.None).Replace("\"hp\":100", "\"hp\":100,\"hp\":200");
            var result = new CommunityDragonAdapter().ParseWithDiagnostics(Document("ko_kr").ToString(), en, Patch);
            Assert.That(result.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
        }

        [TestCase(false, "null")]
        [TestCase(false, "string")]
        [TestCase(false, "number")]
        [TestCase(true, "null")]
        [TestCase(true, "string")]
        [TestCase(true, "number")]
        public void StructuredStatic_UnselectedLocaleMalformedTraitEffectsBlocksBothAdapters(bool official, string damage)
        {
            var ko = Document("ko_kr"); var en = Document("en_us");
            Selected(ko)["traits"][0]["effects"] = damage == "null" ? JValue.CreateNull() : damage == "string" ? new JValue("damaged") : new JValue(7);
            var issues = official ? new DataDragonAdapter(new CommunityDragonShapeTests.ShapeTransport()).ParseWithDiagnostics(OfficialSnapshot(ko,en),Patch).Issues : Parse(ko,en).Issues;
            Assert.That(issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StructuredStatic_DateLikeUnknownStringKeepsExactLiteralInBothAdapters(bool official)
        {
            const string literal = "2026-10-27T12:00:00.000Z";
            var ko = Document("ko_kr"); var en = Document("en_us"); Selected(en)["champions"][0]["unmodeledDate"] = literal;
            var c = official ? new DataDragonAdapter(new CommunityDragonShapeTests.ShapeTransport()).Parse(OfficialSnapshot(ko,en),Patch) : Parse(ko,en).Catalog;
            Assert.That(c.Units.Single().Extensions.Single(x => x.Locale == "enUs" && x.Pointer == "/unmodeledDate").CanonicalJson, Is.EqualTo(JsonConvert.SerializeObject(literal)));
        }

        internal static DataDragonSnapshot OfficialSnapshot(JObject ko, JObject en)
        {
            string Body(JObject document, string kind)
            {
                var data = new JObject();
                foreach (var r in Selected(document)[kind].OfType<JObject>())
                {
                    var row = (JObject)r.DeepClone(); row["id"] = row.Value<string>("apiName"); row.Remove("apiName");
                    data["/TFTSet18/" + row.Value<string>("id")] = row;
                }
                return new JObject { ["data"] = data }.ToString();
            }
            HttpResponseData Response(string locale, string kind, JObject d) => new HttpResponseData("https://example.test/" + locale + "/" + kind,
                200, Encoding.UTF8.GetBytes(Body(d, kind)), "", "application/json", DateTime.UnixEpoch);
            DataDragonPayloadPair Pair(string kind) => new DataDragonPayloadPair(Response("ko", kind, ko), Response("en", kind, en));
            return new DataDragonSnapshot(Pair("champions"), Pair("traits"), Pair("items"), Pair("augments"));
        }

        [Test]
        public void StructuredStatic_OfficialSuppliesTypedStatsAndPreservesOldFlatEffects()
        {
            var c = new DataDragonAdapter(new CommunityDragonShapeTests.ShapeTransport()).Parse(OfficialSnapshot(Document("ko_kr"), Document("en_us")), Patch);
            Assert.That(c.Manifest.SchemaVersion, Is.EqualTo(4));
            Assert.That(c.Units.Single().BaseStats["hp"], Is.EqualTo(100)); Assert.That(c.Units.Single().Effects["hp"], Is.EqualTo(100));
            Assert.That(c.Items.Single().Unique, Is.False);
            Assert.That(c.FieldOrigins.Single(x => x.RecordType == "unit" && x.Pointer == "/baseStats/mana").Sources.Single().IsSupplemental, Is.False);
            Assert.That(new CatalogValidator().Validate(c).CanPublish, Is.True);
        }

        [Test]
        public void StructuredStatic_OfficialMissingPrimaryFieldUsesActualSecondaryOrigin()
        {
            var ko = Document("ko_kr"); var en = Document("en_us"); ((JObject)Selected(en)["champions"][0]).Remove("stats");
            var c = new DataDragonAdapter(new CommunityDragonShapeTests.ShapeTransport()).Parse(OfficialSnapshot(ko, en), Patch);
            Assert.That(c.Units.Single().BaseStats["hp"], Is.EqualTo(100));
            Assert.That(c.FieldOrigins.Single(x => x.RecordType == "unit" && x.Pointer == "/baseStats/hp").Sources.Single().Url, Does.Contain("/ko/"));
        }

        internal sealed class StructuredTransport : IHttpTransport
        {
            internal JObject Ko = Document("ko_kr"), En = Document("en_us");
            internal int Requests, CommunityRequests;
            internal int IconRequests;
            internal bool AllowSupplement;
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); Requests++;
                if (url.EndsWith(".png"))
                {
                    IconRequests++;
                    return Task.FromResult(new HttpResponseData(url, 200, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII="), "", "image/png", DateTime.UnixEpoch));
                }
                if (url.Contains("/cdragon/tft/"))
                {
                    CommunityRequests++;
                    if (!AllowSupplement) throw new InvalidOperationException("Unexpected supplementary fetch.");
                    var document = (JObject)(url.Contains("ko_kr") ? Ko : En).DeepClone(); var set = Selected(document); var rows = new JArray();
                    foreach (var kindName in new[] { "items", "augments" })
                    {
                        foreach (var row in set[kindName].OfType<JObject>()) { row["isAugment"] = kindName == "augments"; rows.Add(row); }
                        set[kindName] = new JArray();
                    }
                    rows.Add(new JObject { ["apiName"] = "TFT_Outside_Item", ["isAugment"] = false, ["unmodeled"] = new string('x', 65537) });
                    document["items"] = rows;
                    return Task.FromResult(new HttpResponseData(url, 200, Encoding.UTF8.GetBytes(document.ToString()), "", "application/json", DateTime.UnixEpoch));
                }
                var kind = url.EndsWith("tft-champion.json") ? "champions" : url.EndsWith("tft-trait.json") ? "traits" :
                    url.EndsWith("tft-item.json") ? "items" : url.EndsWith("tft-augments.json") ? "augments" : throw new InvalidOperationException("Unexpected fake URL.");
                var locale = url.Contains("ko_KR") ? "ko" : "en";
                var body = OfficialSnapshot(Ko, En).AllResponses.Single(x => x.Url.Contains("/" + locale + "/") && x.Url.EndsWith("/" + kind)).Body;
                return Task.FromResult(new HttpResponseData(url, 200, body, "", "application/json", DateTime.UnixEpoch));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StructuredStatic_NetworkSourceReturnsOfficialErrorsAndSkipsSupplement(bool supplement)
        {
            var t = new StructuredTransport(); Selected(t.En)["champions"][0]["stats"]["hp"] = "damaged";
            var source = new NetworkStaticCatalogSource(new DataDragonAdapter(t), new CommunityDragonAdapter(), new CatalogMerger(), t);
            StaticSourceLoadResult result = null;
            Assert.DoesNotThrowAsync(async () => result = await source.LoadAsync(new StaticImportRequest(new CatalogIdentity(Patch,
                supplement ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly)), CancellationToken.None));
            Assert.That(result.Issues.Any(x => x.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(t.CommunityRequests, Is.Zero);
        }

        [Test]
        public async Task StructuredStatic_NetworkSourcePreservesOfficialLocaleWarning()
        {
            var t = new StructuredTransport(); Selected(t.Ko)["champions"][0]["stats"]["hp"] = 200;
            var source = new NetworkStaticCatalogSource(new DataDragonAdapter(t), new CommunityDragonAdapter(), new CatalogMerger(), t);
            var result = await source.LoadAsync(new StaticImportRequest(new CatalogIdentity(Patch, CatalogSourceProfile.OfficialOnly)), CancellationToken.None);
            Assert.That(result.Catalog.Units.Single().BaseStats["hp"], Is.EqualTo(100));
            Assert.That(result.Issues.Any(x => x.Code == "structured_locale_conflict"), Is.True);
        }
    }
}
