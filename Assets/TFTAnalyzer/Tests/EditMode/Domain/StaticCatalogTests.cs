using System;
using System.Collections.Generic;
using NUnit.Framework;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Tests.Domain
{
    public sealed class StaticCatalogTests
    {
        [Test]
        public void LocalizedText_UsesEnglishWhenKoreanIsBlank()
        {
            var text = new LocalizedText("", "Test Unit");

            Assert.That(text.DisplayValue, Is.EqualTo("Test Unit"));
            Assert.That(text.UsedEnglishFallback, Is.True);
        }

        [Test]
        public void LocalizedText_PrefersKoreanWhenBothLanguagesExist()
        {
            var text = new LocalizedText("시험 기물", "Test Unit");

            Assert.That(text.DisplayValue, Is.EqualTo("시험 기물"));
            Assert.That(text.UsedEnglishFallback, Is.False);
        }

        [TestCase("", 18, "18.2", "16.18.1")]
        [TestCase("TFTSet18", 18, "", "16.18.1")]
        [TestCase("TFTSet18", 18, "18.2", "")]
        public void PatchKey_RejectsInvalidStableParts(
            string setId,
            int setNumber,
            string patchId,
            string sourceGameVersion)
        {
            Assert.Throws<ArgumentException>(
                () => new PatchKey(setId, setNumber, patchId, sourceGameVersion));
        }

        [Test]
        public void PatchKey_RejectsNonPositiveSetNumber()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PatchKey("TFTSet18", 0, "18.2", "16.18.1"));
        }

        [Test]
        public void UnitRecord_NormalizesNullCollectionsAndRejectsBlankId()
        {
            Assert.Throws<ArgumentException>(
                () => new UnitRecord("", new LocalizedText("시험", "Test"), 1, null, "", null));

            var unit = new UnitRecord(
                "TEST18_Unit_A",
                new LocalizedText("시험", "Test"),
                1,
                null,
                "",
                null);

            Assert.That(unit.TraitIds, Is.Empty);
            Assert.That(unit.Sources, Is.Empty);
            Assert.That(unit.Effects, Is.Empty);
        }

        [Test]
        public void StaticCatalog_CopiesInputCollections()
        {
            var units = new List<UnitRecord>();
            var catalog = new StaticCatalog(TestManifest(), units, null, null, null);

            units.Add(new UnitRecord(
                "TEST18_Unit_A",
                new LocalizedText("시험", "Test"),
                1,
                null,
                "",
                null));

            Assert.That(catalog.Units, Is.Empty);
            Assert.That(catalog.Traits, Is.Empty);
            Assert.That(catalog.Items, Is.Empty);
            Assert.That(catalog.Augments, Is.Empty);
        }

        private static PatchManifest TestManifest()
        {
            return new PatchManifest(
                1,
                new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                "Test Set",
                DateTime.UnixEpoch,
                "",
                null,
                new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
                null,
                0,
                0,
                "",
                "");
        }
    }
}
