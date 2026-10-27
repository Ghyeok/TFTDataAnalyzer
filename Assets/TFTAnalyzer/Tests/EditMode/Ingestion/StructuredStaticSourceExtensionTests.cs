using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Editor.Ingestion.DataDragon;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class StructuredStaticSourceExtensionTests
    {
        [Test]
        public void StructuredStatic_SourceSpecificUnmappedFieldsAreRetainedWithoutChangingLegacyValues()
        {
            var c = new DataDragonAdapter(new CommunityDragonShapeTests.ShapeTransport()).Parse(
                StructuredStaticIngestionTests.OfficialSnapshot(StructuredStaticIngestionTests.Document("ko_kr"), StructuredStaticIngestionTests.Document("en_us")), CommunityDragonShapeTests.Patch);
            var unit = c.Units.Single();
            // The existing official adapter has not mapped traits or nested ability.desc.
            // Keep that legacy meaning and preserve its unmodeled source instead of deleting it.
            Assert.That(unit.TraitIds, Is.Empty);
            Assert.That(unit.Description.EnUs, Is.Empty);
            Assert.That(unit.Extensions.Count(x => x.Pointer == "/traits"), Is.EqualTo(2));
            Assert.That(unit.Extensions.Count(x => x.Pointer == "/ability/desc"), Is.EqualTo(2));
        }

        [Test]
        public void StructuredStatic_NonRepresentableFlatEffectMembersRemainInertSourceData()
        {
            var ko = StructuredStaticIngestionTests.Document("ko_kr"); var en = StructuredStaticIngestionTests.Document("en_us");
            foreach (var d in new[] { ko, en })
            {
                var effects = StructuredStaticIngestionTests.Selected(d)["items"][0]["effects"];
                effects["Missing"] = JValue.CreateNull(); effects["Custom"] = new JObject { ["flag"] = false };
            }
            var item = StructuredStaticIngestionTests.Parse(ko, en).Catalog.Items.Single();
            Assert.That(item.Effects["Power"], Is.Zero);
            Assert.That(item.Effects.ContainsKey("Missing"), Is.False);
            Assert.That(item.Extensions.Count(x => x.Pointer == "/effects/Missing" && x.CanonicalJson == "null"), Is.EqualTo(2));
            Assert.That(item.Extensions.Count(x => x.Pointer == "/effects/Custom" && x.CanonicalJson == "{\"flag\":false}"), Is.EqualTo(2));
        }
    }
}
