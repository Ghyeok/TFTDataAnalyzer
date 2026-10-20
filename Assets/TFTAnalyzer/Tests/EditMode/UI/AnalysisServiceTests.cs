using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Compliance;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.Compliance;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class AnalysisServiceTests
    {
        private string _temporary;
        private AnalyzerStorePaths _paths;
        [SetUp]
        public void SetUp()
        {
            _temporary = Path.Combine(Path.GetTempPath(), "TftAnalysisTests-" + Guid.NewGuid().ToString("N"));
            _paths = new AnalyzerStorePaths(Path.Combine(_temporary, "Project"));
        }
        [TearDown]
        public void TearDown() { if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true); }

        [Test]
        public async Task ExplicitRecoveryBuildPolicyFilterCsvUsesOneCoherentAnonymousSnapshot()
        {
            var observations = AnalysisFixture.Observations(2);
            var store = new ObservationStore(_paths);
            foreach (var observation in observations) await store.AppendIfNewAsync(observation, CancellationToken.None);
            var before = File.ReadAllBytes(_paths.ObservationPath(observations[0].Scope));
            var service = new AnalysisService(store, new AggregateStore(_paths));
            using (var vm = new AnalysisViewModel(service))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                Assert.That(File.Exists(_paths.AggregatePath(observations[0].Scope)), Is.False);
                await vm.RefreshAsync();
                Assert.That(vm.ErrorCode, Is.Empty); Assert.That(vm.Rows.Count, Is.EqualTo(8));
                Assert.That(File.Exists(_paths.AggregatePath(observations[0].Scope)), Is.True);
                vm.SetFilters(new AnalysisFilters(unit: "A", trait: "ACTIVE_A", item: "ITEM_A"));
                Assert.That(vm.Rows.Count, Is.EqualTo(1));
                var export = Path.Combine(_temporary, "개인연구.csv");
                new AnalysisCsvExporter(_paths.ProjectRoot).Save(export, vm.Rows.Concat(vm.Pairs).Select(x => x.Presentation));
                var csv = File.ReadAllText(export);
                Assert.That(csv, Does.Contain("InsufficientSample"));
                Assert.That(csv, Does.Not.Contain("relative_placement_advantage"));
                foreach (var observation in observations) Assert.That(csv, Does.Not.Contain(observation.MatchHash));
                Assert.That(File.ReadAllBytes(_paths.ObservationPath(observations[0].Scope)), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_temporary, "*.tmp", SearchOption.AllDirectories), Is.Empty);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CorruptOrDisallowedScopeFailsWithoutPartialPublication(bool invalidScope)
        {
            var store = new ObservationStore(_paths); var observation = AnalysisFixture.Observations(1)[0];
            if (invalidScope) observation = new AnonymousMatchObservation(observation.MatchHash,
                new MetricScope(observation.Scope.ActiveCatalog, "not-a-region", "1100", "ALL"), observation.PlayedAt, observation.Participants);
            await store.AppendIfNewAsync(observation, CancellationToken.None);
            if (!invalidScope) File.AppendAllText(_paths.ObservationPath(observation.Scope), "corrupt complete record\n");
            using (var vm = new AnalysisViewModel(new AnalysisService(store, new AggregateStore(_paths))))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity()); await vm.RefreshAsync();
                Assert.That(vm.ErrorCode, Is.EqualTo("analysis_load_failed"));
                Assert.That(vm.Snapshot, Is.Null); Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Pairs, Is.Empty);
                Assert.That(vm.CanExport, Is.False); Assert.That(vm.CanRefresh, Is.True);
                Assert.That(File.Exists(_paths.AggregatePath(observation.Scope)), Is.False);
            }
        }

        [Test]
        public async Task ReviewedPolicyFlowsFromPinnedFileThroughStoreToPresentationAndCsv()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(AugmentPolicyFixture.Document().ToString(Newtonsoft.Json.Formatting.None));
            var directory = Path.Combine(_paths.ProjectRoot, "Assets", "TFTAnalyzer", "Data", "Policy"); Directory.CreateDirectory(directory);
            var document = Path.Combine(directory, ContentHasher.Sha256Hex(bytes) + ".json"); File.WriteAllBytes(document, bytes);
            var observations = AnalysisFixture.Observations(100, "AUG"); var store = new ObservationStore(_paths);
            foreach (var observation in observations) await store.AppendIfNewAsync(observation, CancellationToken.None);
            var before = File.ReadAllBytes(_paths.ObservationPath(observations[0].Scope));
            var provider = new ReviewedAugmentPolicyProvider(_paths.ProjectRoot, new[] { AugmentPolicyFixture.Registration(bytes) });
            var service = new AnalysisService(store, new AggregateStore(_paths), provider: provider);
            var snapshot = await service.LoadAsync(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
            var row = snapshot.AllRows.Single(x => x.First.UnitIds.Contains("A"));
            Assert.That(row.Presentation.State, Is.EqualTo(MetricVisibility.Visible));
            Assert.That(row.Presentation.SampleCount, Is.EqualTo(100));
            var export = Path.Combine(_temporary, "reviewed.csv");
            new AnalysisCsvExporter(_paths.ProjectRoot).Save(export, new[] { row.Presentation });
            Assert.That(File.ReadAllText(export), Does.Contain("top_four_rate"));
            Assert.That(File.ReadAllBytes(document), Is.EqualTo(bytes));
            Assert.That(File.ReadAllBytes(_paths.ObservationPath(observations[0].Scope)), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_temporary, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public async Task MissingInvalidAndOtherCatalogNeverReuseAnEarlierReviewedPolicy()
        {
            var store = new ObservationStore(_paths); var first = AnalysisFixture.Observations(1, "AUG");
            var secondIdentity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.3", "16.18.2"), CatalogSourceProfile.OfficialOnly);
            foreach (var observation in first)
            {
                await store.AppendIfNewAsync(observation, CancellationToken.None);
                await store.AppendIfNewAsync(new AnonymousMatchObservation(ContentHasher.Sha256Hex("second|" + observation.MatchHash),
                    new MetricScope(secondIdentity, "asia", "1100", "ALL"), observation.PlayedAt, observation.Participants), CancellationToken.None);
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(AugmentPolicyFixture.Document().ToString(Newtonsoft.Json.Formatting.None));
            var inner = AugmentPolicyFixture.Provider(_paths.ProjectRoot, bytes);
            var calls = 0;
            var provider = new CallbackProvider((catalog, identity, token) => { calls++; return inner.Load(catalog, identity, token); });
            var service = new AnalysisService(store, provider: provider);
            var a = await service.LoadAsync(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
            var b = await service.LoadAsync(AugmentPolicyFixture.Catalog(secondIdentity), secondIdentity, CancellationToken.None);
            Assert.That(a.AllRows.Single(x => x.First.UnitIds.Contains("A")).Presentation.State, Is.EqualTo(MetricVisibility.InsufficientSample));
            Assert.That(b.AllRows.Single(x => x.First.UnitIds.Contains("A")).Presentation.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(calls, Is.EqualTo(2));
            foreach (var read in new[] { AugmentPolicyLoadResult.Missing, AugmentPolicyLoadResult.Invalid })
            {
                service = new AnalysisService(store, provider: new CallbackProvider((catalog, identity, token) => read));
                var snapshot = await service.LoadAsync(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
                var presentation = snapshot.AllRows.Single(x => x.First.UnitIds.Contains("A")).Presentation;
                Assert.That(presentation.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
                Assert.That(presentation.ReasonCode, Is.EqualTo("unclassified_augment"));
                Assert.That(presentation.Ratios, Is.Empty);
            }
        }

        [Test]
        public async Task ProviderCancellationKeepsOwnedOperationAndDoesNotPublishOrWriteAggregates()
        {
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var release = new ManualResetEventSlim())
            using (var cancellation = new CancellationTokenSource())
            {
                var calls = 0;
                var provider = new CallbackProvider((catalog, identity, token) =>
                {
                    calls++; Assert.That(identity, Is.SameAs(AnalysisFixtureIdentity));
                    entered.TrySetResult(true); release.Wait(TimeSpan.FromSeconds(10), token); token.ThrowIfCancellationRequested();
                    return AugmentPolicyLoadResult.Missing;
                });
                var service = new AnalysisService(new ObservationStore(_paths), new AggregateStore(_paths), provider: provider);
                var pending = service.LoadAsync(AugmentPolicyFixture.Catalog(), AnalysisFixtureIdentity, cancellation.Token);
                try
                {
                    Assert.That(await Task.WhenAny(entered.Task, Task.Delay(5000)), Is.SameAs(entered.Task));
                    cancellation.Cancel();
                    try { await pending; Assert.Fail("Cancellation must propagate."); }
                    catch (OperationCanceledException) { }
                    Assert.That(calls, Is.EqualTo(1));
                    Assert.That(Directory.Exists(Path.Combine(_paths.RootDirectory, "aggregates")), Is.False);
                }
                finally { cancellation.Cancel(); release.Set(); try { await pending; } catch (OperationCanceledException) { } }
            }
        }

        private static readonly CatalogIdentity AnalysisFixtureIdentity = AnalysisFixture.Identity();

        [Test]
        public async Task ExplicitPolicyInjectionRemainsCompatibleAndConflictingConfigurationIsRejected()
        {
            var store = new ObservationStore(_paths); var metadata = new MetricPolicyMetadata(AnalysisFixture.Identity(),
                new System.Collections.Generic.Dictionary<string, AugmentPolicyKind> { ["AUG"] = AugmentPolicyKind.Standard });
            foreach (var observation in AnalysisFixture.Observations(1, "AUG")) await store.AppendIfNewAsync(observation, CancellationToken.None);
            var policy = new MetricPolicy(100, metadata);
            var service = new AnalysisService(store, policy: policy);
            Assert.That((await service.LoadAsync(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None)).AllRows
                .Single(x => x.First.UnitIds.Contains("A")).Presentation.State, Is.EqualTo(MetricVisibility.InsufficientSample));
            Assert.Throws<ArgumentException>(() => new AnalysisService(store, policy: policy, provider: new ReviewedAugmentPolicyProvider(_paths.ProjectRoot)));
        }

        private sealed class CallbackProvider : IAugmentPolicyMetadataProvider
        {
            private readonly Func<StaticCatalog, CatalogIdentity, CancellationToken, AugmentPolicyLoadResult> _read;
            internal CallbackProvider(Func<StaticCatalog, CatalogIdentity, CancellationToken, AugmentPolicyLoadResult> read) { _read = read; }
            public AugmentPolicyLoadResult Load(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token) => _read(catalog, identity, token);
        }
    }
}
