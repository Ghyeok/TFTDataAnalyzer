using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.Ingestion;
using TFTAnalyzer.Tests.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Validation
{
    public sealed class StructuredStaticValidationTests
    {
        [TestCase("minOverMax")]
        [TestCase("negative")]
        [TestCase("float")]
        [TestCase("missingBound")]
        [TestCase("missingStyle")]
        public void StructuredStatic_StoredBreakpointRejectsInvalidOrAbsentScalarKeys(string change)
        {
            var doc = StructuredStaticProvenanceTests.WithNullBreakpoint(); var row = (JObject)doc["traits"][0]["breakpoints"][0];
            if (change == "minOverMax") row["minUnits"] = 10;
            if (change == "negative") row["style"] = -1;
            if (change == "float") row["minUnits"] = 2.5;
            if (change == "missingBound") row.Remove("minUnits");
            if (change == "missingStyle") row.Remove("style");
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => CanonicalCatalogSerializer.Deserialize(doc.ToString()));
        }

        [Test]
        public void StructuredStatic_CompleteDuplicateTraitRangesCannotPublish()
        {
            var doc = StructuredStaticProvenanceTests.WithNullBreakpoint();
            doc["traits"][0]["breakpoints"][0]["minUnits"] = 2;
            ((JArray)doc["traits"][0]["breakpoints"]).Add(doc["traits"][0]["breakpoints"][0].DeepClone());
            foreach (var origin in doc["fieldOrigins"].Where(x => x.Value<string>("recordType") == "trait" && x.Value<string>("pointer").StartsWith("/breakpoints/0/", StringComparison.Ordinal)).ToArray())
            {
                var copy = origin.DeepClone(); copy["pointer"] = copy.Value<string>("pointer").Replace("/breakpoints/0/", "/breakpoints/1/"); ((JArray)doc["fieldOrigins"]).Add(copy);
            }
            var report = new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(doc.ToString()));
            Assert.That(report.Issues.Any(x => x.Code == "duplicate_trait_breakpoint"), Is.True);
            Assert.That(report.CanPublish, Is.False);
        }

        [Test]
        public void StructuredStatic_ProducerCannotPublishDuplicateSeriesNames()
        {
            var c = StructuredStaticMergeTests.Catalog(false, series: new[] { new StaticNumericSeries("D", new double?[] { 0 }), new StaticNumericSeries("D", new double?[] { 1 }) });
            var report = new CatalogValidator().Validate(c);
            Assert.That(report.Issues.Any(x => x.Code == "duplicate_static_series"), Is.True);
        }

        [TestCase("unit")]
        [TestCase("trait")]
        [TestCase("item")]
        [TestCase("augment")]
        public void StructuredStatic_NonFiniteLegacyEffectCannotEnterPublishedSchemaFour(string kind)
        {
            var c = StructuredStaticMergeTests.Catalog(false); var source = c.Manifest.Sources;
            var bad = new Dictionary<string, double> { ["Power"] = double.NaN };
            var u = new UnitRecord("TEST18_Bad", new LocalizedText("", "Bad"), 1, null, "", source, effects: bad);
            var t = new TraitRecord("TEST18_Bad", new LocalizedText("", "Bad"), LocalizedText.Empty, bad, "", source);
            var i = new ItemRecord("TEST18_Bad", new LocalizedText("", "Bad"), LocalizedText.Empty, null, bad, "", source);
            var a = new AugmentRecord("TEST18_Bad", new LocalizedText("", "Bad"), "", LocalizedText.Empty, null, bad, "", source);
            object value = kind == "unit" ? (object)u : kind == "trait" ? t : kind == "item" ? i : a;
            var origins = FieldOriginFields.Required(value, 4).Select(p => new FieldOrigin(kind, "TEST18_Bad", p, source));
            var input = new StaticCatalog(c.Manifest, kind == "unit" ? new[] { u } : null, kind == "trait" ? new[] { t } : null,
                kind == "item" ? new[] { i } : null, kind == "augment" ? new[] { a } : null, origins);
            var report = new CatalogValidator().Validate(input);
            Assert.That(report.Issues.Any(x => x.Code == "invalid_static_effect"), Is.True);
        }
    }
}
