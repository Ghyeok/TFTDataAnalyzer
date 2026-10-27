using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Diff;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class StructuredStaticMergeTests
    {
        internal static StaticCatalog Catalog(bool supplement, IReadOnlyDictionary<string, double?> stats = null,
            IEnumerable<StaticNumericSeries> series = null, IEnumerable<TraitBreakpoint> tiers = null, bool? unique = null,
            IEnumerable<StaticExtensionField> extensions = null, string sourceName = null, string sourceUrl = null)
        {
            var source = new SourceStamp(sourceName ?? (supplement ? "CommunityDragon" : "Riot"),
                sourceUrl ?? "https://example.test/" + (supplement ? "supplement" : "official"), DateTime.UnixEpoch, "", supplement ? "sup" : "official", supplement);
            var patch = new PatchKey("TFTSet18", 18, "18.2", "16.18.1");
            var manifest = new PatchManifest(4, new CatalogIdentity(patch, supplement ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly),
                "Test", DateTime.UnixEpoch, "", new[] { source }, DateTime.UnixEpoch, null, 0, 0, "", supplement ? "16.18" : "");
            var u = new UnitRecord("TEST18_Unit", new LocalizedText("", "Unit"), 1, null, "", new[] { source }, baseStats: stats, abilityVariables: series, extensions: extensions);
            var t = new TraitRecord("TEST18_Trait", new LocalizedText("", "Trait"), LocalizedText.Empty, null, "", new[] { source }, tiers);
            var i = new ItemRecord("TEST18_Item", new LocalizedText("", "Item"), LocalizedText.Empty, null, null, "", new[] { source }, unique);
            var origins = FieldOriginFields.Required(u, 4).Select(p => new FieldOrigin("unit", u.StableId, p, new[] { source }))
                .Concat(FieldOriginFields.Required(t, 4).Select(p => new FieldOrigin("trait", t.StableId, p, new[] { source })))
                .Concat(FieldOriginFields.Required(i, 4).Select(p => new FieldOrigin("item", i.StableId, p, new[] { source })));
            return new StaticCatalog(manifest, new[] { u }, new[] { t }, new[] { i }, null, origins);
        }

        private static IReadOnlyDictionary<string, double?> Stats(string state, double number) => state == "absent" ?
            new Dictionary<string, double?>() : new Dictionary<string, double?> { ["zero/~"] = state == "null" ? (double?)null : number };

        [TestCase("absent", "absent", "absent", "none", "none")]
        [TestCase("absent", "null", "null", "supplement", "SupplementFill")]
        [TestCase("absent", "value", "5", "supplement", "SupplementFill")]
        [TestCase("null", "absent", "null", "official", "none")]
        [TestCase("null", "null", "null", "official", "none")]
        [TestCase("null", "value", "5", "supplement", "SupplementFill")]
        [TestCase("value", "absent", "0", "official", "none")]
        [TestCase("value", "null", "0", "official", "none")]
        [TestCase("value", "value", "0", "official", "OfficialWins")]
        public void StructuredStatic_BaseStatsFollowApprovedNineStateTable(string left, string right, string expected, string owner, string resolution)
        {
            var result = new CatalogMerger().Merge(Catalog(false, Stats(left, 0)), Catalog(true, Stats(right, 5)));
            Assert.That(result.Catalog.Manifest.SchemaVersion, Is.EqualTo(4));
            var values = result.Catalog.Units.Single().BaseStats;
            Assert.That(values.ContainsKey("zero/~"), Is.EqualTo(expected != "absent"));
            if (expected != "absent")
            {
                Assert.That(values["zero/~"], Is.EqualTo(expected == "null" ? (double?)null : double.Parse(expected, CultureInfo.InvariantCulture)));
                Assert.That(result.Catalog.FieldOrigins.Single(x => x.Pointer == "/baseStats/zero~1~0").Sources.Single().IsSupplemental, Is.EqualTo(owner == "supplement"));
            }
            var decisions = result.Conflicts.Where(x => x.FieldName == "/baseStats/zero~1~0").ToArray();
            Assert.That(decisions.Length, Is.EqualTo(resolution == "none" ? 0 : 1));
            if (decisions.Length > 0) Assert.That(decisions[0].Resolution.ToString(), Is.EqualTo(resolution));
            Assert.That(new CatalogValidator().Validate(result.Catalog).CanPublish, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StructuredStatic_OrderedStructuresChooseWholeOfficialListOrFillEmpty(bool officialEmpty)
        {
            var oldSeries = new[] { new StaticNumericSeries("D", new double?[] { null, 0 }) };
            var newSeries = new[] { new StaticNumericSeries("D", new double?[] { 5, 6, 7 }) };
            var oldTiers = new[] { new TraitBreakpoint(null, 9, 1, new Dictionary<string, double?> { ["power"] = null }) };
            var newTiers = new[] { new TraitBreakpoint(2, 4, 2, new Dictionary<string, double?> { ["power"] = 5 }) };
            var c = new CatalogMerger().Merge(Catalog(false, series: officialEmpty ? null : oldSeries, tiers: officialEmpty ? null : oldTiers),
                Catalog(true, series: newSeries, tiers: newTiers)).Catalog;
            Assert.That(c.Units.Single().AbilityVariables.Single().Values, Is.EqualTo((officialEmpty ? newSeries : oldSeries)[0].Values));
            Assert.That(c.Traits.Single().Breakpoints.Single().MinUnits, Is.EqualTo(officialEmpty ? (int?)2 : null));
            Assert.That(c.FieldOrigins.Single(x => x.Pointer == "/breakpoints/0/minUnits").Sources.Single().IsSupplemental, Is.EqualTo(officialEmpty));
            Assert.That(c.FieldOrigins.Single(x => x.Pointer == "/abilityVariables/0/values/0").Sources.Single().IsSupplemental, Is.EqualTo(officialEmpty));
            Assert.That(new CatalogValidator().Validate(c).CanPublish, Is.True);
        }

        [TestCase(false, true, false, false)]
        [TestCase(null, false, false, true)]
        [TestCase(true, false, true, false)]
        public void StructuredStatic_UniqueFalseIsAValue(bool? official, bool? supplement, bool expected, bool owner)
        {
            var c = new CatalogMerger().Merge(Catalog(false, unique: official), Catalog(true, unique: supplement)).Catalog;
            Assert.That(c.Items.Single().Unique, Is.EqualTo(expected));
            Assert.That(c.FieldOrigins.Single(x => x.RecordType == "item" && x.Pointer == "/unique").Sources.Single().IsSupplemental, Is.EqualTo(owner));
        }

        [Test]
        public void StructuredStatic_ExtensionSortKeepsEachDocumentOriginAndNoRuleInterpretation()
        {
            var c = new CatalogMerger().Merge(Catalog(false, extensions: new[] { new StaticExtensionField("Riot", "enUs", "/raw", "{\"$type\":\"inert\"}") }),
                Catalog(true, extensions: new[] { new StaticExtensionField("CommunityDragon", "enUs", "/raw", "false") })).Catalog;
            Assert.That(c.Units.Single().Extensions.Select(x => x.SourceName), Is.EqualTo(new[] { "CommunityDragon", "Riot" }));
            Assert.That(c.FieldOrigins.Single(x => x.Pointer == "/extensions/0/canonicalJson").Sources.Single().Sha256, Is.EqualTo("sup"));
            Assert.That(c.FieldOrigins.Single(x => x.Pointer == "/extensions/1/canonicalJson").Sources.Single().Sha256, Is.EqualTo("official"));
            Assert.That(new CatalogValidator().Validate(c).CanPublish, Is.True);
        }

        [Test]
        public void StructuredStatic_SameExtensionKeyWithDifferentContentsIsAnError()
        {
            Assert.Throws<JsonSerializationException>(() => new CatalogMerger().Merge(
                Catalog(false, extensions: new[] { new StaticExtensionField("Same", "enUs", "/raw", "0") }, sourceName: "Same"),
                Catalog(true, extensions: new[] { new StaticExtensionField("Same", "enUs", "/raw", "1") }, sourceName: "Same")));
        }

        [Test]
        public void StructuredStatic_MissingSelectedOriginIsNeverReplacedByOtherSource()
        {
            var c = Catalog(false, Stats("value", 0));
            c = new StaticCatalog(c.Manifest, c.Units, c.Traits, c.Items, c.Augments, c.FieldOrigins.Where(x => x.Pointer != "/baseStats/zero~1~0"));
            var result = new CatalogMerger().Merge(c, Catalog(true, Stats("value", 5))).Catalog;
            Assert.That(new CatalogValidator().Validate(result).Issues.Any(x => x.Code == "missing_field_origin" && x.Message.Contains("/baseStats/zero~1~0")), Is.True);
        }

        [TestCase("stat")]
        [TestCase("series")]
        [TestCase("breakpoint")]
        [TestCase("unique")]
        [TestCase("extension")]
        public void StructuredStatic_PatchDifferenceIncludesStructuredOnlyValueChanges(string kind)
        {
            StaticCatalog C(bool after) => Catalog(false,
                stats: new Dictionary<string, double?> { ["hp"] = kind == "stat" && after ? 200 : 100 },
                series: new[] { new StaticNumericSeries("D", kind == "series" && after ? new double?[] { 1, 0 } : new double?[] { 0, 1 }) },
                tiers: new[] { new TraitBreakpoint(kind == "breakpoint" && after ? (int?)2 : null, 9, 1, null) },
                unique: kind == "unique" && after,
                extensions: new[] { new StaticExtensionField("Riot", "enUs", "/raw", kind == "extension" && after ? "1" : "0") });
            var diff = new CatalogDiffer().Compare(C(false), C(true));
            var changes = diff.Units.Changed.Concat(diff.Traits.Changed).Concat(diff.Items.Changed).ToArray();
            Assert.That(changes.Length, Is.EqualTo(1));
            Assert.That(changes.Single().Fields.Any(x => x.FieldName.StartsWith("/", StringComparison.Ordinal)), Is.True);
        }
    }
}
