using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class CatalogMergerTests
    {
        [Test]
        public void Merge_PreservesOfficialValuesAndFillsMissingFields()
        {
            var official = Catalog(Unit(
                "Official Name",
                "",
                new Dictionary<string, double> { ["OfficialPower"] = 10 },
                false));
            var supplement = Catalog(Unit(
                "Supplement Name",
                "Mage",
                new Dictionary<string, double>
                {
                    ["OfficialPower"] = 99,
                    ["SupplementPower"] = 25
                },
                true),
                "16.18");

            var result = new CatalogMerger().Merge(official, supplement);
            var merged = result.Catalog.Units.Single();

            Assert.That(merged.Name.EnUs, Is.EqualTo("Official Name"));
            Assert.That(merged.Role, Is.EqualTo("Mage"));
            Assert.That(merged.Effects["OfficialPower"], Is.EqualTo(10));
            Assert.That(merged.Effects["SupplementPower"], Is.EqualTo(25));
            Assert.That(merged.Sources.Count, Is.EqualTo(2));
            Assert.That(result.Conflicts.Select(value => value.FieldName),
                Is.Ordered.Using<string>(StringComparer.Ordinal));
            Assert.That(result.Conflicts,
                Has.Some.Matches<MergeConflict>(value =>
                    value.FieldName == "role" && value.Resolution == MergeResolution.SupplementFill));
            Assert.That(result.Conflicts,
                Has.Some.Matches<MergeConflict>(value =>
                    value.FieldName == "name.enUs" && value.Resolution == MergeResolution.OfficialWins));
        }

        private static UnitRecord Unit(
            string englishName,
            string role,
            IReadOnlyDictionary<string, double> effects,
            bool supplemental)
        {
            return new UnitRecord(
                "TEST18_Unit_A",
                new LocalizedText("", englishName),
                4,
                null,
                "https://example.test/unit.png",
                new[]
                {
                    new SourceStamp(
                        supplemental ? "CommunityDragon" : "Riot Data Dragon",
                        supplemental
                            ? "https://raw.communitydragon.org/16.18/cdragon/tft/en_us.json"
                            : "https://ddragon.leagueoflegends.com/test.json",
                        DateTime.UnixEpoch,
                        "etag",
                        supplemental ? "supplement" : "official",
                        supplemental)
                },
                role,
                LocalizedText.Empty,
                effects);
        }

        private static StaticCatalog Catalog(UnitRecord unit, string communityVersion = "")
        {
            return new StaticCatalog(
                new PatchManifest(
                    1,
                    new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                    "Test Set",
                    DateTime.UnixEpoch,
                    "",
                    unit.Sources,
                    DateTime.UnixEpoch,
                    null,
                    0,
                    0,
                    "",
                    communityVersion),
                new[] { unit },
                null,
                null,
                null);
        }
    }
}
