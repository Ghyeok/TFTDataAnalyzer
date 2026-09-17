using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
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
                _paths.FinalDirectory(new PatchKey("TFTSet18", 18, patchId, "16.18.1")));
        }

        private static StaticImportRequest Request()
        {
            return new StaticImportRequest(
                new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                false);
        }

        private static StaticCatalog ValidCatalog(string englishName, string iconPath)
        {
            return new StaticCatalog(
                Manifest(),
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

        private static PatchManifest Manifest()
        {
            return new PatchManifest(
                1,
                new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                "Test Set",
                DateTime.UnixEpoch,
                "",
                null,
                DateTime.UnixEpoch,
                new[] { "https://example.test/policy" },
                0,
                0,
                "",
                "");
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

            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                if (!Responses.TryGetValue(url, out var response))
                {
                    throw new InvalidOperationException($"Unexpected HTTP request: {url}");
                }

                return Task.FromResult(response);
            }
        }

        private sealed class FakeIndexWriter : ICatalogIndexWriter
        {
            public int UpdateCount { get; private set; }

            public void Upsert(PatchKey patch, string contentHash, string catalogAssetPath)
            {
                UpdateCount++;
            }
        }
    }
}
