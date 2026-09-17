using System;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Validation
{
    public sealed class CatalogValidatorTests
    {
        [Test]
        public void Validate_BlocksDuplicateIdsAndMissingTraits()
        {
            var unit = Unit("TEST18_Unit_A", new[] { "TEST18_Trait_Missing" }, 1);
            var catalog = new StaticCatalog(Manifest(), new[] { unit, unit }, null, null, null);

            var report = new CatalogValidator().Validate(catalog);

            Assert.That(report.CanPublish, Is.False);
            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Is.EquivalentTo(new[] { "duplicate_unit_id", "missing_trait_reference" }));
        }

        [Test]
        public void Validate_ReportsInvalidNamesCostsItemReferencesAndIcons()
        {
            var unit = new UnitRecord(
                "TEST18_Unit_A",
                LocalizedText.Empty,
                10,
                null,
                "not-an-icon-location",
                null);
            var item = new ItemRecord(
                "TEST18_Item_A",
                new LocalizedText("시험", "Test"),
                LocalizedText.Empty,
                new[] { "TEST18_Item_Missing" },
                null,
                "https://example.test/item.png",
                null);

            var report = new CatalogValidator().Validate(
                new StaticCatalog(Manifest(), new[] { unit }, null, new[] { item }, null));

            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain("blank_unit_name"));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain("unit_cost_out_of_range"));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain("missing_item_reference"));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain("invalid_icon_metadata"));
        }

        private static UnitRecord Unit(string id, string[] traitIds, int cost)
        {
            return new UnitRecord(
                id,
                new LocalizedText("시험 기물", "Test Unit"),
                cost,
                traitIds,
                "https://example.test/unit.png",
                null);
        }

        private static PatchManifest Manifest()
        {
            return new PatchManifest(
                1,
                new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                "Test Set",
                DateTime.UnixEpoch,
                "",
                null,
                DateTime.UnixEpoch,
                new[] { "https://example.test/policy" },
                0,
                0,
                "",
                "");
        }
    }
}
