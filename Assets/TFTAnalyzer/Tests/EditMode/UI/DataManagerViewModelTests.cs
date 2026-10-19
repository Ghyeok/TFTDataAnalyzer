using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.UI.Data;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class DataManagerViewModelTests
    {
        private DataManagerFakeService _service;
        private DataManagerViewModel _vm;

        [SetUp]
        public void SetUp()
        {
            _service = new DataManagerFakeService();
            _vm = new DataManagerViewModel(_service);
        }

        [TearDown]
        public void TearDown() => _vm.Dispose();

        [Test]
        public async Task OnlyExplicitVersionCheckStartsARequestAndDoesNotInferPatch()
        {
            Assert.That(_service.VersionCalls, Is.Zero);
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Idle));
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            var patch = DataManagerTestSupport.Identity().Patch;
            Assert.That(_vm.TrySetInput(patch, false), Is.True);

            await _vm.CheckForUpdatesAsync();

            Assert.That(_vm.Versions, Is.EqualTo(new[] { "16.18.2", "16.18.1" }));
            Assert.That(_vm.InputPatch, Is.SameAs(patch));
            Assert.That(_service.Imports, Is.Empty);
        }

        [Test]
        public async Task ImportFailureDiagnosticsOmitMessagesAndClearWhenNextImportStarts()
        {
            const string privateMessage = "E2E-PRIVATE-KEY-and-response";
            _service.Run = token => ThrowImportDiagnosticFailure(privateMessage);
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);

            await _vm.ImportAsync(summary => true);

            Assert.That(_vm.ErrorCode, Is.EqualTo("ImportFailed"));
            Assert.That(_vm.IsBusy, Is.False);
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            Assert.That(ReadImportDiagnostic("LastImportFailureType"), Is.EqualTo(typeof(InvalidOperationException).FullName));
            Assert.That(ReadImportDiagnostic("LastImportFailureStack"), Does.Contain(nameof(ThrowImportDiagnosticFailure)).And.Not.Contain(privateMessage));
            Assert.That(ReadImportDiagnostic("LastImportFailureStack"), Does.Not.Contain(".cs:").And.Not.Contain("C:\\"));
            Assert.That(_vm.StatusText, Does.Not.Contain(privateMessage));

            _service.Run = token => Task.FromResult(DataManagerTestSupport.ImportResult());
            await _vm.ImportAsync(summary =>
            {
                Assert.That(ReadImportDiagnostic("LastImportFailureType"), Is.Empty);
                Assert.That(ReadImportDiagnostic("LastImportFailureStack"), Is.Empty);
                return true;
            });
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Succeeded));
        }

        [Test]
        public async Task ImportFailureDiagnosticsAlsoCaptureOneShotSucceededSubscriberFailure()
        {
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);
            Action observer = () =>
            {
                if (_vm.State == DataManagerState.Succeeded) throw new InvalidOperationException("E2E-private-subscriber-message");
            };
            _vm.Changed += observer;
            try { await _vm.ImportAsync(summary => true); }
            finally { _vm.Changed -= observer; }

            Assert.That(_service.Imports, Has.Count.EqualTo(1));
            Assert.That(_vm.ErrorCode, Is.EqualTo("ImportFailed"));
            Assert.That(_vm.IsBusy, Is.False);
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            Assert.That(ReadImportDiagnostic("LastImportFailureType"), Is.EqualTo(typeof(InvalidOperationException).FullName));
            Assert.That(ReadImportDiagnostic("LastImportFailureStack"), Does.Contain("DataManagerViewModel").And.Not.Contain("E2E-private-subscriber-message"));
        }

        private string ReadImportDiagnostic(string property) =>
            typeof(DataManagerViewModel).GetProperty(property,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_vm) as string ?? "";

        private static Task<StaticImportResult> ThrowImportDiagnosticFailure(string message) =>
            throw new InvalidOperationException(message);

        [TestCase(false)]
        [TestCase(true)]
        public async Task ImportRequiresConsentForTheCapturedIdentityAndResetsOptIn(bool approve)
        {
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);
            var confirmations = 0;
            await _vm.ImportAsync(summary =>
            {
                confirmations++;
                Assert.That(_vm.State, Is.EqualTo(DataManagerState.AwaitingApproval));
                Assert.That(summary, Does.Contain("TFTSet18").And.Contain("18.2")
                    .And.Contain("16.18.1").And.Contain("official-plus-communitydragon"));
                Assert.That(summary, Does.Contain(_service.ResolveDestination(new StaticImportRequest(
                    DataManagerTestSupport.Identity(profile: CatalogSourceProfile.OfficialPlusCommunityDragon)))));
                Assert.That(_vm.TrySetInput(DataManagerTestSupport.Identity("16.18.2").Patch, false), Is.False);
                return approve;
            });

            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(_service.Imports.Count, Is.EqualTo(approve ? 1 : 0));
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            if (approve)
            {
                Assert.That(_service.Imports[0].Identity.ProfileId, Is.EqualTo("official-plus-communitydragon"));
                Assert.That(_service.Imports[0].Patch.SourceGameVersion, Is.EqualTo("16.18.1"));
                Assert.That(_vm.State, Is.EqualTo(DataManagerState.Succeeded));
            }
            else
            {
                Assert.That(_vm.State, Is.EqualTo(DataManagerState.Cancelled));
                Assert.That(_vm.ErrorCode, Is.Empty);
            }
        }

        [Test]
        public async Task DestinationChangedDuringApprovalRejectsDownloadAndRequestsFreshConfirmation()
        {
            var destination = "test/approved-directory";
            _service.Destination = request => destination;
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);

            await _vm.ImportAsync(summary =>
            {
                Assert.That(summary, Does.Contain("test/approved-directory"));
                destination = "test/changed-directory";
                return true;
            });

            Assert.That(_service.Imports, Is.Empty, "Consent for the original directory cannot authorize another destination.");
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo("DestinationChanged"));
            Assert.That(_vm.StatusText, Does.Contain("다시"));
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            Assert.That(_vm.IsBusy, Is.False);
        }

        [Test]
        public async Task InvalidDestinationRejectsBeforeConfirmationOrDownloadAndResetsOptIn()
        {
            _vm.TrySetInput(new PatchKey("../unsafe", 18, "18.2", "16.18.1"), true);
            var confirmed = false;
            await _vm.ImportAsync(summary => { confirmed = true; return true; });
            Assert.That(confirmed, Is.False);
            Assert.That(_service.Imports, Is.Empty);
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo("InvalidInput"));
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public async Task PublishedCatalogWithUnreadableLocalListKeepsRecoveryGuidanceInsteadOfNormalSuccess()
        {
            // Initial local loading succeeded; only the post-publication reload fails.
            _service.LocalLoadError = new InvalidOperationException("private-index-details");
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);

            await _vm.ImportAsync(summary => true);

            Assert.That(_service.Imports, Has.Count.EqualTo(1));
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo("LocalCatalogsUnavailable"));
            Assert.That(_vm.StatusText, Does.Contain("저장").And.Contain("목록").And.Not.Contain("private-index-details"));
            Assert.That(_vm.IsBusy, Is.False);
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public async Task UnpublishedResultIsValidationFailureRatherThanSuccess()
        {
            _service.Run = token => Task.FromResult(DataManagerTestSupport.ImportResult(false));
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);
            await _vm.ImportAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo("ValidationRejected"));
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
        }

        [TestCase(StaticImportRecoveryState.PublishedUnindexed, "PublishedUnindexed")]
        [TestCase(StaticImportRecoveryState.PublishedIndexedRefreshPending, "PublishedIndexedRefreshPending")]
        [TestCase(StaticImportRecoveryState.PublishedIndexStateUncertain, "PublishedIndexStateUncertain")]
        public async Task RecoveryStatesHaveDistinctSafeGuidance(StaticImportRecoveryState state, string code)
        {
            _service.Run = token => Task.FromException<StaticImportResult>(new StaticImportRecoveryException(
                state, "private-directory", "private-index", new InvalidOperationException("secret-body")));
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);
            await _vm.ImportAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo(code));
            Assert.That(_vm.StatusText, Is.Not.Empty.And.Not.Contains("secret-body")
                .And.Not.Contains("private-directory").And.Not.Contains("private-index"));
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public async Task ArbitraryExceptionBodyIsNeverPresented()
        {
            _service.Run = token => Task.FromException<StaticImportResult>(
                new InvalidOperationException("response-secret", new Exception("inner-secret")));
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);
            await _vm.ImportAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo("ImportFailed"));
            Assert.That(_vm.StatusText, Does.Not.Contain("response-secret").And.Not.Contain("inner-secret"));
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public async Task CancelDoesNotReleaseBusyUntilImportSettlesAndLatePublishedSuccessWins()
        {
            var pending = new TaskCompletionSource<StaticImportResult>();
            _service.Run = token => pending.Task;
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);
            var first = _vm.ImportAsync(summary => true);
            var token = _service.LastToken;
            _vm.CancelImport();

            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(_vm.IsBusy, Is.True);
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Importing));
            Assert.That(_vm.TrySetInput(DataManagerTestSupport.Identity("16.18.2").Patch, true), Is.False);
            await _vm.ImportAsync(summary => { Assert.Fail("Busy import must not confirm again."); return true; });
            await _vm.CheckForUpdatesAsync();
            Assert.That(_service.Imports, Has.Count.EqualTo(1));
            Assert.That(_service.VersionCalls, Is.Zero);

            pending.SetResult(DataManagerTestSupport.ImportResult());
            await first;
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Succeeded));
            Assert.That(_vm.IsBusy, Is.False);
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public async Task CancelledCheckSettlesBeforeNextOperationGetsItsOwnToken()
        {
            var pending = new TaskCompletionSource<IReadOnlyList<string>>();
            _service.Versions = token => pending.Task;
            var first = _vm.CheckForUpdatesAsync();
            var oldToken = _service.LastToken;
            _vm.CancelImport();
            await _vm.CheckForUpdatesAsync();
            Assert.That(_service.VersionCalls, Is.EqualTo(1));
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Checking));
            pending.SetCanceled();
            await first;
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Cancelled));
            _service.Versions = token => Task.FromResult<IReadOnlyList<string>>(new[] { "16.18.3" });
            await _vm.CheckForUpdatesAsync();
            Assert.That(_vm.Versions, Is.EqualTo(new[] { "16.18.3" }));
            Assert.That(_service.LastToken, Is.Not.EqualTo(oldToken));
            Assert.That(_service.LastToken.IsCancellationRequested, Is.False);
        }

        [Test]
        public async Task DisposeCancelsAndSuppressesLateCompletionAndNotifications()
        {
            var pending = new TaskCompletionSource<StaticImportResult>();
            _service.Run = token => pending.Task;
            _vm.TrySetInput(DataManagerTestSupport.Identity().Patch, true);
            var task = _vm.ImportAsync(summary => true);
            var changes = 0;
            _vm.Changed += () => changes++;
            _vm.Dispose();
            var changesAtDisposal = changes;
            var stateAtDisposal = _vm.State;
            Assert.That(_service.LastToken.IsCancellationRequested, Is.True);
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            pending.SetResult(DataManagerTestSupport.ImportResult());
            await task;
            Assert.That(changes, Is.EqualTo(changesAtDisposal));
            Assert.That(_vm.State, Is.EqualTo(stateAtDisposal));
            await _vm.CheckForUpdatesAsync();
            Assert.That(_service.VersionCalls, Is.Zero);
        }

        [Test]
        public async Task ActiveSelectionAndDiffUseFullIdentityAndRejectInvalidOrBusyRows()
        {
            _vm.Dispose();
            var before = DataManagerTestSupport.Identity();
            var after = DataManagerTestSupport.Identity(profile: CatalogSourceProfile.OfficialPlusCommunityDragon);
            var invalid = DataManagerTestSupport.Identity("16.18.9");
            _service.Rows = new[] { DataManagerTestSupport.Row(before), DataManagerTestSupport.Row(after, "After"),
                DataManagerTestSupport.Row(invalid, valid: false) };
            _vm = new DataManagerViewModel(_service);
            Assert.That(_vm.SelectActiveCatalog(after), Is.True);
            Assert.That(_vm.ActiveIdentity, Is.EqualTo(after));
            Assert.That(_vm.SelectActiveCatalog(invalid), Is.False);
            Assert.That(_vm.ActiveIdentity, Is.EqualTo(after));
            Assert.That(_vm.Compare(before, after), Is.True);
            Assert.That(_vm.DiffBeforeIdentity, Is.EqualTo(before));
            Assert.That(_vm.DiffAfterIdentity, Is.EqualTo(after));
            var change = _vm.Diff.Units.Changed.Single().Fields.Single();
            Assert.That(change.Before, Is.EqualTo("Before"));
            Assert.That(change.After, Is.EqualTo("After"));
            Assert.That(_vm.Compare(after, before), Is.True);
            Assert.That(_vm.Diff.Units.Changed.Single().Fields.Single().Before, Is.EqualTo("After"));
            var pending = new TaskCompletionSource<IReadOnlyList<string>>();
            _service.Versions = token => pending.Task;
            var checking = _vm.CheckForUpdatesAsync();
            Assert.That(_vm.SelectActiveCatalog(before), Is.False);
            Assert.That(_vm.Compare(before, after), Is.False);
            Assert.That(_vm.ActiveIdentity, Is.EqualTo(after));
            Assert.That(_vm.DiffBeforeIdentity, Is.EqualTo(after));
            pending.SetResult(Array.Empty<string>());
            await checking;
        }

        [Test]
        public void LocalRowListIsASnapshotRatherThanTheServicesMutableCollection()
        {
            _vm.Dispose();
            var original = DataManagerTestSupport.Row(DataManagerTestSupport.Identity());
            var rows = new List<PatchRowViewModel> { original };
            _service.Rows = rows;
            _vm = new DataManagerViewModel(_service);
            rows.Clear();
            Assert.That(_vm.Rows, Has.Count.EqualTo(1));
            Assert.That(_vm.SelectActiveCatalog(original.Identity), Is.True);
        }

        [Test]
        public void TimestampOnlyDifferenceProducesNoContentChanges()
        {
            _vm.Dispose();
            var before = DataManagerTestSupport.Identity();
            var after = DataManagerTestSupport.Identity("16.18.2");
            _service.Rows = new[] { DataManagerTestSupport.Row(before),
                DataManagerTestSupport.Row(after, retrieved: DataManagerTestSupport.RetrievedUtc.AddDays(1)) };
            _vm = new DataManagerViewModel(_service);
            Assert.That(_vm.Compare(before, after), Is.True);
            Assert.That(_vm.Diff.Units.Changed, Is.Empty);
            Assert.That(_vm.Diff.Traits.Changed, Is.Empty);
            Assert.That(_vm.Diff.Items.Changed, Is.Empty);
            Assert.That(_vm.Diff.Augments.Changed, Is.Empty);
        }
    }
}
