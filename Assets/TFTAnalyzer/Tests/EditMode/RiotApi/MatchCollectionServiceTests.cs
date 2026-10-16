using System;
using System.Collections.Generic;
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
using UnityEngine;

namespace TFTAnalyzer.Tests.RiotApi
{
    public sealed class MatchCollectionServiceTests
    {
        private string _root;
        private AnalyzerStorePaths _paths;
        private readonly List<CollectionProgress> _progress = new List<CollectionProgress>();

        /// <summary>Uses a unique disposable store and fabricated transport only.</summary>
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftCollectionTests-" + Guid.NewGuid().ToString("N"));
            _paths = new AnalyzerStorePaths(Path.Combine(_root, "Project"));
            _progress.Clear();
        }

        /// <summary>Deletes only this test's own randomly named directory.</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        /// <summary>Catches duplicate downloads, participant crawling and admission outside the selected queue.</summary>
        [Test]
        public async Task OverlappingSeedsFetchEachMatchOnceAndPersistOnlySelectedScope()
        {
            var transport = new Transport();
            transport.Ids = (seed, start) => seed == "seed-a" ? new[] { "M1", "M2" } : new[] { "M1", "M3" };
            transport.Body = id => Body(id, id == "M3" ? 1090 : 1100);
            var summary = await Service(transport, "seed-a,seed-b").CollectAsync(Request(), Progress(), default);
            Assert.That(summary.Requested, Is.EqualTo(3));
            Assert.That(summary.Fetched, Is.EqualTo(3));
            Assert.That(summary.Stored, Is.EqualTo(2));
            Assert.That(summary.Duplicate, Is.EqualTo(1));
            Assert.That(summary.Filtered, Is.EqualTo(1));
            Assert.That(transport.Details, Is.EqualTo(new[] { "M1", "M2", "M3" }));
            Assert.That(transport.Seeds, Is.EqualTo(new[] { "seed-a", "seed-b" }));
            Assert.That((await Observations()).Count, Is.EqualTo(2));
            Assert.That(transport.Streams.All(stream => stream.Disposed), Is.True);
            AssertSafe(summary);
            foreach (var value in _progress) AssertSafe(value);
            foreach (var file in Directory.GetFiles(_paths.RootDirectory, "*", SearchOption.AllDirectories))
                AssertSafe(File.ReadAllText(file));
        }

        /// <summary>Catches treating the limit as accepted matches instead of unique attempted downloads.</summary>
        [Test]
        public async Task UniqueMatchLimitStopsEvenWhenEveryMatchIsFiltered()
        {
            var transport = new Transport { Ids = (seed, start) => new[] { "M1", "M2", "M3" }, Body = id => Body(id, 1090) };
            var summary = await Service(transport).CollectAsync(Request(2), null, default);
            Assert.That(summary.Requested, Is.EqualTo(2));
            Assert.That(summary.Filtered, Is.EqualTo(2));
            Assert.That(transport.Details, Is.EqualTo(new[] { "M1", "M2" }));
        }

        /// <summary>Catches unbounded paging when a server repeatedly returns the same full page.</summary>
        [Test]
        public async Task RepeatedFullPagesStopAtGlobalScanBudget()
        {
            var transport = new Transport { Ids = (seed, start) => Enumerable.Repeat("M1", 100).ToArray() };
            var summary = await Service(transport).CollectAsync(Request(1000), null, default);
            Assert.That(transport.Starts, Is.EqualTo(Enumerable.Range(0, 10).Select(i => i * 100)));
            Assert.That(summary.IdPages, Is.EqualTo(10));
            Assert.That(summary.ScanLimitReached, Is.True);
            Assert.That(summary.Requested, Is.EqualTo(1));
            Assert.That(summary.Stored, Is.EqualTo(1));
        }

        /// <summary>Catches resetting the global page budget when moving to another configured seed.</summary>
        [Test]
        public async Task ShortPagesAcrossSeedsShareOneGlobalScanBudget()
        {
            var transport = new Transport();
            var seeds = string.Join(",", Enumerable.Range(0, 11).Select(i => "seed-" + i));
            var summary = await Service(transport, seeds).CollectAsync(Request(1000), null, default);
            Assert.That(transport.Seeds.Count, Is.EqualTo(10));
            Assert.That(summary.IdPages, Is.EqualTo(10));
            Assert.That(summary.ScanLimitReached, Is.True);
            Assert.That(summary.Stored, Is.EqualTo(1));
        }

        /// <summary>Catches ignored continuation offsets and incorrect termination on short ID pages.</summary>
        [Test]
        public async Task FullPageContinuesAndShortPageStops()
        {
            var transport = new Transport { Ids = (seed, start) => start == 0
                ? Enumerable.Repeat("M1", 100).ToArray() : new[] { "LAST" } };
            var summary = await Service(transport).CollectAsync(Request(101), null, default);
            Assert.That(transport.Starts, Is.EqualTo(new[] { 0, 100 }));
            Assert.That(summary.Stored, Is.EqualTo(2));
            Assert.That(summary.Requested, Is.EqualTo(2));
        }

        /// <summary>Catches accepting incompatible game releases, static IDs, malformed JSON or mismatched response IDs.</summary>
        [TestCase("release", 1, 0)]
        [TestCase("unknown", 1, 0)]
        [TestCase("malformed", 0, 1)]
        [TestCase("wrong-id", 0, 1)]
        public async Task UnsafeOrOutOfScopeResponsesNeverReachStore(string damage, int filtered, int failed)
        {
            var transport = new Transport { Body = id =>
            {
                if (damage == "malformed") return "{ FAKE-PUUID secret-key";
                var value = JObject.Parse(Body(damage == "wrong-id" ? "OTHER" : id));
                if (damage == "release") value["info"]["game_version"] = "16.19.123.456";
                if (damage == "unknown") value["info"]["participants"][0]["units"][0]["character_id"] = "FAKE-PUUID-unknown";
                return value.ToString();
            } };
            var summary = await Service(transport).CollectAsync(Request(), null, default);
            Assert.That(summary.Stored, Is.Zero);
            Assert.That(summary.Filtered, Is.EqualTo(filtered));
            Assert.That(summary.Failed, Is.EqualTo(failed));
            Assert.That(transport.Streams.All(stream => stream.Disposed), Is.True);
            Assert.That((await Observations()).Count, Is.Zero);
            AssertSafe(summary);
        }

        /// <summary>Catches loss of committed observations or requesting another match after progress-triggered cancellation.</summary>
        [Test]
        public async Task CancellationAfterCommitRetainsDataAndAllowsDeduplicatedResume()
        {
            using (var cancel = new CancellationTokenSource())
            {
                var transport = new Transport { Ids = (seed, start) => new[] { "M1", "M2" } };
                var service = Service(transport);
                var summary = await service.CollectAsync(Request(), new Reporter(value =>
                { if (value.Summary.Stored == 1) cancel.Cancel(); }), cancel.Token);
                Assert.That(summary.Cancelled, Is.True);
                Assert.That(summary.Stored, Is.EqualTo(1));
                Assert.That(transport.Details, Is.EqualTo(new[] { "M1" }));
                var resumed = await service.CollectAsync(Request(), null, default);
                Assert.That(resumed.Stored, Is.EqualTo(1));
                Assert.That(resumed.Duplicate, Is.EqualTo(1));
                Assert.That((await Observations()).Count, Is.EqualTo(2));
            }
        }

        /// <summary>Catches continuing parse or retaining the active response after cancellation.</summary>
        [Test]
        public async Task CancellationWhileReadingDisposesActiveStream()
        {
            using (var cancel = new CancellationTokenSource())
            {
                var transport = new Transport { OnDetailRead = () => cancel.Cancel() };
                var summary = await Service(transport).CollectAsync(Request(), null, cancel.Token);
                Assert.That(summary.Cancelled, Is.True);
                Assert.That(summary.Stored, Is.Zero);
                Assert.That(transport.Streams.All(stream => stream.Disposed), Is.True);
            }
        }

        /// <summary>Catches a pre-cancelled run reading seeds or making any network request.</summary>
        [Test]
        public async Task PreCancelledRunDoesNotReadSeedsOrSend()
        {
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                var transport = new Transport();
                var summary = await Service(transport, null, () => throw new Exception("seed-reader-called"))
                    .CollectAsync(Request(), null, cancel.Token);
                Assert.That(summary.Cancelled, Is.True);
                Assert.That(summary.Failed, Is.Zero);
                Assert.That(transport.Seeds, Is.Empty);
            }
        }

        /// <summary>Catches repeated authenticated calls after terminal authorization failures or rate limiting.</summary>
        [TestCase(401)]
        [TestCase(403)]
        [TestCase(429)]
        public async Task TerminalHttpStatusStopsRunAndRetainsSafeRetryTime(int status)
        {
            var transport = new Transport { Status = status };
            var summary = await Service(transport, "seed-a,seed-b").CollectAsync(Request(), Progress(), default);
            Assert.That(summary.Failed, Is.EqualTo(1));
            Assert.That(summary.StatusCode, Is.EqualTo(status));
            Assert.That(summary.RetryAfterUtc.HasValue, Is.EqualTo(status == 429));
            Assert.That(transport.Seeds.Count, Is.EqualTo(1));
            AssertSafe(summary);
        }

        /// <summary>Catches missing credentials causing transport work or escaping identifying errors.</summary>
        [Test]
        public async Task MissingKeyNeverContactsTransport()
        {
            var transport = new Transport();
            var summary = await Service(transport, key: () => null).CollectAsync(Request(), null, default);
            Assert.That(summary.Failed, Is.EqualTo(1));
            Assert.That(transport.Seeds, Is.Empty);
            AssertSafe(summary);
        }

        /// <summary>Catches continued collection after an uncertain index commit and loss of its recoverable observation.</summary>
        [Test]
        public async Task IndexFailureStopsFurtherDownloadsAndResumeRecoversCommittedObservation()
        {
            Directory.CreateDirectory(_paths.ProcessedMatchIndexPath);
            var transport = new Transport { Ids = (seed, start) => new[] { "M1", "M2" } };
            var service = Service(transport);
            var failed = await service.CollectAsync(Request(), null, default);
            Assert.That(failed.Failed, Is.EqualTo(1));
            Assert.That(transport.Details, Is.EqualTo(new[] { "M1" }));
            Assert.That(Directory.GetFiles(_paths.ObservationsDirectory, "*.ndjson")
                .Sum(file => File.ReadAllLines(file).Length), Is.EqualTo(1));
            Directory.Delete(_paths.ProcessedMatchIndexPath);
            var resumed = await service.CollectAsync(Request(), null, default);
            Assert.That(resumed.Duplicate, Is.EqualTo(1));
            Assert.That(resumed.Stored, Is.EqualTo(1));
            Assert.That((await Observations()).Count, Is.EqualTo(2));
            AssertSafe(failed);
        }

        /// <summary>Catches observer exceptions escaping or stopping otherwise valid durable collection.</summary>
        [Test]
        public async Task ProgressObserverFailureDoesNotUndoOrAbortCollection()
        {
            var summary = await Service(new Transport()).CollectAsync(Request(),
                new Reporter(value => throw new Exception("FAKE-PUUID secret-key")), default);
            Assert.That(summary.Stored, Is.EqualTo(1));
            Assert.That(summary.Failed, Is.Zero);
            Assert.That((await Observations()).Count, Is.EqualTo(1));
            AssertSafe(summary);
        }

        /// <summary>Catches unbounded requests or untrusted labels entering progress and observation scopes.</summary>
        [TestCase(0)]
        [TestCase(1001)]
        public void RequestRejectsInvalidLimits(int limit)
            => Assert.Throws<ArgumentOutOfRangeException>(() => Request(limit));

        /// <summary>Catches selecting a different TFT patch sharing the same source release.</summary>
        [Test]
        public void CatalogMismatchFailsBeforeCollection()
        {
            var transport = new Transport();
            var request = new MatchCollectionRequest("KR", Identity("18.4"), 1100, "MASTER+", 5);
            Assert.ThrowsAsync<ArgumentException>(async () => await Service(transport).CollectAsync(request, null, default));
            Assert.That(transport.Seeds, Is.Empty);
        }

        /// <summary>Catches saving arbitrary tier labels that might contain identity values.</summary>
        [TestCase("FAKE-PUUID")]
        [TestCase("")]
        public void RequestRejectsUnknownTier(string tier)
            => Assert.Throws<ArgumentException>(() => new MatchCollectionRequest("KR", Identity(), 1100, tier, 1));

        /// <summary>Catches missing, excessive or control-bearing seed input without exposing its contents.</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("seed-a,\nsecret-key")]
        public async Task InvalidSeedsFailSafelyBeforeTransport(string input)
        {
            var transport = new Transport();
            var summary = await Service(transport, input).CollectAsync(Request(), null, default);
            Assert.That(summary.Failed, Is.EqualTo(1));
            Assert.That(transport.Seeds, Is.Empty);
            AssertSafe(summary);
        }

        /// <summary>Checks an authorized environment only when the user explicitly selects this network test.</summary>
        [Test, Explicit("Calls one configured user-supplied seed; never runs in the default suite")]
        public async Task ConfiguredSeedCanCallOneIdPage()
        {
            if (!new EnvironmentRiotApiKeyProvider().IsConfigured) Assert.Ignore("API key is not configured.");
            IReadOnlyList<string> seeds = null;
            try { seeds = new EnvironmentSeedPuuidProvider().GetSeeds(); }
            catch { Assert.Ignore("Seed identifiers are not configured or valid."); }
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                Assert.That(await new RiotApiClient().GetMatchIdsAsync("asia", seeds[0], 0, 1, timeout.Token), Is.Not.Null);
        }

        /// <summary>Builds the real client, anonymizer and store with an offline HTTP boundary.</summary>
        private MatchCollectionService Service(Transport transport, string seeds = "seed-a", Func<string> seedRead = null,
            Func<string> key = null)
            => new MatchCollectionService(Catalog(), new ObservationStore(_paths),
                new RiotApiClient(new EnvironmentRiotApiKeyProvider(key ?? (() => "secret-key")), transport),
                new EnvironmentSeedPuuidProvider(seedRead ?? (() => seeds)));
        /// <summary>Captures synchronous progress snapshots without scheduling races.</summary>
        private IProgress<CollectionProgress> Progress() => new Reporter(value => _progress.Add(value));
        /// <summary>Selects a fully identified fabricated catalog and bounded scope.</summary>
        private static MatchCollectionRequest Request(int limit = 100)
            => new MatchCollectionRequest("KR", Identity(), 1100, "MASTER+", limit);
        /// <summary>Builds the same explicit identity used by the fixture's source release.</summary>
        private static CatalogIdentity Identity(string patch = "18.2")
            => new CatalogIdentity(new PatchKey("TFTSet18", 18, patch, "16.18.1"), CatalogSourceProfile.OfficialOnly);
        /// <summary>Defines only the static records referenced by fabricated matches.</summary>
        private static StaticCatalog Catalog() => new StaticCatalog(
            new PatchManifest(2, Identity(), "Test", DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, "", ""),
            new[] { new UnitRecord("TEST_UNIT", null, 1, null, "", null) },
            new[] { new TraitRecord("TEST_TRAIT", null, null, null, "", null) },
            new[] { new ItemRecord("TEST_ITEM", null, null, null, null, "", null) },
            new[] { new AugmentRecord("TEST_AUGMENT", null, "", null, null, null, "", null) });
        /// <summary>Changes only the fabricated match's identifying request key and queue.</summary>
        private static string Body(string id, int queue = 1100)
        {
            var value = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,
                "TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json")));
            value["metadata"]["match_id"] = id;
            value["info"]["queue_id"] = queue;
            return value.ToString();
        }
        /// <summary>Reads the actual store after collection or recovery.</summary>
        private async Task<List<AnonymousMatchObservation>> Observations()
        {
            var result = new List<AnonymousMatchObservation>();
            await foreach (var value in new ObservationStore(_paths).ReadAllAsync(default)) result.Add(value);
            return result;
        }
        /// <summary>Checks output and disk for fabricated sensitive markers.</summary>
        private static void AssertSafe(object value)
        {
            var text = value as string ?? JsonConvert.SerializeObject(value);
            foreach (var forbidden in new[] { "seed-a", "seed-b", "secret-key", "FAKE-PUUID", "FakePlayer", "FAKE-TAG", "FAKE-BUILD-NOTE", "M1", "M2", "M3" })
                Assert.That(text, Does.Not.Contain(forbidden));
        }
        private sealed class Reporter : IProgress<CollectionProgress>
        {
            private readonly Action<CollectionProgress> _report;
            /// <summary>Accepts the test's synchronous numeric snapshot consumer.</summary>
            internal Reporter(Action<CollectionProgress> report) { _report = report; }
            /// <summary>Delivers one progress event immediately.</summary>
            public void Report(CollectionProgress value) => _report(value);
        }
        private sealed class Transport : IRiotApiTransport
        {
            internal Func<string, int, string[]> Ids = (seed, start) => new[] { "M1" };
            internal Func<string, string> Body = id => MatchCollectionServiceTests.Body(id);
            internal Action OnDetailRead;
            internal int Status = 200;
            internal readonly List<string> Seeds = new List<string>();
            internal readonly List<int> Starts = new List<int>();
            internal readonly List<string> Details = new List<string>();
            internal readonly List<TrackedStream> Streams = new List<TrackedStream>();
            /// <summary>Returns fabricated HTTP responses while tracking the real service's requests.</summary>
            public Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                var isIds = uri.AbsolutePath.Contains("/by-puuid/");
                string body;
                if (isIds)
                {
                    var seed = Uri.UnescapeDataString(uri.Segments[uri.Segments.Length - 2].TrimEnd('/'));
                    var start = int.Parse(uri.Query.Split('&')[0].Split('=')[1]);
                    Seeds.Add(seed); Starts.Add(start);
                    body = JsonConvert.SerializeObject(Ids(seed, start));
                }
                else
                {
                    var id = Uri.UnescapeDataString(uri.Segments.Last());
                    Details.Add(id); body = Body(id);
                }
                var stream = new TrackedStream(body, isIds ? null : OnDetailRead);
                Streams.Add(stream);
                return Task.FromResult(new RiotApiResponse(Status, stream,
                    new Dictionary<string, string> { ["Retry-After"] = "30" }));
            }
        }
        private sealed class TrackedStream : MemoryStream
        {
            private Action _onRead;
            internal bool Disposed;
            /// <summary>Creates a bounded in-memory response with optional cancellation during parsing.</summary>
            internal TrackedStream(string body, Action onRead) : base(Encoding.UTF8.GetBytes(body), false) { _onRead = onRead; }
            /// <summary>Triggers the cancellation probe only on the first read.</summary>
            public override int Read(byte[] buffer, int offset, int count)
            {
                var callback = _onRead; _onRead = null; callback?.Invoke();
                return base.Read(buffer, offset, count);
            }
            /// <summary>Records ownership release even when parsing is interrupted.</summary>
            protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        }
    }
}
