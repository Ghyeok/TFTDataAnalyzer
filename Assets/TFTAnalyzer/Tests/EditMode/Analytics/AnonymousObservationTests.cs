using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Analytics
{
    /// <summary>Regression tests for anonymous contracts, immutable snapshots and deterministic composition keys.</summary>
    public sealed class AnonymousObservationTests
    {
        private const string MatchHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        /// <summary>Verifies equivalent final compositions share a key despite input enumeration order.</summary>
        [Test]
        public void FingerprintIsStableAcrossUnitItemTraitAndAugmentOrder()
        {
            var a = Participant(new[] { Unit("TEST_B"), Unit("TEST_A", 2, "ITEM_B", "ITEM_A") },
                new[] { Trait("TRAIT_B"), Trait("TRAIT_A") }, new[] { "AUG_B", "AUG_A" });
            var b = Participant(new[] { Unit("TEST_A", 2, "ITEM_A", "ITEM_B"), Unit("TEST_B") },
                new[] { Trait("TRAIT_A"), Trait("TRAIT_B") }, new[] { "AUG_A", "AUG_B" });

            Assert.That(CompositionFingerprintBuilder.Build(a), Is.EqualTo(CompositionFingerprintBuilder.Build(b)));
        }

        /// <summary>Pins the fingerprint version prefix and JSON structure used by future persisted composition keys.</summary>
        [Test]
        public void FingerprintUsesVersionedStructuredCanonicalEncoding()
        {
            var participant = Participant(new[] { Unit("TEST_A", 2, "ITEM_B", "ITEM_A") },
                new[] { Trait("TRAIT_A") }, new[] { "AUG_A" });
            const string canonical = "fingerprint-v1|[[[\"TEST_A\",2,[\"ITEM_A\",\"ITEM_B\"]]],[\"TRAIT_A\"],[\"AUG_A\"]]";

            Assert.That(CompositionFingerprintBuilder.Build(participant).Value,
                Is.EqualTo(ContentHasher.Sha256Hex(canonical)));
        }

        /// <summary>Ensures an ID containing a delimiter cannot impersonate multiple item IDs.</summary>
        [Test]
        public void FingerprintSeparatesDelimiterLikeIdsWithoutCollisions()
        {
            var a = Participant(new[] { Unit("TEST_A", 1, "ITEM_A|ITEM_B") });
            var b = Participant(new[] { Unit("TEST_A", 1, "ITEM_A", "ITEM_B") });
            Assert.That(CompositionFingerprintBuilder.Build(a), Is.Not.EqualTo(CompositionFingerprintBuilder.Build(b)));
        }

        /// <summary>Checks that every composition dimension and repeated unit changes the resulting key.</summary>
        [Test]
        public void FingerprintIncludesUnitTierItemsActiveTraitsAndAugments()
        {
            var baseline = CompositionFingerprintBuilder.Build(Participant(new[] { Unit("TEST_A") }));
            var variants = new[]
            {
                Participant(new[] { Unit("TEST_B") }),
                Participant(new[] { Unit("TEST_A", 2) }),
                Participant(new[] { Unit("TEST_A", 1, "ITEM_A") }),
                Participant(new[] { Unit("TEST_A") }, new[] { Trait("TRAIT_A") }),
                Participant(new[] { Unit("TEST_A") }, augments: new[] { "AUG_A" }),
                Participant(new[] { Unit("TEST_A"), Unit("TEST_A") })
            };
            foreach (var variant in variants)
                Assert.That(CompositionFingerprintBuilder.Build(variant), Is.Not.EqualTo(baseline));
        }

        /// <summary>Ensures a repeated equipped item remains significant rather than becoming a set entry.</summary>
        [Test]
        public void FingerprintRetainsRepeatedEquippedItems()
        {
            var a = Participant(new[] { Unit("TEST_A", 1, "ITEM_A") });
            var b = Participant(new[] { Unit("TEST_A", 1, "ITEM_A", "ITEM_A") });
            Assert.That(CompositionFingerprintBuilder.Build(a), Is.Not.EqualTo(CompositionFingerprintBuilder.Build(b)));
        }

        /// <summary>Keeps composition identity independent of placement, last round and inactive trait records.</summary>
        [Test]
        public void FingerprintIgnoresInactiveTraitsAndParticipantOutcome()
        {
            var a = Participant(new[] { Unit("TEST_A") });
            var b = new AnonymousParticipant(8, 99, new[] { Unit("TEST_A") },
                new[] { new AnonymousTrait("INACTIVE_TRAIT", 0) }, null);
            Assert.That(CompositionFingerprintBuilder.Build(a), Is.EqualTo(CompositionFingerprintBuilder.Build(b)));
        }

        /// <summary>Verifies ordinal ID ordering remains stable across English and Turkish cultures.</summary>
        [Test]
        public void FingerprintDoesNotDependOnCurrentCulture()
        {
            var before = CultureInfo.CurrentCulture;
            try
            {
                var participant = Participant(new[] { Unit("TEST_I"), Unit("TEST_i"), Unit("TEST_İ") });
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                var first = CompositionFingerprintBuilder.Build(participant);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.That(CompositionFingerprintBuilder.Build(participant), Is.EqualTo(first));
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        /// <summary>Prevents observations with any different catalog or filter dimension from sharing a scope.</summary>
        [TestCase("set")]
        [TestCase("patch")]
        [TestCase("version")]
        [TestCase("profile")]
        [TestCase("region")]
        [TestCase("queue")]
        [TestCase("tier")]
        public void MetricScopeKeepsEveryCatalogAndFilterDimensionSeparate(string dimension)
        {
            var baseline = Scope();
            var changed = new MetricScope(Identity(
                dimension == "set" ? "OTHER_SET" : "TEST_SET",
                dimension == "patch" ? "18.3" : "18.2",
                dimension == "version" ? "16.18.2" : "16.18.1",
                dimension == "profile" ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly),
                dimension == "region" ? "europe" : "asia", dimension == "queue" ? "OTHER_QUEUE" : "TEST_QUEUE",
                dimension == "tier" ? "DIAMOND" : "MASTER+");
            Assert.That(changed, Is.Not.EqualTo(baseline));
            Assert.That(new HashSet<MetricScope> { baseline, changed }.Count, Is.EqualTo(2));
        }

        /// <summary>Checks the equality and collection-hashing contract for independently constructed scopes.</summary>
        [Test]
        public void EqualScopesHaveEqualHashCodes()
        {
            Assert.That(Scope(), Is.EqualTo(Scope()));
            Assert.That(Scope().GetHashCode(), Is.EqualTo(Scope().GetHashCode()));
        }

        /// <summary>Rejects missing region, queue and tier filters before observations can be grouped.</summary>
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ScopeRejectsBlankDimensions(string blank)
        {
            Assert.Throws<ArgumentException>(() => new MetricScope(Identity(), blank, "QUEUE", "TIER"));
            Assert.Throws<ArgumentException>(() => new MetricScope(Identity(), "asia", blank, "TIER"));
            Assert.Throws<ArgumentException>(() => new MetricScope(Identity(), "asia", "QUEUE", blank));
        }

        /// <summary>Requires an explicit active catalog instead of inferring one from a patch string.</summary>
        [Test]
        public void ScopeRejectsMissingCatalog()
        {
            Assert.Throws<ArgumentNullException>(() => new MetricScope(null, "asia", "QUEUE", "TIER"));
        }

        /// <summary>Rejects placements outside the supported 1–8 interval.</summary>
        [TestCase(0)]
        [TestCase(9)]
        public void ParticipantRejectsOutOfRangePlacement(int placement)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnonymousParticipant(placement, 1,
                new[] { Unit("TEST_A") }, null, null));
        }

        /// <summary>Accepts both ends of the supported placement interval.</summary>
        [TestCase(1)]
        [TestCase(8)]
        public void ParticipantAcceptsBoundaryPlacements(int placement)
        {
            Assert.That(new AnonymousParticipant(placement, 1, new[] { Unit("TEST_A") }, null, null).Placement,
                Is.EqualTo(placement));
        }

        /// <summary>Rejects blank, raw and malformed match identifiers at the anonymous observation boundary.</summary>
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        [TestCase("KR1_FAKE_RAW_MATCH_ID")]
        [TestCase("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
        public void ObservationRejectsMissingOrUnhashedMatchIdentity(string hash)
        {
            Assert.Throws<ArgumentException>(() => Observation(hash));
        }

        /// <summary>Exercises constructor guards and verifies rejected raw identifiers do not appear in errors.</summary>
        [Test]
        public void ContractsRejectMissingStateWithoutEchoingInputValues()
        {
            Assert.Throws<ArgumentException>(() => new AnonymousUnit(" ", 1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit("TEST_A", 0));
            Assert.Throws<ArgumentException>(() => Unit("TEST_A", 1, " "));
            Assert.Throws<ArgumentException>(() => new AnonymousTrait(" ", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnonymousTrait("TEST_TRAIT", -1));
            Assert.Throws<ArgumentException>(() => Participant(Array.Empty<AnonymousUnit>()));
            Assert.Throws<ArgumentException>(() => Participant(new AnonymousUnit[] { null }));
            Assert.Throws<ArgumentException>(() => Participant(new[] { Unit("TEST_A") }, new AnonymousTrait[] { null }));
            Assert.Throws<ArgumentException>(() => Participant(new[] { Unit("TEST_A") }, augments: new[] { " " }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnonymousParticipant(1, -1,
                new[] { Unit("TEST_A") }, null, null));
            Assert.Throws<ArgumentNullException>(() => new AnonymousMatchObservation(MatchHash, null,
                DateTimeOffset.UnixEpoch, new[] { Participant(new[] { Unit("TEST_A") }) }));
            Assert.Throws<ArgumentException>(() => new AnonymousMatchObservation(MatchHash, Scope(),
                DateTimeOffset.UnixEpoch, Array.Empty<AnonymousParticipant>()));
            Assert.Throws<ArgumentException>(() => new AnonymousMatchObservation(MatchHash, Scope(),
                DateTimeOffset.UnixEpoch, new AnonymousParticipant[] { null }));
            Assert.Throws<ArgumentNullException>(() => CompositionFingerprintBuilder.Build(null));
            var error = Assert.Throws<ArgumentException>(() => Observation("FAKE_PRIVATE_RAW_ID"));
            Assert.That(error.ToString(), Does.Not.Contain("FAKE_PRIVATE_RAW_ID"));
        }

        /// <summary>Verifies callers cannot mutate stored composition state through original or exposed collections.</summary>
        [Test]
        public void CollectionsAreDefensiveReadOnlySnapshots()
        {
            var items = new[] { "ITEM_A" };
            var unit = new AnonymousUnit("TEST_A", 1, items);
            var units = new[] { unit };
            var traits = new[] { Trait("TRAIT_A") };
            var augments = new[] { "AUG_A" };
            var participant = Participant(units, traits, augments);
            var participants = new[] { participant };
            var observation = new AnonymousMatchObservation(MatchHash, Scope(), DateTimeOffset.UnixEpoch, participants);
            items[0] = "OTHER"; units[0] = Unit("OTHER"); traits[0] = Trait("OTHER");
            augments[0] = "OTHER"; participants[0] = Participant(new[] { Unit("OTHER") });

            Assert.That(observation.Participants[0].Units[0].StableId, Is.EqualTo("TEST_A"));
            Assert.That(unit.ItemIds[0], Is.EqualTo("ITEM_A"));
            Assert.That(participant.Traits[0].StableId, Is.EqualTo("TRAIT_A"));
            Assert.That(participant.AugmentIds[0], Is.EqualTo("AUG_A"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)unit.ItemIds)[0] = "OTHER");
            Assert.Throws<NotSupportedException>(() => ((IList<AnonymousUnit>)participant.Units)[0] = Unit("OTHER"));
        }

        /// <summary>Checks JSON reconstruction preserves complete scope, normalized time and composition identity.</summary>
        [Test]
        public void ObservationRoundTripsWithFullScopeAndUtcTimestamp()
        {
            var observation = new AnonymousMatchObservation(MatchHash.ToUpperInvariant(), Scope(),
                new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(9)),
                new[] { Participant(new[] { Unit("TEST_A", 2, "ITEM_A") }, new[] { Trait("TRAIT_A") }, new[] { "AUG_A" }) });
            var json = JsonConvert.SerializeObject(observation);
            var restored = JsonConvert.DeserializeObject<AnonymousMatchObservation>(json);
            Assert.That(restored.MatchHash, Is.EqualTo(MatchHash));
            Assert.That(restored.Scope, Is.EqualTo(observation.Scope));
            Assert.That(restored.PlayedAt.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(restored.PlayedAt, Is.EqualTo(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
            Assert.That(CompositionFingerprintBuilder.Build(restored.Participants[0]),
                Is.EqualTo(CompositionFingerprintBuilder.Build(observation.Participants[0])));
        }

        /// <summary>Guards the public analytics contract against accidental personal-identity properties.</summary>
        [Test]
        public void AnalyticsContractsExposeNoPersonalIdentityProperties()
        {
            var forbidden = new[] { "Puuid", "SummonerId", "RiotId", "GameName", "TagLine", "PlayerName", "MatchId" };
            var types = typeof(AnonymousMatchObservation).Assembly.GetTypes()
                .Where(t => t.Namespace == "TFTAnalyzer.Analytics" && t.IsPublic);
            foreach (var type in types)
            foreach (var property in type.GetProperties())
                Assert.That(forbidden, Does.Not.Contain(property.Name), type.FullName + "." + property.Name);
        }

        /// <summary>Creates a fabricated catalog identity whose dimensions can be varied independently.</summary>
        private static CatalogIdentity Identity(string set = "TEST_SET", string patch = "18.2",
            string version = "16.18.1", CatalogSourceProfile profile = CatalogSourceProfile.OfficialOnly)
            => new CatalogIdentity(new PatchKey(set, 18, patch, version), profile);

        /// <summary>Creates the baseline fabricated statistics scope used by contract tests.</summary>
        private static MetricScope Scope() => new MetricScope(Identity(), "asia", "TEST_QUEUE", "MASTER+");
        /// <summary>Creates a fabricated final unit with the requested tier and equipped item sequence.</summary>
        private static AnonymousUnit Unit(string id, int tier = 1, params string[] items)
            => new AnonymousUnit(id, tier, items);
        /// <summary>Creates an active fabricated trait for composition-key tests.</summary>
        private static AnonymousTrait Trait(string id) => new AnonymousTrait(id, 1);
        /// <summary>Creates a fabricated participant with a valid baseline outcome and supplied final board.</summary>
        private static AnonymousParticipant Participant(IEnumerable<AnonymousUnit> units,
            IEnumerable<AnonymousTrait> traits = null, IEnumerable<string> augments = null)
            => new AnonymousParticipant(1, 20, units, traits, augments);
        /// <summary>Creates a valid fabricated observation while varying only its match hash.</summary>
        private static AnonymousMatchObservation Observation(string hash)
            => new AnonymousMatchObservation(hash, Scope(), DateTimeOffset.UnixEpoch,
                new[] { Participant(new[] { Unit("TEST_A") }) });
    }
}
