using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Menu;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class StaticCatalogPipelineTests
    {
        private string _root;
        private StaticCatalogPaths _paths;
        private FakeSource _source;
        private FakeTransport _transport;
        private FakeIndexWriter _index;
        private StaticCatalogPipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TFTAnalyzerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _paths = new StaticCatalogPaths(_root);
            _source = new FakeSource(ValidCatalog("Test Unit", ""));
            _transport = new FakeTransport();
            _index = new FakeIndexWriter();
            _pipeline = new StaticCatalogPipeline(
                _source,
                _transport,
                _paths,
                new CatalogAssetWriter(_paths),
                new CatalogValidator(),
                _index,
                null);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public async Task RunAsync_DoesNotPublishWhenValidationFails()
        {
            _source.Catalog = InvalidCatalog();

            var result = await _pipeline.RunAsync(Request(), CancellationToken.None);

            Assert.That(result.Published, Is.False);
            Assert.That(Directory.Exists(result.FinalDirectory), Is.False);
            Assert.That(_index.UpdateCount, Is.Zero);
        }

        [Test]
        public async Task RunAsync_IsIdempotentForAnIdenticalPatch()
        {
            var first = await _pipeline.RunAsync(Request(), CancellationToken.None);
            var second = await _pipeline.RunAsync(Request(), CancellationToken.None);

            Assert.That(first.Published, Is.True);
            Assert.That(second.Published, Is.True);
            Assert.That(second.Idempotent, Is.True);
            Assert.That(File.Exists(Path.Combine(second.FinalDirectory, "catalog.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(second.FinalDirectory, "manifest.json")), Is.True);
        }

        [Test]
        public async Task RunAsync_ProfilesAndVersionsPublishToFourIndependentDirectories()
        {
            var results = new List<StaticImportResult>();
            foreach (var version in new[] { "16.18.1", "16.18.2" })
            foreach (var profile in new[] { CatalogSourceProfile.OfficialOnly, CatalogSourceProfile.OfficialPlusCommunityDragon })
            {
                _source.Catalog = ValidCatalog("Test Unit", "", version,
                    profile == CatalogSourceProfile.OfficialPlusCommunityDragon);
                var request = new StaticImportRequest(new CatalogIdentity(
                    new PatchKey("TFTSet18", 18, "18.2", version), profile));
                results.Add(await _pipeline.RunAsync(request, CancellationToken.None));
            }

            Assert.That(results.TrueForAll(result => result.Published), Is.True);
            Assert.That(results.ConvertAll(result => result.FinalDirectory).Distinct().Count(), Is.EqualTo(4));
            Assert.That(results.TrueForAll(result => File.Exists(Path.Combine(result.FinalDirectory, "manifest.json"))), Is.True);
            Assert.That(_index.UpdateCount, Is.EqualTo(4));
        }

        [Test]
        public async Task RunAsync_IdenticalOfficialLegacyRerunUsesVerifiedExistingDirectory()
        {
            _source.Catalog = LegacyReadyCatalog();
            var writer = new CatalogAssetWriter(_paths);
            var stagedRoot = _paths.CreateStagingDirectory();
            var staged = writer.WriteStagedCatalog(_source.Catalog, new CatalogValidator().Validate(_source.Catalog), stagedRoot);
            var legacyDirectory = _paths.LegacyFinalDirectory(Request().Patch);
            writer.Publish(staged, legacyDirectory);
            writer.CleanupStaging(stagedRoot);
            var legacyHash = ContentHasher.Sha256Hex(File.ReadAllBytes(Path.Combine(legacyDirectory, "catalog.json")));

            var result = await _pipeline.RunAsync(Request(), CancellationToken.None);

            Assert.That(result.Idempotent, Is.True);
            Assert.That(result.FinalDirectory, Is.EqualTo(legacyDirectory));
            Assert.That(ContentHasher.Sha256Hex(File.ReadAllBytes(Path.Combine(legacyDirectory, "catalog.json"))), Is.EqualTo(legacyHash));
            Assert.That(Directory.Exists(_paths.CanonicalFinalDirectory(Request().Identity)), Is.False);
            Assert.That(_index.LastAssetPath, Is.EqualTo("Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/catalog.json"));
        }

        [Test]
        public void RunAsync_IndexFailureReportsRecoverableUnindexedDirectory()
        {
            _index.ThrowOnUpdate = true;
            var finalDirectory = _paths.CanonicalFinalDirectory(Request().Identity);

            var error = Assert.ThrowsAsync<StaticImportRecoveryException>(async () =>
                await _pipeline.RunAsync(Request(), CancellationToken.None));

            Assert.That(error.Message, Does.Contain(finalDirectory));
            Assert.That(error.Message, Does.Contain("unindexed"));
            Assert.That(File.Exists(Path.Combine(finalDirectory, "catalog.json")), Is.True);
            var status = new StaticImportPresentation(_paths).FailureStatus(Request().Identity, finalDirectory, error);
            Assert.That(status, Does.Contain("published but unindexed"));
            Assert.That(status, Does.Contain("rerun import"));
            Assert.That(status, Does.Not.Contain("index failure"));
        }

        [Test]
        public void RunAsync_RollbackFailureReportsUncertainIndexStateForRecovery()
        {
            _index.ErrorOnUpdate = new CatalogIndexRollbackException("Assets/TFTAnalyzer/Data/Generated/CatalogIndex.asset",
                new IOException("private response body"));
            var directory = _paths.CanonicalFinalDirectory(Request().Identity);

            var error = Assert.ThrowsAsync<StaticImportRecoveryException>(async () =>
                await _pipeline.RunAsync(Request(), CancellationToken.None));
            var status = new StaticImportPresentation(_paths).FailureStatus(Request().Identity, directory, error);
            Assert.That(status, Does.Contain("index state uncertain"));
            Assert.That(status, Does.Contain("inspect or repair"));
            Assert.That(status, Does.Contain("CatalogIndex.asset"));
            Assert.That(status, Does.Not.Contain("private response body"));
        }

        [Test]
        public void RunAsync_ChangedStandaloneManifestIdentityCannotReturnIdempotent()
        {
            var first = _pipeline.RunAsync(Request(), CancellationToken.None).GetAwaiter().GetResult();
            var manifestPath = Path.Combine(first.FinalDirectory, "manifest.json");
            var tampered = File.ReadAllText(manifestPath).Replace("official-only", "official-plus-communitydragon");
            File.WriteAllText(manifestPath, tampered);

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await _pipeline.RunAsync(Request(), CancellationToken.None));
            Assert.That(File.ReadAllText(manifestPath), Is.EqualTo(tampered));
            Assert.That(_index.UpdateCount, Is.EqualTo(1));
        }

        [TestCase("community-version")]
        [TestCase("supplemental-stamp")]
        public void RunAsync_ChangedStandaloneProvenanceCannotReturnIdempotent(string mutation)
        {
            _source.Catalog = CatalogWithRetrievalTime(DateTime.UnixEpoch);
            var first = _pipeline.RunAsync(Request(), CancellationToken.None).GetAwaiter().GetResult();
            var manifestPath = Path.Combine(first.FinalDirectory, "manifest.json");
            var standalone = JObject.Parse(File.ReadAllText(manifestPath));
            if (mutation == "community-version")
                standalone["communityDragonVersion"] = "16.18";
            else
                standalone["sources"][0]["IsSupplemental"] = true;
            File.WriteAllText(manifestPath, standalone.ToString());

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await _pipeline.RunAsync(Request(), CancellationToken.None));
            Assert.That(_index.UpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void RunAsync_RejectsSupplementedProfileWhenNoSupplementalSourceWasLoaded()
        {
            var request = new StaticImportRequest(new CatalogIdentity(Request().Patch,
                CatalogSourceProfile.OfficialPlusCommunityDragon));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _pipeline.RunAsync(request, CancellationToken.None));
            Assert.That(Directory.Exists(_paths.CanonicalFinalDirectory(request.Identity)), Is.False);
            Assert.That(_index.UpdateCount, Is.Zero);
        }

        [Test]
        public void RunAsync_InvalidIconKeepsExistingOutputAndIndex()
        {
            var first = _pipeline.RunAsync(Request(), CancellationToken.None).GetAwaiter().GetResult();
            var catalogPath = Path.Combine(first.FinalDirectory, "catalog.json");
            var before = File.ReadAllBytes(catalogPath);
            var iconUrl = "https://example.test/invalid.png";
            _source.Catalog = ValidCatalog("Test Unit", iconUrl);
            _transport.Responses[iconUrl] = new HttpResponseData(iconUrl, 200,
                new byte[] { 1, 2, 3 }, "", "image/png", DateTime.UnixEpoch);

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await _pipeline.RunAsync(Request(), CancellationToken.None));
            Assert.That(File.ReadAllBytes(catalogPath), Is.EqualTo(before));
            Assert.That(_index.UpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void RunAsync_CancellationKeepsExistingOutputAndIndex()
        {
            var first = _pipeline.RunAsync(Request(), CancellationToken.None).GetAwaiter().GetResult();
            var catalogPath = Path.Combine(first.FinalDirectory, "catalog.json");
            var before = File.ReadAllBytes(catalogPath);
            _source.Cancel = true;

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _pipeline.RunAsync(Request(), CancellationToken.None));
            Assert.That(File.ReadAllBytes(catalogPath), Is.EqualTo(before));
            Assert.That(_index.UpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void RunAsync_CancellationAfterIconResponseDoesNotPublish()
        {
            var iconUrl = "https://example.test/cancel-after-icon.png";
            _source.Catalog = ValidCatalog("Test Unit", iconUrl);
            _transport.Responses[iconUrl] = new HttpResponseData(iconUrl, 200,
                new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1 },
                "", "image/png", DateTime.UnixEpoch);
            using (var cancellation = new CancellationTokenSource())
            {
                _transport.CancelAfterResponse = cancellation;
                Assert.CatchAsync<OperationCanceledException>(async () =>
                    await _pipeline.RunAsync(Request(), cancellation.Token));
            }

            Assert.That(Directory.Exists(_paths.CanonicalFinalDirectory(Request().Identity)), Is.False);
            Assert.That(_index.UpdateCount, Is.Zero);
        }

        [Test]
        public void RunAsync_RefreshFailureReportsPublishedDirectoryForRecovery()
        {
            var pipeline = new StaticCatalogPipeline(_source, _transport, _paths,
                new CatalogAssetWriter(_paths), new CatalogValidator(), _index,
                () => throw new IOException("refresh failed"));
            var directory = _paths.CanonicalFinalDirectory(Request().Identity);

            var error = Assert.ThrowsAsync<StaticImportRecoveryException>(async () =>
                await pipeline.RunAsync(Request(), CancellationToken.None));

            Assert.That(error.Message, Does.Contain(directory));
            Assert.That(error.Message, Does.Contain("refresh"));
            Assert.That(File.Exists(Path.Combine(directory, "catalog.json")), Is.True);
            Assert.That(_index.UpdateCount, Is.EqualTo(1));
            var status = new StaticImportPresentation(_paths).FailureStatus(Request().Identity, directory, error);
            Assert.That(status, Does.Contain("published and indexed"));
            Assert.That(status, Does.Contain("refresh assets"));
            Assert.That(status, Does.Not.Contain("refresh failed"));
        }

        [Test]
        public async Task RunAsync_IsIdempotentWhenOnlyRetrievalTimeChanges()
        {
            _source.Catalog = CatalogWithRetrievalTime(DateTime.UnixEpoch);
            await _pipeline.RunAsync(Request(), CancellationToken.None);
            _source.Catalog = CatalogWithRetrievalTime(DateTime.UnixEpoch.AddDays(1));

            var second = await _pipeline.RunAsync(Request(), CancellationToken.None);

            Assert.That(second.Idempotent, Is.True);
        }

        [Test]
        public async Task RunAsync_RejectsDifferentContentForExistingPatch()
        {
            await _pipeline.RunAsync(Request(), CancellationToken.None);
            _source.Catalog = ValidCatalog("Changed Unit", "");

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _pipeline.RunAsync(Request(), CancellationToken.None));
        }

        [Test]
        public async Task RunAsync_WritesContentAddressedPngAndUpdatesCatalogReference()
        {
            var iconUrl = "https://example.test/unit.png";
            var png = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3 };
            _source.Catalog = ValidCatalog("Test Unit", iconUrl);
            _transport.Responses[iconUrl] = new HttpResponseData(
                iconUrl,
                200,
                png,
                "etag",
                "image/png",
                DateTime.UnixEpoch);

            var result = await _pipeline.RunAsync(Request(), CancellationToken.None);

            var hash = ContentHasher.Sha256Hex(png);
            var expectedAssetPath = $"Assets/TFTAnalyzer/Data/Generated/Icons/{hash}.png";
            Assert.That(result.Catalog.Units[0].IconPath, Is.EqualTo(expectedAssetPath));
            Assert.That(File.Exists(Path.Combine(_root, expectedAssetPath.Replace('/', Path.DirectorySeparatorChar))), Is.True);
        }

        [Test]
        public void RunAsync_CleansStagingWhenCancelled()
        {
            _source.Cancel = true;

            Assert.ThrowsAsync<TaskCanceledException>(
                async () => await _pipeline.RunAsync(Request(), CancellationToken.None));
            Assert.That(
                Directory.Exists(_paths.StagingDirectory)
                    ? Directory.GetDirectories(_paths.StagingDirectory)
                    : Array.Empty<string>(),
                Is.Empty);
        }

        [TestCase("../escape")]
        [TestCase("bad/path")]
        public void Paths_RejectUnsafePatchSegments(string patchId)
        {
            Assert.Throws<ArgumentException>(() =>
                _paths.CanonicalFinalDirectory(new CatalogIdentity(
                    new PatchKey("TFTSet18", 18, patchId, "16.18.1"),
                    CatalogSourceProfile.OfficialOnly)));
        }

        private static StaticImportRequest Request()
        {
            return new StaticImportRequest(new CatalogIdentity(
                new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                CatalogSourceProfile.OfficialOnly));
        }

        private static StaticCatalog ValidCatalog(string englishName, string iconPath,
            string version = "16.18.1", bool supplemental = false)
        {
            return new StaticCatalog(
                Manifest(version, supplemental),
                new[]
                {
                    new UnitRecord(
                        "TEST18_Unit_A",
                        new LocalizedText("시험 기물", englishName),
                        1,
                        null,
                        iconPath,
                        null)
                },
                null,
                null,
                null);
        }

        private static StaticCatalog InvalidCatalog()
        {
            return new StaticCatalog(
                Manifest(),
                new[]
                {
                    new UnitRecord(
                        "TEST18_Unit_A",
                        LocalizedText.Empty,
                        0,
                        null,
                        "invalid-icon",
                        null)
                },
                null,
                null,
                null);
        }

        private static StaticCatalog CatalogWithRetrievalTime(DateTime retrievedUtc)
        {
            var source = new SourceStamp(
                "Riot Data Dragon",
                "https://example.test/unit.json",
                retrievedUtc,
                "etag",
                "same-body-hash");
            var manifest = new PatchManifest(
                1,
                new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                "Test Set",
                retrievedUtc,
                "",
                new[] { source },
                DateTime.UnixEpoch,
                new[] { "https://example.test/policy" },
                0,
                0,
                "",
                "");
            return new StaticCatalog(
                manifest,
                new[]
                {
                    new UnitRecord(
                        "TEST18_Unit_A",
                        new LocalizedText("시험 기물", "Test Unit"),
                        1,
                        null,
                        "",
                        new[] { source })
                },
                null,
                null,
                null);
        }

        private static PatchManifest Manifest(string version = "16.18.1", bool supplemental = false)
        {
            var sources = supplemental
                ? new[] { new SourceStamp("CommunityDragon",
                    "https://raw.communitydragon.org/16.18/cdragon/tft/en_us.json",
                    DateTime.UnixEpoch, "", "supplement-hash", true) }
                : null;
            return new PatchManifest(
                1,
                new PatchKey("TFTSet18", 18, "18.2", version),
                "Test Set",
                DateTime.UnixEpoch,
                "",
                sources,
                DateTime.UnixEpoch,
                new[] { "https://example.test/policy" },
                0,
                0,
                "",
                supplemental ? "16.18" : "");
        }

        private static StaticCatalog LegacyReadyCatalog()
        {
            var source = new SourceStamp("Riot Data Dragon",
                "https://ddragon.leagueoflegends.com/cdn/16.18.1/data/en_US/tft-champion.json",
                DateTime.UnixEpoch, "", "source-hash");
            return new StaticCatalog(new PatchManifest(1,
                    new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                    "Test Set", DateTime.UnixEpoch, "", new[] { source },
                    DateTime.UnixEpoch, new[] { "https://example.test/policy" }, 0, 0, "", ""),
                new[] { new UnitRecord("TEST18_Unit_A", new LocalizedText("시험 기물", "Test Unit"),
                    1, null, "", new[] { source }) }, null, null, null);
        }

        private sealed class FakeSource : IStaticCatalogSource
        {
            public StaticCatalog Catalog { get; set; }
            public bool Cancel { get; set; }

            public FakeSource(StaticCatalog catalog)
            {
                Catalog = catalog;
            }

            public Task<StaticSourceLoadResult> LoadAsync(
                StaticImportRequest request,
                CancellationToken token)
            {
                if (Cancel)
                {
                    return Task.FromCanceled<StaticSourceLoadResult>(new CancellationToken(true));
                }

                return Task.FromResult(new StaticSourceLoadResult(Catalog, null, null));
            }
        }

        private sealed class FakeTransport : IHttpTransport
        {
            public Dictionary<string, HttpResponseData> Responses { get; } =
                new Dictionary<string, HttpResponseData>(StringComparer.Ordinal);
            public CancellationTokenSource CancelAfterResponse { get; set; }

            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                if (!Responses.TryGetValue(url, out var response))
                {
                    throw new InvalidOperationException($"Unexpected HTTP request: {url}");
                }

                CancelAfterResponse?.Cancel();
                return Task.FromResult(response);
            }
        }

        private sealed class FakeIndexWriter : ICatalogIndexWriter
        {
            public int UpdateCount { get; private set; }
            public string LastAssetPath { get; private set; }
            public bool ThrowOnUpdate { get; set; }
            public Exception ErrorOnUpdate { get; set; }

            public void Upsert(CatalogIdentity identity, string contentHash, string catalogAssetPath)
            {
                if (ThrowOnUpdate) throw new IOException("index failure");
                if (ErrorOnUpdate != null) throw ErrorOnUpdate;
                UpdateCount++;
                LastAssetPath = catalogAssetPath;
            }
        }
    }
}
