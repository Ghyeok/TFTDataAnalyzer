using System;
using NUnit.Framework;
using TFTAnalyzer.Diff;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Tests.Diff
{
    public sealed class CatalogDifferTests
    {
        [Test]
        public void Compare_ReportsAddedRemovedAndChangedFields()
        {
            var previous = Catalog(
                Unit("TEST18_Removed", "Old", DateTime.UnixEpoch),
                Unit("TEST18_Changed", "Before", DateTime.UnixEpoch));
            var current = Catalog(
                Unit("TEST18_Added", "New", DateTime.UnixEpoch),
                Unit("TEST18_Changed", "After", DateTime.UnixEpoch));

            var diff = new CatalogDiffer().Compare(previous, current);

            Assert.That(diff.Units.AddedIds, Is.EqualTo(new[] { "TEST18_Added" }));
            Assert.That(diff.Units.RemovedIds, Is.EqualTo(new[] { "TEST18_Removed" }));
            Assert.That(diff.Units.Changed[0].StableId, Is.EqualTo("TEST18_Changed"));
            Assert.That(diff.Units.Changed[0].Fields[0].FieldName, Is.EqualTo("name.enUs"));
            Assert.That(diff.Units.Changed[0].Fields[0].Before, Is.EqualTo("Before"));
            Assert.That(diff.Units.Changed[0].Fields[0].After, Is.EqualTo("After"));
        }

        [Test]
        public void Compare_IgnoresSourceRetrievalTimestampOnlyChanges()
        {
            var previous = Catalog(Unit("TEST18_Same", "Same", DateTime.UnixEpoch));
            var current = Catalog(Unit("TEST18_Same", "Same", DateTime.UnixEpoch.AddDays(1)));

            var diff = new CatalogDiffer().Compare(previous, current);

            Assert.That(diff.Units.Changed, Is.Empty);
        }

        private static StaticCatalog Catalog(params UnitRecord[] units)
        {
            return new StaticCatalog(Manifest(), units, null, null, null);
        }

        private static UnitRecord Unit(string id, string englishName, DateTime retrievedUtc)
        {
            return new UnitRecord(
                id,
                new LocalizedText("동일", englishName),
                1,
                null,
                "https://example.test/unit.png",
                new[]
                {
                    new SourceStamp(
                        "Riot Data Dragon",
                        "https://example.test/unit.json",
                        retrievedUtc,
                        "etag",
                        "hash")
                });
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
                null,
                0,
                0,
                "",
                "");
        }
    }
}
