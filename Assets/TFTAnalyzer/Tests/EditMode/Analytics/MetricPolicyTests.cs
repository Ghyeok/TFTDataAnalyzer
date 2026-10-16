using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Analytics
{
    /// <summary>Tests policy-approved output rather than merely hiding rates in a future UI.</summary>
    public sealed class MetricPolicyTests
    {
        /// <summary>Retains approved averages and sample counts while suppressing every ratio field below100.</summary>
        [TestCase(1, MetricVisibility.InsufficientSample, "insufficient_sample")]
        [TestCase(99, MetricVisibility.InsufficientSample, "insufficient_sample")]
        [TestCase(100, MetricVisibility.Visible, null)]
        public void MinimumSampleBoundaryControlsAllRatioPayloads(int count, MetricVisibility state, string reason)
        {
            var result = new MetricPolicy().Evaluate(Metric(count), Catalog());
            Assert.That(result.State, Is.EqualTo(state));
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
            Assert.That(result.SampleCount, Is.EqualTo(count));
            Assert.That(result.AveragePlacement, Is.EqualTo(1));
            Assert.That(result.CopyKey, Is.EqualTo("historical_observation"));
            if (count < 100) AssertHidden(result);
            else
            {
                Assert.That(result.Ratios.Select(row => row.Key), Is.EqualTo(new[] { "top_four_rate", "first_place_rate" }));
                Assert.That(result.Ratios.All(row => row.Numerator == count && row.Denominator == count && row.Rate == 1), Is.True);
                Assert.That(result.Ratios.All(row => row.Interval.Lower > 0.96 && row.Interval.Upper == 1), Is.True);
            }
        }

        /// <summary>Users can raise the immutable sample floor but cannot lower it.</summary>
        [Test]
        public void ConfiguredMinimumCanOnlyIncrease()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetricPolicy(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetricPolicy(0));
            var policy = new MetricPolicy(200);
            Assert.That(policy.Evaluate(Metric(199), Catalog()).State, Is.EqualTo(MetricVisibility.InsufficientSample));
            Assert.That(policy.Evaluate(Metric(200), Catalog()).State, Is.EqualTo(MetricVisibility.Visible));
        }

        /// <summary>Legend-based restrictions take precedence over sample count and never retain hidden numerators.</summary>
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(200)]
        public void LegendBasedRatiosAreAlwaysPolicyBlocked(int count)
        {
            var result = new MetricPolicy(100, Metadata("LEGEND_BASED", AugmentPolicyKind.LegendBased))
                .Evaluate(Metric(count, "LEGEND_BASED"), Catalog());
            Assert.That(result.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(result.ReasonCode, Is.EqualTo("legend_based_augment"));
            Assert.That(result.AveragePlacement, Is.EqualTo(1));
            AssertHidden(result);
        }

        /// <summary>A Legend-tagged record cannot expose an average or ratio even at sufficient samples.</summary>
        [Test]
        public void LegendValuesAreBlockedEntirely()
        {
            var result = new MetricPolicy(100, Metadata("LEGEND", AugmentPolicyKind.Legend))
                .Evaluate(Metric(100, "LEGEND"), Catalog());
            Assert.That(result.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(result.AveragePlacement, Is.Null);
            AssertHidden(result);
        }

        /// <summary>Only explicit reviewed ordinary classification enables an augmented composition's rates.</summary>
        [Test]
        public void KnownStandardAugmentCanBePresented()
        {
            var result = new MetricPolicy(100, Metadata("SAFE_AUGMENT", AugmentPolicyKind.Standard))
                .Evaluate(Metric(100, "SAFE_AUGMENT"), Catalog());
            Assert.That(result.State, Is.EqualTo(MetricVisibility.Visible));
            Assert.That(result.Ratios.Count, Is.EqualTo(2));
        }

        /// <summary>Missing, unrelated and unknown policy metadata cannot silently authorize augment rates.</summary>
        [TestCase("missing")]
        [TestCase("unrelated")]
        [TestCase("unknown")]
        public void UnclassifiedAugmentsFailClosed(string kind)
        {
            var metadata = kind == "missing" ? null : Metadata(kind == "unrelated" ? "LEGEND" : "SAFE_AUGMENT",
                kind == "unknown" ? AugmentPolicyKind.Unknown : AugmentPolicyKind.Standard);
            var result = new MetricPolicy(100, metadata).Evaluate(Metric(100, "SAFE_AUGMENT"), Catalog());
            Assert.That(result.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(result.ReasonCode, Is.EqualTo("unclassified_augment"));
            AssertHidden(result);
        }

        /// <summary>Classification from a different patch or source profile cannot authorize this catalog.</summary>
        [TestCase("patch")]
        [TestCase("profile")]
        public void MetadataRequiresExactCatalogIdentity(string dimension)
        {
            var metadata = new MetricPolicyMetadata(Identity(dimension), new Dictionary<string, AugmentPolicyKind>
                { ["SAFE_AUGMENT"] = AugmentPolicyKind.Standard });
            var result = new MetricPolicy(100, metadata).Evaluate(Metric(100, "SAFE_AUGMENT"), Catalog());
            Assert.That(result.ReasonCode, Is.EqualTo("policy_catalog_mismatch"));
            AssertHidden(result);
        }

        /// <summary>All catalog identity fields, including set number, must agree before any value is presented.</summary>
        [TestCase("set")]
        [TestCase("number")]
        [TestCase("patch")]
        [TestCase("version")]
        [TestCase("profile")]
        public void MismatchedCatalogHidesScopeAndValues(string dimension)
        {
            var result = new MetricPolicy().Evaluate(Metric(100), Catalog(Identity(dimension)));
            Assert.That(result.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(result.ReasonCode, Is.EqualTo("catalog_mismatch"));
            Assert.That(result.Scope, Is.Null);
            Assert.That(result.AveragePlacement, Is.Null);
            AssertHidden(result);
        }

        /// <summary>Untrusted filter strings cannot escape into a nominally export-safe scope.</summary>
        [TestCase("region")]
        [TestCase("queue")]
        [TestCase("tier")]
        public void InvalidScopeTextIsNotEchoed(string dimension)
        {
            var source = AggregateTestData.Match("first");
            var scope = new MetricScope(Identity(), dimension == "region" ? "FAKE-PUUID" : "asia",
                dimension == "queue" ? "FakePlayer" : "1100", dimension == "tier" ? "KR1_123456" : "MASTER+");
            var observation = new AnonymousMatchObservation(source.MatchHash, scope, source.PlayedAt, source.Participants);
            var metric = AggregateBuilder.Build(new[] { observation }).Scopes.Single().Metrics.First();
            var result = new MetricPolicy().Evaluate(metric, Catalog());
            Assert.That(result.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(result.Scope, Is.Null);
            foreach (var forbidden in new[] { "FAKE-PUUID", "FakePlayer", "KR1_123456" })
                Assert.That(result.ToJson(), Does.Not.Contain(forbidden));
        }

        /// <summary>Unknown augment text cannot leak through policy errors, labels or presentation metadata.</summary>
        [Test]
        public void UnknownAugmentProducesFixedReasonWithoutEchoingIt()
        {
            var result = new MetricPolicy().Evaluate(Metric(100, "FAKE-PUUID-unknown"), Catalog());
            Assert.That(result.ReasonCode, Is.EqualTo("unknown_augment"));
            Assert.That(result.ToJson(), Does.Not.Contain("FAKE-PUUID"));
            AssertHidden(result);
        }

        /// <summary>Pairwise rates use eligible lobbies as their denominator and the same immutable sample floor.</summary>
        [TestCase(99, MetricVisibility.InsufficientSample)]
        [TestCase(100, MetricVisibility.Visible)]
        public void PairwiseBoundaryUsesEligibleLobbyCount(int count, MetricVisibility state)
        {
            var pair = Pair(count);
            var result = new MetricPolicy().Evaluate(pair, Catalog());
            Assert.That(result.State, Is.EqualTo(state));
            Assert.That(result.CopyKey, Is.EqualTo("relative_placement_advantage"));
            Assert.That(result.SampleCount, Is.EqualTo(count));
            Assert.That(result.AveragePlacement, Is.Null);
            if (count < 100) AssertHidden(result);
            else
            {
                var ratio = result.Ratios.Single();
                Assert.That(ratio.Key, Is.EqualTo("relative_placement_advantage"));
                Assert.That(ratio.Numerator, Is.EqualTo(pair.FirstHigherCount));
                Assert.That(ratio.Denominator, Is.EqualTo(100));
            }
        }

        /// <summary>Neither orientation of a pair can bypass Legend-based augment suppression.</summary>
        [TestCase("A")]
        [TestCase("B")]
        public void EitherPairCompositionCanBlockRates(string board)
        {
            var pair = Pair(100, "LEGEND_BASED", board);
            var result = new MetricPolicy(100, Metadata("LEGEND_BASED", AugmentPolicyKind.LegendBased)).Evaluate(pair, Catalog());
            Assert.That(result.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            AssertHidden(result);
        }

        /// <summary>Zero eligible lobbies with ambiguity exclusions are not promoted to sufficient samples.</summary>
        [Test]
        public void AmbiguousLobbiesNeverSatisfyTheSampleFloor()
        {
            var boards = Enumerable.Range(1, 8).Select(place => AggregateTestData.Participant(place,
                place == 8 ? "A" : ((char)('A' + place - 1)).ToString())).ToArray();
            var scope = AggregateBuilder.Build(Enumerable.Range(0, 100).Select(i => AggregateTestData.Match("ambiguous" + i, boards))).Scopes.Single();
            var pair = scope.Pairs.First(row => row.AmbiguousLobbyCount == 100);
            var result = new MetricPolicy().Evaluate(pair, Catalog());
            Assert.That(result.SampleCount, Is.Zero);
            Assert.That(result.AmbiguousLobbyCount, Is.EqualTo(100));
            Assert.That(result.State, Is.EqualTo(MetricVisibility.InsufficientSample));
            AssertHidden(result);
        }

        /// <summary>Caller mutations cannot change a previously constructed policy's classification snapshot.</summary>
        [Test]
        public void ClassificationMetadataCopiesCallerDictionary()
        {
            var tags = new Dictionary<string, AugmentPolicyKind> { ["LEGEND_BASED"] = AugmentPolicyKind.LegendBased };
            var metadata = new MetricPolicyMetadata(Identity(), tags);
            tags["LEGEND_BASED"] = AugmentPolicyKind.Standard;
            Assert.That(new MetricPolicy(100, metadata).Evaluate(Metric(100, "LEGEND_BASED"), Catalog()).State,
                Is.EqualTo(MetricVisibility.PolicyBlocked));
        }

        /// <summary>The dedicated JSON writer ignores unsafe global settings and exposes no raw domain references.</summary>
        [Test]
        public void ExportContainsOnlyApprovedDataAndIgnoresGlobalJsonSettings()
        {
            var result = new MetricPolicy().Evaluate(Metric(100), Catalog());
            var defaults = JsonConvert.DefaultSettings;
            try
            {
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                var json = result.ToJson();
                foreach (var forbidden in new[] { "$type", "TEST_A", "MatchHash", "AugmentIds", "Participants", "TopFourCount", "FirstPlaceCount", "puuid", "prediction", "combat_win" })
                    Assert.That(json, Does.Not.Contain(forbidden));
                Assert.That(JObject.Parse(json)["Ratios"].Count(), Is.EqualTo(2));
            }
            finally { JsonConvert.DefaultSettings = defaults; }
            Assert.That(typeof(MetricPresentation).GetProperties().Any(property =>
                property.PropertyType == typeof(AggregateMetric) || property.PropertyType == typeof(PairwiseMetric)), Is.False);
        }

        /// <summary>Rejects absent inputs and invalid classification codes without inventing policy decisions.</summary>
        [Test]
        public void InvalidPolicyInputsAreRejected()
        {
            var policy = new MetricPolicy();
            Assert.Throws<ArgumentNullException>(() => policy.Evaluate((AggregateMetric)null, Catalog()));
            Assert.Throws<ArgumentNullException>(() => policy.Evaluate((PairwiseMetric)null, Catalog()));
            Assert.Throws<ArgumentNullException>(() => policy.Evaluate(Metric(1), null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Metadata("SAFE_AUGMENT", (AugmentPolicyKind)99));
        }

        /// <summary>Checks serialized output cannot reveal suppressed numerators, percentages or intervals indirectly.</summary>
        private static void AssertHidden(MetricPresentation result)
        {
            Assert.That(result.Ratios, Is.Empty);
            var json = JObject.Parse(result.ToJson());
            Assert.That(json["Ratios"], Is.Empty);
            foreach (var forbidden in new[] { "Numerator", "Denominator", "Interval", "TopFourCount", "FirstPlaceCount", "PlacementSum", "FirstHigherCount" })
                Assert.That(result.ToJson(), Does.Not.Contain(forbidden));
        }

        /// <summary>Builds one real aggregate metric with exactly one selected board per fabricated lobby.</summary>
        private static AggregateMetric Metric(int count, string augment = null)
        {
            var scope = Snapshot(count, augment, "A");
            return scope.Metrics.Single(row => row.Fingerprint.Equals(Key("A", augment)));
        }

        /// <summary>Finds the A/B comparison independent of its canonical fingerprint orientation.</summary>
        private static PairwiseMetric Pair(int count, string augment = null, string augmentedBoard = "A")
        {
            var a = Key("A", augmentedBoard == "A" ? augment : null);
            var b = Key("B", augmentedBoard == "B" ? augment : null);
            return Snapshot(count, augment, augmentedBoard).Pairs.Single(row =>
                (row.First.Equals(a) && row.Second.Equals(b)) || (row.First.Equals(b) && row.Second.Equals(a)));
        }

        /// <summary>Creates complete unique-placement matches while attaching optional synthetic augment provenance.</summary>
        private static ScopeAggregate Snapshot(int count, string augment, string augmentedBoard)
        {
            var boards = Enumerable.Range(1, 8).Select(place => Participant(place,
                ((char)('A' + place - 1)).ToString(), ((char)('A' + place - 1)).ToString() == augmentedBoard ? augment : null)).ToArray();
            return AggregateBuilder.Build(Enumerable.Range(0, count).Select(i => AggregateTestData.Match("policy" + i, boards))).Scopes.Single();
        }

        /// <summary>Creates a fabricated board with only static identity-free final-state fields.</summary>
        private static AnonymousParticipant Participant(int placement, string board, string augment) => new AnonymousParticipant(
            placement, 30, new[] { new AnonymousUnit("TEST_" + board, 2, null) }, null, augment == null ? null : new[] { augment });

        /// <summary>Uses the established production fingerprint contract for the selected synthetic composition.</summary>
        private static CompositionFingerprint Key(string board, string augment) => CompositionFingerprintBuilder.Build(Participant(1, board, augment));

        /// <summary>Builds reviewed synthetic classification metadata bound to one exact catalog identity.</summary>
        private static MetricPolicyMetadata Metadata(string id, AugmentPolicyKind kind) => new MetricPolicyMetadata(Identity(),
            new Dictionary<string, AugmentPolicyKind> { [id] = kind });

        /// <summary>Creates the selected identity or one intentionally mismatched catalog dimension.</summary>
        private static CatalogIdentity Identity(string dimension = "") => new CatalogIdentity(new PatchKey(
            dimension == "set" ? "TFTSet19" : "TFTSet18", dimension == "number" ? 19 : 18,
            dimension == "patch" ? "18.3" : "18.2", dimension == "version" ? "16.18.2" : "16.18.1"),
            dimension == "profile" ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly);

        /// <summary>Supplies trusted fabricated static IDs without modifying any real catalog or importer.</summary>
        private static StaticCatalog Catalog(CatalogIdentity identity = null) => new StaticCatalog(
            new PatchManifest(2, identity ?? Identity(), "Test", DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, "", ""),
            null, null, null, new[] { "SAFE_AUGMENT", "LEGEND_BASED", "LEGEND" }.Select(id =>
                new AugmentRecord(id, null, "", null, null, null, "", null)));
    }
}
