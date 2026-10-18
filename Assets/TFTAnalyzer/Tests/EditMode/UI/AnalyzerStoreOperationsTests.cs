using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Editor.UI.Collection;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class AnalyzerStoreOperationsTests
    {
        private string _temporary;
        private AnalyzerStorePaths _paths;
        [SetUp]
        public void SetUp()
        {
            _temporary = Path.Combine(Path.GetTempPath(), "TftStoreOperations-" + Guid.NewGuid().ToString("N"));
            _paths = new AnalyzerStorePaths(Path.Combine(_temporary, "Project"));
        }
        [TearDown]
        public void TearDown() { if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true); }

        [Test]
        public async Task SecondOperationCannotStartUntilFirstActuallySettles()
        {
            var coordinator = new AnalyzerStoreOperations();
            var release = new TaskCompletionSource<int>();
            var first = coordinator.RunAsync(() => release.Task, CancellationToken.None);
            var entered = false;
            var second = coordinator.RunAsync(() => { entered = true; return Task.FromResult(2); }, CancellationToken.None);
            try { Assert.That(entered, Is.False); Assert.That(second.IsCompleted, Is.False); }
            finally { release.TrySetResult(1); await first; await second; }
            Assert.That(entered, Is.True);
        }

        [Test]
        public async Task CancellingActiveOwnerDoesNotReleaseItBeforeItsWorkActuallyFinishes()
        {
            var coordinator = new AnalyzerStoreOperations();
            var release = new TaskCompletionSource<int>();
            using (var cancellation = new CancellationTokenSource())
            {
                var first = coordinator.RunAsync(() => release.Task, cancellation.Token);
                cancellation.Cancel();
                var entered = false;
                var next = coordinator.RunAsync(() => { entered = true; return Task.FromResult(2); }, CancellationToken.None);
                try { Assert.That(entered, Is.False); Assert.That(next.IsCompleted, Is.False); }
                finally { release.TrySetResult(1); await first; await next; }
                Assert.That(entered, Is.True);
            }
        }

        [Test]
        public async Task CancelledWaiterDoesNotRunAndFailureReleasesTheNextOperation()
        {
            var coordinator = new AnalyzerStoreOperations();
            var release = new TaskCompletionSource<int>();
            var first = coordinator.RunAsync(() => release.Task, CancellationToken.None);
            using (var cancellation = new CancellationTokenSource())
            {
                var entered = false;
                var queued = coordinator.RunAsync(() => { entered = true; return Task.FromResult(2); }, cancellation.Token);
                cancellation.Cancel();
                try
                {
                    try { await queued; Assert.Fail("Cancelled waiting operation must not run."); }
                    catch (OperationCanceledException) { }
                    Assert.That(entered, Is.False);
                }
                finally { release.TrySetResult(1); await first; }
            }
            try { await coordinator.RunAsync<int>(() => throw new IOException("synthetic"), CancellationToken.None); Assert.Fail(); }
            catch (IOException) { }
            Assert.That(await coordinator.RunAsync(() => Task.FromResult(3), CancellationToken.None), Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CollectionCannotSendWhileAnotherStoreOperationOwnsTheCoordinator(bool cancel)
        {
            var release = new TaskCompletionSource<int>();
            var owner = AnalyzerStoreOperations.Shared.RunAsync(() => release.Task, CancellationToken.None);
            var transport = new OfflineTransport();
            var keys = new EnvironmentRiotApiKeyProvider(() => "fabricated-key");
            var collection = new CollectionService(new ObservationStore(_paths), keys,
                new EnvironmentSeedPuuidProvider(() => "fabricated-seed"), new RiotApiClient(keys, transport));
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = collection.CollectAsync(AnalysisFixture.Catalog(),
                    new MatchCollectionRequest("KR", AnalysisFixture.Identity(), 1100, "ALL", 1), null, cancellation.Token);
                try
                {
                    Assert.That(transport.Sends, Is.Zero, "No HTTP or observation write may start ahead of the owner.");
                    if (cancel)
                    {
                        cancellation.Cancel();
                        try { await pending; Assert.Fail("Queued cancellation must be observed."); }
                        catch (OperationCanceledException) { }
                        Assert.That(transport.Sends, Is.Zero);
                    }
                }
                finally
                {
                    release.TrySetResult(1); await owner;
                    try { await pending; } catch (OperationCanceledException) { }
                }
                if (!cancel) { Assert.That((await pending).Failed, Is.Zero); Assert.That(transport.Sends, Is.EqualTo(1)); }
            }
        }

        [Test]
        public async Task CollectionStartsOnlyAfterAnalysisHasCommittedItsAggregateCache()
        {
            var observation = AnalysisFixture.Observations(1)[0];
            await new ObservationStore(_paths).AppendIfNewAsync(observation, CancellationToken.None);
            var cache = _paths.AggregatePath(observation.Scope);
            var transport = new OfflineTransport { CachePath = cache };
            var keys = new EnvironmentRiotApiKeyProvider(() => "fabricated-key");
            var analysis = new AnalysisService(new ObservationStore(_paths), new AggregateStore(_paths));
            var collection = new CollectionService(new ObservationStore(_paths), keys,
                new EnvironmentSeedPuuidProvider(() => "fabricated-seed"), new RiotApiClient(keys, transport));
            var refresh = analysis.LoadAsync(AnalysisFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
            var collect = collection.CollectAsync(AnalysisFixture.Catalog(),
                new MatchCollectionRequest("KR", AnalysisFixture.Identity(), 1100, "ALL", 1), null, CancellationToken.None);
            await Task.WhenAll(refresh, collect);
            Assert.That(transport.CachePresentAtSend, Is.True, "Collection must wait through aggregate SaveAsync, not only observation read.");
            Assert.That((await collect).Failed, Is.Zero);
            Assert.That((await refresh).AllRows.Count, Is.EqualTo(8));
            Assert.That(File.Exists(cache), Is.True);
        }

        private sealed class OfflineTransport : IRiotApiTransport
        {
            internal int Sends;
            internal string CachePath;
            internal bool CachePresentAtSend;
            public Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); Sends++;
                CachePresentAtSend = CachePath != null && File.Exists(CachePath);
                return Task.FromResult(new RiotApiResponse(200, new MemoryStream(Encoding.UTF8.GetBytes("[]")),
                    new Dictionary<string, string>()));
            }
        }
    }
}
