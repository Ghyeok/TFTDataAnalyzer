using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI.Collection;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class CollectionTabTests
    {
        private EditorWindow _host;
        private VisualElement _tree;
        private CollectionViewModel _vm;
        private CollectionFakeService _service;
        private CollectionTab _binding;
        private bool _approve;
        private int _confirmations;
        private OwnedEditorWindowScope _windows;

        [SetUp]
        public void SetUp()
        {
            _windows = new OwnedEditorWindowScope();
            try
            {
                _approve = false;
                _confirmations = 0;
                _service = new CollectionFakeService();
                _vm = new CollectionViewModel(_service);
                _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()));
                _host = _windows.Create<EditorWindow>();
                Rebuild();
            }
            catch { TearDown(); throw; }
        }

        [TearDown]
        public void TearDown()
        {
            try { _binding?.Dispose(); }
            finally
            {
                try { _vm?.Dispose(); }
                finally { _windows?.Dispose(); }
            }
        }

        [UnityTest]
        public IEnumerator ReadinessLabelTracksConfigurationChecksWithoutConsentOrRequests()
        {
            _service.KeyAvailable = _service.SeedsAvailable = false;
            Submit("collection-configuration-check");
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("collection-status").text, Does.Contain("API 키와 수집 대상"));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.False);

            _service.KeyAvailable = true;
            Submit("collection-configuration-check");
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("collection-status").text, Does.Contain("수집 대상 설정"));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.False);

            _service.SeedsAvailable = true;
            Submit("collection-configuration-check");
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("collection-status").text, Does.Contain("입력 조건이 갖춰졌습니다"));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.True);

            _tree.Q<IntegerField>("collection-maximum").value = 1001;
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("collection-status").text, Does.Contain("상한"));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.False);
            _tree.Q<IntegerField>("collection-maximum").value = 5;
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("collection-status").text, Does.Contain("입력 조건이 갖춰졌습니다"));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.True);
            Assert.That(_confirmations, Is.Zero);
            Assert.That(_service.Requests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RealFieldsAndButtonRequireConsentAndPassExactlyTheVisibleBound()
        {
            Assert.That(_tree.Q<Label>("collection-credentials").text, Does.Contain("설정됨"));
            _tree.Q<IntegerField>("collection-maximum").value = 17;
            Submit("collection-start");
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_confirmations, Is.EqualTo(1));
            Assert.That(_service.Requests, Is.Empty);
            _approve = true;
            Submit("collection-start");
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_confirmations, Is.EqualTo(2));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.Requests[0].MaximumUniqueMatches, Is.EqualTo(17));
            Assert.That(_tree.Q<Label>("collection-status").text, Is.EqualTo(_vm.StatusText));
        }

        [UnityTest]
        public IEnumerator InvalidVisibleBoundBlocksStartInsteadOfUsingThePreviousValidValue()
        {
            _approve = true;
            _tree.Q<IntegerField>("collection-maximum").value = 17;
            _tree.Q<IntegerField>("collection-maximum").value = 1001;
            // A queued render must not hide invalid visible input by restoring the previous accepted value.
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<IntegerField>("collection-maximum").value, Is.EqualTo(1001));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.False);
            Submit("collection-start");
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_confirmations, Is.Zero);
            _tree.Q<IntegerField>("collection-maximum").value = 1;
            yield return CollectionUITestSupport.WaitForRender();
            Submit("collection-start");
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.Requests[0].MaximumUniqueMatches, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EditingBeforeCatalogSelectionPreservesAndRevalidatesVisibleInput()
        {
            _vm.SetCatalog(null);
            _tree.Q<IntegerField>("collection-maximum").value = 23;
            _tree.Q<IntegerField>("collection-queue").value = 1090;
            _tree.Q<DropdownField>("collection-platform").value = "NA1";
            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity("16.18.2")));
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<IntegerField>("collection-maximum").value, Is.EqualTo(23));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.True);
            _approve = true;
            Submit("collection-start");
            Assert.That(_service.Requests[0].MaximumUniqueMatches, Is.EqualTo(23));
            Assert.That(_service.Requests[0].QueueId, Is.EqualTo(1090));
            Assert.That(_service.Requests[0].Region, Is.EqualTo("americas"));
            Assert.That(_service.Requests[0].ActiveCatalog.Patch.SourceGameVersion, Is.EqualTo("16.18.2"));
        }

        [UnityTest]
        public IEnumerator RebuildPreservesInvalidVisibleDraftAndCorrectedBoundStartsNormally()
        {
            _approve = true;
            _tree.Q<IntegerField>("collection-maximum").value = 17;
            _tree.Q<IntegerField>("collection-maximum").value = 1001;

            Rebuild();

            Assert.That(_tree.Q<IntegerField>("collection-maximum").value, Is.EqualTo(1001),
                "Rebinding must keep the invalid visible draft consistent with the model's blocked state.");
            Assert.That(_vm.CanStart, Is.False);
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.False);
            Submit("collection-start");
            Assert.That(_confirmations, Is.Zero);
            Assert.That(_service.Requests, Is.Empty);
            // Rejected free text must not become a dropdown value when numeric draft restoration is added.
            Assert.That(_vm.TrySetInput("fabricated-api-key", 1100, "fabricated-seed", 1001), Is.False);
            Rebuild();
            Assert.That(_tree.Q<DropdownField>("collection-platform").value, Is.EqualTo("KR"));
            Assert.That(_tree.Q<DropdownField>("collection-tier").value, Is.EqualTo("ALL"));
            Assert.That(_tree.Q<IntegerField>("collection-maximum").value, Is.EqualTo(1001));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.False);
            _tree.Q<IntegerField>("collection-maximum").value = 18;
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.True);
            Submit("collection-start");
            Assert.That(_confirmations, Is.EqualTo(1));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.Requests[0].MaximumUniqueMatches, Is.EqualTo(18));
            Assert.That(_service.Requests[0].Region, Is.EqualTo("asia"));
            Assert.That(_service.Requests[0].TierBucket, Is.EqualTo("ALL"));
        }

        [UnityTest]
        public IEnumerator RebuildBeforeCatalogSelectionPreservesCurrentDraftAndSelectionEnablesIt()
        {
            _vm.SetCatalog(null);
            _tree.Q<IntegerField>("collection-maximum").value = 23;
            _tree.Q<IntegerField>("collection-queue").value = 1090;
            _tree.Q<DropdownField>("collection-platform").value = "NA1";
            _tree.Q<DropdownField>("collection-tier").value = "DIAMOND+";

            Rebuild();

            Assert.That(_tree.Q<IntegerField>("collection-maximum").value, Is.EqualTo(23));
            Assert.That(_tree.Q<IntegerField>("collection-queue").value, Is.EqualTo(1090));
            Assert.That(_tree.Q<DropdownField>("collection-platform").value, Is.EqualTo("NA1"));
            Assert.That(_tree.Q<DropdownField>("collection-tier").value, Is.EqualTo("DIAMOND+"));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.False);
            Assert.That(_service.Requests, Is.Empty);
            _vm.SetCatalog(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity("16.18.2")));
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.True);
            _approve = true;
            Submit("collection-start");
            Assert.That(_confirmations, Is.EqualTo(1));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.Requests[0].MaximumUniqueMatches, Is.EqualTo(23));
            Assert.That(_service.Requests[0].QueueId, Is.EqualTo(1090));
            Assert.That(_service.Requests[0].Region, Is.EqualTo("americas"));
            Assert.That(_service.Requests[0].TierBucket, Is.EqualTo("DIAMOND+"));
        }

        [UnityTest]
        public IEnumerator BusyInputsAndCancelStayLockedUntilTheOperationActuallySettles()
        {
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            _approve = true;
            Submit("collection-start");
            _service.LastProgress.Report(new CollectionProgress(CollectionFakeService.Summary(3, 2, 1, 1, 1)));
            yield return CollectionUITestSupport.WaitForRender();
            foreach (var name in new[] { "collection-platform", "collection-tier", "collection-queue", "collection-maximum", "collection-start", "collection-configuration-check" })
                Assert.That(_tree.Q(name).enabledInHierarchy, Is.False, name);
            Assert.That(_tree.Q<Label>("collection-counts").text, Does.Contain("시도 3").And.Contain("저장 1"));
            Submit("collection-cancel");
            Assert.That(_service.LastToken.IsCancellationRequested, Is.True);
            Assert.That(_vm.IsBusy, Is.True);
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("collection-cancellation").text, Does.Contain("종료 대기"));
            pending.SetCanceled();
            yield return CollectionUITestSupport.WaitForSettled(_vm);
            Assert.That(_vm.Progress.Cancelled, Is.True);
            Assert.That(_vm.Progress.Stored, Is.EqualTo(1));
            foreach (var name in new[] { "collection-platform", "collection-tier", "collection-queue", "collection-maximum", "collection-start", "collection-configuration-check" })
                Assert.That(_tree.Q(name).enabledInHierarchy, Is.True, name);
            Assert.That(_tree.Q<Label>("collection-cancellation").text, Does.Contain("종료됨"));
        }

        [UnityTest]
        public IEnumerator RebuiltBindingDropsOldCallbacksAndKeepsTheActiveRunWithoutRecollecting()
        {
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            _approve = true;
            Submit("collection-start");
            var oldMaximum = _tree.Q<IntegerField>("collection-maximum");
            var oldStatus = _tree.Q<Label>("collection-status");
            var before = oldStatus.text;
            Rebuild();
            oldMaximum.value = 999;
            Assert.That(_vm.MaximumMatches, Is.EqualTo(100));
            Assert.That(_service.LastToken.IsCancellationRequested, Is.False);
            pending.SetResult(CollectionFakeService.Summary(3, 3, 2, 1));
            yield return CollectionUITestSupport.WaitForSettled(_vm);
            Assert.That(oldStatus.text, Is.EqualTo(before));
            Assert.That(_tree.Q<Label>("collection-counts").text, Does.Contain("저장 2"));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RateLimitShowsUtcAndManualRetryWithoutAutomaticallyStartingAnotherRun()
        {
            _service.Run = token => Task.FromResult(CollectionFakeService.Summary(1, failed: 1, statusCode: 429,
                retryAfterUtc: new DateTimeOffset(2026, 10, 16, 4, 30, 0, TimeSpan.Zero)));
            _approve = true;
            Submit("collection-start");
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_tree.Q<Label>("collection-rate-limit").text,
                Does.Contain("2026-10-16 04:30:00 UTC").And.Contain("수동"));
            Assert.That(_tree.Q<Label>("collection-status").text, Does.Contain("429"));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_tree.Q<Button>("collection-start").enabledInHierarchy, Is.True);
        }

        private void Rebuild()
        {
            _binding?.Dispose();
            _host.rootVisualElement.Clear();
            _tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            _tree.Q("collection-tab").RemoveFromClassList("tft-tab-hidden");
            _host.rootVisualElement.Add(_tree);
            _binding = new CollectionTab(_tree.Q("collection-tab"), _vm, summary =>
            {
                _confirmations++;
                Assert.That(summary, Does.Contain("TFTSet18").And.Contain("사용자").And.Contain("시드"));
                Assert.That(summary, Does.Not.Contain("fabricated-api-key").And.Not.Contain("fabricated-seed"));
                return _approve;
            });
        }

        private void Submit(string name) => CollectionUITestSupport.Submit(_tree, name);
    }
}
