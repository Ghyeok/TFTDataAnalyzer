using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.Collection;
using TFTAnalyzer.Editor.UI.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class CollectionWindowTests
    {
        private TftDataAnalyzerWindow _window;
        private DataManagerViewModel _data;
        private CollectionViewModel _collection;
        private CollectionFakeService _service;
        private OwnedEditorWindowScope _windows;

        [SetUp]
        public void SetUp()
        {
            _windows = new OwnedEditorWindowScope();
            try
            {
                _service = new CollectionFakeService();
                _collection = new CollectionViewModel(_service);
                _data = new DataManagerViewModel(new DataManagerFakeService
                {
                    Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()),
                        DataManagerTestSupport.Row(DataManagerTestSupport.Identity("16.18.2",
                            CatalogSourceProfile.OfficialPlusCommunityDragon)),
                        DataManagerTestSupport.Row(DataManagerTestSupport.Identity("16.18.3"), valid: false),
                        DataManagerTestSupport.Row(DataManagerTestSupport.Identity("16.18.4"), legacy: true) }
                });
                _window = _windows.Create<TftDataAnalyzerWindow>(window =>
                {
                    CollectionUITestSupport.SetWindowModel(window, "_collection", _collection);
                    CollectionUITestSupport.SetWindowModel(window, "_dataManager", _data);
                });
                _window.CreateGUI();
            }
            catch { TearDown(); throw; }
        }

        [TearDown]
        public void TearDown()
        {
            try { _collection?.Dispose(); }
            finally
            {
                try { _data?.Dispose(); }
                finally { _windows?.Dispose(); }
            }
        }

        [UnityTest]
        public IEnumerator SelectionTransfersOnlyTheVerifiedFullIdentityAndNavigationNeverCollects()
        {
            Assert.That(_collection.Catalog, Is.Null);
            _window.rootVisualElement.Q<ListView>("catalog-list").SetSelection(1);
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_collection.Catalog.Manifest.Identity, Is.EqualTo(_data.ActiveIdentity));
            Assert.That(_collection.Catalog.Manifest.Identity.ProfileId, Is.EqualTo("official-plus-communitydragon"));
            _window.rootVisualElement.Q<ListView>("catalog-list").SetSelection(2);
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_collection.Catalog.Manifest.Identity.Patch.SourceGameVersion, Is.EqualTo("16.18.2"));
            foreach (var name in new[] { "collection-tab-button", "analysis-tab-button", "data-tab-button" })
                CollectionUITestSupport.Submit(_window.rootVisualElement, name);
            _window.CreateGUI();
            _window.CreateGUI();
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_collection.Catalog.Manifest.Identity, Is.EqualTo(_data.ActiveIdentity));
            Assert.That(_window.rootVisualElement.Q<Label>("collection-catalog").text,
                Does.Contain("16.18.2").And.Contain("official-plus-communitydragon"));
        }

        [UnityTest]
        public IEnumerator RebuildingAndClosingWindowRetainThenCancelOneActiveRunAndDropLateCallbacks()
        {
            _window.rootVisualElement.Q<ListView>("catalog-list").SetSelection(0);
            var original = _collection.Catalog;
            var pending = new TaskCompletionSource<MatchCollectionSummary>();
            _service.Run = token => pending.Task;
            var running = _collection.StartAsync(summary => true);
            _data.SelectActiveCatalog(DataManagerTestSupport.Identity("16.18.2", CatalogSourceProfile.OfficialPlusCommunityDragon));
            _window.CreateGUI();
            Assert.That(_collection.Catalog, Is.SameAs(original));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.LastToken.IsCancellationRequested, Is.False);
            Assert.That(_window.rootVisualElement.Q<Label>("collection-catalog").text,
                Does.Contain("16.18.1").And.Contain("official-only"));
            var oldStatus = _window.rootVisualElement.Q<Label>("collection-status");
            var before = oldStatus.text;
            _window.Close();
            _window = null;
            Assert.That(_service.LastToken.IsCancellationRequested, Is.True);
            _service.LastProgress.Report(new CollectionProgress(CollectionFakeService.Summary(999)));
            pending.SetResult(CollectionFakeService.Summary(999));
            yield return CollectionUITestSupport.WaitForSettled(_collection);
            Assert.That(running.IsCompleted, Is.True);
            Assert.That(oldStatus.text, Is.EqualTo(before));
            Assert.That(_collection.Progress.Requested, Is.Zero);
        }

        [UnityTest]
        public IEnumerator VerifiedLegacyRowIdentityFlowsThroughWindowConfirmationRequestAndVisibleCatalog()
        {
            var expected = DataManagerTestSupport.Identity("16.18.4");
            _window.rootVisualElement.Q<ListView>("catalog-list").SetSelection(3);
            yield return CollectionUITestSupport.WaitForRender();
            Assert.That(_data.ActiveIdentity, Is.EqualTo(expected));
            Assert.That(_collection.Catalog.Manifest.SchemaVersion, Is.EqualTo(1));
            Assert.That(_collection.Catalog.Manifest.Identity, Is.Null, "Legacy source intentionally has no identity field.");
            Assert.That(_collection.ActiveIdentity, Is.EqualTo(expected), "Use the verified row's identity for schema 1.");
            Assert.That(_window.rootVisualElement.Q<Label>("collection-catalog").text,
                Does.Contain("TFTSet18").And.Contain("18.2").And.Contain("16.18.4").And.Contain("official-only"));
            Assert.That(_window.rootVisualElement.Q<Button>("collection-start").enabledInHierarchy, Is.True);
            var confirmations = 0;
            var operation = _collection.StartAsync(summary =>
            {
                confirmations++;
                Assert.That(summary, Does.Contain("16.18.4").And.Contain("official-only"));
                return true;
            });
            yield return CollectionUITestSupport.WaitForSettled(_collection);
            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(_service.Requests, Has.Count.EqualTo(1));
            Assert.That(_service.Requests[0].ActiveCatalog, Is.EqualTo(expected));
        }
    }
}
