using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Serialization;
using UnityEngine;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class ObservationStoreTests
    {
        private string _temporary;
        private AnalyzerStorePaths _paths;

        /// <summary>Creates unique temporary project paths without touching the active project store.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporary = Path.Combine(Path.GetTempPath(), "TftObservationTests-" + Guid.NewGuid().ToString("N"));
            _paths = new AnalyzerStorePaths(Path.Combine(_temporary, "Project"));
        }

        /// <summary>Removes only this test's uniquely named temporary directory.</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true);
        }

        /// <summary>Checks one durable line per match and lossless restoration of all anonymous fields.</summary>
        [Test]
        public async Task AppendIsIdempotentAndRoundTripsFullObservation()
        {
            var store = new ObservationStore(_paths);
            var observation = Observation();
            Assert.That(await store.AppendIfNewAsync(observation, CancellationToken.None), Is.True);
            Assert.That(await store.AppendIfNewAsync(observation, CancellationToken.None), Is.False);
            Assert.That(File.ReadAllLines(_paths.ObservationPath(observation.Scope)).Length, Is.EqualTo(1));
            var restored = (await ReadAll(new ObservationStore(_paths))).Single();
            Assert.That(JsonConvert.SerializeObject(restored), Is.EqualTo(JsonConvert.SerializeObject(observation)));
            Assert.That(File.ReadAllText(_paths.ProcessedMatchIndexPath), Does.Contain(observation.MatchHash));
        }

        /// <summary>Scans real store files after anonymization for fabricated identity and raw-response markers.</summary>
        [Test]
        public async Task AnonymizerToDiskNeverPersistsFabricatedIdentityOrRawResponse()
        {
            var identity = Identity();
            var catalog = new StaticCatalog(
                new PatchManifest(2, identity, "Test", DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, "", ""),
                new[] { new UnitRecord("TEST_UNIT", null, 1, null, "", null) },
                new[] { new TraitRecord("TEST_TRAIT", null, null, null, "", null) },
                new[] { new ItemRecord("TEST_ITEM", null, null, null, null, "", null) },
                new[] { new AugmentRecord("TEST_AUGMENT", null, "", null, null, null, "", null) });
            using (var input = File.OpenRead(Path.Combine(Application.dataPath,
                       "TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json")))
            {
                var accepted = new MatchAnonymizer(catalog, identity).Read(input, "asia", "MASTER+");
                Assert.That(accepted.IsAccepted, Is.True);
                await new ObservationStore(_paths).AppendIfNewAsync(accepted.Observation, CancellationToken.None);
            }
            foreach (var file in Directory.GetFiles(_paths.RootDirectory, "*", SearchOption.AllDirectories))
            foreach (var forbidden in new[] { "FAKE-PUUID", "FakePlayer", "FAKE-TAG", "KR1_123456", "FAKE-BUILD-NOTE", "puuid", "riotIdGameName" })
                Assert.That(File.ReadAllText(file), Does.Not.Contain(forbidden));
        }

        /// <summary>Separates observation files when any catalog identity or collection filter changes.</summary>
        [TestCase("set")]
        [TestCase("patch")]
        [TestCase("version")]
        [TestCase("profile")]
        [TestCase("region")]
        [TestCase("queue")]
        [TestCase("tier")]
        public async Task ScopeFilesSeparateEveryCatalogAndFilterDimension(string dimension)
        {
            var first = Observation();
            var second = Observation("other-match", dimension);
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(first, CancellationToken.None);
            await store.AppendIfNewAsync(second, CancellationToken.None);
            Assert.That(_paths.ObservationPath(first.Scope), Is.Not.EqualTo(_paths.ObservationPath(second.Scope)));
            Assert.That(Directory.GetFiles(_paths.ObservationsDirectory, "*.ndjson").Length, Is.EqualTo(2));
            Assert.That((await ReadAll(store)).Count, Is.EqualTo(2));
        }

        /// <summary>Confirms store-wide match deduplication retains the first admitted scope.</summary>
        [Test]
        public async Task MatchHashDeduplicatesAcrossScopesWithinOneStore()
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            Assert.That(await store.AppendIfNewAsync(Observation(dimension: "region"), CancellationToken.None), Is.False);
            Assert.That((await ReadAll(store)).Count, Is.EqualTo(1));
        }

        /// <summary>Recovers authoritative hashes despite missing, malformed, empty or forged index contents.</summary>
        [TestCase("missing")]
        [TestCase("malformed")]
        [TestCase("empty")]
        [TestCase("phantom")]
        public async Task IndexIsRebuiltFromCommittedObservations(string damage)
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            if (damage == "missing") File.Delete(_paths.ProcessedMatchIndexPath);
            else File.WriteAllText(_paths.ProcessedMatchIndexPath, damage == "malformed" ? "FAKE-PUUID {" :
                new JObject { ["schemaVersion"] = 1, ["hashes"] = damage == "empty" ? new JArray() :
                    new JArray(Observation("second").MatchHash) }.ToString(Formatting.None));
            var reopened = new ObservationStore(_paths);
            Assert.That(await reopened.AppendIfNewAsync(Observation(), CancellationToken.None), Is.False);
            Assert.That(await reopened.AppendIfNewAsync(Observation("second"), CancellationToken.None), Is.True);
            Assert.That((await ReadAll(reopened)).Count, Is.EqualTo(2));
            Assert.That(File.ReadAllText(_paths.ProcessedMatchIndexPath), Does.Not.Contain("FAKE-PUUID"));
        }

        /// <summary>Runs competing appends on one instance and verifies exactly one durable insertion.</summary>
        [Test]
        public async Task ConcurrentDuplicateAttemptsCommitExactlyOneRecord()
        {
            var store = new ObservationStore(_paths);
            var results = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(_ => store.AppendIfNewAsync(Observation(), CancellationToken.None)));
            Assert.That(results.Count(value => value), Is.EqualTo(1));
            Assert.That((await ReadAll(store)).Count, Is.EqualTo(1));
        }

        /// <summary>Ensures sequential store instances observe each other's committed match hashes.</summary>
        [Test]
        public async Task SeparateInstancesReconcileChangesBeforeAppending()
        {
            var a = new ObservationStore(_paths);
            var b = new ObservationStore(_paths);
            await a.AppendIfNewAsync(Observation(), CancellationToken.None);
            await b.AppendIfNewAsync(Observation("second"), CancellationToken.None);
            Assert.That(await a.AppendIfNewAsync(Observation("second"), CancellationToken.None), Is.False);
            Assert.That((await ReadAll(b)).Count, Is.EqualTo(2));
        }

        /// <summary>Verifies pre-cancelled reads and writes preserve every file and permit a later retry.</summary>
        [Test]
        public async Task PreCancelledAppendLeavesExistingFilesByteIdentical()
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            var before = Files();
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                await ThrowsAsync<OperationCanceledException>(async () =>
                    await store.AppendIfNewAsync(Observation("second"), source.Token));
                await ThrowsAsync<OperationCanceledException>(async () => await ReadAll(store, source.Token));
            }
            AssertFiles(before);
            Assert.That(await store.AppendIfNewAsync(Observation("second"), CancellationToken.None), Is.True);
        }

        /// <summary>Forces index publication failure and recovers the already committed record without duplication.</summary>
        [Test]
        public async Task IndexPublicationFailureRetainsLogAndRetryDoesNotDuplicate()
        {
            Directory.CreateDirectory(_paths.ProcessedMatchIndexPath);
            var store = new ObservationStore(_paths);
            var error = await ThrowsAsync<IOException>(async () =>
                await store.AppendIfNewAsync(Observation(), CancellationToken.None));
            Assert.That(error.InnerException, Is.Null);
            Assert.That(File.ReadAllLines(_paths.ObservationPath(Observation().Scope)).Length, Is.EqualTo(1));
            Directory.Delete(_paths.ProcessedMatchIndexPath);
            Assert.That(await store.AppendIfNewAsync(Observation(), CancellationToken.None), Is.False);
            Assert.That((await ReadAll(new ObservationStore(_paths))).Count, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(_paths.RootDirectory, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        /// <summary>Repairs an interrupted trailing record while retaining the complete prefix byte for byte.</summary>
        [Test]
        public async Task IncompleteTailIsRemovedWithoutLosingCommittedPrefix()
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            var path = _paths.ObservationPath(Observation().Scope);
            var committed = File.ReadAllBytes(path);
            File.AppendAllText(path, "{\"MatchHash\":\"interrupted");
            Assert.That((await ReadAll(new ObservationStore(_paths))).Count, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(committed));
            Assert.That(await store.AppendIfNewAsync(Observation("second"), CancellationToken.None), Is.True);
            Assert.That((await ReadAll(store)).Count, Is.EqualTo(2));
        }

        /// <summary>Rejects complete corrupt records without changing files or disclosing their contents in errors.</summary>
        [TestCase("malformed")]
        [TestCase("extra-property")]
        [TestCase("duplicate")]
        [TestCase("wrong-scope")]
        public async Task CorruptCompleteRecordsFailClosedWithoutModificationOrLeakingContent(string damage)
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            var path = _paths.ObservationPath(Observation().Scope);
            if (damage == "malformed") File.AppendAllText(path, "FAKE-PUUID FakePlayer KR1_123456\n");
            else if (damage == "extra-property")
                File.WriteAllText(path, File.ReadAllText(path).TrimEnd('\n').Insert(1, "\"puuid\":\"FAKE-PUUID\",") + "\n");
            else if (damage == "duplicate") File.AppendAllText(path, File.ReadAllText(path));
            else File.Move(path, Path.Combine(_paths.ObservationsDirectory, new string('f', 64) + ".ndjson"));
            var before = Files();
            var error = await ThrowsAsync<IOException>(async () => await ReadAll(new ObservationStore(_paths)));
            Assert.That(error.InnerException, Is.Null);
            foreach (var marker in new[] { "FAKE-PUUID", "FakePlayer", "KR1_123456" })
                Assert.That(error.ToString(), Does.Not.Contain(marker));
            await ThrowsAsync<IOException>(async () => await store.AppendIfNewAsync(Observation("second"), CancellationToken.None));
            AssertFiles(before);
        }

        /// <summary>Uses a real file lock to verify failed access preserves data and a later append succeeds.</summary>
        [Test]
        public async Task WriteLockFailurePreservesExistingObservationAndAllowsRetry()
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            var before = Files();
            using (File.Open(_paths.ObservationPath(Observation().Scope), FileMode.Open, FileAccess.Read, FileShare.None))
                await ThrowsAsync<IOException>(async () => await store.AppendIfNewAsync(Observation("second"), CancellationToken.None));
            AssertFiles(before);
            Assert.That(await store.AppendIfNewAsync(Observation("second"), CancellationToken.None), Is.True);
        }

        /// <summary>Checks identical bytes across cultures and a single NDJSON line despite embedded newlines.</summary>
        [Test]
        public async Task CanonicalBytesIgnoreCurrentCultureAndEscapeEmbeddedNewlines()
        {
            var otherPaths = new AnalyzerStorePaths(Path.Combine(_temporary, "OtherProject"));
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                await new ObservationStore(_paths).AppendIfNewAsync(Observation(), CancellationToken.None);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                await new ObservationStore(otherPaths).AppendIfNewAsync(Observation(), CancellationToken.None);
                Assert.That(File.ReadAllBytes(otherPaths.ObservationPath(Observation().Scope)),
                    Is.EqualTo(File.ReadAllBytes(_paths.ObservationPath(Observation().Scope))));
                Assert.That(File.ReadAllLines(_paths.ObservationPath(Observation().Scope)).Length, Is.EqualTo(1));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        /// <summary>Rejects unsafe in-project overrides before any project directory is created.</summary>
        [TestCase("Assets")]
        [TestCase("Assets/../Packages/data")]
        [TestCase("ProjectSettings/data")]
        [TestCase(".git/data")]
        [TestCase("Library/other")]
        public void InProjectOverridesAreRejectedWithoutWriting(string relative)
        {
            Assert.Throws<ArgumentException>(() => new AnalyzerStorePaths(_paths.ProjectRoot,
                Path.Combine(_paths.ProjectRoot, relative)));
            Assert.That(Directory.Exists(_paths.ProjectRoot), Is.False);
        }

        /// <summary>Checks the default root and project-external override use digest-only observation filenames.</summary>
        [Test]
        public async Task DefaultRootAndExternalOverrideUseOnlyHashedObservationNames()
        {
            Assert.That(_paths.RootDirectory, Is.EqualTo(Path.GetFullPath(Path.Combine(_paths.ProjectRoot, "Library", "TftAnalyzerStore"))));
            var external = new AnalyzerStorePaths(_paths.ProjectRoot, Path.Combine(_temporary, "External"));
            await new ObservationStore(external).AppendIfNewAsync(Observation(), CancellationToken.None);
            var name = Path.GetFileName(external.ObservationPath(Observation().Scope));
            Assert.That(name, Does.Match("^[0-9a-f]{64}\\.ndjson$"));
        }

        /// <summary>Confirms a newly initialized store yields no observation records.</summary>
        [Test]
        public async Task EmptyStoreReadsNoObservations()
        {
            Assert.That(await ReadAll(new ObservationStore(_paths)), Is.Empty);
        }

        /// <summary>Appends during enumeration to verify locks are released and the original snapshot stays stable.</summary>
        [Test]
        public async Task EnumerationDoesNotHoldTheWriteGateAndKeepsItsOriginalSnapshot()
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            var count = 0;
            await foreach (var record in store.ReadAllAsync(CancellationToken.None))
            {
                count++;
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    Assert.That(await store.AppendIfNewAsync(Observation("second"), timeout.Token), Is.True);
            }
            Assert.That(count, Is.EqualTo(1));
            Assert.That((await ReadAll(store)).Count, Is.EqualTo(2));
        }

        /// <summary>Holds the OS lease to verify another instance fails safely and succeeds after release.</summary>
        [Test]
        public async Task CompetingOperationLeaseFailsWithoutChangingCommittedData()
        {
            var store = new ObservationStore(_paths);
            await store.AppendIfNewAsync(Observation(), CancellationToken.None);
            var before = Files();
            using (File.Open(_paths.LeasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await ThrowsAsync<IOException>(async () =>
                    await new ObservationStore(_paths).AppendIfNewAsync(Observation("second"), CancellationToken.None));
            AssertFiles(before);
            Assert.That(await store.AppendIfNewAsync(Observation("second"), CancellationToken.None), Is.True);
        }

        /// <summary>Rejects an encoded observation beyond the record limit without creating store files.</summary>
        [Test]
        public async Task OversizedRecordFailsBeforeCreatingStoreFiles()
        {
            var baseline = Observation();
            var oversized = new AnonymousMatchObservation(baseline.MatchHash, baseline.Scope, baseline.PlayedAt,
                new[] { new AnonymousParticipant(1, 30,
                    new[] { new AnonymousUnit(new string('X', 9 * 1024 * 1024), 1, null) }, null, null) });
            await ThrowsAsync<IOException>(async () =>
                await new ObservationStore(_paths).AppendIfNewAsync(oversized, CancellationToken.None));
            Assert.That(Directory.Exists(_paths.RootDirectory), Is.False);
        }

        /// <summary>Snapshots every store file's contents for preservation assertions.</summary>
        private Dictionary<string, byte[]> Files() => Directory.GetFiles(_paths.RootDirectory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);

        /// <summary>Asserts the store has the same file set and byte contents as the supplied snapshot.</summary>
        private void AssertFiles(Dictionary<string, byte[]> before)
        {
            Assert.That(Directory.GetFiles(_paths.RootDirectory, "*", SearchOption.AllDirectories), Is.EquivalentTo(before.Keys));
            foreach (var pair in before) Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value));
        }

        /// <summary>Materializes the real asynchronous store reader while preserving cancellation behavior.</summary>
        private static async Task<List<AnonymousMatchObservation>> ReadAll(ObservationStore store, CancellationToken token = default)
        {
            var records = new List<AnonymousMatchObservation>();
            await foreach (var record in store.ReadAllAsync(token)) records.Add(record);
            return records;
        }

        /// <summary>Asserts an awaited exception without blocking Unity's main-thread file-I/O continuations.</summary>
        private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try { await action(); }
            catch (T error) { return error; }
            Assert.Fail("Expected exception: " + typeof(T).Name);
            return null;
        }

        /// <summary>Builds a fabricated catalog identity with an optional changed scope dimension.</summary>
        private static CatalogIdentity Identity(string dimension = "") => new CatalogIdentity(
            new PatchKey(dimension == "set" ? "TFTSet19" : "TFTSet18", dimension == "set" ? 19 : 18,
                dimension == "patch" ? "18.3" : "18.2", dimension == "version" ? "16.18.2" : "16.18.1"),
            dimension == "profile" ? CatalogSourceProfile.OfficialPlusCommunityDragon : CatalogSourceProfile.OfficialOnly);

        /// <summary>Builds an eight-participant anonymous fixture with repeated items, Unicode and safe version metadata.</summary>
        private static AnonymousMatchObservation Observation(string seed = "first", string dimension = "") =>
            new AnonymousMatchObservation(ContentHasher.Sha256Hex("match-v1|" + seed),
                new MetricScope(Identity(dimension), dimension == "region" ? "europe" : "asia",
                    dimension == "queue" ? "1160" : "1100", dimension == "tier" ? "DIAMOND" : "MASTER+"),
                new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(9)),
                Enumerable.Range(1, 8).Select(place => new AnonymousParticipant(place, 30,
                    new[] { new AnonymousUnit("TEST_유닛\nI", 2, new[] { "TEST_ITEM", "TEST_ITEM" }) },
                    new[] { new AnonymousTrait("TEST_TRAIT", 2), new AnonymousTrait("INACTIVE", 0) },
                    new[] { "TEST_AUGMENT" })).ToArray(), "2", "16.18.123.456");
    }
}
