using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Serialization
{
    public sealed class StructuredStaticProvenanceTests
    {
        [Test]
        public void StructuredStatic_CompleteSelectedLeafOriginsAllowPublication()
        {
            var catalog = CanonicalCatalogSerializer.Deserialize(StructuredStaticSerializationTests.Document().ToString());
            Assert.That(new CatalogValidator().Validate(catalog).CanPublish, Is.True);
        }

        [TestCase("/baseStats/mana")]
        [TestCase("/baseStats/zero~1~0")]
        [TestCase("/abilityVariables/0/values/2")]
        [TestCase("/extensions/0/canonicalJson")]
        public void StructuredStatic_MissingNullZeroSlotOrExtensionOriginIsRejected(string pointer)
        {
            var doc = StructuredStaticSerializationTests.Document();
            ((JArray)doc["fieldOrigins"]).First(x => x.Value<string>("pointer") == pointer).Remove();
            var report = new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(doc.ToString()));
            Assert.That(report.Issues.Any(x => x.Code == "missing_field_origin" && x.Message.Contains(pointer)), Is.True);
            Assert.That(report.CanPublish, Is.False);
        }

        [Test]
        public void StructuredStatic_ExtensionCannotClaimDifferentSourceName()
        {
            var doc = StructuredStaticSerializationTests.Document();
            doc["units"][0]["extensions"][0]["sourceName"] = "Invented Source";
            var report = new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(doc.ToString()));
            Assert.That(report.Issues.Any(x => x.Code == "invalid_extension_origin_source"), Is.True);
        }

        internal static JObject WithNullBreakpoint()
        {
            var doc = StructuredStaticSerializationTests.Document(); var sources = doc["manifest"]["sources"];
            doc["traits"] = new JArray(new JObject { ["stableId"] = "TEST18_T", ["name"] = new JObject { ["koKr"] = "", ["enUs"] = "T" },
                ["description"] = new JObject { ["koKr"] = "", ["enUs"] = "" }, ["effects"] = new JObject(), ["iconPath"] = "", ["sources"] = sources.DeepClone(),
                ["breakpoints"] = new JArray(new JObject { ["minUnits"] = JValue.CreateNull(), ["maxUnits"] = 9, ["style"] = 1,
                    ["effects"] = new JObject { ["power"] = JValue.CreateNull() } }), ["extensions"] = new JArray() });
            foreach (var pointer in new[] { "/stableId", "/name/enUs", "/breakpoints/0/minUnits", "/breakpoints/0/maxUnits", "/breakpoints/0/style", "/breakpoints/0/effects/power" })
                ((JArray)doc["fieldOrigins"]).Add(new JObject { ["recordType"] = "trait", ["stableId"] = "TEST18_T", ["pointer"] = pointer, ["sources"] = sources.DeepClone() });
            return doc;
        }

        [Test]
        public void StructuredStatic_ExplicitNullBreakpointKeepsRequiredOriginAfterReload()
        {
            var d = WithNullBreakpoint();
            var saved = CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(d.ToString()));
            Assert.That(new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(saved)).CanPublish, Is.True);
            ((JArray)d["fieldOrigins"]).First(x => x.Value<string>("pointer") == "/breakpoints/0/minUnits").Remove();
            Assert.That(new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(d.ToString())).Issues
                .Any(x => x.Code == "missing_field_origin" && x.Message.Contains("minUnits")), Is.True);
        }
    }
}
