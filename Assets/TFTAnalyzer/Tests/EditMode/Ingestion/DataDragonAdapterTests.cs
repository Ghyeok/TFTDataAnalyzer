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
using UnityEngine;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class DataDragonAdapterTests
    {
        private static readonly PatchKey Set18 =
            new PatchKey("TFTSet18", 18, "18.2", "16.18.1");

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
    }
}
