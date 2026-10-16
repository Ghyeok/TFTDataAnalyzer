using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Analytics
{
    /// <summary>Verifies exact participant counts, lobby comparisons, scope isolation and deterministic rebuilds.</summary>
    public sealed class AggregateBuilderTests
    {
        /// <summary>Counts each final participant independently with correct raw numerators and placement sums.</summary>
        [Test]
        public void ExactPlacementAndRatioCountsRetainPeriodAndVersions()
        {
            var observations = new[] { 1, 4, 5, 8 }.Select((place, i) => AggregateTestData.Match("m" + i,
                AggregateTestData.Board(place, "A"), day: i + 1, gameVersion: i == 0 ? "16.18.1" : "16.18.2"));
            var scope = AggregateBuilder.Build(observations).Scopes.Single();
            var metric = scope.Metrics.Single(row => row.Fingerprint.Equals(AggregateTestData.Key("A")));
            Assert.That(scope.MatchCount, Is.EqualTo(4));
            Assert.That(metric.Count, Is.EqualTo(4));
            Assert.That(metric.PlacementSum, Is.EqualTo(18));
            Assert.That(metric.AveragePlacement, Is.EqualTo(4.5));
            Assert.That(metric.TopFourCount, Is.EqualTo(2));
            Assert.That(metric.FirstPlaceCount, Is.EqualTo(1));
            Assert.That(metric.TopFourRate, Is.EqualTo(0.5));
            Assert.That(metric.FirstPlaceRate, Is.EqualTo(0.25));
            Assert.That(metric.TopFourInterval.Lower, Is.InRange(0.15, 0.151));
            Assert.That(metric.FirstPlayedAt, Is.EqualTo(AggregateTestData.Time(1)));
            Assert.That(metric.LastPlayedAt, Is.EqualTo(AggregateTestData.Time(4)));
            Assert.That(scope.MatchDataVersions, Is.EqualTo(new[] { "2" }));
            Assert.That(scope.MatchGameVersions, Is.EqualTo(new[] { "16.18.1", "16.18.2" }));
            Assert.That(scope.Metrics.Sum(row => row.Count), Is.EqualTo(32));
        }

        /// <summary>Eight distinct boards produce 28 unordered comparisons with one result per lobby.</summary>
        [Test]
        public void EightDistinctBoardsProduceTwentyEightUnorderedPairs()
        {
            var scope = AggregateBuilder.Build(new[] { AggregateTestData.Match("one") }).Scopes.Single();
            Assert.That(scope.Pairs.Count, Is.EqualTo(28));
            Assert.That(scope.Pairs.Sum(pair => pair.Count), Is.EqualTo(28));
            foreach (var pair in scope.Pairs)
            {
                Assert.That(string.CompareOrdinal(pair.First.Value, pair.Second.Value), Is.LessThan(0));
                Assert.That(pair.FirstHigherCount + pair.SecondHigherCount, Is.EqualTo(1));
                Assert.That(pair.AmbiguousLobbyCount, Is.Zero);
            }
            var ab = Pair(scope, "A", "B");
            Assert.That(HigherCount(ab, "A"), Is.EqualTo(1));
        }

        /// <summary>Same-lobby placement comparisons accumulate without cross-lobby Cartesian products.</summary>
        [Test]
        public void PairwiseCountsStayInsideEachLobby()
        {
            var first = AggregateTestData.Match("one");
            var second = AggregateTestData.Match("two", Enumerable.Range(1, 8).Select(place =>
                AggregateTestData.Participant(place, place == 8 ? "A" : place == 1 ? "H" :
                    ((char)('A' + place - 1)).ToString())).ToArray());
            var pair = Pair(AggregateBuilder.Build(new[] { first, second }).Scopes.Single(), "A", "B");
            Assert.That(pair.Count, Is.EqualTo(2));
            Assert.That(HigherCount(pair, "A"), Is.EqualTo(1));
            Assert.That(HigherCount(pair, "B"), Is.EqualTo(1));
        }

        /// <summary>Repeated boards retain participant metrics but explicitly exclude ambiguous lobby comparisons.</summary>
        [Test]
        public void DuplicateCompositionsExcludeAmbiguousPairsAndKeepPlacementSamples()
        {
            var boards = Enumerable.Range(1, 8).Select(place => AggregateTestData.Participant(place,
                place == 8 ? "A" : ((char)('A' + place - 1)).ToString())).ToArray();
            var scope = AggregateBuilder.Build(new[] { AggregateTestData.Match("one", boards) }).Scopes.Single();
            var a = scope.Metrics.Single(row => row.Fingerprint.Equals(AggregateTestData.Key("A")));
            Assert.That(a.Count, Is.EqualTo(2));
            Assert.That(a.PlacementSum, Is.EqualTo(9));
            var ab = Pair(scope, "A", "B");
            Assert.That(ab.Count, Is.Zero);
            Assert.That(ab.FirstHigherCount + ab.SecondHigherCount, Is.Zero);
            Assert.That(ab.AmbiguousLobbyCount, Is.EqualTo(1));
            Assert.That(ab.FirstHigherInterval, Is.Null);
            Assert.That(scope.Pairs.Sum(pair => pair.Count), Is.EqualTo(15));
            Assert.That(scope.Pairs.Sum(pair => pair.AmbiguousLobbyCount), Is.EqualTo(6));
            Assert.That(scope.Pairs.All(pair => !pair.First.Equals(pair.Second)), Is.True);
        }

        /// <summary>All catalog and filter dimensions keep observations in distinct statistics scopes.</summary>
        [TestCase("set")]
        [TestCase("patch")]
        [TestCase("version")]
        [TestCase("profile")]
        [TestCase("region")]
        [TestCase("queue")]
        [TestCase("tier")]
        public void ScopeDimensionsNeverMerge(string dimension)
        {
            var snapshot = AggregateBuilder.Build(new[] { AggregateTestData.Match("one"),
                AggregateTestData.Match("two", dimension: dimension) });
            Assert.That(snapshot.Scopes.Count, Is.EqualTo(2));
            Assert.That(snapshot.Scopes.All(scope => scope.MatchCount == 1 && scope.Metrics.Sum(row => row.Count) == 8), Is.True);
            Assert.That(snapshot.Scopes.Sum(scope => scope.Pairs.Sum(pair => pair.Count)), Is.EqualTo(56));
        }

        /// <summary>Identical repeated match hashes contribute once, even when participant enumeration is reversed.</summary>
        [Test]
        public void IdenticalDuplicatesDoNotInflateMetrics()
        {
            var original = AggregateTestData.Match("one");
            var reversed = AggregateTestData.Match("one", original.Participants.Reverse().ToArray());
            var scope = AggregateBuilder.Build(new[] { original, reversed }).Scopes.Single();
            Assert.That(scope.MatchCount, Is.EqualTo(1));
            Assert.That(scope.Metrics.Sum(row => row.Count), Is.EqualTo(8));
            Assert.That(scope.Pairs.Sum(pair => pair.Count), Is.EqualTo(28));
        }

        /// <summary>A hash conflict cannot silently choose different scope or outcomes based on input order.</summary>
        [TestCase("scope")]
        [TestCase("placement")]
        [TestCase("time")]
        [TestCase("metadata")]
        public void ConflictingDuplicatesAreRejected(string conflict)
        {
            var second = AggregateTestData.Match("one", conflict == "placement" ? AggregateTestData.Board(8, "A") : null,
                dimension: conflict == "scope" ? "region" : "", day: conflict == "time" ? 2 : 1,
                gameVersion: conflict == "metadata" ? "16.18.2" : "16.18.1");
            Assert.Throws<ArgumentException>(() => AggregateBuilder.Build(new[] { AggregateTestData.Match("one"), second }));
        }

        /// <summary>Rejects invalid complete-lobby inputs rather than returning plausible partial statistics.</summary>
        [Test]
        public void InvalidLobbiesAndNullInputsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => AggregateBuilder.Build(null));
            Assert.Throws<ArgumentException>(() => AggregateBuilder.Build(new AnonymousMatchObservation[] { null }));
            Assert.Throws<ArgumentException>(() => AggregateBuilder.Build(new[] { AggregateTestData.Match("one",
                new[] { AggregateTestData.Participant(1, "A") }) }));
            Assert.Throws<ArgumentException>(() => AggregateBuilder.Build(new[] { AggregateTestData.Match("one",
                Enumerable.Repeat(AggregateTestData.Participant(1, "A"), 8).ToArray()) }));
        }

        /// <summary>Empty input yields no scopes, and reordered matches or participants produce identical snapshots.</summary>
        [Test]
        public void EmptyAndReorderedInputsAreDeterministic()
        {
            Assert.That(AggregateBuilder.Build(Array.Empty<AnonymousMatchObservation>()).Scopes, Is.Empty);
            var one = AggregateTestData.Match("one", day: 2);
            var two = AggregateTestData.Match("two", dimension: "region");
            var a = AggregateBuilder.Build(new[] { one, two });
            var b = AggregateBuilder.Build(new[] { two, AggregateTestData.Match("one", one.Participants.Reverse().ToArray(), day: 2) });
            Assert.That(JsonConvert.SerializeObject(b), Is.EqualTo(JsonConvert.SerializeObject(a)));
        }

        /// <summary>Finds a semantic composition pair regardless of its canonical hash orientation.</summary>
        private static PairwiseMetric Pair(ScopeAggregate scope, string a, string b) => scope.Pairs.Single(pair =>
            (pair.First.Equals(AggregateTestData.Key(a)) && pair.Second.Equals(AggregateTestData.Key(b))) ||
            (pair.First.Equals(AggregateTestData.Key(b)) && pair.Second.Equals(AggregateTestData.Key(a))));

        /// <summary>Reads the numerator belonging to a chosen board rather than assuming hash order.</summary>
        private static int HigherCount(PairwiseMetric pair, string board) =>
            pair.First.Equals(AggregateTestData.Key(board)) ? pair.FirstHigherCount : pair.SecondHigherCount;
    }

    /// <summary>Shares fabricated complete-lobby fixtures across core and persistence tests.</summary>
    internal static class AggregateTestData
    {
        /// <summary>Creates one participant containing a single fabricated stable unit ID.</summary>
        internal static AnonymousParticipant Participant(int placement, string board) => new AnonymousParticipant(
            placement, 30, new[] { new AnonymousUnit("TEST_" + board, 2, null) }, null, null);

        /// <summary>Derives a composition key from a fabricated board using the existing production contract.</summary>
        internal static CompositionFingerprint Key(string board) => CompositionFingerprintBuilder.Build(Participant(1, board));

        /// <summary>Returns an independently specified UTC played-at timestamp.</summary>
        internal static DateTimeOffset Time(int day) => new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero);

        /// <summary>Assigns one board a chosen placement and fills the remaining seven with distinct boards.</summary>
        internal static AnonymousParticipant[] Board(int placement, string board) => Enumerable.Range(1, 8)
            .Select(place => Participant(place, place == placement ? board : "FILLER_" + place)).ToArray();

        /// <summary>Creates an identity-free complete match with an optional changed scope dimension.</summary>
        internal static AnonymousMatchObservation Match(string seed, IReadOnlyList<AnonymousParticipant> participants = null,
            string dimension = "", int day = 1, string gameVersion = "16.18.1") => new AnonymousMatchObservation(
            ContentHasher.Sha256Hex("match-v1|" + seed), new MetricScope(new CatalogIdentity(
                new PatchKey(dimension == "set" ? "TFTSet19" : "TFTSet18", dimension == "set" ? 19 : 18,
                    dimension == "patch" ? "18.3" : "18.2", dimension == "version" ? "16.18.2" : "16.18.1"),
                dimension == "profile" ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly),
                dimension == "region" ? "europe" : "asia", dimension == "queue" ? "1160" : "1100",
                dimension == "tier" ? "DIAMOND" : "MASTER+"), Time(day), participants ?? Enumerable.Range(1, 8)
                .Select(place => Participant(place, ((char)('A' + place - 1)).ToString())).ToArray(), "2", gameVersion);
    }
}
