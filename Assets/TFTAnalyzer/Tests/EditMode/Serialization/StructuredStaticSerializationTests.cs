using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Serialization
{
    public sealed class StructuredStaticSerializationTests
    {
        internal static JObject Document()
        {
            var doc = FieldOriginTests.Document(); doc["manifest"]["schemaVersion"] = 4;
            var unit = doc["units"][0];
            unit["baseStats"] = new JObject { ["hp"] = 100, ["zero/~"] = 0, ["mana"] = JValue.CreateNull() };
            unit["abilityVariables"] = new JArray(new JObject { ["name"] = "Damage", ["values"] = new JArray(0, 10, JValue.CreateNull(), 30) });
            unit["extensions"] = new JArray(new JObject { ["sourceName"] = "Riot", ["locale"] = "enUs", ["pointer"] = "/unmodeled", ["canonicalJson"] = "{\"unknown\":false}" });
            var origins = (JArray)doc["fieldOrigins"];
            foreach (var p in new[] { "/baseStats/hp", "/baseStats/zero~1~0", "/baseStats/mana", "/abilityVariables/0/name", "/abilityVariables/0/values/0", "/abilityVariables/0/values/1", "/abilityVariables/0/values/2", "/abilityVariables/0/values/3", "/extensions/0/canonicalJson" })
                origins.Add(new JObject { ["recordType"] = "unit", ["stableId"] = "TEST18_A", ["pointer"] = p, ["sources"] = doc["manifest"]["sources"].DeepClone() });
            return doc;
        }

        [Test]
        public void StructuredStatic_SchemaFourRoundTripPreservesNullZeroAndSlotOrder()
        {
            var input = Document(); StaticCatalog catalog = null;
            Assert.DoesNotThrow(() => catalog = CanonicalCatalogSerializer.Deserialize(input.ToString()), "Schema4 must load the supplied public static structures.");
            var saved = JObject.Parse(CanonicalCatalogSerializer.Serialize(catalog));
            Assert.That(saved["units"][0]["baseStats"]["hp"].Value<double>(), Is.EqualTo(100));
            Assert.That(saved["units"][0]["baseStats"]["zero/~"].Value<double>(), Is.Zero);
            Assert.That(saved["units"][0]["baseStats"]["mana"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(saved["units"][0]["abilityVariables"][0]["values"].Select(x => x.Type), Is.EqualTo(new[] { JTokenType.Float, JTokenType.Float, JTokenType.Null, JTokenType.Float }));
            Assert.That(saved["units"][0]["abilityVariables"][0]["values"][3].Value<double>(), Is.EqualTo(30));
            Assert.That(saved["units"][0]["extensions"][0].Value<string>("canonicalJson"), Is.EqualTo("{\"unknown\":false}"));
            Assert.That(saved["fieldOrigins"].Count(), Is.EqualTo(input["fieldOrigins"].Count()));
        }

        [TestCase("baseStats")]
        [TestCase("BaseStats")]
        [TestCase("abilityVariables")]
        [TestCase("extensions")]
        public void StructuredStatic_OldSchemaRejectsNewRecordKeys(string key)
        {
            var doc = FieldOriginTests.Document(); doc["units"][0][key] = new JObject();
            Assert.Throws<JsonSerializationException>(() => CanonicalCatalogSerializer.Deserialize(doc.ToString()));
        }

        [Test]
        public void StructuredStatic_OriginalSchemaThreeGoldenRemainsByteIdentical()
        {
            var json = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "TFTAnalyzer/Tests/Fixtures/Static/schema3-structured-compat.json"));
            Assert.That(ContentHasher.Sha256Hex(json), Is.EqualTo("8f671fbd575971178ad39eb3f037dbde0d6a1a36f415bad673001ef822382727"));
            Assert.That(CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(json)), Is.EqualTo(json));
        }

        [Test]
        public void StructuredStatic_NumericSeriesDefensivelyCopiesInputAndRetainsNull()
        {
            var type = typeof(UnitRecord).Assembly.GetType("TFTAnalyzer.Domain.StaticNumericSeries");
            Assert.That(type, Is.Not.Null, "An immutable source-slot series is required.");
            var values = new double?[] { 0, null, 3 };
            var series = Activator.CreateInstance(type, "Damage", values); values[0] = 999;
            var stored = ((IEnumerable)type.GetProperty("Values").GetValue(series)).Cast<double?>().ToArray();
            Assert.That(stored, Is.EqualTo(new double?[] { 0, null, 3 }));
        }

        [Test]
        public void StructuredStatic_CanonicalMapOrderIsStableButSeriesOrderChangesHash()
        {
            var first = Document(); var reordered = Document();
            reordered["units"][0]["baseStats"] = new JObject(((JObject)first["units"][0]["baseStats"]).Properties().Reverse().Select(x => new JProperty(x.Name, x.Value.DeepClone())));
            string Save(JObject d) => CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(d.ToString()));
            Assert.That(Save(first), Is.EqualTo(Save(reordered)));
            reordered["units"][0]["abilityVariables"][0]["values"] = new JArray(30, JValue.CreateNull(), 10, 0);
            Assert.That(ContentHasher.Sha256Hex(Save(first)), Is.Not.EqualTo(ContentHasher.Sha256Hex(Save(reordered))));
        }

        [Test]
        public void StructuredStatic_TraitNullBoundaryAndItemFalseSurviveSaving()
        {
            var d = Document(); var source = d["manifest"]["sources"];
            d["traits"] = new JArray(new JObject { ["stableId"] = "TEST18_T", ["name"] = new JObject { ["koKr"] = "", ["enUs"] = "T" }, ["description"] = new JObject { ["koKr"] = "", ["enUs"] = "" }, ["effects"] = new JObject(), ["iconPath"] = "", ["sources"] = source.DeepClone(), ["breakpoints"] = new JArray(new JObject { ["minUnits"] = JValue.CreateNull(), ["maxUnits"] = 9, ["style"] = 1, ["effects"] = new JObject { ["power"] = JValue.CreateNull() } }), ["extensions"] = new JArray() });
            d["items"] = new JArray(new JObject { ["stableId"] = "TEST18_I", ["name"] = new JObject { ["koKr"] = "", ["enUs"] = "I" }, ["description"] = new JObject { ["koKr"] = "", ["enUs"] = "" }, ["componentIds"] = new JArray(), ["effects"] = new JObject(), ["iconPath"] = "", ["sources"] = source.DeepClone(), ["unique"] = false, ["extensions"] = new JArray() });
            var saved = JObject.Parse(CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(d.ToString())));
            Assert.That(saved["traits"][0]["breakpoints"][0]["minUnits"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(saved["traits"][0]["breakpoints"][0]["effects"]["power"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(saved["items"][0].Value<bool?>("unique"), Is.False);
        }
    }
}
