using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Tests.Analytics;

namespace TFTAnalyzer.Tests.Persistence
{
    /// <summary>Checks canonical per-scope aggregate publication against real temporary files.</summary>
    public sealed class AggregateStoreTests
    {
        private string _temporary;
        private AnalyzerStorePaths _paths;

        /// <summary>Isolates each test from real observations and generated catalogs.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporary = Path.Combine(Path.GetTempPath(), "TftAggregateTests-" + Guid.NewGuid().ToString("N"));
            _paths = new AnalyzerStorePaths(Path.Combine(_temporary, "Project"));
        }

        /// <summary>Deletes only this test's uniquely named temporary root.</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true);
        }

        /// <summary>Rebuilding reordered input under another culture produces identical persisted bytes.</summary>
        [Test]
        public async Task RebuildIsByteIdenticalAndContainsCountsWithoutMatchHashesOrBoards()
        {
            var observations = new[] { AggregateTestData.Match("one"), AggregateTestData.Match("two", day: 2) };
            var store = new AggregateStore(_paths);
            var scope = AggregateBuilder.Build(observations).Scopes.Single();
            await store.SaveAsync(scope, CancellationToken.None);
            var path = _paths.AggregatePath(scope.Scope);
            var first = File.ReadAllBytes(path);
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                await store.SaveAsync(AggregateBuilder.Build(observations.Reverse()).Scopes.Single(), CancellationToken.None);
            }
            finally { CultureInfo.CurrentCulture = originalCulture; }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(first));
            var json = JObject.Parse(File.ReadAllText(path));
            Assert.That((int)json["schemaVersion"], Is.EqualTo(2));
            Assert.That((int)json["matchCount"], Is.EqualTo(2));
            Assert.That(json["metrics"].Sum(row => (int)row["count"]), Is.EqualTo(16));
            Assert.That(json["metrics"].Sum(row => (long)row["placementSum"]), Is.EqualTo(72));
            Assert.That(json["pairs"].Sum(row => (int)row["count"]), Is.EqualTo(56));
            foreach (var observation in observations)
                Assert.That(File.ReadAllText(path), Does.Not.Contain(observation.MatchHash));
            Assert.That(File.ReadAllText(path), Does.Not.Contain("TEST_A"));
            Assert.That(Path.GetFileName(path), Does.Match("^[0-9a-f]{64}\\.json$"));
        }

        /// <summary>Each complete catalog and filter dimension has an independent aggregate file.</summary>
        [TestCase("set")]
        [TestCase("patch")]
        [TestCase("version")]
        [TestCase("profile")]
        [TestCase("region")]
        [TestCase("queue")]
        [TestCase("tier")]
        public async Task ScopeFilesNeverOverwriteEachOther(string dimension)
        {
            var store = new AggregateStore(_paths);
            var first = Build("one");
            var second = AggregateBuilder.Build(new[] { AggregateTestData.Match("two", dimension: dimension) }).Scopes.Single();
            await store.SaveAsync(first, CancellationToken.None);
            var before = File.ReadAllBytes(_paths.AggregatePath(first.Scope));
            await store.SaveAsync(second, CancellationToken.None);
            Assert.That(_paths.AggregatePath(first.Scope), Is.Not.EqualTo(_paths.AggregatePath(second.Scope)));
            Assert.That(File.ReadAllBytes(_paths.AggregatePath(first.Scope)), Is.EqualTo(before));
            Assert.That(File.Exists(_paths.AggregatePath(second.Scope)), Is.True);
        }

        /// <summary>A successful rebuild replaces the chosen scope while leaving observation bytes untouched.</summary>
        [Test]
        public async Task RebuildReplacesOneScopeWithoutChangingObservationSource()
        {
            var observation = AggregateTestData.Match("one");
            await new ObservationStore(_paths).AppendIfNewAsync(observation, CancellationToken.None);
            var observationPath = _paths.ObservationPath(observation.Scope);
            var observationBytes = File.ReadAllBytes(observationPath);
            var store = new AggregateStore(_paths);
            await store.SaveAsync(Build("one"), CancellationToken.None);
            await store.SaveAsync(AggregateBuilder.Build(new[] { observation, AggregateTestData.Match("two") }).Scopes.Single(), CancellationToken.None);
            Assert.That((int)JObject.Parse(File.ReadAllText(_paths.AggregatePath(observation.Scope)))["matchCount"], Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(observationPath), Is.EqualTo(observationBytes));
        }

        /// <summary>A real publication lock failure preserves the previous snapshot and removes staging files.</summary>
        [Test]
        public async Task PublicationFailurePreservesPriorBytesAndAllowsRetry()
        {
            var store = new AggregateStore(_paths);
            var snapshot = Build("one");
            await store.SaveAsync(snapshot, CancellationToken.None);
            var path = _paths.AggregatePath(snapshot.Scope);
            var before = File.ReadAllBytes(path);
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var error = await ThrowsAsync<IOException>(() => store.SaveAsync(snapshot, CancellationToken.None));
                Assert.That(error.InnerException, Is.Null);
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_paths.RootDirectory, "*.tmp", SearchOption.AllDirectories), Is.Empty);
            await store.SaveAsync(snapshot, CancellationToken.None);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        /// <summary>Pre-cancelled writes preserve the previous canonical snapshot.</summary>
        [Test]
        public async Task PreCancelledSaveDoesNotChangeSnapshot()
        {
            var store = new AggregateStore(_paths);
            var snapshot = Build("one");
            await store.SaveAsync(snapshot, CancellationToken.None);
            var path = _paths.AggregatePath(snapshot.Scope);
            var before = File.ReadAllBytes(path);
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                await ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(snapshot, source.Token));
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        /// <summary>The same OS lease excludes observation and aggregate operations from conflicting writes.</summary>
        [Test]
        public async Task StoreLeaseConflictFailsSafelyAndCanBeRetried()
        {
            var store = new AggregateStore(_paths);
            var snapshot = Build("one");
            await store.SaveAsync(snapshot, CancellationToken.None);
            using (File.Open(_paths.LeasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await ThrowsAsync<IOException>(() => store.SaveAsync(snapshot, CancellationToken.None));
            await store.SaveAsync(snapshot, CancellationToken.None);
        }

        /// <summary>Policy classification receives copied augment provenance for compositions and both pair orientations.</summary>
        [Test]
        public async Task PolicyAugmentProvenanceSurvivesBuildAndPersistence()
        {
            var original = AggregateTestData.Match("provenance");
            var first = original.Participants[0];
            var augmented = new AnonymousParticipant(first.Placement, first.LastRound, first.Units, first.Traits,
                new[] { "AUG_B", "AUG_A", "AUG_A" });
            var boards = new[] { augmented }.Concat(original.Participants.Skip(1)).ToArray();
            var scope = AggregateBuilder.Build(new[] { AggregateTestData.Match("provenance", boards) }).Scopes.Single();
            var key = CompositionFingerprintBuilder.Build(augmented);
            var metric = scope.Metrics.Single(row => row.Fingerprint.Equals(key));
            Assert.That(metric.AugmentIds, Is.EqualTo(new[] { "AUG_A", "AUG_B" }));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)metric.AugmentIds).Add("OTHER"));
            foreach (var pair in scope.Pairs.Where(row => row.First.Equals(key) || row.Second.Equals(key)))
                Assert.That(pair.First.Equals(key) ? pair.FirstAugmentIds : pair.SecondAugmentIds,
                    Is.EqualTo(new[] { "AUG_A", "AUG_B" }));
            await new AggregateStore(_paths).SaveAsync(scope, CancellationToken.None);
            var json = JObject.Parse(File.ReadAllText(_paths.AggregatePath(scope.Scope)));
            Assert.That(json["metrics"].Single(row => (string)row["fingerprint"] == key.Value)["augmentIds"].Values<string>(),
                Is.EqualTo(new[] { "AUG_A", "AUG_B" }));
            Assert.That(json["pairs"].All(row => row["firstAugmentIds"] != null && row["secondAugmentIds"] != null), Is.True);
        }

        /// <summary>Adding policy provenance creates a new cache schema without altering existing schema-one files.</summary>
        [Test]
        public async Task PolicySchemaUpgradeLeavesPreviousCacheUntouched()
        {
            var scope = Build("one");
            var legacyPath = _paths.AggregatePath(scope.Scope, 1);
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));
            File.WriteAllText(legacyPath, "legacy-snapshot");
            await new AggregateStore(_paths).SaveAsync(scope, CancellationToken.None);
            Assert.That(File.ReadAllText(legacyPath), Is.EqualTo("legacy-snapshot"));
            Assert.That(_paths.AggregatePath(scope.Scope), Is.Not.EqualTo(legacyPath));
            Assert.That((int)JObject.Parse(File.ReadAllText(_paths.AggregatePath(scope.Scope)))["schemaVersion"], Is.EqualTo(2));
        }

        /// <summary>Builds a single fabricated statistics scope through the real aggregate builder.</summary>
        private static ScopeAggregate Build(string seed) => AggregateBuilder.Build(new[] { AggregateTestData.Match(seed) }).Scopes.Single();

        /// <summary>Checks async failures without blocking Unity's synchronization context.</summary>
        private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try { await action(); }
            catch (T error) { return error; }
            Assert.Fail("Expected exception: " + typeof(T).Name);
            return null;
        }
    }
}
