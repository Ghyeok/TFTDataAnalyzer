using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Serialization
{
    public sealed class FieldOriginTests
    {
        internal static JObject Document(int schema = 3)
        {
            var patch = new PatchKey("TFTSet18", 18, "18.2", "16.18.1");
            var source = new SourceStamp("Riot", "https://example.test/en", DateTime.UnixEpoch, "", "abc");
            var manifest = new PatchManifest(schema, new CatalogIdentity(patch, CatalogSourceProfile.OfficialOnly),
                "Test", DateTime.UnixEpoch, "", new[] { source }, DateTime.UnixEpoch, null, 0, 0, "", "");
            var unit = new UnitRecord("TEST18_A", new LocalizedText("", "A"), 1, null, "", new[] { source },
                effects: new System.Collections.Generic.Dictionary<string, double> { ["zero/~"] = 0 });
            // Start with existing public APIs so the first RED proves lost metadata, not a compile error.
            var json = JObject.Parse(CanonicalCatalogSerializer.Serialize(new StaticCatalog(manifest,
                new[] { unit }, null, null, null)));
            json["fieldOrigins"] = new JArray(new[] { "/stableId", "/name/enUs", "/cost", "/effects/zero~1~0" }
                .Select(pointer => new JObject { ["recordType"] = "unit", ["stableId"] = "TEST18_A",
                    ["pointer"] = pointer, ["sources"] = json["manifest"]["sources"].DeepClone() }));
            return json;
        }

        [Test]
        public void FieldOrigins_SchemaThreeRoundTripPreservesActualFieldLinks()
        {
            var original = Document();
            var stored = JObject.Parse(CanonicalCatalogSerializer.Serialize(
                CanonicalCatalogSerializer.Deserialize(original.ToString())));
            Assert.That(stored["fieldOrigins"], Is.Not.Null, "Field-level lineage must survive saving and reloading.");
            Assert.That(stored["fieldOrigins"].Count(), Is.EqualTo(4));
            Assert.That(stored["fieldOrigins"].Any(x => x.Value<string>("pointer") == "/effects/zero~1~0"), Is.True);
        }

        [Test]
        public void FieldOrigins_MissingRealZeroOriginBlocksPublication()
        {
            var json = Document();
            ((JArray)json["fieldOrigins"]).Last.Remove();
            var result = new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(json.ToString()));
            Assert.That(result.Issues.Any(x => x.Code == "missing_field_origin"), Is.True);
            Assert.That(result.CanPublish, Is.False);
        }

        [Test]
        public void FieldOrigins_OldSchemaMustRejectInjectedMetadata()
        {
            Assert.Throws<JsonSerializationException>(() => CanonicalCatalogSerializer.Deserialize(Document(2).ToString()));
        }

        [Test]
        public void FieldOrigins_LegacyCaseVariantInjectionAlsoRejected()
        {
            var doc = Document(2); var origins = doc["fieldOrigins"]; doc.Remove("fieldOrigins");
            doc["FieldOrigins"] = origins;
            Assert.Throws<JsonSerializationException>(() => CanonicalCatalogSerializer.Deserialize(doc.ToString()));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void FieldOrigins_LegacyGoldenBytesAndHashStayUnchanged(int schema)
        {
            var path = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "TFTAnalyzer/Tests/Fixtures/Static/schema" + schema + "-field-origin-compat.json");
            var original = System.IO.File.ReadAllText(path);
            var saved = CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(original));
            Assert.That(saved, Is.EqualTo(original));
            Assert.That(ContentHasher.Sha256Hex(saved), Is.EqualTo(ContentHasher.Sha256Hex(original)));
            Assert.That(JObject.Parse(saved).Property("fieldOrigins"), Is.Null);
        }

        [TestCase("duplicate", "duplicate_field_origin")]
        [TestCase("pointer", "invalid_field_origin")]
        [TestCase("record", "invalid_field_origin")]
        [TestCase("source", "invalid_field_origin_source")]
        [TestCase("profile", "invalid_field_origin_source")]
        [TestCase("empty", "invalid_field_origin_source")]
        public void FieldOrigins_TamperedLinksAreRejected(string change, string code)
        {
            var doc = Document(); var list = (JArray)doc["fieldOrigins"]; var first = (JObject)list[0];
            if (change == "duplicate") list.Add(first.DeepClone());
            if (change == "pointer") first["pointer"] = "/effects/zero/~";
            if (change == "record") first["stableId"] = "missing";
            if (change == "source") first["sources"][0]["sha256"] = "different";
            if (change == "profile") first["sources"][0]["isSupplemental"] = true;
            if (change == "empty") first["sources"] = new JArray();
            var report = new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(doc.ToString()));
            Assert.That(report.CanPublish, Is.False);
            Assert.That(report.Issues.Any(x => x.Code == code), Is.True);
        }

        [Test]
        public void FieldOrigins_CollectionsAreCopiedAndCanonicalOrderIsStable()
        {
            var c = CanonicalCatalogSerializer.Deserialize(Document().ToString());
            var mutable = c.FieldOrigins.Reverse().ToList();
            var sourceList = c.Manifest.Sources.ToList();
            var origin = new FieldOrigin("unit", "TEST18_A", "/cost", sourceList);
            sourceList.Clear(); Assert.That(origin.Sources.Count, Is.EqualTo(1));
            var other = new StaticCatalog(c.Manifest, c.Units, c.Traits, c.Items, c.Augments, mutable);
            mutable.Clear(); Assert.That(other.FieldOrigins.Count, Is.EqualTo(4));
            Assert.That(CanonicalCatalogSerializer.Serialize(other), Is.EqualTo(CanonicalCatalogSerializer.Serialize(c)));
        }
    }
}
