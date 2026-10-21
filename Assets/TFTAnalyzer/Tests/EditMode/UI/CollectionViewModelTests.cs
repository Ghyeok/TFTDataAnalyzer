using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Collection;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class CollectionViewModelTests
    {
        private const string FabricatedKey = "fabricated-api-key-never-use";
        private const string FabricatedSeed = "fabricated-seed-never-use";
        private CollectionFakeService _service;
        private CollectionViewModel _vm;

        [SetUp]
        public void SetUp()
        {
            _service = new CollectionFakeService();
            _vm = new CollectionViewModel(_service);
            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()));
        }

        [TearDown]
        public void TearDown() => _vm.Dispose();

        /// <summary>Retains terminal report status during readiness edits and clears it only for a newly started run.</summary>
        [TestCase(true, false, false)] [TestCase(false, true, false)] [TestCase(true, false, true)]
        public async Task QuarantineResultSurvivesReadinessChecksAndNewRunClearsIt(bool saved, bool failed, bool truncated)
        {
            var result = new MatchCollectionSummary(3, 3, 1, 0, 2, 0, false, 1, false, 0, null, 2, saved, failed, truncated);
            _service.Run = token => Task.FromResult(result);
            await _vm.StartAsync(summary => true);
            Assert.That(_vm.Progress.Quarantined, Is.EqualTo(2));
            Assert.That(_vm.Progress.QuarantineReportSaved, Is.EqualTo(saved));
            Assert.That(_vm.Progress.QuarantineReportFailed, Is.EqualTo(failed));
            Assert.That(_vm.Progress.QuarantineReportTruncated, Is.EqualTo(truncated));
            if (failed) Assert.That(_vm.StatusText, Does.Contain("격리 보고서 저장"));
            if (truncated) Assert.That(_vm.StatusText, Does.Contain("상한"));
            var previous = _vm.Progress;
            var oldProgress = _service.LastProgress;
            _vm.RefreshConfiguration();
            _vm.TrySetInput("KR", 1100, "ALL", 1);
            _vm.SetCatalog(null);
            Assert.That(_vm.Progress, Is.SameAs(previous));
            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()));
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            var run = _vm.StartAsync(summary => true);
            Assert.That(_vm.Progress.Quarantined, Is.Zero);
            Assert.That(_vm.Progress.QuarantineReportSaved, Is.False);
            pending.SetResult(CollectionFakeService.Summary(1, 1, 1));
            await run;
            oldProgress.Report(new CollectionProgress(result));
            Assert.That(_vm.Progress.Quarantined, Is.Zero);
            Assert.That(_vm.Progress.QuarantineReportFailed, Is.False);
        }

        /// <summary>Unexpected service cancellation or HTTP exceptions preserve safe diagnostics already reported.</summary>
        [TestCase(true)] [TestCase(false)]
        public async Task ExceptionFallbackPreservesAlreadyReportedQuarantine(bool cancelled)
        {
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            var run = _vm.StartAsync(summary => true);
            _service.LastProgress.Report(new CollectionProgress(new MatchCollectionSummary(2, 2, 0, 0, 2, 0,
                false, 1, false, 0, null, 2, false, false, true)));
            pending.SetException(cancelled ? new OperationCanceledException() : new RiotApiException(RiotApiFailure.HttpError, 503));
            await run;
            Assert.That(_vm.Progress.Quarantined, Is.EqualTo(2));
            Assert.That(_vm.Progress.QuarantineReportTruncated, Is.True);
            Assert.That(_vm.Progress.QuarantineReportSaved, Is.False);
            Assert.That(_vm.State, Is.EqualTo(cancelled ? CollectionState.Cancelled : CollectionState.Failed));
        }

        [TestCase(false, false, "API 키와 수집 대상")]
        [TestCase(false, true, "API 키 설정")]
        [TestCase(true, false, "수집 대상 설정")]
        [TestCase(true, true, "입력 조건이 갖춰졌습니다")]
        public void ReadinessGuidanceMatchesConfigurationWithoutCollecting(bool key, bool seeds, string guidance)
        {
            _service.KeyAvailable = key;
            _service.SeedsAvailable = seeds;
            var changes = 0;
            _vm.Changed += () => changes++;

            _vm.RefreshConfiguration();

            Assert.That(_vm.StatusText, Does.Contain(guidance));
            Assert.That(_vm.StatusText, Does.Not.Contain("카탈로그를 선택"));
            Assert.That(_vm.CanStart, Is.EqualTo(key && seeds));
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Idle));
            Assert.That(_vm.ErrorCode, Is.Empty);
            Assert.That(changes, Is.EqualTo(1), "Refreshing guidance must reuse the existing notification.");
            Assert.That(_service.Requests, Is.Empty);
            AssertPublicTextIsSafe();
        }

        [Test]
        public void ReadinessGuidancePrioritizesCatalogThenInputAndRecoversAfterCorrections()
        {
            _service.KeyAvailable = _service.SeedsAvailable = false;
            _vm.RefreshConfiguration();
            _vm.SetCatalog(null);
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 1001), Is.False);
            Assert.That(_vm.StatusText, Does.Contain("카탈로그를 선택"));

            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()));
            Assert.That(_vm.StatusText, Does.Contain("상한"));
            Assert.That(_vm.CanStart, Is.False);
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 11), Is.True);
            Assert.That(_vm.StatusText, Does.Contain("API 키와 수집 대상"));

            _service.KeyAvailable = _service.SeedsAvailable = true;
            _vm.RefreshConfiguration();
            Assert.That(_vm.StatusText, Does.Contain("입력 조건이 갖춰졌습니다"));
            Assert.That(_vm.CanStart, Is.True);
            Assert.That(_service.Requests, Is.Empty);
            AssertPublicTextIsSafe();
        }

        [Test]
        public void ReadinessGuidanceHandlesConfigurationFailureWithoutExposingItsMessage()
        {
            _service.ConfigurationError = new InvalidOperationException(FabricatedKey + FabricatedSeed);
            _vm.RefreshConfiguration();
            Assert.That(_vm.StatusText, Does.Contain("API 키와 수집 대상"));
            Assert.That(_vm.CanStart, Is.False);
            AssertPublicTextIsSafe();

            _service.ConfigurationError = null;
            _vm.RefreshConfiguration();
            Assert.That(_vm.StatusText, Does.Contain("입력 조건이 갖춰졌습니다"));
            Assert.That(_vm.CanStart, Is.True);
            Assert.That(_service.Requests, Is.Empty);
        }

        [TestCase(CollectionState.Succeeded)]
        [TestCase(CollectionState.Failed)]
        [TestCase(CollectionState.Cancelled)]
        public async Task ReadinessEditsPreserveSettledStatusAndDoNotRestartCollection(CollectionState expected)
        {
            _service.Run = token => Task.FromResult(CollectionFakeService.Summary(3, 2, 1,
                cancelled: expected == CollectionState.Cancelled,
                statusCode: expected == CollectionState.Failed ? 401 : 0));
            var confirmations = 0;
            await _vm.StartAsync(summary => { confirmations++; return true; });
            Assert.That(_vm.State, Is.EqualTo(expected));
            var status = _vm.StatusText;
            var code = _vm.ErrorCode;
            var progress = _vm.Progress;
            var requests = _service.Requests.Count;

            _service.KeyAvailable = _service.SeedsAvailable = false;
            _vm.RefreshConfiguration();
            _vm.TrySetInput("KR", 1100, "ALL", 1001);
            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity("16.18.2")));

            Assert.That(_vm.State, Is.EqualTo(expected));
            Assert.That(_vm.StatusText, Is.EqualTo(status));
            Assert.That(_vm.ErrorCode, Is.EqualTo(code));
            Assert.That(_vm.Progress, Is.SameAs(progress));
            Assert.That(_service.Requests.Count, Is.EqualTo(requests));
            Assert.That(confirmations, Is.EqualTo(1));
            AssertPublicTextIsSafe();
        }

        [Test]
        public async Task ReadinessEditsPreserveRunningStatusUntilTheOwnedRequestSettles()
        {
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            var confirmations = 0;
            var running = _vm.StartAsync(summary => { confirmations++; return true; });
            try
            {
                Assert.That(_vm.State, Is.EqualTo(CollectionState.Running));
                var status = _vm.StatusText;
                _service.KeyAvailable = _service.SeedsAvailable = false;
                _vm.RefreshConfiguration();
                _vm.SetCatalog(null);
                Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 1001), Is.False);
                Assert.That(_vm.StatusText, Is.EqualTo(status));
                Assert.That(_vm.State, Is.EqualTo(CollectionState.Running));
                Assert.That(_vm.IsBusy, Is.True);
                Assert.That(_service.Requests, Has.Count.EqualTo(1));
                Assert.That(confirmations, Is.EqualTo(1));
            }
            finally
            {
                pending.TrySetResult(CollectionFakeService.Summary(1, 1, 1));
                await running;
            }
        }

        [Test]
        public void ConfigurationExposesAvailabilityWithoutCredentialOrSeedValues()
        {
            Assert.That(_vm.ApiKeyConfigured, Is.True);
            Assert.That(_vm.SeedConfigured, Is.True);
            var credentialMembers = typeof(CollectionViewModel).GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(member => member.MemberType == MemberTypes.Property || member.MemberType == MemberTypes.Field)
                .Where(member => member.Name.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 member.Name.IndexOf("seed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 member.Name.IndexOf("puuid", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(member => member.Name).ToArray();
            Assert.That(credentialMembers, Is.EquivalentTo(new[] { "ApiKeyConfigured", "SeedConfigured" }));
            Assert.That(_service.Requests, Is.Empty, "Constructing and configuring a model must never collect.");
            AssertPublicTextIsSafe();
        }

        [TestCase(1)]
        [TestCase(1000)]
        public async Task AcceptedCapBoundsTheCapturedCollectionRequest(int maximum)
        {
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", maximum), Is.True);

            await _vm.StartAsync(summary => true);

            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.Requests[0].MaximumUniqueMatches, Is.EqualTo(maximum));
            Assert.That(_vm.IsBusy, Is.False);
        }

        [TestCase(0)]
        [TestCase(1001)]
        public async Task InvalidCapCannotReplaceThePreviouslyValidBound(int invalid)
        {
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 7), Is.True);
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", invalid), Is.False);

            var confirmed = false;
            await _vm.StartAsync(summary => { confirmed = true; return true; });

            Assert.That(_vm.MaximumMatches, Is.EqualTo(7));
            Assert.That(confirmed, Is.False);
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.ErrorCode, Is.EqualTo("InvalidInput"));
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 8), Is.True);
            await _vm.StartAsync(summary => true);
            Assert.That(_service.Requests.Single().MaximumUniqueMatches, Is.EqualTo(8));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ConsentDescribesTheCapturedCatalogAndBoundBeforeAnyCollection(bool approve)
        {
            var selected = DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity(
                profile: CatalogSourceProfile.OfficialPlusCommunityDragon));
            _vm.SetCatalog(selected);
            _vm.TrySetInput("KR", 1100, "DIAMOND+", 17);
            var confirmations = 0;

            await _vm.StartAsync(summary =>
            {
                confirmations++;
                Assert.That(_vm.IsBusy, Is.True);
                Assert.That(_vm.State, Is.EqualTo(CollectionState.AwaitingApproval));
                Assert.That(_service.Requests, Is.Empty, "Collection must wait for explicit approval.");
                Assert.That(summary, Does.Contain("KR").And.Contain("asia").And.Contain("TFTSet18")
                    .And.Contain("18").And.Contain("18.2").And.Contain("16.18.1")
                    .And.Contain("official-plus-communitydragon").And.Contain("1100")
                    .And.Contain("DIAMOND+").And.Contain("17"));
                Assert.That(summary, Does.Contain("사용자").And.Contain("시드").And.Contain("확장")
                    .And.Contain("시도").And.Contain("저장").And.Contain("검증"));
                Assert.That(summary, Does.Not.Contain(FabricatedKey).And.Not.Contain(FabricatedSeed));
                Assert.That(_vm.TrySetInput("NA1", 1090, "ALL", 999), Is.False);
                _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity("16.18.2")));
                return approve;
            });

            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(_service.Requests.Count, Is.EqualTo(approve ? 1 : 0));
            if (approve)
            {
                Assert.That(_service.Catalogs[0], Is.SameAs(selected));
                var request = _service.Requests[0];
                Assert.That(request.ActiveCatalog, Is.EqualTo(selected.Manifest.Identity));
                Assert.That(request.Region, Is.EqualTo("asia"));
                Assert.That(request.QueueId, Is.EqualTo(1100));
                Assert.That(request.TierBucket, Is.EqualTo("DIAMOND+"));
                Assert.That(request.MaximumUniqueMatches, Is.EqualTo(17));
                Assert.That(_vm.State, Is.EqualTo(CollectionState.Succeeded));
            }
            else Assert.That(_vm.State, Is.EqualTo(CollectionState.Cancelled));
            Assert.That(_vm.Catalog.Manifest.Identity.Patch.SourceGameVersion, Is.EqualTo("16.18.2"));
            Assert.That(_vm.IsBusy, Is.False);
        }

        [Test]
        public async Task ArbitraryConfigurationFailureIsSafeAndNeverRequestsConsentOrCollection()
        {
            _vm.Dispose();
            _service.ConfigurationError = new InvalidOperationException(FabricatedKey + " " + FabricatedSeed);
            Assert.DoesNotThrow(() => _vm = new CollectionViewModel(_service));
            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()));
            _vm.TrySetInput("KR", 1100, "ALL", 10);
            var confirmed = false;

            await _vm.StartAsync(summary => { confirmed = true; return true; });

            Assert.That(_vm.ApiKeyConfigured, Is.False);
            Assert.That(_vm.SeedConfigured, Is.False);
            Assert.That(confirmed, Is.False);
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo("ConfigurationUnavailable"));
            Assert.That(_vm.IsBusy, Is.False);
            AssertPublicTextIsSafe();
        }

        [TestCase("untrusted-host", 1100, "ALL")]
        [TestCase("KR", 0, "ALL")]
        [TestCase("KR", 1000000, "ALL")]
        [TestCase("KR", 1100, FabricatedSeed)]
        public async Task InvalidRouteQueueOrTierCannotStartACollection(string platform, int queue, string tier)
        {
            Assert.That(_vm.TrySetInput(platform, queue, tier, 10), Is.False);
            await _vm.StartAsync(summary => { Assert.Fail("Invalid input cannot ask for consent."); return true; });
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.ErrorCode, Is.EqualTo("InvalidInput"));
            AssertPublicTextIsSafe();
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public async Task MissingConfigurationNeverRequestsConsent(bool key, bool seeds)
        {
            _service.KeyAvailable = key;
            _service.SeedsAvailable = seeds;
            _vm.TrySetInput("KR", 1100, "ALL", 10);
            var confirmed = false;
            await _vm.StartAsync(summary => { confirmed = true; return true; });
            Assert.That(confirmed, Is.False);
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.ErrorCode, Is.EqualTo("ConfigurationUnavailable"));
            Assert.That(_vm.IsBusy, Is.False);
        }

        [Test]
        public async Task MissingCatalogCannotStartOrAskForConsent()
        {
            _vm.SetCatalog(null);
            await _vm.StartAsync(summary => { Assert.Fail("No verified catalog was selected."); return true; });
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.ErrorCode, Is.EqualTo("InvalidInput"));
        }

        [TestCase(true, "missing")]
        [TestCase(true, "profile")]
        [TestCase(true, "patch")]
        [TestCase(true, "set-number")]
        [TestCase(false, "profile")]
        [TestCase(false, "set-number")]
        public async Task MissingOrIncompatibleTrustedIdentityCannotAuthorizeCollection(bool legacy, string mismatch)
        {
            var catalogIdentity = DataManagerTestSupport.Identity();
            CatalogIdentity supplied = null;
            if (mismatch == "profile")
                supplied = DataManagerTestSupport.Identity(profile: CatalogSourceProfile.OfficialPlusCommunityDragon);
            else if (mismatch == "patch")
                supplied = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.3", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            else if (mismatch == "set-number")
                supplied = new CatalogIdentity(new PatchKey("TFTSet18", 19, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            _vm.SetCatalog(DataManagerTestSupport.Catalog(catalogIdentity, legacy: legacy), supplied);
            Assert.That(_vm.ActiveIdentity, Is.Null);
            Assert.That(_vm.CanStart, Is.False);
            var confirmed = false;
            await _vm.StartAsync(summary => { confirmed = true; return true; });
            Assert.That(confirmed, Is.False);
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.ErrorCode, Is.EqualTo("InvalidInput"));
        }

        [Test]
        public async Task ExplicitLegacyIdentityIsCapturedDuringRunAndResynchronizedAfterSelectionChanges()
        {
            var identity = DataManagerTestSupport.Identity();
            var legacy = DataManagerTestSupport.Catalog(identity, legacy: true);
            _vm.SetCatalog(legacy, identity);
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 7), Is.True);
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            var running = _vm.StartAsync(summary => true);
            var nextIdentity = DataManagerTestSupport.Identity("16.18.2", CatalogSourceProfile.OfficialPlusCommunityDragon);
            _vm.SetCatalog(DataManagerTestSupport.Catalog(nextIdentity), nextIdentity);
            Assert.That(_vm.ActiveIdentity, Is.EqualTo(identity));
            Assert.That(_vm.Catalog, Is.SameAs(legacy));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.Requests[0].ActiveCatalog, Is.EqualTo(identity));
            pending.SetResult(CollectionFakeService.Summary(1, 1, 1));
            await running;
            Assert.That(_vm.ActiveIdentity, Is.EqualTo(nextIdentity));
        }

        [Test]
        public async Task ConfirmationExceptionNeverLeaksOrCollectsAndUnlocksInputs()
        {
            await _vm.StartAsync(summary => throw new InvalidOperationException(FabricatedKey + FabricatedSeed));
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo("ConfirmationFailed"));
            Assert.That(_vm.IsBusy, Is.False);
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 1), Is.True);
            AssertPublicTextIsSafe();
        }

        [TestCase(401, "Unauthorized")]
        [TestCase(403, "Forbidden")]
        [TestCase(429, "RateLimited")]
        public async Task ThrownSafeHttpFailureUsesControlledGuidanceAndUnlocks(int status, string code)
        {
            _service.Run = token => Task.FromException<MatchCollectionSummary>(new RiotApiException(
                status == 429 ? RiotApiFailure.RateLimited : RiotApiFailure.HttpError, status,
                status == 429 ? new DateTimeOffset(2026, 10, 16, 4, 0, 0, TimeSpan.Zero) : (DateTimeOffset?)null));
            await _vm.StartAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo(code));
            Assert.That(_vm.IsBusy, Is.False);
            AssertPublicTextIsSafe();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancelRetainsBusyUntilSettledAndFinalSummaryWinsOverLateCancellation(bool cancelled)
        {
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            _vm.TrySetInput("KR", 1100, "ALL", 9);
            var runningCatalog = _vm.Catalog;
            var running = _vm.StartAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Running));
            var next = DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity("16.18.2"));
            _vm.SetCatalog(next);
            Assert.That(_vm.Catalog, Is.SameAs(runningCatalog));
            _vm.Cancel();
            Assert.That(_service.LastToken.IsCancellationRequested, Is.True);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Cancelling));
            Assert.That(_vm.IsBusy, Is.True);
            Assert.That(_vm.TrySetInput("NA1", 1100, "ALL", 5), Is.False);
            await _vm.StartAsync(summary => { Assert.Fail("A run is already active."); return true; });
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            pending.SetResult(CollectionFakeService.Summary(3, 2, 2, cancelled: cancelled));
            await running;
            Assert.That(_vm.State, Is.EqualTo(cancelled ? CollectionState.Cancelled : CollectionState.Succeeded));
            Assert.That(_vm.Progress.Stored, Is.EqualTo(2));
            Assert.That(_vm.Catalog, Is.SameAs(next));
            Assert.That(_vm.IsBusy, Is.False);
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 5), Is.True);
        }

        [Test]
        public async Task CancellationDuringApprovalNeverCallsTheService()
        {
            await _vm.StartAsync(summary => { _vm.Cancel(); return true; });
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Cancelled));
            Assert.That(_vm.IsBusy, Is.False);
        }

        [TestCase(401, "Unauthorized")]
        [TestCase(403, "Forbidden")]
        [TestCase(429, "RateLimited")]
        [TestCase(503, "PartialFailure")]
        public async Task ControlledHttpFailuresUnlockAndPreserveFinalCounters(int status, string code)
        {
            var retry = new DateTimeOffset(2026, 10, 16, 4, 0, 0, TimeSpan.Zero);
            _service.Run = token => Task.FromResult(CollectionFakeService.Summary(5, 4, 2, 1, 1, 1,
                statusCode: status, retryAfterUtc: status == 429 ? retry : (DateTimeOffset?)null));
            await _vm.StartAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Failed));
            Assert.That(_vm.ErrorCode, Is.EqualTo(code));
            Assert.That(_vm.Progress.Requested, Is.EqualTo(5));
            Assert.That(_vm.Progress.Fetched, Is.EqualTo(4));
            Assert.That(_vm.Progress.Stored, Is.EqualTo(2));
            Assert.That(_vm.Progress.Duplicate, Is.EqualTo(1));
            Assert.That(_vm.Progress.Filtered, Is.EqualTo(1));
            Assert.That(_vm.Progress.Failed, Is.EqualTo(1));
            Assert.That(_vm.IsBusy, Is.False);
            Assert.That(_vm.TrySetInput("KR", 1100, "ALL", 1), Is.True);
            AssertPublicTextIsSafe();
            if (status == 429)
            {
                Assert.That(_vm.Progress.RetryAfterUtc, Is.EqualTo(retry));
                Assert.That(_vm.StatusText, Does.Contain("UTC").And.Contain("수동"));
                Assert.That(_service.Requests, Has.Count.EqualTo(1), "429 must not automatically resume.");
            }
        }

        [Test]
        public async Task ScanLimitIsDistinctFromReachingTheRequestedMatchCap()
        {
            _service.Run = token => Task.FromResult(CollectionFakeService.Summary(1, 1, 1,
                idPages: 10, scanLimitReached: true));
            await _vm.StartAsync(summary => true);
            Assert.That(_vm.ErrorCode, Is.EqualTo("ScanLimitReached"));
            Assert.That(_vm.Progress.ScanLimitReached, Is.True);
            Assert.That(_vm.Progress.IdPages, Is.EqualTo(10));
            Assert.That(_vm.StatusText, Does.Contain("탐색").And.Contain("상한"));
            Assert.That(_vm.IsBusy, Is.False);
        }

        [Test]
        public async Task ArbitraryRunExceptionAndThrownCancellationNeverEscapeOrExposeSecrets()
        {
            _service.Run = token => Task.FromException<MatchCollectionSummary>(
                new InvalidOperationException(FabricatedKey, new Exception(FabricatedSeed)));
            await _vm.StartAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Failed));
            Assert.That(_vm.IsBusy, Is.False);
            AssertPublicTextIsSafe();
            _service.Run = token => Task.FromException<MatchCollectionSummary>(new OperationCanceledException());
            await _vm.StartAsync(summary => true);
            Assert.That(_vm.State, Is.EqualTo(CollectionState.Cancelled));
            Assert.That(_vm.IsBusy, Is.False);
            AssertPublicTextIsSafe();
        }

        [Test]
        public async Task ProgressIsNumericAndLateCallbacksCannotOverwriteCurrentOrCompletedRuns()
        {
            // Capture this async test's active context, rather than the setup runner's context.
            _vm.Dispose();
            _vm = new CollectionViewModel(_service);
            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()));
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            var first = _vm.StartAsync(summary => true);
            var oldProgress = _service.LastProgress;
            oldProgress.Report(new CollectionProgress(CollectionFakeService.Summary(3, 2, 1, 1, 1)));
            Assert.That(_vm.Progress.Requested, Is.EqualTo(3));
            Assert.That(_vm.Progress.Stored, Is.EqualTo(1));
            pending.SetResult(CollectionFakeService.Summary(4, 3, 2, 1, 1));
            await first;
            oldProgress.Report(new CollectionProgress(CollectionFakeService.Summary(999)));
            Assert.That(_vm.Progress.Requested, Is.EqualTo(4));
            pending = new TaskCompletionSource<MatchCollectionSummary>();
            var second = _vm.StartAsync(summary => true);
            oldProgress.Report(new CollectionProgress(CollectionFakeService.Summary(999)));
            Assert.That(_vm.Progress.Requested, Is.Zero);
            pending.SetResult(CollectionFakeService.Summary(1, 1, 1));
            await second;
            Assert.That(_vm.Progress.Stored, Is.EqualTo(1));
            var allowed = new[] { typeof(int), typeof(bool), typeof(DateTimeOffset?) };
            Assert.That(typeof(CollectionProgressViewModel).GetProperties().All(property =>
                allowed.Contains(property.PropertyType)), Is.True, "Progress cannot expose raw identifiers or exceptions.");
        }

        [Test]
        public async Task DisposeCancelsAndRejectsCallbacksAndNewRequests()
        {
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            var running = _vm.StartAsync(summary => true);
            var changes = 0;
            _vm.Changed += () => changes++;
            var before = _vm.Progress;
            _vm.Dispose();
            Assert.That(_service.LastToken.IsCancellationRequested, Is.True);
            _service.LastProgress.Report(new CollectionProgress(CollectionFakeService.Summary(999)));
            pending.SetResult(CollectionFakeService.Summary(999));
            await running;
            await _vm.StartAsync(summary => { Assert.Fail("Disposed model cannot ask for approval."); return true; });
            Assert.That(_vm.Progress, Is.SameAs(before));
            Assert.That(changes, Is.Zero);
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ProgressFromAnotherContextIsPostedAndQueuedCallbacksAfterCompletionAreIgnored()
        {
            var context = new QueuedContext();
            var previous = SynchronizationContext.Current;
            CollectionViewModel vm;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                vm = new CollectionViewModel(_service);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            using (vm)
            {
                vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()));
                var pending = new TaskCompletionSource<MatchCollectionSummary>();
                _service.Run = token => pending.Task;
                var running = vm.StartAsync(summary => true);
                var callbackThread = 0;
                vm.Changed += () => callbackThread = Thread.CurrentThread.ManagedThreadId;
                _service.LastProgress.Report(new CollectionProgress(CollectionFakeService.Summary(2, 1, 1)));
                Assert.That(vm.Progress.Requested, Is.Zero, "A foreign context must not mutate bound UI state inline.");
                Assert.That(context.Pending, Is.EqualTo(1));
                context.Drain();
                Assert.That(vm.Progress.Requested, Is.EqualTo(2));
                Assert.That(callbackThread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
                _service.LastProgress.Report(new CollectionProgress(CollectionFakeService.Summary(999)));
                pending.SetResult(CollectionFakeService.Summary(3, 3, 2));
                await running;
                context.Drain();
                Assert.That(vm.Progress.Requested, Is.EqualTo(3));
                Assert.That(vm.Progress.Stored, Is.EqualTo(2));
            }
        }

        private sealed class QueuedContext : SynchronizationContext
        {
            private readonly Queue<Action> _callbacks = new Queue<Action>();
            internal int Pending => _callbacks.Count;
            public override void Post(SendOrPostCallback callback, object state) => _callbacks.Enqueue(() => callback(state));
            internal void Drain() { while (_callbacks.Count > 0) _callbacks.Dequeue()(); }
        }

        private void AssertPublicTextIsSafe()
        {
            foreach (var property in typeof(CollectionViewModel).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.PropertyType == typeof(string)))
            {
                var value = (string)property.GetValue(_vm);
                Assert.That(value, Does.Not.Contain(FabricatedKey).And.Not.Contain(FabricatedSeed), property.Name);
            }
        }
    }
}
