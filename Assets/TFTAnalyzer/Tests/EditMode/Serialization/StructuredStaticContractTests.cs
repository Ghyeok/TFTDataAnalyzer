using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Serialization
{
    public sealed class StructuredStaticContractTests
    {
        [TestCase("manifestNumber")]
        [TestCase("sourceHash")]
        [TestCase("originPointer")]
        public void StructuredStatic_KnownMetadataCannotCoerceWrongJsonTypes(string change)
        {
            var d = StructuredStaticSerializationTests.Document();
            if (change == "manifestNumber") d["manifest"]["setNumber"] = "18";
            if (change == "sourceHash") d["manifest"]["sources"][0]["sha256"] = 123;
            if (change == "originPointer") d["fieldOrigins"][0]["pointer"] = 1;
            Assert.Throws<JsonSerializationException>(() => CanonicalCatalogSerializer.Deserialize(d.ToString()));
        }

        [Test]
        public void StructuredStatic_SortingExtensionsMovesTheirSelectedOriginsTogether()
        {
            var d = StructuredStaticSerializationTests.Document(); var other = d["manifest"]["sources"][0].DeepClone(); other["sha256"] = "def"; other["url"] = "https://example.test/other";
            ((JArray)d["manifest"]["sources"]).Add(other.DeepClone()); ((JArray)d["units"][0]["sources"]).Add(other.DeepClone());
            d["units"][0]["extensions"][0]["pointer"] = "/z";
            var ext = d["units"][0]["extensions"][0].DeepClone(); ext["pointer"] = "/a"; ((JArray)d["units"][0]["extensions"]).Add(ext);
            ((JArray)d["fieldOrigins"]).Add(new JObject { ["recordType"] = "unit", ["stableId"] = "TEST18_A", ["pointer"] = "/extensions/1/canonicalJson", ["sources"] = new JArray(other.DeepClone()) });
            var saved = CanonicalCatalogSerializer.Deserialize(CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(d.ToString())));
            Assert.That(saved.Units[0].Extensions.Select(x => x.Pointer), Is.EqualTo(new[] { "/a", "/z" }));
            Assert.That(saved.FieldOrigins.Single(x => x.Pointer == "/extensions/0/canonicalJson").Sources[0].Sha256, Is.EqualTo("def"));
            Assert.That(saved.FieldOrigins.Single(x => x.Pointer == "/extensions/1/canonicalJson").Sources[0].Sha256, Is.EqualTo("abc"));
        }

        [Test]
        public void StructuredStatic_BaseStatMapCopiesMutableInput()
        {
            var c = CanonicalCatalogSerializer.Deserialize(StructuredStaticSerializationTests.Document().ToString()); var old = c.Units[0];
            var input = new System.Collections.Generic.Dictionary<string, double?> { ["hp"] = 100, ["mana"] = null };
            var record = new UnitRecord(old.StableId, old.Name, old.Cost, old.TraitIds, old.IconPath, old.Sources, baseStats: input);
            input["hp"] = 999; input.Clear();
            Assert.That(record.BaseStats["hp"], Is.EqualTo(100)); Assert.That(record.BaseStats.ContainsKey("mana"), Is.True);
        }

        [TestCase("duplicate")]
        [TestCase("depth")]
        [TestCase("budget")]
        [TestCase("nonfinite")]
        public void StructuredStatic_InvalidExtensionCannotBeStored(string change)
        {
            var d = StructuredStaticSerializationTests.Document();
            string json = change == "duplicate" ? "{\"x\":1,\"x\":2}" : change == "depth" ? new string('[', 20) + "0" + new string(']', 20) :
                change == "budget" ? "\"" + new string('a', 65537) + "\"" : "NaN";
            d["units"][0]["extensions"][0]["canonicalJson"] = json;
            Assert.That(() => CanonicalCatalogSerializer.Deserialize(d.ToString()), Throws.InstanceOf<JsonException>());
        }
    }
}
