using System;
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
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class FieldOriginIngestionTests
    {
        private static readonly PatchKey Patch = new PatchKey("TFTSet18", 18, "18.2", "16.18.1");
        private sealed class NoNetwork : IHttpTransport
        {
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token) =>
                throw new InvalidOperationException("No real network in lineage tests.");
        }
        internal static StaticCatalog Official(int? cost = 1, bool includeEffect = true, int? tier = null,
            string effectKey = "zero/~", bool englishPrimary = true)
        {
            DataDragonPayloadPair Pair(string kind, string ko, string en)
            {
                HttpResponseData Response(string locale, string body) => new HttpResponseData(
                    "https://ddragon.leagueoflegends.com/" + locale + "/" + kind, 200, Encoding.UTF8.GetBytes(body), "", "application/json", DateTime.UnixEpoch);
                return new DataDragonPayloadPair(Response("ko", ko), Response("en", en));
            }
            var enUnit = new JObject { ["id"] = "TEST18_A", ["name"] = "A",
                ["stats"] = new JObject { [effectKey] = 0 } };
            if (cost.HasValue) enUnit["cost"] = cost.Value;
            if (!includeEffect) enUnit.Remove("stats");
            if (tier.HasValue) enUnit["tier"] = tier.Value;
            var full = new JObject { ["data"] = new JObject { ["TEST18_A"] = enUnit } }.ToString();
            var unit = Pair("unit", englishPrimary ? "{\"data\":{\"TEST18_A\":{\"name\":\"가\"}}}" : full,
                englishPrimary ? full : "{\"data\":{}}");
            var empty = Pair("empty", "{\"data\":{}}", "{\"data\":{}}");
            return new DataDragonAdapter(new NoNetwork()).Parse(new DataDragonSnapshot(unit, empty, empty, empty), Patch);
        }
        internal static StaticCatalog Supplemental(string id = "TEST18_A", string effectKey = "extra", bool englishPrimary = true)
        {
            const string ko = "{\"setData\":[{\"number\":18,\"champions\":[{\"apiName\":\"TEST18_A\",\"name\":\"가\",\"desc\":\"보완 설명\"}]}]}";
            const string en = "{\"setData\":[{\"number\":18,\"champions\":[{\"apiName\":\"TEST18_A\",\"name\":\"A\",\"cost\":2,\"desc\":\"Supplement\",\"effects\":{\"extra\":0}}]}]}";
            var full = en.Replace("TEST18_A", id).Replace("extra", effectKey);
            return new CommunityDragonAdapter().Parse(englishPrimary ? ko.Replace("TEST18_A", id) : full,
                englishPrimary ? full : "{\"setData\":[{\"number\":18}]}", Patch);
        }

        [Test]
        public void FieldOriginIngestion_ActualAdaptersAndMergeChooseEachLocaleAndZeroSource()
        {
            var merged = new CatalogMerger().Merge(Official(), Supplemental()).Catalog;
            var saved = CanonicalCatalogSerializer.Deserialize(CanonicalCatalogSerializer.Serialize(merged));
            FieldOrigin At(string pointer) => saved.FieldOrigins.Single(x => x.RecordType == "unit" && x.StableId == "TEST18_A" && x.Pointer == pointer);
            Assert.That(At("/cost").Sources.Single().IsSupplemental, Is.False);
            Assert.That(At("/name/koKr").Sources.Single().Url, Does.Contain("/ko/"));
            Assert.That(At("/name/enUs").Sources.Single().Url, Does.Contain("/en/"));
            Assert.That(At("/description/koKr").Sources.Single().Url, Does.Contain("ko_kr"));
            Assert.That(At("/effects/extra").Sources.Single().IsSupplemental, Is.True);
            Assert.That(At("/effects/zero~1~0").Sources.Single().IsSupplemental, Is.False);
            Assert.That(new CatalogValidator().Validate(saved).CanPublish, Is.True);
        }

        [TestCase(null, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        public void FieldOriginIngestion_CostTokenPresenceIsDistinctFromDefault(int? cost, bool hasOrigin)
        {
            var saved = CanonicalCatalogSerializer.Deserialize(CanonicalCatalogSerializer.Serialize(Official(cost)));
            Assert.That(saved.FieldOrigins.Any(x => x.Pointer == "/cost"), Is.EqualTo(hasOrigin));
            Assert.That(new CatalogValidator().Validate(saved).CanPublish, Is.EqualTo(cost == 1));
            if (cost == 1)
            {
                var stripped = new StaticCatalog(saved.Manifest, saved.Units, null, null, null,
                    saved.FieldOrigins.Where(x => x.Pointer != "/cost"));
                Assert.That(new CatalogValidator().Validate(stripped).CanPublish, Is.False);
            }
        }

        [Test]
        public void FieldOriginIngestion_AbsentOptionalFieldAndEffectAreNotInvented()
        {
            var c = Official(includeEffect: false);
            Assert.That(c.FieldOrigins.Any(x => x.Pointer.StartsWith("/effects/") || x.Pointer == "/role" || x.Pointer == "/traitIds"), Is.False);
            Assert.That(new CatalogValidator().Validate(c).CanPublish, Is.True);
            var zeroWithAlias = Official(0, tier: 9);
            Assert.That(zeroWithAlias.Units.Single().Cost, Is.Zero);
            Assert.That(new CatalogValidator().Validate(zeroWithAlias).CanPublish, Is.False);
            Assert.That(Official(null, tier: 3).Units.Single().Cost, Is.EqualTo(3));
        }

        [Test]
        public void FieldOriginIngestion_SupplementOnlyRecordAndMissingSelectedOrigin()
        {
            var merged = new CatalogMerger().Merge(Official(), Supplemental("TEST18_B")).Catalog;
            Assert.That(merged.FieldOrigins.Where(x => x.StableId == "TEST18_B").All(x => x.Sources.All(s => s.IsSupplemental)), Is.True);
            Assert.That(new CatalogValidator().Validate(merged).CanPublish, Is.True);
            var c = Official();
            var missing = new StaticCatalog(c.Manifest, c.Units, null, null, null, c.FieldOrigins.Where(x => x.Pointer != "/cost"));
            merged = new CatalogMerger().Merge(missing, Supplemental()).Catalog;
            Assert.That(merged.FieldOrigins.Any(x => x.Pointer == "/cost"), Is.False, "Do not borrow a nonselected supplemental origin.");
            Assert.That(new CatalogValidator().Validate(merged).CanPublish, Is.False);
        }

        [TestCase("koKr", true)]
        [TestCase("enUs", true)]
        [TestCase("koKr", false)]
        [TestCase("enUs", false)]
        public void FieldOriginIngestion_EffectKeysNamedLikeLocalesKeepPrimaryDocument(string key, bool englishPrimary)
        {
            var catalog = Official(effectKey: key, englishPrimary: englishPrimary);
            var source = catalog.FieldOrigins.Single(x => x.Pointer == "/effects/" + key).Sources.Single();
            Assert.That(source.Url, Does.Contain(englishPrimary ? "/en/" : "/ko/"));
            Assert.That(new CatalogValidator().Validate(catalog).CanPublish, Is.True);
            catalog = Supplemental(effectKey: key, englishPrimary: englishPrimary);
            source = catalog.FieldOrigins.Single(x => x.Pointer == "/effects/" + key).Sources.Single();
            Assert.That(source.Url, Does.Contain(englishPrimary ? "en_us" : "ko_kr"));
            Assert.That(new CatalogValidator().Validate(catalog).CanPublish, Is.True);
        }
    }
}
