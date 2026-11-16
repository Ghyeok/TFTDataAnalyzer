using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Editor.Diagnostics;
using static TFTAnalyzer.Tests.Diagnostics.MatchShapeTestSupport;

namespace TFTAnalyzer.Tests.Diagnostics
{
    /// <summary>TODO 3: token reader and aggregate counters over synthetic responses.</summary>
    public sealed class MatchShapeReaderTests
    {
        private static readonly string[] Fields =
        {
            "metadata.match_id", "metadata.data_version", "info.game_version", "info.tft_set_number", "info.queue_id",
            "info.participants", "participants[].placement", "participants[].last_round", "units[].character_id",
            "units[].tier", "units[].itemNames", "traits[].name", "traits[].tier_current", "participants[].augments"
        };

        private static MatchShapeSample Read(JObject json) => Read(json.ToString(Formatting.None));

        private static MatchShapeSample Read(string json)
        {
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return new MatchShapeReader(Input()).Read(input, CancellationToken.None);
        }

        private static JObject Report(params JObject[] matches)
        {
            var accumulator = new MatchShapeAccumulator(Input());
            foreach (var match in matches) accumulator.Add(Read(match));
            return accumulator.BuildReport(new MatchShapeRunSummary("completed", false, "none", "none", 1, matches.Length, 21, true));
        }

        private static JObject Queue(JObject report, string queue = "1100")
            => (JObject)report["queues"].Single(entry => (string)entry["queue"] == queue);

        private static JObject Signature(JObject block) => (JObject)((JArray)block["signatures"]).Single();

        [Test]
        public void NormalV2MatchRecordsExpectedShapes()
        {
            var report = Report(Match());
            var queue = Queue(report);
            Assert.That((bool)queue["isTarget"], Is.True);
            Assert.That((int)queue["matches"], Is.EqualTo(1));
            Assert.That((int)queue["gameVersion"]["types"]["string"], Is.EqualTo(1));
            Assert.That((string)Signature((JObject)queue["gameVersion"])["signature"], Is.EqualTo("{x:6-8}_99.99.999.999_({x:9-16})"));
            Assert.That((string)Signature((JObject)queue["gameVersion"])["type"], Is.EqualTo("string"));
            Assert.That((bool)Signature((JObject)queue["gameVersion"])["truncated"], Is.False);
            Assert.That((int)queue["gameVersion"]["versionPrefix"]["present"], Is.EqualTo(1));
            Assert.That((string)queue["gameVersion"]["leadingGroups"][0]["value"], Is.EqualTo("16.18"));
            Assert.That((string)Signature((JObject)queue["dataVersion"])["signature"], Is.EqualTo("9"));
            Assert.That((string)queue["dataVersion"]["leadingGroups"][0]["value"], Is.EqualTo("2"));
            Assert.That((int)queue["gameVersionRegex"]["match"], Is.EqualTo(1));
            Assert.That((int)queue["release"]["match"], Is.EqualTo(1));
            Assert.That((int)queue["setNumber"]["match"], Is.EqualTo(1));
            Assert.That((int)queue["augmentState"]["allProvided"], Is.EqualTo(1));
            foreach (var kind in new[] { "unit", "item", "trait", "augment" })
                Assert.That((int)queue["unknownStaticIds"][kind]["occurrences"], Is.Zero, kind);
            var row = (JObject)((JArray)queue["requiredFieldRows"]).Single();
            Assert.That((int)row["count"], Is.EqualTo(1));
            Assert.That(((JObject)row["fields"]).Properties().Select(p => p.Name), Is.EqualTo(Fields));
            var expected = new[] { "string", "string", "string", "integer", "integer", "array:8", "integer", "integer",
                "string", "integer", "array", "string", "integer", "array" };
            Assert.That(Fields.Select(field => (string)row["fields"][field]), Is.EqualTo(expected));
        }

        [Test]
        public void MissingAugmentsAreCountedAsNoneOrMixedWithoutStoppingTheReader()
        {
            var none = Match(1);
            foreach (var participant in none["info"]["participants"]) ((JObject)participant).Remove("augments");
            var mixed = Match(2);
            ((JObject)mixed["info"]["participants"][0]).Remove("augments");
            mixed["info"]["participants"][1]["augments"] = null;
            var queue = Queue(Report(Match(0), none, mixed));
            Assert.That((int)queue["augmentState"]["allProvided"], Is.EqualTo(1));
            Assert.That((int)queue["augmentState"]["noneProvided"], Is.EqualTo(1));
            Assert.That((int)queue["augmentState"]["mixed"], Is.EqualTo(1));
            var rows = ((JArray)queue["requiredFieldRows"]).Select(row => (string)row["fields"]["participants[].augments"]).OrderBy(v => v, StringComparer.Ordinal);
            Assert.That(rows, Is.EqualTo(new[] { "array", "array+missing+null", "missing" }));
        }

        [Test]
        public void UnknownDataVersionIntegerAndNullVersionsKeepTypeFirst()
        {
            var unknown = Match(0, dataVersion: "SENTINEL-DV-77");
            var integer = Match(1);
            integer["info"]["game_version"] = 1618;
            integer["metadata"]["data_version"] = 2;
            var nullVersion = Match(2);
            nullVersion["info"]["game_version"] = null;
            nullVersion["metadata"]["data_version"] = null;
            var missing = Match(3);
            ((JObject)missing["info"]).Remove("game_version");
            ((JObject)missing["metadata"]).Remove("data_version");
            var queue = Queue(Report(unknown, integer, nullVersion, missing));
            var game = (JObject)queue["gameVersion"];
            Assert.That((int)game["types"]["string"], Is.EqualTo(1));
            Assert.That((int)game["types"]["integer"], Is.EqualTo(1));
            Assert.That((int)game["types"]["null"], Is.EqualTo(1));
            Assert.That((int)game["types"]["missing"], Is.EqualTo(1));
            var signatures = ((JArray)queue["dataVersion"]["signatures"]).ToDictionary(s => (string)s["type"] + "|" + (string)s["signature"], s => (int)s["count"]);
            Assert.That(signatures, Is.EquivalentTo(new Dictionary<string, int>
            {
                ["string|{x:9-16}"] = 1, ["integer|9"] = 1, ["null|"] = 1, ["missing|"] = 1
            }));
            Assert.That((int)queue["gameVersionRegex"]["noMatch"], Is.EqualTo(3));
            Assert.That((int)queue["release"]["unextractable"], Is.EqualTo(3));
            Assert.That((int)queue["release"]["match"], Is.EqualTo(1));
        }

        [Test]
        public void AbnormalGameVersionsAreClassifiedAgainstTheCatalogRelease()
        {
            var legacy = Match(0, gameVersion: "Version 14.1.555.1234 (…) [PUBLIC] <Releases/14.1>");
            var injected = Match(1, gameVersion: "Version 16.18.123.456\r\nInjected");
            var other = Match(2);
            other["info"]["game_version"] = new JArray("16.18.1");
            var queue = Queue(Report(legacy, injected, other));
            Assert.That((int)queue["release"]["mismatch"], Is.EqualTo(1));
            Assert.That((int)queue["release"]["unextractable"], Is.EqualTo(2));
            Assert.That((int)queue["gameVersionRegex"]["match"], Is.EqualTo(1));
            Assert.That((int)queue["gameVersion"]["types"]["other"], Is.EqualTo(1));
            Assert.That((int)queue["gameVersion"]["versionPrefix"]["present"], Is.EqualTo(2));
            var groups = ((JArray)queue["gameVersion"]["leadingGroups"]).ToDictionary(g => (string)g["value"], g => (int)g["count"]);
            Assert.That(groups, Is.EquivalentTo(new Dictionary<string, int> { ["14.1"] = 1, ["16.18"] = 1, ["none"] = 1 }));
        }

        [Test]
        public void UnknownStaticIdsAndSetMismatchAreCountedWithoutValues()
        {
            var json = Match();
            json["info"]["tft_set_number"] = 17;
            var participant = json["info"]["participants"][0];
            participant["units"][0]["character_id"] = "SENTINEL_UNKNOWN_UNIT";
            participant["units"][0]["itemNames"] = new JArray("SENTINEL_ITEM", "TFT_Item_A", "SENTINEL_ITEM");
            participant["traits"] = new JArray(new JObject { ["name"] = "SENTINEL_TRAIT", ["tier_current"] = 1 });
            participant["augments"] = new JArray("SENTINEL_AUGMENT");
            var queue = Queue(Report(json, Match(1)));
            Assert.That((int)queue["unknownStaticIds"]["unit"]["occurrences"], Is.EqualTo(1));
            Assert.That((int)queue["unknownStaticIds"]["item"]["occurrences"], Is.EqualTo(2));
            Assert.That((int)queue["unknownStaticIds"]["trait"]["occurrences"], Is.EqualTo(1));
            Assert.That((int)queue["unknownStaticIds"]["augment"]["occurrences"], Is.EqualTo(1));
            Assert.That((int)queue["unknownStaticIds"]["unit"]["matches"], Is.EqualTo(1));
            Assert.That((int)queue["setNumber"]["mismatch"], Is.EqualTo(1));
            Assert.That((int)queue["setNumber"]["match"], Is.EqualTo(1));
            Assert.That(Report(json).ToString(), Does.Not.Contain("SENTINEL"));
        }

        [Test]
        public void OtherQueuesAreReportedSeparatelyAndExcludedFromDiversity()
        {
            var report = Report(Match(0), Match(1, queue: 1090), Match(2, queue: 1090));
            Assert.That((int)Queue(report)["matches"], Is.EqualTo(1));
            Assert.That((int)Queue(report, "1090")["matches"], Is.EqualTo(2));
            Assert.That((bool)Queue(report, "1090")["isTarget"], Is.False);
            Assert.That((int)report["compositionDiversity"]["totalBoards"], Is.EqualTo(8));
            var stringQueue = Match(3);
            stringQueue["info"]["queue_id"] = "1100";
            Assert.That((int)Queue(Report(stringQueue), "type:string")["matches"], Is.EqualTo(1));
        }

        [Test]
        public void MalformedOrDuplicateKeyPayloadsAreUnreadable()
        {
            foreach (var payload in new[]
            {
                "{\"metadata\":{},\"metadata\":{}}", "{\"info\":{'queue_id':1}}", "{\"info\":{\"queue_id\":0x10}}",
                "[1,2]", "{\"info\":{}} {}", "{\"info\":{\"participants\":[1,]}}", "{\"info\""
            })
                Assert.Throws<InvalidDataException>(() => Read(payload), payload);
        }

        [Test]
        public void ReaderRejectsPayloadsAboveTheSharedEightMebibyteBound()
        {
            var json = "{\"padding\":\"" + new string('x', 8 * 1024 * 1024) + "\"}";
            Assert.Throws<InvalidDataException>(() => Read(json));
        }

        [Test]
        public void UniqueCompositionsEqualCompositionFingerprintBuilderUniqueCount()
        {
            var units = new[] { "TFT18_UnitA", "TFT18_UnitB", "TFT18_UnitC", "SENTINEL_UNIT" };
            var random = new System.Random(18);
            var matches = new List<JObject>();
            var expected = new List<string>();
            for (var m = 0; m < 6; m++)
            {
                var json = Match(m);
                for (var p = 0; p < 8; p++)
                {
                    var participant = (JObject)json["info"]["participants"][p];
                    var boardUnits = Enumerable.Range(0, 1 + random.Next(3)).Select(_ => units[random.Next(units.Length)]).ToArray();
                    var boardTraits = Enumerable.Range(0, random.Next(4)).Select(_ => (Name: "TFT18_Trait" + (char)('A' + random.Next(3)), Tier: random.Next(3))).ToArray();
                    participant["units"] = new JArray(boardUnits.Select(id => new JObject { ["character_id"] = id, ["tier"] = 1 + random.Next(3), ["itemNames"] = new JArray("TFT_Item_" + (char)('A' + random.Next(2))) }));
                    participant["traits"] = new JArray(boardTraits.Select(t => new JObject { ["name"] = t.Name, ["tier_current"] = t.Tier }));
                    if (p == 7 && m % 2 == 0) participant["augments"] = null;
                    expected.Add(CompositionFingerprintBuilder.Build(new AnonymousParticipant(p + 1, 30,
                        boardUnits.Select(id => new AnonymousUnit(id, 1, Array.Empty<string>())),
                        boardTraits.Select(t => new AnonymousTrait(t.Name, t.Tier)), Array.Empty<string>())).Value);
                }
                matches.Add(json);
            }
            var excluded = Match(6);
            ((JObject)excluded["info"]["participants"][0]).Remove("units");
            excluded["info"]["participants"][1]["traits"][0]["tier_current"] = "1";
            matches.Add(excluded);
            foreach (var participant in excluded["info"]["participants"].Skip(2))
                expected.Add(CompositionFingerprintBuilder.Build(new AnonymousParticipant(1, 30,
                    participant["units"].Select(unit => new AnonymousUnit((string)unit["character_id"], 1, Array.Empty<string>())),
                    participant["traits"].Select(trait => new AnonymousTrait((string)trait["name"], (int)trait["tier_current"])),
                    Array.Empty<string>())).Value);

            var samples = matches.Select(match => Read(match)).ToArray();
            Assert.That(samples.SelectMany(sample => sample.CompositionKeys).OrderBy(v => v, StringComparer.Ordinal),
                Is.EqualTo(expected.OrderBy(v => v, StringComparer.Ordinal)));

            var diversity = Report(matches.ToArray())["compositionDiversity"];
            var frequencies = expected.GroupBy(v => v).Select(g => g.Count()).OrderByDescending(c => c).ToArray();
            Assert.That((int)diversity["totalBoards"], Is.EqualTo(56));
            Assert.That((int)diversity["excludedBoards"], Is.EqualTo(2));
            Assert.That((int)diversity["uniqueCompositions"], Is.EqualTo(expected.Distinct().Count()));
            Assert.That((int)diversity["singletonCompositions"], Is.EqualTo(frequencies.Count(c => c == 1)));
            Assert.That(diversity["top5Frequencies"].Select(v => (int)v), Is.EqualTo(frequencies.Take(5)));
            var fof = diversity["frequencyOfFrequencies"].ToDictionary(e => (int)e["frequency"], e => (int)e["compositions"]);
            Assert.That(fof, Is.EquivalentTo(frequencies.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count())));
            Assert.That(diversity.ToString(), Does.Not.Contain("TFT18_"));
        }

        [Test]
        public void ReaderHonoursCancellation()
        {
            using (var cancelled = new CancellationTokenSource())
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(Match().ToString(Formatting.None))))
            {
                cancelled.Cancel();
                Assert.Catch<OperationCanceledException>(() => new MatchShapeReader(Input()).Read(input, cancelled.Token));
            }
        }
    }
}
