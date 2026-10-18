using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Analysis;

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
    }
}
