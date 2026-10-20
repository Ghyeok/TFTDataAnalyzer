using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Editor.Compliance;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.About;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Editor.UI.Collection;
using TFTAnalyzer.Editor.UI.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class TftDataAnalyzerEndToEndTests
    {
        [UnityTest]
        public IEnumerator WindowNoticeUsesLoadedRowsWithoutReloadingImportedVersion()
        {
            var name = "TftWindowLoadedNotice_" + Guid.NewGuid().ToString("N");
            var root = Path.Combine(Path.GetTempPath(), name);
            var now = new DateTimeOffset(2026, 10, 19, 1, 0, 0, TimeSpan.Zero);
            var transport = new TFTAnalyzer.Tests.Ingestion.StaticVersionCheckSchedulerTests.RecordingTransport();
            var service = new DataManagerFakeService
                { Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()) } };
            var importedVersionCalls = 0;
            try
            {
                using (var windows = new OwnedEditorWindowScope())
                using (var data = new DataManagerViewModel(service))
                using (var collection = new CollectionViewModel(new CollectionFakeService()))
                using (var analysis = new AnalysisViewModel(new AnalysisFixture.Service()))
                using (var scheduler = new StaticVersionCheckScheduler(transport, () => now, root,
                    () => { importedVersionCalls++; return "16.18.1"; }))
                {
                    var check = scheduler.CheckAsync(default);
                    var deadline = EditorApplication.timeSinceStartup + 5;
                    while (!check.IsCompleted)
                    {
                        Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                        yield return null;
                    }
                    check.GetAwaiter().GetResult();
                    var window = windows.Create<TftDataAnalyzerWindow>(value => value.Configure(data,
                        collection, analysis, new AboutViewModel(new ComplianceSettingsStore(root)),
                        summary => true, summary => false, scheduler));
                    window.CreateGUI();
                    Assert.That(window.rootVisualElement.Q<Label>("startup-version-notice").text,
                        Does.Contain("16.18.2").And.Contain("16.18.1"));
                    Assert.That(importedVersionCalls, Is.Zero,
                        "Window creation must compare the already loaded Rows without reloading catalogs.");

                    Assert.That(data.TrySetInput(DataManagerTestSupport.Identity().Patch, false), Is.True);
                    Assert.That(window.rootVisualElement.Q<Label>("startup-version-notice").text,
                        Does.Contain("16.18.2").And.Contain("16.18.1"));
                    Assert.That(importedVersionCalls, Is.Zero, "Data Changed must reuse the loaded Rows.");
                    window.CreateGUI();
                    Assert.That(window.rootVisualElement.Q<Label>("startup-version-notice").text,
                        Does.Contain("16.18.2").And.Contain("16.18.1"));
                    Assert.That(importedVersionCalls, Is.Zero, "GUI reconstruction must reuse the loaded Rows.");

                    service.Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity("16.18.2")) };
                    var import = data.ImportAsync(summary => true);
                    deadline = EditorApplication.timeSinceStartup + 5;
                    while (!import.IsCompleted)
                    {
                        Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                        yield return null;
                    }
                    import.GetAwaiter().GetResult();
                    Assert.That(data.State, Is.EqualTo(DataManagerState.Succeeded));
                    var notice = window.rootVisualElement.Q<Label>("startup-version-notice");
                    Assert.That(notice.text, Is.Empty, "The refreshed Rows now match the latest source version.");
                    Assert.That(notice.ClassListContains("tft-tab-hidden"), Is.True);
                    Assert.That(importedVersionCalls, Is.Zero, "Import notifications must use the refreshed Rows.");
                    Assert.That(transport.Urls, Has.Count.EqualTo(1));
                }
            }
            finally
            {
                Assert.That(Path.GetFullPath(root), Is.EqualTo(Path.Combine(Path.GetFullPath(Path.GetTempPath()), name)));
                AnalyzerStorePaths.RejectReparsePoints(root);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [UnityTest]
        public IEnumerator NewWindowRestoresCachedNoticeWithFreshModelsAndNoAdditionalHttp()
        {
            var name = "TftWindowNotice_" + Guid.NewGuid().ToString("N");
            var root = Path.Combine(Path.GetTempPath(), name);
            var now = new DateTimeOffset(2026, 10, 19, 1, 0, 0, TimeSpan.Zero);
            var transport = new TFTAnalyzer.Tests.Ingestion.StaticVersionCheckSchedulerTests.RecordingTransport();
            var changedField = typeof(StaticVersionCheckScheduler).GetField("Changed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(changedField, Is.Not.Null);
            bool HasListener(StaticVersionCheckScheduler scheduler, TftDataAnalyzerWindow window) =>
                (changedField.GetValue(scheduler) as Delegate)?.GetInvocationList()
                    .Any(listener => ReferenceEquals(listener.Target, window)) == true;
            try
            {
                using (var windows = new OwnedEditorWindowScope())
                using (var firstData = new DataManagerViewModel(new DataManagerFakeService
                    { Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()) } }))
                using (var firstCollection = new CollectionViewModel(new CollectionFakeService()))
                using (var firstAnalysis = new AnalysisViewModel(new AnalysisFixture.Service()))
                using (var firstScheduler = new StaticVersionCheckScheduler(transport, () => now, root,
                    () => LastImportedSourceVersion.Select(firstData.Rows)))
                {
                    var first = windows.Create<TftDataAnalyzerWindow>(window => window.Configure(firstData,
                        firstCollection, firstAnalysis, new AboutViewModel(new ComplianceSettingsStore(root)),
                        summary => false, summary => false, firstScheduler));
                    first.CreateGUI();
                    var firstId = first.GetInstanceID();
                    var initialCheck = firstScheduler.CheckAsync(default);
                    var deadline = EditorApplication.timeSinceStartup + 5;
                    while (!initialCheck.IsCompleted)
                    {
                        Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                        yield return null;
                    }
                    initialCheck.GetAwaiter().GetResult();
                    Assert.That(first.rootVisualElement.Q<Label>("startup-version-notice").text,
                        Does.Contain("16.18.2").And.Contain("16.18.1"));
                    Assert.That(transport.Urls, Has.Count.EqualTo(1));
                    Assert.That(HasListener(firstScheduler, first), Is.True);
                    first.Close();
                    Assert.That(HasListener(firstScheduler, first), Is.False,
                        "Closing the first window must release its exact scheduler subscription.");

                    using (var secondData = new DataManagerViewModel(new DataManagerFakeService
                        { Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()) } }))
                    using (var secondCollection = new CollectionViewModel(new CollectionFakeService()))
                    using (var secondAnalysis = new AnalysisViewModel(new AnalysisFixture.Service()))
                    using (var secondScheduler = new StaticVersionCheckScheduler(transport, () => now.AddHours(1), root,
                        () => LastImportedSourceVersion.Select(secondData.Rows)))
                    {
                        var second = windows.Create<TftDataAnalyzerWindow>(window => window.Configure(secondData,
                            secondCollection, secondAnalysis, new AboutViewModel(new ComplianceSettingsStore(root)),
                            summary => false, summary => false, secondScheduler));
                        second.CreateGUI();
                        Assert.That(second.GetInstanceID(), Is.Not.EqualTo(firstId));
                        Assert.That(second.rootVisualElement.Q<Label>("startup-version-notice").text, Is.Empty);
                        var cachedCheck = secondScheduler.CheckAsync(default);
                        deadline = EditorApplication.timeSinceStartup + 5;
                        while (!cachedCheck.IsCompleted)
                        {
                            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                            yield return null;
                        }
                        cachedCheck.GetAwaiter().GetResult();
                        Assert.That(second.rootVisualElement.Q<Label>("startup-version-notice").text,
                            Does.Contain("16.18.2").And.Contain("16.18.1"));
                        Assert.That(transport.Urls, Has.Count.EqualTo(1), "The fresh window must use cached state without additional HTTP.");
                        Assert.That(HasListener(secondScheduler, second), Is.True);
                        second.Close();
                        Assert.That(HasListener(secondScheduler, second), Is.False);
                    }
                }
            }
            finally
            {
                Assert.That(Path.GetFullPath(root), Is.EqualTo(Path.Combine(Path.GetFullPath(Path.GetTempPath()), name)));
                AnalyzerStorePaths.RejectReparsePoints(root);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase(99, MetricVisibility.InsufficientSample)]
        [TestCase(100, MetricVisibility.Visible)]
        public void TrustedSyntheticClassificationKeepsDefaultUnknownBlockedAndSeparatesSampleThreshold(int count, MetricVisibility expected)
        {
            var observations = AnalysisFixture.Observations(count, "AUG");
            var catalog = AnalysisFixture.Catalog();
            var identity = AnalysisFixture.Identity();
            var defaultRow = AnalysisService.BuildSnapshot(catalog, identity, observations).AllRows
                .Single(row => row.First.AugmentIds.Contains("AUG"));
            Assert.That(defaultRow.Presentation.State, Is.EqualTo(MetricVisibility.PolicyBlocked));
            Assert.That(defaultRow.Presentation.ReasonCode, Is.EqualTo("unclassified_augment"));
            Assert.That(defaultRow.Presentation.Ratios, Is.Empty);
            var trustedFixturePolicy = new MetricPolicy(100, new MetricPolicyMetadata(identity,
                new Dictionary<string, AugmentPolicyKind> { ["AUG"] = AugmentPolicyKind.Standard }));
            var row = AnalysisService.BuildSnapshot(catalog, identity, observations, trustedFixturePolicy).AllRows
                .Single(value => value.First.AugmentIds.Contains("AUG"));
            Assert.That(row.Presentation.State, Is.EqualTo(expected));
            if (count == 99)
            {
                Assert.That(row.Presentation.ReasonCode, Is.EqualTo("insufficient_sample"));
                Assert.That(row.Presentation.Ratios, Is.Empty);
            }
            else
            {
                var ratio = row.Presentation.Ratios.Single(value => value.Key == "first_place_rate");
                Assert.That(ratio.Numerator, Is.EqualTo(100));
                Assert.That(ratio.Denominator, Is.EqualTo(100));
                Assert.That(ratio.Interval.Lower, Is.EqualTo(0.9630065).Within(0.00001));
                Assert.That(ratio.Interval.Upper, Is.EqualTo(1).Within(0.00001));
                Assert.That(row.RatioText, Does.Contain("95%"));
            }
        }

        [UnityTest]
        public IEnumerator PreCancelledScenarioMakesNoRequestAndPublishesNothing()
        {
            using (var windows = new OwnedEditorWindowScope())
            using (var scenario = new TftAnalyzerEndToEndScenario())
            using (var cancellation = new CancellationTokenSource())
            {
                var window = windows.Create<TftDataAnalyzerWindow>(scenario.ConfigureWindow);
                window.CreateGUI();
                cancellation.Cancel();
                var operation = scenario.RunAsync(window, cancellationToken: cancellation.Token);
                var deadline = EditorApplication.timeSinceStartup + 30;
                while (!operation.IsCompleted)
                {
                    Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                }
                Exception failure = null;
                try { operation.GetAwaiter().GetResult(); }
                catch (Exception error) { failure = error; }
                Assert.That(scenario.StaticRequestCount, Is.Zero, "Pre-cancellation must stop before version HTTP or publication.");
                Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            }
        }

        [UnityTest]
        public IEnumerator OfflineVersionImportCollectionAndAnalysisUseRealBindingsAndIsolatedWriters()
        {
            using (var windows = new OwnedEditorWindowScope())
            using (var scenario = new TftAnalyzerEndToEndScenario())
            {
                var window = windows.Create<TftDataAnalyzerWindow>(scenario.ConfigureWindow);
                window.CreateGUI();
                var operation = scenario.RunAsync(window);
                var deadline = EditorApplication.timeSinceStartup + 30;
                while (!operation.IsCompleted)
                {
                    Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                }
                operation.GetAwaiter().GetResult();
            }
        }
    }

    // Owns only fixture data. The caller owns and closes its window, including after a failed stage/capture.
    internal sealed class TftAnalyzerEndToEndScenario : IDisposable
    {
        private readonly string _id = Guid.NewGuid().ToString("N");
        private readonly string _projectRoot = Path.GetDirectoryName(Application.dataPath);
        private readonly string _fixtureRoot, _assetRoot, _cacheRoot;
        private readonly StaticCatalogPaths _catalogPaths;
        private readonly AnalyzerStorePaths _storePaths;
        private readonly FixtureTransport _transport = new FixtureTransport();
        private readonly DataManagerViewModel _data;
        private readonly CollectionViewModel _collection;
        private readonly AnalysisViewModel _analysis;
        private readonly AboutViewModel _about;
        private readonly StaticVersionCheckScheduler _version;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly HashSet<EditorApplication.CallbackFunction> _renders = new HashSet<EditorApplication.CallbackFunction>();
        private CancellationToken _runToken;
        private string _assetGuid, _indexGuid;
        private DateTimeOffset _now = new DateTimeOffset(2026, 10, 19, 1, 0, 0, TimeSpan.Zero);
        private bool _approveImport, _approveCollection;
        private int _importConfirmations, _collectionConfirmations;
        private bool _disposed;
        internal int StaticRequestCount => _transport.StaticUrls.Count;

        internal TftAnalyzerEndToEndScenario()
        {
            _assetRoot = "Assets/TftAnalyzerE2E_" + _id;
            _cacheRoot = Path.Combine(_projectRoot, "Library", "TftAnalyzerValidation", "e2e_" + _id);
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "TftAnalyzerE2E_" + _id);
            // All publishing stays in this unique root; no fallback to the user's Generated/index/cache.
            _catalogPaths = new StaticCatalogPaths(_projectRoot, _assetRoot + "/Generated",
                "Library/TftAnalyzerValidation/e2e_" + _id + "/cache");
            _storePaths = new AnalyzerStorePaths(_fixtureRoot);
            _data = new DataManagerViewModel(new DataManagerService(_catalogPaths, _transport,
                () => AssetDatabase.LoadAssetAtPath<CatalogIndex>(_catalogPaths.IndexAssetPath), () => _now));
            var keys = new EnvironmentRiotApiKeyProvider(() => "E2E-FAKE-KEY");
            _collection = new CollectionViewModel(new CollectionService(new ObservationStore(_storePaths), keys,
                new EnvironmentSeedPuuidProvider(() => "E2E-FAKE-SEED"), new RiotApiClient(keys, _transport, utcNow: () => _now)));
            _analysis = new AnalysisViewModel(new AnalysisService(new ObservationStore(_storePaths), new AggregateStore(_storePaths)));
            _about = new AboutViewModel(new ComplianceSettingsStore(_fixtureRoot));
            _version = new StaticVersionCheckScheduler(_transport, () => _now, _fixtureRoot,
                () => LastImportedSourceVersion.Select(_data.Rows));
        }

        internal void ConfigureWindow(TftDataAnalyzerWindow window) => window.Configure(_data, _collection, _analysis, _about,
            summary => { _importConfirmations++; Assert.That(summary.Replace('\\', '/'), Does.Contain("TFTSet18").And.Contain(_assetRoot)); return _approveImport; },
            summary => { _collectionConfirmations++; Assert.That(summary, Does.Contain("1100").And.Contain("ALL")); return _approveCollection; }, _version);

        internal async Task RunAsync(TftDataAnalyzerWindow window, Func<string, Task> afterStage = null,
            CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            _runToken = linked.Token;
            _runToken.ThrowIfCancellationRequested();
            using var cancellation = _runToken.Register(CancelOperations);
            try
            {
                var tree = window.rootVisualElement;
                await _version.CheckAsync(_runToken);
                _runToken.ThrowIfCancellationRequested();
                Assert.That(_transport.StaticUrls, Is.EqualTo(new[] { "https://ddragon.leagueoflegends.com/api/versions.json" }));
                SetImportInputs(tree, true);
                Submit(tree, "import-button"); await Settle(() => _data.IsBusy);
                Assert.That(_importConfirmations, Is.EqualTo(1));
                Assert.That(_transport.StaticUrls, Has.Count.EqualTo(1));
                Assert.That(AssetDatabase.LoadAssetAtPath<CatalogIndex>(_catalogPaths.IndexAssetPath), Is.Null);
                Assert.That(tree.Q<Toggle>("communitydragon-toggle").value, Is.False);

                _approveImport = true;
                SetImportInputs(tree, false);
                Submit(tree, "import-button"); await Settle(() => _data.IsBusy);
                Assert.That(_data.State, Is.EqualTo(DataManagerState.Succeeded), _data.ErrorCode);
                Assert.That(_transport.StaticUrls.Count(url => url.EndsWith("/api/versions.json", StringComparison.Ordinal)), Is.EqualTo(1),
                    "Import completion must refresh its notice without another version-list request.");
                Assert.That(tree.Q<Label>("startup-version-notice").text, Does.Contain("16.18.2").And.Contain("16.18.1"));
                var callsBeforeRebuild = _transport.StaticUrls.Count;
                window.CreateGUI();
                Assert.That(tree.Q<Label>("startup-version-notice").text, Does.Contain("16.18.2").And.Contain("16.18.1"));
                Assert.That(_transport.StaticUrls.Count, Is.EqualTo(callsBeforeRebuild));
                var index = AssetDatabase.LoadAssetAtPath<CatalogIndex>(_catalogPaths.IndexAssetPath);
                Assert.That(index, Is.Not.Null);
                Assert.That(index.Entries, Has.Count.EqualTo(1));
                Assert.That(index.Entries[0].ImportCompletedUtc, Is.EqualTo(_now.ToString("O")));
                _assetGuid = AssetDatabase.AssetPathToGUID(_assetRoot);
                _indexGuid = AssetDatabase.AssetPathToGUID(_catalogPaths.IndexAssetPath);
                Assert.That(_assetGuid, Is.Not.Empty);
                Assert.That(_indexGuid, Is.Not.Empty);
                AssetDatabase.ImportAsset(_catalogPaths.IndexAssetPath, ImportAssetOptions.ForceUpdate);
                index = AssetDatabase.LoadAssetAtPath<CatalogIndex>(_catalogPaths.IndexAssetPath);
                Assert.That(AssetDatabase.AssetPathToGUID(_catalogPaths.IndexAssetPath), Is.EqualTo(_indexGuid));
                Assert.That(index.Entries[0].ImportCompletedUtc, Is.EqualTo(_now.ToString("O")));
                Assert.That(new PublishedCatalogLoader(_catalogPaths).Load(index).Single().IsSelectable, Is.True);
                var immutable = CatalogBytes();
                _now = _now.AddMinutes(1);
                Submit(tree, "import-button"); await Settle(() => _data.IsBusy);
                Assert.That(_data.State, Is.EqualTo(DataManagerState.Succeeded));
                Assert.That(index.Entries[0].ImportCompletedUtc, Is.EqualTo(_now.ToString("O")));
                AssertBytesUnchanged(immutable, CatalogBytes());
                Assert.That(_transport.StaticUrls.Any(url => url.Contains("communitydragon")), Is.False);
                tree.Q<ListView>("catalog-list").SetSelection(0);
                await Render();
                Assert.That(_collection.ActiveIdentity, Is.EqualTo(DataManagerTestSupport.Identity()));
                await AfterStage(afterStage, "import");

                _now = _now.AddHours(24);
                await _version.CheckAsync(_runToken); await Render();
                Assert.That(tree.Q<Label>("startup-version-notice").text, Does.Contain("16.18.2").And.Contain("16.18.1"));
                Submit(tree, "collection-tab-button");
                tree.Q<IntegerField>("collection-maximum").value = 2;
                Submit(tree, "collection-start"); await Settle(() => _collection.IsBusy);
                Assert.That(_collectionConfirmations, Is.EqualTo(1));
                Assert.That(_transport.RiotUrls, Is.Empty);
                _approveCollection = true;
                Submit(tree, "collection-start"); await Settle(() => _collection.IsBusy);
                Assert.That(_collection.State, Is.EqualTo(CollectionState.Succeeded), _collection.ErrorCode);
                Assert.That(_collection.Progress.Stored, Is.EqualTo(2));
                Assert.That(_collection.Progress.Requested, Is.EqualTo(2));
                Assert.That(_transport.RiotUrls, Has.Count.EqualTo(3));
                Assert.That(_transport.RiotUrls.Count(uri => uri.AbsolutePath.Contains("/by-puuid/")), Is.EqualTo(1));
                var observations = new List<AnonymousMatchObservation>();
                await foreach (var observation in new ObservationStore(_storePaths).ReadAllAsync(_runToken)) observations.Add(observation);
                Assert.That(observations, Has.Count.EqualTo(2));
                Assert.That(observations.All(value => value.Scope.ActiveCatalog.Equals(DataManagerTestSupport.Identity()) &&
                    value.Scope.Region == "asia" && value.Scope.QueueId == "1100" && value.Scope.TierBucket == "ALL"), Is.True);
                await AfterStage(afterStage, "collection");

                Submit(tree, "collection-start"); await Settle(() => _collection.IsBusy);
                Assert.That(_collection.Progress.Stored, Is.Zero);
                Assert.That(_collection.Progress.Duplicate, Is.EqualTo(2));
                Submit(tree, "analysis-tab-button");
                Submit(tree, "analysis-refresh"); await Settle(() => _analysis.IsBusy);
                Assert.That(_analysis.Rows, Is.Not.Empty);
                Assert.That(_analysis.Pairs, Is.Not.Empty);
                Assert.That(_analysis.Rows.Concat(_analysis.Pairs).All(row => row.Presentation.SampleCount < 100 && row.Presentation.Ratios.Count == 0), Is.True);
                Assert.That(_analysis.Rows.Any(row => row.Presentation.State == MetricVisibility.PolicyBlocked), Is.True);
                Assert.That(_analysis.Rows.Single(row => row.First.AugmentIds.Contains("TEST18_Augment_A")).Presentation.ReasonCode,
                    Is.EqualTo("unclassified_augment"), "The default service must keep unreviewed augments blocked.");
                Assert.That(_analysis.Rows.Where(row => row.First.AugmentIds.Count == 0)
                    .All(row => row.Presentation.State == MetricVisibility.InsufficientSample && row.Presentation.ReasonCode == "insufficient_sample"), Is.True);
                Assert.That(tree.Q<Label>("analysis-pairwise-heading").text, Is.EqualTo("상대 배치 우위율"));
                Assert.That(tree.Q<Label>("analysis-pairwise-heading").tooltip, Does.Contain("실제 전투 승률이 아닙니다"));
                Assert.That(string.Join("\n", tree.Q("analysis-rows").Query<Label>().ToList().Select(label => label.text)), Does.Contain("표본 부족"));
                var aggregate = Directory.GetFiles(_storePaths.RootDirectory, "*.json", SearchOption.AllDirectories)
                    .Where(path => path.Contains(Path.DirectorySeparatorChar + "aggregates" + Path.DirectorySeparatorChar))
                    .ToDictionary(path => path, File.ReadAllBytes);
                Assert.That(aggregate, Is.Not.Empty);
                Submit(tree, "analysis-refresh"); await Settle(() => _analysis.IsBusy);
                foreach (var pair in aggregate) Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value));
                var texts = tree.Query<TextElement>().ToList().Select(value => value.text)
                    .Concat(tree.Query<VisualElement>().ToList().Select(value => value.tooltip));
                Assert.That(texts.Where(TFTAnalyzer.Tests.Compliance.ForbiddenSurfaceAudit.HasForbiddenClaim), Is.Empty);
                foreach (var root in new[] { _fixtureRoot, _cacheRoot, Path.Combine(_projectRoot, _assetRoot) })
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".png")))
                foreach (var secret in new[] { "E2E-FAKE-KEY", "E2E-FAKE-SEED", "KR1_E2E_", "FAKE-PUUID", "FakePlayer", "FAKE-TAG" })
                    Assert.That(File.ReadAllText(file), Does.Not.Contain(secret), file);
                await AfterStage(afterStage, "analysis");

                var versionCalls = _transport.StaticUrls.Count(url => url.EndsWith("/api/versions.json", StringComparison.Ordinal));
                Submit(tree, "data-tab-button");
                tree.Q<TextField>("source-version").value = "16.18.2";
                Submit(tree, "import-button"); await Settle(() => _data.IsBusy);
                Assert.That(_data.State, Is.EqualTo(DataManagerState.Succeeded), _data.ErrorCode);
                Assert.That(tree.Q<Label>("startup-version-notice").text, Is.Empty);
                Assert.That(_transport.StaticUrls.Count(url => url.EndsWith("/api/versions.json", StringComparison.Ordinal)), Is.EqualTo(versionCalls));
                window.CreateGUI();
                Assert.That(tree.Q<Label>("startup-version-notice").text, Is.Empty);
                foreach (var file in immutable) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value));
            }
            finally
            {
                if (_runToken.IsCancellationRequested)
                {
                    CancelOperations();
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (_data.IsBusy || _collection.IsBusy || _analysis.IsBusy)
                    {
                        if (DateTime.UtcNow >= deadline) throw new TimeoutException("E2E cancellation did not settle its owned operations.");
                        await Task.Delay(10);
                    }
                }
            }
        }

        private async Task AfterStage(Func<string, Task> callback, string stage)
        {
            _runToken.ThrowIfCancellationRequested();
            if (callback != null) await callback(stage);
            _runToken.ThrowIfCancellationRequested();
        }

        private void CancelOperations()
        {
            try { _version.CancelPending(); }
            finally
            {
                try { _data.CancelImport(); }
                finally { try { _collection.Cancel(); } finally { _analysis.Cancel(); } }
            }
        }

        private Dictionary<string, byte[]> CatalogBytes() => Directory.GetFiles(_catalogPaths.AbsoluteAssetPath(_catalogPaths.GeneratedRelativeDirectory), "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("CatalogIndex.asset") && !path.EndsWith(".meta"))
            .ToDictionary(path => path, File.ReadAllBytes);
        private static void AssertBytesUnchanged(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
        { Assert.That(after.Keys, Is.EquivalentTo(before.Keys)); foreach (var pair in before) Assert.That(after[pair.Key], Is.EqualTo(pair.Value)); }
        private static void SetImportInputs(VisualElement tree, bool supplemental)
        {
            tree.Q<TextField>("set-id").value = "TFTSet18";
            tree.Q<IntegerField>("set-number").value = 18;
            tree.Q<TextField>("tft-patch").value = "18.2";
            tree.Q<TextField>("source-version").value = "16.18.1";
            tree.Q<Toggle>("communitydragon-toggle").value = supplemental;
        }
        private static void Submit(VisualElement tree, string name)
        {
            var button = tree.Q<Button>(name); Assert.That(button, Is.Not.Null);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        private async Task Settle(Func<bool> busy)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            _runToken.ThrowIfCancellationRequested();
            while (busy()) { Assert.That(DateTime.UtcNow, Is.LessThan(deadline)); await Task.Delay(10, _runToken); }
            await Render();
        }
        private async Task Render()
        {
            _runToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<bool>();
            void Update() { completion.TrySetResult(true); }
            _renders.Add(Update);
            EditorApplication.update += Update;
            try
            {
                using (_runToken.Register(() => completion.TrySetCanceled(_runToken))) await completion.Task;
                _runToken.ThrowIfCancellationRequested();
            }
            finally { EditorApplication.update -= Update; _renders.Remove(Update); }
        }

        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            void Clean(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            Clean(() => _lifetime.Cancel());
            foreach (var callback in _renders.ToArray()) Clean(() => EditorApplication.update -= callback);
            _renders.Clear();
            Clean(() => _version?.Dispose());
            Clean(() => _data?.Dispose());
            Clean(() => _collection?.Dispose());
            Clean(() => _analysis?.Dispose());
            Clean(() => DeleteOwnedDirectory(_fixtureRoot, Path.GetTempPath(), "TftAnalyzerE2E_" + _id));
            Clean(() => DeleteOwnedDirectory(_cacheRoot, Path.Combine(_projectRoot, "Library", "TftAnalyzerValidation"), "e2e_" + _id));
            Clean(() =>
            {
                var assetDirectory = Path.GetFullPath(Path.Combine(_projectRoot, _assetRoot));
                var expected = Path.Combine(Path.GetFullPath(Application.dataPath), "TftAnalyzerE2E_" + _id);
                Assert.That(assetDirectory, Is.EqualTo(expected));
                if (!string.IsNullOrEmpty(_assetGuid)) Assert.That(AssetDatabase.AssetPathToGUID(_assetRoot), Is.EqualTo(_assetGuid));
                if (!string.IsNullOrEmpty(_indexGuid)) Assert.That(AssetDatabase.AssetPathToGUID(_catalogPaths.IndexAssetPath), Is.EqualTo(_indexGuid));
                RejectOwnedLinks(assetDirectory);
                if (Directory.Exists(assetDirectory)) Assert.That(AssetDatabase.DeleteAsset(_assetRoot), Is.True);
            });
            Clean(() => _lifetime.Dispose());
            if (errors.Count != 0) throw new AggregateException("E2E fixture cleanup failed.", errors);
        }
        private static void DeleteOwnedDirectory(string path, string parent, string name)
        {
            Assert.That(Path.GetFullPath(path), Is.EqualTo(Path.Combine(Path.GetFullPath(parent), name)));
            RejectOwnedLinks(path);
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static void RejectOwnedLinks(string root)
        {
            AnalyzerStorePaths.RejectReparsePoints(root);
            if (!Directory.Exists(root)) return;
            var pending = new Stack<string>(); pending.Push(root);
            while (pending.Count != 0)
            foreach (var entry in Directory.GetFileSystemEntries(pending.Pop()))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Owned fixture contains a filesystem link.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
            }
        }

        private sealed class FixtureTransport : IHttpTransport, IRiotApiTransport
        {
            internal readonly List<string> StaticUrls = new List<string>();
            internal readonly List<Uri> RiotUrls = new List<Uri>();
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); StaticUrls.Add(url);
                byte[] body;
                if (url == "https://ddragon.leagueoflegends.com/api/versions.json") body = Encoding.UTF8.GetBytes("[\"16.18.2\",\"16.18.1\"]");
                else if (url.EndsWith(".png", StringComparison.Ordinal))
                    body = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
                else
                {
                    var language = url.Contains("/ko_KR/") ? "ko_KR" : "en_US";
                    var fixture = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "TFTAnalyzer/Tests/Fixtures/Static/ddragon-" + language + ".json")));
                    var section = url.EndsWith("tft-champion.json") ? "champions" : url.EndsWith("tft-trait.json") ? "traits" : url.EndsWith("tft-item.json") ? "items" : "augments";
                    body = Encoding.UTF8.GetBytes(fixture[section].ToString());
                }
                return Task.FromResult(new HttpResponseData(url, 200, body, "fixture", "application/json", DataManagerTestSupport.RetrievedUtc));
            }
            public Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); RiotUrls.Add(uri);
                Assert.That(headers["X-Riot-Token"], Is.EqualTo("E2E-FAKE-KEY"));
                string body;
                if (uri.AbsolutePath.Contains("/by-puuid/")) body = "[\"KR1_E2E_1\",\"KR1_E2E_2\"]";
                else
                {
                    var match = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json")));
                    match["metadata"]["match_id"] = uri.Segments.Last();
                    match["info"]["game_datetime"] = uri.AbsolutePath.EndsWith("_1") ? 1767225600000L : 1767312000000L;
                    body = match.ToString().Replace("TEST_UNIT", "TEST18_Unit_A").Replace("TEST_TRAIT", "TEST18_Trait_A")
                        .Replace("TEST_ITEM", "TEST18_Item_A").Replace("TEST_AUGMENT", "TEST18_Augment_A");
                }
                return Task.FromResult(new RiotApiResponse(200, new MemoryStream(Encoding.UTF8.GetBytes(body)), new Dictionary<string, string>()));
            }
        }
    }
}
