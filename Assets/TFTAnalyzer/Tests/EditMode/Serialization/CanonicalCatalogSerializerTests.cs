using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Serialization
{
    public sealed class CanonicalCatalogSerializerTests
    {
        [Test]
        public void Serialize_SortsRecordsReferencesAndEffectKeys()
        {
            var first = CatalogWithUnitOrder("TEST18_B", "TEST18_A");
            var second = CatalogWithUnitOrder("TEST18_A", "TEST18_B");

            Assert.That(
                CanonicalCatalogSerializer.Serialize(first),
                Is.EqualTo(CanonicalCatalogSerializer.Serialize(second)));
        }

        [Test]
        public void Deserialize_RoundTripsCanonicalCatalog()
        {
            var original = CatalogWithUnitOrder("TEST18_B", "TEST18_A");

            var roundTrip = CanonicalCatalogSerializer.Deserialize(
                CanonicalCatalogSerializer.Serialize(original));

            Assert.That(roundTrip.Manifest.Patch.PatchId, Is.EqualTo("18.2"));
            Assert.That(roundTrip.Units.Select(unit => unit.StableId),
                Is.EqualTo(new[] { "TEST18_A", "TEST18_B" }));
            Assert.That(roundTrip.Units[0].Effects["AbilityPower"], Is.EqualTo(15d));
            Assert.That(roundTrip.Units[0].Name.KoKr, Is.EqualTo("시험 TEST18_A"));
        }

        [Test]
        public void Sha256Hex_ReturnsKnownVector()
        {
            Assert.That(
                ContentHasher.Sha256Hex("abc"),
                Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        }

        private static StaticCatalog CatalogWithUnitOrder(string firstId, string secondId)
        {
            return new StaticCatalog(
                Manifest(),
                new[] { Unit(firstId), Unit(secondId) },
                new[]
                {
                    new TraitRecord(
                        "TEST18_Trait_B",
                        new LocalizedText("특성 B", "Trait B"),
                        new LocalizedText("설명 B", "Description B"),
                        new Dictionary<string, double> { ["Z"] = 2, ["A"] = 1 },
                        "trait-b.png",
                        null),
                    new TraitRecord(
                        "TEST18_Trait_A",
                        new LocalizedText("특성 A", "Trait A"),
                        LocalizedText.Empty,
                        null,
                        "trait-a.png",
                        null)
                },
                null,
                null);
        }

        private static UnitRecord Unit(string id)
        {
            return new UnitRecord(
                id,
                new LocalizedText($"시험 {id}", $"Test {id}"),
                1,
                new[] { "TEST18_Trait_B", "TEST18_Trait_A" },
                $"{id}.png",
                null,
                "Mage",
                new LocalizedText("스킬", "Ability"),
                new Dictionary<string, double>
                {
                    ["Mana"] = 30,
                    ["AbilityPower"] = 15
                });
        }

        private static PatchManifest Manifest()
        {
            return new PatchManifest(
                1,
                new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                "Test Set",
                DateTime.UnixEpoch,
                "",
                new[]
                {
                    new SourceStamp("Riot", "https://example.test/en", DateTime.UnixEpoch, "e2", "b"),
                    new SourceStamp("Riot", "https://example.test/ko", DateTime.UnixEpoch, "e1", "a")
                },
                new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
                new[] { "https://example.test/policy-b", "https://example.test/policy-a" },
                0,
                0,
                "",
                "");
        }
    }
}
