using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Menu;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;
using System.Collections.Generic;
using System.Text;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class StaticImportPresentationTests
    {
        private readonly PatchKey _patch = new PatchKey("TFTSet18", 18, "18.2", "16.18.1");

        [Test]
        public void DefaultSummaryNamesFullIdentityAndResolvedPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "presentation-tests");
            var presentation = new StaticImportPresentation(new StaticCatalogPaths(root));
            Assert.That(presentation.IncludeCommunityDragon, Is.False);
            var summary = presentation.BuildSummary(presentation.CreateRequest(_patch).Identity);
            Assert.That(summary, Does.Contain("TFTSet18"));
            Assert.That(summary, Does.Contain("18.2"));
            Assert.That(summary, Does.Contain("16.18.1"));
            Assert.That(summary, Does.Contain("official-only"));
            Assert.That(summary, Does.Contain(Path.Combine(root, "Assets", "TFTAnalyzer", "Data", "Generated",
                "TFTSet18", "18.2", "16.18.1", "official-only")));
        }

        [Test]
        public void FailureStatusIncludesIdentityAndTargetWithoutExceptionBody()
        {
            var presentation = NewPresentation();
            var identity = presentation.CreateRequest(_patch).Identity;
            var target = presentation.TargetDirectory(identity);
            var status = presentation.FailureStatus(identity, target,
                new InvalidOperationException("sensitive response body"));
            Assert.That(status, Does.Contain("TFTSet18"));
            Assert.That(status, Does.Contain("18.2"));
            Assert.That(status, Does.Contain("16.18.1"));
            Assert.That(status, Does.Contain("official-only"));
            Assert.That(status, Does.Contain(target));
            Assert.That(status, Does.Not.Contain("sensitive response body"));
        }

        [Test]
        public void CancellationStatusNamesFullIdentityAndTarget()
        {
            var presentation = NewPresentation();
            var identity = presentation.CreateRequest(_patch).Identity;
            var target = presentation.TargetDirectory(identity);
            var status = presentation.CancellationStatus(identity, target);
            Assert.That(status, Does.Contain("TFTSet18"));
            Assert.That(status, Does.Contain("18.2"));
            Assert.That(status, Does.Contain("16.18.1"));
            Assert.That(status, Does.Contain("official-only"));
            Assert.That(status, Does.Contain(target));
        }

        [Test]
        public async Task CancelledConfirmationDoesNotRunImportAndResetsConsent()
        {
            var presentation = NewPresentation();
            presentation.IncludeCommunityDragon = true;
            var calls = 0;
            var result = await presentation.ExecuteAsync(_patch, _ => false,
                (request, token) => { calls++; return Task.FromResult("result"); }, CancellationToken.None);
            Assert.That(result, Is.Null);
            Assert.That(calls, Is.Zero);
            Assert.That(presentation.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public async Task SuccessResetsConsentAndPassesSelectedProfile()
        {
            var presentation = NewPresentation();
            presentation.IncludeCommunityDragon = true;
            var result = await presentation.ExecuteAsync(_patch, summary =>
                summary.Contains("official-plus-communitydragon"),
                (request, token) => Task.FromResult(request.Identity.ProfileId), CancellationToken.None);
            Assert.That(result, Is.EqualTo("official-plus-communitydragon"));
            Assert.That(presentation.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public void FailureResetsConsent()
        {
            var presentation = NewPresentation();
            presentation.IncludeCommunityDragon = true;
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await presentation.ExecuteAsync<string>(_patch, _ => true,
                    (request, token) => throw new InvalidOperationException("failed"), CancellationToken.None));
            Assert.That(presentation.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public void CancellationResetsConsent()
        {
            var presentation = NewPresentation();
            presentation.IncludeCommunityDragon = true;
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await presentation.ExecuteAsync<string>(_patch, _ => true,
                    (request, token) => throw new OperationCanceledException(), CancellationToken.None));
            Assert.That(presentation.IncludeCommunityDragon, Is.False);
        }

        [Test]
        public async Task OfficialOnlyNetworkSourceNeverRequestsCommunityDragonHost()
        {
            var transport = new RecordingTransport();
            var source = new NetworkStaticCatalogSource(new DataDragonAdapter(transport),
                new CommunityDragonAdapter(), new CatalogMerger(), transport);
            var request = NewPresentation().CreateRequest(_patch);
            await source.LoadAsync(request, CancellationToken.None);

            Assert.That(transport.Requested.Count, Is.EqualTo(8));
            Assert.That(transport.Requested, Has.None.Contains("communitydragon.org"));
        }

        [Test]
        public async Task RejectedConfirmationNeverStartsNetworkSource()
        {
            var transport = new RecordingTransport();
            var source = new NetworkStaticCatalogSource(new DataDragonAdapter(transport),
                new CommunityDragonAdapter(), new CatalogMerger(), transport);
            var presentation = NewPresentation();
            presentation.IncludeCommunityDragon = true;

            await presentation.ExecuteAsync(_patch, _ => false,
                (request, token) => source.LoadAsync(request, token), CancellationToken.None);

            Assert.That(transport.Requested, Is.Empty);
            Assert.That(presentation.IncludeCommunityDragon, Is.False);
        }

        private sealed class RecordingTransport : IHttpTransport
        {
            public List<string> Requested { get; } = new List<string>();

            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                Requested.Add(url);
                return Task.FromResult(new HttpResponseData(url, 200,
                    Encoding.UTF8.GetBytes("{\"data\":{}}"), "", "application/json", DateTime.UnixEpoch));
            }
        }

        private static StaticImportPresentation NewPresentation() =>
            new StaticImportPresentation(new StaticCatalogPaths(Path.GetTempPath()));
    }
}
