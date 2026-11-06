using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        /// <summary>Detects distinct effect maps whose legacy display strings collide in both directions.</summary>
        [Test]
        public void Compare_DetectsEffectsCollision(
            [Values("unit", "trait", "item", "augment")] string kind,
            [Values(false, true)] bool reverse)
        {
            var previous = RecordCatalog(kind, effects: new Dictionary<string, double> { ["A"] = 1, ["B"] = 2 });
            var current = RecordCatalog(kind, effects: new Dictionary<string, double> { ["A=1\u001fB"] = 2 });

            AssertCollision(kind, previous, current, "effects", "A=1\u001fB=2", reverse);
        }

        /// <summary>Detects distinct relationship lists whose legacy display strings collide in both directions.</summary>
        [Test]
        public void Compare_DetectsRelationsCollision(
            [Values("unit", "item", "augment")] string kind,
            [Values(false, true)] bool reverse)
        {
            var previous = RecordCatalog(kind, relations: new[] { "A", "B" });
            var current = RecordCatalog(kind, relations: new[] { "A\u001fB" });

            AssertCollision(kind, previous, current, RelationField(kind), "A\u001fB", reverse);
        }

        /// <summary>Detects distinct source components whose legacy display strings collide in both directions.</summary>
        [Test]
        public void Compare_DetectsSourcesCollision(
            [Values("unit", "trait", "item", "augment")] string kind,
            [Values(false, true)] bool reverse)
        {
            var previous = RecordCatalog(kind, sources: new[] { Source(etag: "a\u001fb", hash: "c") });
            var current = RecordCatalog(kind, sources: new[] { Source(etag: "a", hash: "b\u001fc") });

            AssertCollision(kind, previous, current, "sources",
                "Source\u001fhttps://example.test/data.json\u001fa\u001fb\u001fc\u001f0", reverse);
        }

        /// <summary>Ignores relationship order while detecting a change in duplicate count.</summary>
        [Test]
        public void Compare_IgnoresRelationshipOrderButPreservesDuplicateCounts(
            [Values("unit", "item", "augment")] string kind)
        {
            var previous = RecordCatalog(kind, relations: new[] { "a", "A", "A" });
            var reordered = RecordCatalog(kind, relations: new[] { "A", "a", "A" });
            Assert.That(Entity(new CatalogDiffer().Compare(previous, reordered), kind).Changed, Is.Empty);

            var fewer = RecordCatalog(kind, relations: new[] { "A", "a" });
            AssertField(Entity(new CatalogDiffer().Compare(previous, fewer), kind),
                RelationField(kind), "A\u001fA\u001fa", "A\u001fa");
        }

        /// <summary>Preserves ordinal key order and round-trip values across culture changes.</summary>
        [Test]
        public void Compare_EffectsKeepOrdinalKeysRoundTripValuesAndInvariantCulture(
            [Values("unit", "trait", "item", "augment")] string kind)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var previous = RecordCatalog(kind, effects: new Dictionary<string, double> { ["a"] = 2.5, ["A"] = 1.25 });
                var reordered = RecordCatalog(kind, effects: new Dictionary<string, double> { ["A"] = 1.25, ["a"] = 2.5 });
                Assert.That(Entity(new CatalogDiffer().Compare(previous, reordered), kind).Changed, Is.Empty);

                var current = RecordCatalog(kind, effects: new Dictionary<string, double> { ["A"] = 1.2500000000000002, ["a"] = 2.5 });
                AssertField(Entity(new CatalogDiffer().Compare(previous, current), kind), "effects",
                    "A=1.25\u001fa=2.5", "A=1.2500000000000002\u001fa=2.5");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        /// <summary>Detects case-sensitive changes to effect keys.</summary>
        [Test]
        public void Compare_DetectsEffectKeyChanges(
            [Values("unit", "trait", "item", "augment")] string kind)
        {
            var previous = RecordCatalog(kind, effects: new Dictionary<string, double> { ["A"] = 1 });
            var current = RecordCatalog(kind, effects: new Dictionary<string, double> { ["a"] = 1 });

            AssertField(Entity(new CatalogDiffer().Compare(previous, current), kind), "effects", "A=1", "a=1");
        }

        /// <summary>Ignores source retrieval time and sorts by name then URL.</summary>
        [Test]
        public void Compare_SortsSourcesByNameThenUrlAndIgnoresRetrievalTime(
            [Values("unit", "trait", "item", "augment")] string kind)
        {
            var previous = RecordCatalog(kind, sources: new[]
            {
                Source(name: "b"), Source(name: "A", url: "https://example.test/z"),
                Source(name: "A", url: "https://example.test/a")
            });
            var current = RecordCatalog(kind, sources: new[]
            {
                Source(name: "A", url: "https://example.test/a", retrieved: DateTime.UnixEpoch.AddDays(1)),
                Source(name: "b", retrieved: DateTime.UnixEpoch.AddDays(2)),
                Source(name: "A", url: "https://example.test/z", retrieved: DateTime.UnixEpoch.AddDays(3))
            });

            Assert.That(Entity(new CatalogDiffer().Compare(previous, current), kind).Changed, Is.Empty);
            Assert.That(Entity(new CatalogDiffer().Compare(current, previous), kind).Changed, Is.Empty);
        }

        /// <summary>Detects changes to each compared source field.</summary>
        [Test]
        public void Compare_DetectsEachSourceFieldChange(
            [Values("name", "url", "etag", "hash", "supplemental")] string field)
        {
            var previous = RecordCatalog("unit", sources: new[] { Source() });
            var current = RecordCatalog("unit", sources: new[]
            {
                Source(name: field == "name" ? "Other" : "Source",
                    url: field == "url" ? "https://example.test/other.json" : "https://example.test/data.json",
                    etag: field == "etag" ? "changed" : "etag",
                    hash: field == "hash" ? "changed" : "hash", supplemental: field == "supplemental")
            });

            var changed = Entity(new CatalogDiffer().Compare(previous, current), "unit");
            Assert.That(changed.Changed, Has.Count.EqualTo(1));
            Assert.That(changed.Changed[0].Fields.Select(value => value.FieldName), Is.EqualTo(new[] { "sources" }));
            Assert.That(changed.Changed[0].Fields[0].Before,
                Is.EqualTo("Source\u001fhttps://example.test/data.json\u001fetag\u001fhash\u001f0"));
            Assert.That(changed.Changed[0].Fields[0].After, Is.Not.EqualTo(changed.Changed[0].Fields[0].Before));
        }

        /// <summary>Preserves input order when source name and URL are equal.</summary>
        [Test]
        public void Compare_PreservesSourceInputOrderForEqualNameAndUrl(
            [Values("unit", "trait", "item", "augment")] string kind)
        {
            var previous = RecordCatalog(kind, sources: new[] { Source(etag: "a"), Source(etag: "b") });
            var current = RecordCatalog(kind, sources: new[] { Source(etag: "b"), Source(etag: "a") });

            AssertField(Entity(new CatalogDiffer().Compare(previous, current), kind), "sources",
                "Source\u001fhttps://example.test/data.json\u001fa\u001fhash\u001f0\u001eSource\u001fhttps://example.test/data.json\u001fb\u001fhash\u001f0",
                "Source\u001fhttps://example.test/data.json\u001fb\u001fhash\u001f0\u001eSource\u001fhttps://example.test/data.json\u001fa\u001fhash\u001f0");
        }

        /// <summary>Detects a change in the number of duplicate sources.</summary>
        [Test]
        public void Compare_PreservesSourceDuplicateCounts(
            [Values("unit", "trait", "item", "augment")] string kind)
        {
            var previous = RecordCatalog(kind, sources: new[] { Source(), Source() });
            var current = RecordCatalog(kind, sources: new[] { Source() });

            AssertField(Entity(new CatalogDiffer().Compare(previous, current), kind), "sources",
                "Source\u001fhttps://example.test/data.json\u001fetag\u001fhash\u001f0\u001eSource\u001fhttps://example.test/data.json\u001fetag\u001fhash\u001f0",
                "Source\u001fhttps://example.test/data.json\u001fetag\u001fhash\u001f0");
        }

        /// <summary>Treats missing and empty collections consistently.</summary>
        [Test]
        public void Compare_TreatsNullAndEmptyCollectionsEqually(
            [Values("unit", "trait", "item", "augment")] string kind)
        {
            var previous = RecordCatalog(kind);
            var current = RecordCatalog(kind, relations: Array.Empty<string>(),
                effects: new Dictionary<string, double>(), sources: Array.Empty<SourceStamp>());

            Assert.That(Entity(new CatalogDiffer().Compare(previous, current), kind).Changed, Is.Empty);
        }

        /// <summary>Preserves added, removed and ordinary text changes in both directions.</summary>
        [Test]
        public void Compare_PreservesAddedRemovedAndOrdinaryTextInBothDirections(
            [Values("unit", "trait", "item", "augment")] string kind,
            [Values(false, true)] bool reverse)
        {
            var previous = Combine(RecordCatalog(kind, id: "Removed"), RecordCatalog(kind, name: "Before"));
            var current = Combine(RecordCatalog(kind, id: "Added"), RecordCatalog(kind, name: "After"));
            var diff = Entity(new CatalogDiffer().Compare(reverse ? current : previous, reverse ? previous : current), kind);

            Assert.That(diff.AddedIds, Is.EqualTo(new[] { reverse ? "Removed" : "Added" }));
            Assert.That(diff.RemovedIds, Is.EqualTo(new[] { reverse ? "Added" : "Removed" }));
            AssertField(diff, "name.enUs", reverse ? "After" : "Before", reverse ? "Before" : "After");
        }

        /// <summary>Retains the existing first-record policy for duplicate stable IDs.</summary>
        [Test]
        public void Compare_UsesFirstRecordForDuplicateIds()
        {
            var previous = Catalog(Unit("Same", "First", DateTime.UnixEpoch), Unit("Same", "Ignored before", DateTime.UnixEpoch));
            var current = Catalog(Unit("Same", "First", DateTime.UnixEpoch), Unit("Same", "Ignored after", DateTime.UnixEpoch));

            Assert.That(new CatalogDiffer().Compare(previous, current).Units.Changed, Is.Empty);
        }

        /// <summary>Confirms identical legacy values still produce a change when their structures differ.</summary>
        private static void AssertCollision(string kind, StaticCatalog previous, StaticCatalog current,
            string fieldName, string publicValue, bool reverse)
        {
            var empty = RecordCatalog(kind);
            foreach (var catalog in new[] { previous, current })
            {
                var baseline = Entity(new CatalogDiffer().Compare(empty, catalog), kind);
                Assert.That(baseline.Changed.Single().Fields.Single(value => value.FieldName == fieldName).After,
                    Is.EqualTo(publicValue), "Both original structures must have the same legacy public value.");
            }

            var diff = new CatalogDiffer().Compare(reverse ? current : previous, reverse ? previous : current);
            AssertField(Entity(diff, kind), fieldName, publicValue, publicValue);
        }

        private static void AssertField(EntityDiff diff, string name, string before, string after)
        {
            Assert.That(diff.Changed, Has.Count.EqualTo(1));
            Assert.That(diff.Changed[0].StableId, Is.EqualTo("Same"));
            Assert.That(diff.Changed[0].Fields, Has.Count.EqualTo(1));
            Assert.That(diff.Changed[0].Fields[0].FieldName, Is.EqualTo(name));
            Assert.That(diff.Changed[0].Fields[0].Before, Is.EqualTo(before));
            Assert.That(diff.Changed[0].Fields[0].After, Is.EqualTo(after));
        }

        private static SourceStamp Source(string name = "Source", string url = "https://example.test/data.json",
            string etag = "etag", string hash = "hash", bool supplemental = false, DateTime? retrieved = null)
        {
            return new SourceStamp(name, url, retrieved ?? DateTime.UnixEpoch, etag, hash, supplemental);
        }

        /// <summary>Maps each record kind with relationships to its comparison field.</summary>
        private static string RelationField(string kind)
        {
            switch (kind)
            {
                case "unit": return "traitIds";
                case "item": return "componentIds";
                case "augment": return "relatedIds";
                default: throw new ArgumentException(nameof(kind));
            }
        }

        /// <summary>Selects the comparison result for the requested record kind.</summary>
        private static EntityDiff Entity(CatalogDiff diff, string kind)
        {
            switch (kind)
            {
                case "unit": return diff.Units;
                case "trait": return diff.Traits;
                case "item": return diff.Items;
                case "augment": return diff.Augments;
                default: throw new ArgumentException(nameof(kind));
            }
        }

        /// <summary>Creates a single-record catalog with controllable relationships, effects and sources.</summary>
        private static StaticCatalog RecordCatalog(string kind, IEnumerable<string> relations = null,
            IReadOnlyDictionary<string, double> effects = null, IEnumerable<SourceStamp> sources = null,
            string id = "Same", string name = "Same")
        {
            var text = new LocalizedText("동일", name);
            switch (kind)
            {
                case "unit": return new StaticCatalog(Manifest(),
                    new[] { new UnitRecord(id, text, 1, relations, "", sources, effects: effects) }, null, null, null);
                case "trait": return new StaticCatalog(Manifest(), null,
                    new[] { new TraitRecord(id, text, null, effects, "", sources) }, null, null);
                case "item": return new StaticCatalog(Manifest(), null, null,
                    new[] { new ItemRecord(id, text, null, relations, effects, "", sources) }, null);
                case "augment": return new StaticCatalog(Manifest(), null, null, null,
                    new[] { new AugmentRecord(id, text, "", null, relations, effects, "", sources) });
                default: throw new ArgumentException(nameof(kind));
            }
        }

        private static StaticCatalog Combine(StaticCatalog first, StaticCatalog second)
        {
            return new StaticCatalog(Manifest(), first.Units.Concat(second.Units), first.Traits.Concat(second.Traits),
                first.Items.Concat(second.Items), first.Augments.Concat(second.Augments));
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
