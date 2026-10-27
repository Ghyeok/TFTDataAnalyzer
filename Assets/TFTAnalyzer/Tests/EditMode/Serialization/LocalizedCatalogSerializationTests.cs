using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Serialization
{
    public sealed class LocalizedCatalogSerializationTests
    {
        internal const string GoldenHash = "6152c706c5e36c459c81cb08bbd4d384c18141f0f69240d9ee734ebc7021cd7e";
        internal static string Golden => File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,"TFTAnalyzer/Tests/Fixtures/Static/schema4-localization-compat.json"));
        private static readonly string[] Kinds = { "units", "traits", "items", "augments" };
        // Independent wire fixture derived from the frozen ORIGINAL schema4 serializer.
        internal static JObject Document()
        {
            var root = JObject.Parse(Golden); root["manifest"]["schemaVersion"] = 5;
            var table = new JArray(); var types = new[] { "unit", "trait", "item", "augment" };
            for (var k = 0; k < Kinds.Length; k++) foreach (var row in root[Kinds[k]].OfType<JObject>())
            foreach (var field in new[] { "name", "description" })
            {
                var text = (JObject)row[field]; var ko = text.Value<string>("koKr"); var en = text.Value<string>("enUs");
                table.Add(new JObject { ["recordType"] = types[k], ["stableId"] = row.Value<string>("stableId"), ["field"] = field,
                    ["koKr"] = ko, ["enUs"] = en, ["usedEnglishFallback"] = string.IsNullOrWhiteSpace(ko) && !string.IsNullOrWhiteSpace(en) });
                row.Remove(field);
            }
            root["localizations"] = table; return root;
        }
        private static JObject UnitName(JObject d) => d["localizations"].OfType<JObject>().Single(x=>x.Value<string>("recordType")=="unit" && x.Value<string>("field")=="name");

        [Test]
        public void LocalizedCatalog_LiteralRoundTripAndFallbackRecords()
        {
            var input=Document(); var c=CanonicalCatalogSerializer.Deserialize(input.ToString());
            Assert.That(c.Manifest.SchemaVersion,Is.EqualTo(5)); Assert.That(new CatalogValidator().Validate(c).CanPublish,Is.True);
            Assert.That(c.Units.Single().Name.UsedEnglishFallback,Is.True); Assert.That(c.Traits.Single().Name.KoKr,Is.EqualTo("시너지"));
            Assert.That(c.Units.Single().Description.EnUs,Does.Contain("\n2026-10-27T12:00:00.000Z"));
            Assert.That(c.Units.Single().BaseStats["mana"],Is.Null); Assert.That(c.Items.Single().Unique,Is.False);
            var json=CanonicalCatalogSerializer.Serialize(c);var saved=JObject.Parse(json);
            Assert.That(saved["localizations"].Count(),Is.EqualTo(8));
            foreach(var kind in Kinds) foreach(var row in saved[kind].OfType<JObject>())
            { Assert.That(row["name"],Is.Null);Assert.That(row["description"],Is.Null); }
            Assert.That(JToken.DeepEquals(saved["fieldOrigins"],input["fieldOrigins"]),Is.True);
            Assert.That(json,Does.Not.Contain("\r\n").And.EndsWith("\n"));
            Assert.That(CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(json)),Is.EqualTo(json));
        }

        [Test]
        public void LocalizedCatalog_OriginalSchemaFourGoldenRemainsByteIdentical()
        {
            Assert.That(ContentHasher.Sha256Hex(Golden),Is.EqualTo(GoldenHash));
            Assert.That(CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(Golden)),Is.EqualTo(Golden));
        }

        [TestCase(1,"localizations")][TestCase(1,"Localizations")]
        [TestCase(2,"localizations")][TestCase(2,"Localizations")]
        [TestCase(3,"localizations")][TestCase(3,"Localizations")]
        [TestCase(4,"localizations")][TestCase(4,"Localizations")]
        public void LocalizedCatalog_LegacySchemasRejectInjectedTable(int schema,string key)
        {
            var file=schema==4?"schema4-localization-compat.json":schema==3?"schema3-structured-compat.json":"schema"+schema+"-field-origin-compat.json";
            var d=JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,"TFTAnalyzer/Tests/Fixtures/Static",file)));d[key]=new JArray();
            Assert.Throws<JsonSerializationException>(()=>CanonicalCatalogSerializer.Deserialize(d.ToString()));
        }

        [TestCase("missingTable")][TestCase("notArray")][TestCase("missingEntry")][TestCase("duplicateEntry")]
        [TestCase("orphanId")][TestCase("wrongKind")][TestCase("wrongField")][TestCase("extraProperty")]
        [TestCase("wrongLocale")][TestCase("missingFlag")][TestCase("nullFlag")][TestCase("stringFlag")]
        [TestCase("inconsistentFlag")][TestCase("inlineName")][TestCase("inlineDescription")]
        [TestCase("missingKo")][TestCase("intEn")][TestCase("duplicateRecord")][TestCase("intId")]
        [TestCase("stringSchema")][TestCase("intMetadata")]
        public void LocalizedCatalog_InvalidLocalizationTable(string change)
        {
            var d=Document();var table=(JArray)d["localizations"];var row=UnitName(d);
            if(change=="missingTable")d.Remove("localizations");if(change=="notArray")d["localizations"]=new JObject();
            if(change=="missingEntry")row.Remove();if(change=="duplicateEntry")table.Add(row.DeepClone());
            if(change=="orphanId")row["stableId"]="missing";if(change=="wrongKind")row["recordType"]="Unit";
            if(change=="wrongField")row["field"]="cost";if(change=="extraProperty")row["other"]=1;
            if(change=="wrongLocale"){row["ko-KR"]=row["koKr"];row.Remove("koKr");}
            if(change=="missingFlag")row.Remove("usedEnglishFallback");if(change=="nullFlag")row["usedEnglishFallback"]=JValue.CreateNull();
            if(change=="stringFlag")row["usedEnglishFallback"]="true";if(change=="inconsistentFlag")row["usedEnglishFallback"]=false;
            if(change=="inlineName")d["units"][0]["name"]=new JObject{["koKr"]="",["enUs"]="inline"};
            if(change=="inlineDescription")d["units"][0]["Description"]=new JObject();
            if(change=="missingKo")row.Remove("koKr");if(change=="intEn")row["enUs"]=42;
            if(change=="duplicateRecord")((JArray)d["units"]).Add(d["units"][0].DeepClone());if(change=="intId")row["stableId"]=42;
            if(change=="stringSchema")d["manifest"]["schemaVersion"]="5";if(change=="intMetadata")d["manifest"]["sources"][0]["sha256"]=42;
            Assert.Throws<JsonSerializationException>(()=>CanonicalCatalogSerializer.Deserialize(d.ToString()));
        }

        [TestCase("","English",true)][TestCase("한국어","English",false)]
        [TestCase(" \t","English",true)][TestCase(" \t"," \t",false)]
        public void LocalizedCatalog_FallbackMatchesLiteralWhitespace(string ko,string en,bool expected)
        {
            var d=Document();var text=d["localizations"].OfType<JObject>().Single(x=>x.Value<string>("recordType")=="augment"&&x.Value<string>("field")=="description");
            text["koKr"]=ko;text["enUs"]=en;text["usedEnglishFallback"]=expected;
            var id=text.Value<string>("stableId");var origins=(JArray)d["fieldOrigins"];
            foreach(var locale in new[]{"koKr","enUs"})
            {
                var value=locale=="koKr"?ko:en;
                if(!string.IsNullOrWhiteSpace(value))origins.Add(new JObject{["recordType"]="augment",["stableId"]=id,["pointer"]="/description/"+locale,["sources"]=d["manifest"]["sources"].DeepClone()});
            }
            var c=CanonicalCatalogSerializer.Deserialize(d.ToString());Assert.That(new CatalogValidator().Validate(c).CanPublish,Is.True);
            Assert.That(c.Augments.Single().Description.UsedEnglishFallback,Is.EqualTo(expected));
            Assert.That(c.Augments.Single().Description.KoKr,Is.EqualTo(ko));Assert.That(c.Augments.Single().Description.EnUs,Is.EqualTo(en));
        }

        [Test]
        public void LocalizedCatalog_CanonicalTableOrderingAndStringHash()
        {
            string Save(JObject d)=>CanonicalCatalogSerializer.Serialize(CanonicalCatalogSerializer.Deserialize(d.ToString()));
            var a=Document();var b=Document();b["localizations"]=new JArray(b["localizations"].Reverse());
            Assert.That(Save(a),Is.EqualTo(Save(b)));
            UnitName(b)["enUs"]="2026-10-27T12:00:00.000Z";
            var saved=Save(b);Assert.That(saved,Does.Contain("2026-10-27T12:00:00.000Z"));
            Assert.That(ContentHasher.Sha256Hex(Save(a)),Is.Not.EqualTo(ContentHasher.Sha256Hex(saved)));
        }

        [TestCase(false)][TestCase(true)]
        public void LocalizedCatalog_StableIdKeysDoNotCollideAcrossKindsOrEscapedText(bool escaped)
        {
            var d=Document();var old="TEST18_Unit";var id=escaped?"TEST18_/~|Unit":old;
            if(escaped)
            {
                d["units"][0]["stableId"]=id;
                foreach(var row in d["localizations"].OfType<JObject>().Where(x=>x.Value<string>("recordType")=="unit"))row["stableId"]=id;
                foreach(var row in d["fieldOrigins"].OfType<JObject>().Where(x=>x.Value<string>("recordType")=="unit"))row["stableId"]=id;
                var related=(JArray)d["augments"][0]["relatedIds"];
                for(var i=0;i<related.Count;i++) if(related[i].Value<string>()==old) related[i]=id;
            }
            else
            {
                d["traits"][0]["stableId"]=id;d["units"][0]["traitIds"][0]=id;
                foreach(var row in d["localizations"].OfType<JObject>().Where(x=>x.Value<string>("recordType")=="trait"))row["stableId"]=id;
                foreach(var row in d["fieldOrigins"].OfType<JObject>().Where(x=>x.Value<string>("recordType")=="trait"))row["stableId"]=id;
            }
            var c=CanonicalCatalogSerializer.Deserialize(d.ToString());Assert.That(new CatalogValidator().Validate(c).CanPublish,Is.True);
            Assert.That(c.Units.Single().Name.EnUs,Is.EqualTo("Unit /~|"));Assert.That(c.Traits.Single().Name.KoKr,Is.EqualTo("시너지"));
            Assert.That(JObject.Parse(CanonicalCatalogSerializer.Serialize(c))["localizations"].Count(),Is.EqualTo(8));
        }

        [TestCase(false)][TestCase(true)]
        public void LocalizedCatalog_NonCanonicalIdsCannotCollapseDomainKeys(bool duplicate)
        {
            var d=Document();var padded=" TEST18_Unit ";
            if(duplicate)
            {
                var record=d["units"][0].DeepClone();record["stableId"]=padded;((JArray)d["units"]).Add(record);
                foreach(var entry in d["localizations"].OfType<JObject>().Where(x=>x.Value<string>("recordType")=="unit").ToArray())
                {var copy=(JObject)entry.DeepClone();copy["stableId"]=padded;((JArray)d["localizations"]).Add(copy);}
            }
            else
            {
                d["units"][0]["stableId"]=padded;
                foreach(var entry in d["localizations"].OfType<JObject>().Where(x=>x.Value<string>("recordType")=="unit"))entry["stableId"]=padded;
            }
            Assert.Throws<JsonSerializationException>(()=>CanonicalCatalogSerializer.Deserialize(d.ToString()));
        }

        [TestCase("SchemaVersion",3)]
        [TestCase("SetNumber","19")]
        public void LocalizedCatalog_CaseAliasCannotOverrideVerifiedManifestValue(string alias,object value)
        {
            var d=Document();d["manifest"][alias]=JToken.FromObject(value);
            Assert.Throws<JsonSerializationException>(()=>CanonicalCatalogSerializer.Deserialize(d.ToString()));
        }

        [Test]
        public void LocalizedCatalog_DuplicateJsonKeysAreRejectedBeforeHydration()
        {
            var json=Document().ToString(Formatting.None).Replace("\"usedEnglishFallback\":true","\"usedEnglishFallback\":true,\"usedEnglishFallback\":true");
            Assert.That(()=>CanonicalCatalogSerializer.Deserialize(json),Throws.InstanceOf<JsonException>());
        }

        [TestCase(false)][TestCase(true)]
        public void LocalizedCatalog_SchemaFiveStillRequiresStructuredLineage(bool forgedSource)
        {
            var d=Document();
            if(forgedSource)d["units"][0]["extensions"][0]["sourceName"]="forged";
            else d["fieldOrigins"].OfType<JObject>().Single(x=>x.Value<string>("pointer")=="/baseStats/hp").Remove();
            Assert.That(new CatalogValidator().Validate(CanonicalCatalogSerializer.Deserialize(d.ToString())).CanPublish,Is.False);
        }
    }
}
