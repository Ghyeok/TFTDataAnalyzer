using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class AnalysisViewModelTests
    {
        [TestCase(99)]
        [TestCase(0)]
        [TestCase(-1)]
        public async Task InvalidMinimumClearsResultsAndBlocksReadsAndExport(int minimum)
        {
            var service = new AnalysisFixture.Service { Result = AnalysisFixture.Snapshot(100) };
            using (var vm = new AnalysisViewModel(service))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                await vm.RefreshAsync(); Assert.That(vm.CanExport, Is.True);
                vm.SetMinimumSamples(minimum);
                Assert.That(vm.MinimumSamples, Is.EqualTo(minimum));
                Assert.That(vm.Snapshot, Is.Null); Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Pairs, Is.Empty);
                Assert.That(vm.CanRefresh, Is.False); Assert.That(vm.CanExport, Is.False);
                await vm.RefreshAsync(); Assert.That(service.Calls, Is.EqualTo(1));
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                Assert.That(vm.MinimumSamples, Is.EqualTo(minimum));
                Assert.That(vm.StatusText, Does.Contain("100"));
            }
        }

        [TestCase(100)]
        [TestCase(200)]
        [TestCase(int.MaxValue)]
        public async Task RequestedMinimumIsCapturedAndStricterSnapshotIsAccepted(int minimum)
        {
            var service = new AnalysisFixture.Service { Result = AnalysisFixture.Snapshot(1, minimumSamples: Math.Max(250, minimum)) };
            using (var vm = new AnalysisViewModel(service))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                vm.SetMinimumSamples(minimum); await vm.RefreshAsync();
                Assert.That(service.MinimumSamples, Is.EqualTo(minimum));
                Assert.That(vm.ErrorCode, Is.Empty);
                Assert.That(vm.Snapshot.MinimumSamples, Is.EqualTo(Math.Max(250, minimum)));
                Assert.That(vm.CanExport, Is.True);
                vm.SetMinimumSamples(minimum);
                Assert.That(vm.Snapshot, Is.SameAs(service.Result), "The same input is idempotent.");
            }
        }

        [TestCase(200)]
        [TestCase(99)]
        public async Task ChangedMinimumCancelsAndDiscardsLateCompletion(int minimum)
        {
            var completion = new TaskCompletionSource<AnalysisSnapshot>();
            var service = new AnalysisFixture.Service { Pending = completion.Task };
            using (var vm = new AnalysisViewModel(service))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                var pending = vm.RefreshAsync();
                vm.SetMinimumSamples(100); Assert.That(service.Token.IsCancellationRequested, Is.False);
                vm.SetMinimumSamples(minimum); Assert.That(service.Token.IsCancellationRequested, Is.True);
                Assert.That(vm.IsBusy, Is.True);
                completion.SetResult(AnalysisFixture.Snapshot(100)); await pending;
                Assert.That(vm.Snapshot, Is.Null); Assert.That(vm.CanExport, Is.False);
                Assert.That(vm.CanRefresh, Is.EqualTo(minimum >= 100));
                Assert.That(vm.MinimumSamples, Is.EqualTo(minimum));
            }
        }

        [Test]
        public async Task SnapshotWithLowerMinimumCannotPublishRatiosForRaisedInput()
        {
            var service = new AnalysisFixture.Service { Result = AnalysisFixture.Snapshot(100) };
            using (var vm = new AnalysisViewModel(service))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                vm.SetMinimumSamples(200); await vm.RefreshAsync();
                Assert.That(vm.ErrorCode, Is.EqualTo("analysis_load_failed"));
                Assert.That(vm.Snapshot, Is.Null); Assert.That(vm.CanExport, Is.False);
            }
        }

        [TestCase(99, MetricVisibility.InsufficientSample)]
        [TestCase(100, MetricVisibility.Visible)]
        public void SnapshotUsesPolicyThresholdAndObservedTime(int samples, MetricVisibility expected)
        {
            var snapshot = AnalysisFixture.Snapshot(samples);
            var row = snapshot.AllRows.Single(x => x.First.UnitIds.Contains("A"));
            Assert.That(row.Presentation.State, Is.EqualTo(expected));
            Assert.That(row.Presentation.SampleCount, Is.EqualTo(samples));
            Assert.That(row.DisplayText, Does.Contain(samples.ToString()));
            Assert.That(row.RatioText.Contains("%"), Is.EqualTo(samples >= 100));
            if (samples < 100) Assert.That(row.StateText, Is.EqualTo("표본 부족"));
            else Assert.That(row.RatioText, Does.Contain("95%"));
            Assert.That(snapshot.FirstPlayedAt, Is.EqualTo(DateTimeOffset.UnixEpoch));
            Assert.That(snapshot.LastPlayedAt, Is.EqualTo(DateTimeOffset.UnixEpoch.AddDays(samples - 1)));
        }

        [Test]
        public void MissingAugmentClassificationFailsClosed()
        {
            var row = AnalysisFixture.Snapshot(100, "AUG").AllRows.Single(x => x.First.UnitIds.Contains("A"));
            Assert.That(row.Presentation.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(row.Presentation.Ratios, Is.Empty);
            Assert.That(row.RatioText, Does.Not.Contain("%"));
        }

        [Test]
        public async Task FiltersAreLocalAndPairsRequireOneCompleteEndpoint()
        {
            var service = new AnalysisFixture.Service { Result = AnalysisFixture.Snapshot(1) };
            using (var vm = new AnalysisViewModel(service))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                Assert.That(service.Calls, Is.Zero);
                await vm.RefreshAsync();
                vm.SetFilters(new AnalysisFilters(unit: "A", item: "ITEM_B"));
                Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Pairs, Is.Empty);
                vm.SetFilters(new AnalysisFilters(unit: "A", trait: "ACTIVE_A", item: "ITEM_A"));
                Assert.That(vm.Rows.Count, Is.EqualTo(1)); Assert.That(vm.Pairs.Count, Is.EqualTo(7));
                Assert.That(vm.Pairs.All(x => x.First.UnitIds.Contains("A") || x.Second.UnitIds.Contains("A")), Is.True);
                vm.SetFilters(new AnalysisFilters(trait: "INACTIVE_A"));
                Assert.That(vm.Rows, Is.Empty);
                Assert.That(service.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task CatalogSwitchCancelsAndDiscardsStaleCompletion()
        {
            var completion = new TaskCompletionSource<AnalysisSnapshot>();
            var service = new AnalysisFixture.Service { Pending = completion.Task };
            var vm = new AnalysisViewModel(service);
            vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
            var loading = vm.RefreshAsync();
            Assert.That(vm.IsBusy, Is.True);
            vm.SetCatalog(null);
            Assert.That(service.Token.IsCancellationRequested, Is.True);
            completion.SetResult(AnalysisFixture.Snapshot(1));
            await loading;
            Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Snapshot, Is.Null); Assert.That(vm.CanExport, Is.False);
            vm.Dispose();
        }

        [Test]
        public async Task FailureIsSafeAndCanBeRetried()
        {
            var service = new AnalysisFixture.Service { Failure = new IOException("secret observation payload") };
            using (var vm = new AnalysisViewModel(service))
            {
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                await vm.RefreshAsync();
                Assert.That(vm.ErrorCode, Is.EqualTo("analysis_load_failed"));
                Assert.That(vm.StatusText, Does.Not.Contain("secret"));
                Assert.That(vm.CanRefresh, Is.True);
                service.Failure = null; service.Result = AnalysisFixture.Snapshot(1);
                await vm.RefreshAsync();
                Assert.That(vm.Rows.Count, Is.EqualTo(8)); Assert.That(vm.CanExport, Is.True);
            }
        }

        [Test]
        public void LegacyRequiresVerifiedOfficialIdentityAndSetNumber()
        {
            var service = new AnalysisFixture.Service();
            using (var vm = new AnalysisViewModel(service))
            {
                var legacy = AnalysisFixture.Catalog(legacy: true);
                vm.SetCatalog(legacy); Assert.That(vm.CanRefresh, Is.False);
                vm.SetCatalog(legacy, AnalysisFixture.Identity()); Assert.That(vm.CanRefresh, Is.True);
                vm.SetCatalog(legacy, AnalysisFixture.Identity(number: 19)); Assert.That(vm.CanRefresh, Is.False);
                vm.SetCatalog(legacy, AnalysisFixture.Identity(profile: CatalogSourceProfile.OfficialPlusCommunityDragon));
                Assert.That(vm.CanRefresh, Is.False);
            }
        }

        [Test]
        public async Task CancelAndDisposeKeepBusyUntilSettledAndIgnoreLateResults()
        {
            foreach (var dispose in new[] { false, true })
            {
                var pending = new TaskCompletionSource<AnalysisSnapshot>();
                var service = new AnalysisFixture.Service { Pending = pending.Task };
                var vm = new AnalysisViewModel(service);
                vm.SetCatalog(AnalysisFixture.Catalog(), AnalysisFixture.Identity());
                var running = vm.RefreshAsync();
                var changes = 0; vm.Changed += () => changes++;
                if (dispose) vm.Dispose(); else vm.Cancel();
                var before = changes;
                Assert.That(service.Token.IsCancellationRequested, Is.True);
                Assert.That(vm.IsBusy, Is.True);
                await vm.RefreshAsync(); Assert.That(service.Calls, Is.EqualTo(1));
                pending.SetResult(AnalysisFixture.Snapshot(1)); await running;
                Assert.That(vm.Snapshot, Is.Null); Assert.That(vm.CanExport, Is.False);
                if (dispose) Assert.That(changes, Is.EqualTo(before));
                else { Assert.That(vm.IsBusy, Is.False); Assert.That(vm.CanRefresh, Is.True); }
                vm.Dispose();
            }
        }

        [Test]
        public void DistinctScopesNeverPoolAndWrongSetNumberIsExcluded()
        {
            var source = AnalysisFixture.Observations(60).Concat(AnalysisFixture.Observations(60, region: "europe")).ToList();
            var wrong = AnalysisFixture.Observations(1)[0];
            source.Add(new AnonymousMatchObservation(ContentHasher.Sha256Hex("wrong-number"),
                new MetricScope(AnalysisFixture.Identity(number: 19), "asia", "1100", "ALL"), wrong.PlayedAt, wrong.Participants));
            var snapshot = AnalysisService.BuildSnapshot(AnalysisFixture.Catalog(), AnalysisFixture.Identity(), source);
            var a = snapshot.AllRows.Where(x => x.First.UnitIds.Contains("A")).ToArray();
            Assert.That(a.Length, Is.EqualTo(2));
            Assert.That(a.All(x => x.Presentation.SampleCount == 60 && x.Presentation.State == MetricVisibility.InsufficientSample), Is.True);
            Assert.That(snapshot.AllRows.All(x => x.Presentation.Scope.ActiveCatalog.Patch.SetNumber == 18), Is.True);
            source.Clear(); Assert.That(snapshot.AllRows.Count, Is.EqualTo(16));
            Assert.That(snapshot.AllRows is IList<MetricRowViewModel> list && list.IsReadOnly, Is.True);
        }

        [TestCase(99)]
        [TestCase(100)]
        public void PairKeepsOriginalDirectionAndEligibleLobbyDenominator(int count)
        {
            var source = AnalysisFixture.Observations(count).ToList();
            var template = source[0];
            var duplicated = template.Participants.Select((p, i) => i == 2 ? new AnonymousParticipant(3, 30,
                template.Participants[0].Units, template.Participants[0].Traits, template.Participants[0].AugmentIds) : p).ToArray();
            source.Add(new AnonymousMatchObservation(ContentHasher.Sha256Hex("ambiguous"), template.Scope,
                template.PlayedAt.AddDays(200), duplicated));
            var snapshot = AnalysisService.BuildSnapshot(AnalysisFixture.Catalog(), AnalysisFixture.Identity(), source);
            var pair = snapshot.AllPairs.Single(x => x.First.UnitIds.Concat(x.Second.UnitIds).OrderBy(x => x).SequenceEqual(new[] { "A", "B" }));
            Assert.That(pair.Presentation.SampleCount, Is.EqualTo(count));
            Assert.That(pair.Presentation.AmbiguousLobbyCount, Is.EqualTo(1));
            Assert.That(pair.Presentation.FirstFingerprint, Is.EqualTo(pair.First.Fingerprint));
            Assert.That(pair.Presentation.SecondFingerprint, Is.EqualTo(pair.Second.Fingerprint));
            if (count == 99) Assert.That(pair.Presentation.Ratios, Is.Empty);
            else
            {
                var ratio = pair.Presentation.Ratios.Single();
                Assert.That(ratio.Denominator, Is.EqualTo(100));
                Assert.That(ratio.Numerator, Is.EqualTo(pair.First.UnitIds.Contains("A") ? 100 : 0));
                Assert.That(pair.RatioText, Does.Contain(MetricRowViewModel.PairwiseLabel));
            }
        }

        [Test]
        public void ReviewedLegendAndUnknownAugmentCannotRecoverAverageOrRatios()
        {
            var observations = AnalysisFixture.Observations(100, "AUG");
            var legend = new MetricPolicy(100, new MetricPolicyMetadata(AnalysisFixture.Identity(),
                new Dictionary<string, AugmentPolicyKind> { ["AUG"] = AugmentPolicyKind.Legend }));
            var row = AnalysisService.BuildSnapshot(AnalysisFixture.Catalog(), AnalysisFixture.Identity(), observations, legend)
                .AllRows.Single(x => x.First.UnitIds.Contains("A"));
            Assert.That(row.Presentation.AveragePlacement, Is.Null); Assert.That(row.Presentation.Ratios, Is.Empty);
            Assert.That(row.AverageText, Does.Contain("—"));
            var unknown = AnalysisService.BuildSnapshot(AnalysisFixture.Catalog(), AnalysisFixture.Identity(), AnalysisFixture.Observations(100, "UNKNOWN"))
                .AllRows.Single(x => x.First.UnitIds.Contains("A"));
            Assert.That(unknown.Presentation.AveragePlacement, Is.Null);
            Assert.That(unknown.Presentation.ReasonCode, Is.EqualTo("unknown_augment"));
            var raised = AnalysisService.BuildSnapshot(AnalysisFixture.Catalog(), AnalysisFixture.Identity(), AnalysisFixture.Observations(100), new MetricPolicy(200));
            Assert.That(raised.AllRows.All(x => x.Presentation.State == MetricVisibility.InsufficientSample), Is.True);
        }

        [Test]
        public void CopyContainsOnlyHistoricalMetricsAndAllowsExactPairDisclaimer()
        {
            var snapshot = AnalysisFixture.Snapshot(100);
            var copy = string.Join(" ", snapshot.AllRows.Concat(snapshot.AllPairs).Select(x => x.DisplayText)) + MetricRowViewModel.PairwiseTooltip;
            copy = copy.Replace(MetricRowViewModel.PairwiseTooltip, "");
            foreach (var forbidden in new[] { "예측", "전투 승률", "승리 확률", "prediction", "combat win" })
                Assert.That(copy, Does.Not.Contain(forbidden));
        }
    }

    internal static class AnalysisFixture
    {
        internal static CatalogIdentity Identity(int number = 18, CatalogSourceProfile profile = CatalogSourceProfile.OfficialOnly) =>
            new CatalogIdentity(new PatchKey("TFTSet18", number, "18.2", "16.18.1"), profile);
        internal static StaticCatalog Catalog(bool legacy = false) => new StaticCatalog(legacy
            ? new PatchManifest(1, Identity().Patch, "Test", DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, "", "")
            : new PatchManifest(2, Identity(), "Test", DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, "", ""),
            null, null, null, new[] { new AugmentRecord("AUG", null, "", null, null, null, "", null) });
        internal static AnonymousMatchObservation[] Observations(int count, string augment = null, string region = "asia") =>
            Enumerable.Range(0, count).Select(i => new AnonymousMatchObservation(ContentHasher.Sha256Hex(region + i),
                new MetricScope(Identity(), region, "1100", "ALL"), DateTimeOffset.UnixEpoch.AddDays(i),
                Enumerable.Range(0, 8).Select(p => new AnonymousParticipant(p + 1, 30,
                    new[] { new AnonymousUnit(((char)('A' + p)).ToString(), 2, new[] { "ITEM_" + (char)('A' + p) }) },
                    new[] { new AnonymousTrait("ACTIVE_" + (char)('A' + p), 1), new AnonymousTrait("INACTIVE_" + (char)('A' + p), 0) },
                    p == 0 && augment != null ? new[] { augment } : null)).ToArray())).ToArray();
        internal static AnalysisSnapshot Snapshot(int count, string augment = null, int minimumSamples = 100) =>
            AnalysisService.BuildSnapshot(Catalog(), Identity(), Observations(count, augment), new MetricPolicy(minimumSamples));
        internal sealed class Service : IAnalysisService
        {
            internal AnalysisSnapshot Result;
            internal Task<AnalysisSnapshot> Pending;
            internal Exception Failure;
            internal int Calls;
            internal int? MinimumSamples;
            internal CancellationToken Token;
            public Task<AnalysisSnapshot> LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token, int? minimumSamples = null)
            {
                Calls++; Token = token; MinimumSamples = minimumSamples;
                if (Failure != null) return Task.FromException<AnalysisSnapshot>(Failure);
                return Pending ?? Task.FromResult(Result);
            }
        }
    }
}
