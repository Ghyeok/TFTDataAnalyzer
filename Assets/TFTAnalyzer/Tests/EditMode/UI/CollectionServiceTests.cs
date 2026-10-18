using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI.Collection;
using UnityEngine;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class CollectionServiceTests
    {
        private string _root;
        private AnalyzerStorePaths _paths;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftCollectionAdapter-" + Guid.NewGuid().ToString("N"));
            _paths = new AnalyzerStorePaths(Path.Combine(_root, "Project"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ApprovedVmUsesRealAdapterAnonymizerAndStoreWithOfflineTransport(bool legacy)
        {
            var keyReads = 0;
            var seedReads = 0;
            var keys = new EnvironmentRiotApiKeyProvider(() => { keyReads++; return "fabricated-api-key"; });
            var seeds = new EnvironmentSeedPuuidProvider(() => { seedReads++; return "fabricated-seed"; });
            var transport = new OfflineTransport { ReturnMatch = true };
            var service = new CollectionService(new ObservationStore(_paths), keys, seeds,
                new RiotApiClient(keys, transport));
            Assert.That(keyReads, Is.Zero, "Constructing the adapter must not read credentials.");
            Assert.That(seedReads, Is.Zero);
            Assert.That(transport.Sends, Is.Zero);
            using (var vm = new CollectionViewModel(service))
            {
                var identity = DataManagerTestSupport.Identity();
                vm.SetCatalog(Catalog(legacy), legacy ? identity : null);
                vm.TrySetInput("KR", 1100, "ALL", 1);
                Assert.That(vm.ApiKeyConfigured, Is.True);
                Assert.That(vm.SeedConfigured, Is.True);
                Assert.That(transport.Sends, Is.Zero);
                await vm.StartAsync(summary => false);
                Assert.That(transport.Sends, Is.Zero);
                await vm.StartAsync(summary => true);
                Assert.That(vm.State, Is.EqualTo(CollectionState.Succeeded));
                Assert.That(vm.Progress.Requested, Is.EqualTo(1));
                Assert.That(vm.Progress.Fetched, Is.EqualTo(1));
                Assert.That(vm.Progress.Stored, Is.EqualTo(1));
                Assert.That(transport.Sends, Is.EqualTo(2));
                await vm.StartAsync(summary => true);
                Assert.That(vm.Progress.Stored, Is.Zero);
                Assert.That(vm.Progress.Duplicate, Is.EqualTo(1));
                var observations = new List<TFTAnalyzer.Analytics.AnonymousMatchObservation>();
                await foreach (var value in new ObservationStore(_paths).ReadAllAsync(default)) observations.Add(value);
                Assert.That(observations, Has.Count.EqualTo(1));
                Assert.That(observations[0].Scope.ActiveCatalog, Is.EqualTo(identity));
                foreach (var file in Directory.GetFiles(_paths.RootDirectory, "*", SearchOption.AllDirectories))
                {
                    var text = File.ReadAllText(file);
                    foreach (var secret in new[] { "fabricated-api-key", "fabricated-seed", "FAKE-PUUID", "FakePlayer", "KR1_123456" })
                        Assert.That(text, Does.Not.Contain(secret));
                }
            }
        }

        [Test]
        public async Task ManualRetryRetainsOneClientsQuotaStateAcrossCatalogRuns()
        {
            var now = new DateTimeOffset(2026, 10, 16, 5, 0, 0, TimeSpan.Zero);
            var transport = new OfflineTransport { Status = 429 };
            var keys = new EnvironmentRiotApiKeyProvider(() => "fabricated-api-key");
            var client = new RiotApiClient(keys, transport, utcNow: () => now);
            var service = new CollectionService(new ObservationStore(_paths), keys,
                new EnvironmentSeedPuuidProvider(() => "fabricated-seed"), client);
            using (var vm = new CollectionViewModel(service))
            {
                vm.SetCatalog(Catalog());
                await vm.StartAsync(summary => true);
                Assert.That(vm.ErrorCode, Is.EqualTo("RateLimited"));
                Assert.That(vm.Progress.RetryAfterUtc, Is.EqualTo(now.AddSeconds(30)));
                Assert.That(transport.Sends, Is.EqualTo(1));
                Assert.That(vm.IsBusy, Is.False);
                transport.Status = 200;
                vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity("16.18.2")));
                await vm.StartAsync(summary => true);
                Assert.That(vm.ErrorCode, Is.EqualTo("RateLimited"));
                Assert.That(transport.Sends, Is.EqualTo(1), "Manual retry before eligibility must not reset client quotas.");
                now = now.AddSeconds(31);
                await vm.StartAsync(summary => true);
                Assert.That(vm.State, Is.EqualTo(CollectionState.Succeeded));
                Assert.That(transport.Sends, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task BoundedClientRetriesFinishWithSafeManualRetryGuidance()
        {
            var transport = new OfflineTransport { Status = 503 };
            var keys = new EnvironmentRiotApiKeyProvider(() => "fabricated-api-key");
            var delays = new List<TimeSpan>();
            var service = new CollectionService(new ObservationStore(_paths), keys,
                new EnvironmentSeedPuuidProvider(() => "fabricated-seed"),
                new RiotApiClient(keys, transport, delay: (duration, token) => { delays.Add(duration); return Task.CompletedTask; }));
            using (var vm = new CollectionViewModel(service))
            {
                vm.SetCatalog(Catalog());
                await vm.StartAsync(summary => true);
                Assert.That(transport.Sends, Is.EqualTo(4));
                Assert.That(delays.Select(value => value.TotalSeconds), Is.EqualTo(new[] { 1d, 2d, 4d }));
                Assert.That(vm.State, Is.EqualTo(CollectionState.Failed));
                Assert.That(vm.ErrorCode, Is.EqualTo("PartialFailure"));
                Assert.That(vm.StatusText, Does.Contain("수동").And.Not.Contain("fabricated-api-key")
                    .And.Not.Contain("fabricated-seed"));
                Assert.That(vm.IsBusy, Is.False);
            }
        }

        [Test]
        public void ArbitraryProviderFailuresBecomeOnlyUnavailableFlags()
        {
            var service = new CollectionService(new ObservationStore(_paths), new FaultingKeyProvider(),
                new EnvironmentSeedPuuidProvider(() => throw new Exception("fabricated-seed")));
            Assert.That(service.ApiKeyConfigured, Is.False);
            Assert.That(service.SeedConfigured, Is.False);
        }

        private static StaticCatalog Catalog(bool legacy = false) => new StaticCatalog(
            legacy ? new PatchManifest(1, DataManagerTestSupport.Identity().Patch, "Test", DateTime.UnixEpoch, "", null,
                DateTime.UnixEpoch, null, 0, 0, "", "") :
                new PatchManifest(2, DataManagerTestSupport.Identity(), "Test", DateTime.UnixEpoch, "", null,
                    DateTime.UnixEpoch, null, 0, 0, "", ""),
            new[] { new UnitRecord("TEST_UNIT", null, 1, null, "", null) },
            new[] { new TraitRecord("TEST_TRAIT", null, null, null, "", null) },
            new[] { new ItemRecord("TEST_ITEM", null, null, null, null, "", null) },
            new[] { new AugmentRecord("TEST_AUGMENT", null, "", null, null, null, "", null) });

        private sealed class FaultingKeyProvider : IRiotApiKeyProvider
        {
            public bool IsConfigured => throw new InvalidOperationException("fabricated-api-key");
            public string GetRequiredKey() => throw new InvalidOperationException("fabricated-api-key");
        }

        private sealed class OfflineTransport : IRiotApiTransport
        {
            internal int Status = 200;
            internal bool ReturnMatch;
            internal int Sends;
            public Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers,
                CancellationToken token)
            {
                Sends++;
                token.ThrowIfCancellationRequested();
                var body = "[]";
                if (ReturnMatch)
                    body = uri.AbsolutePath.Contains("/by-puuid/") ? "[\"KR1_123456\"]" : File.ReadAllText(
                        Path.Combine(Application.dataPath, "TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json"));
                return Task.FromResult(new RiotApiResponse(Status, new MemoryStream(Encoding.UTF8.GetBytes(body)),
                    new Dictionary<string, string> { ["Retry-After"] = "30" }));
            }
        }
    }
}
