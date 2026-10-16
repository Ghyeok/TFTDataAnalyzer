using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using UnityEngine;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class DataManagerServiceTests
    {
        [Test]
        public async Task ExplicitVersionCheckUsesOnlyVersionsEndpointAndDoesNotCreateFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "TftManagerServiceTests", Guid.NewGuid().ToString("N"));
            var index = ScriptableObject.CreateInstance<CatalogIndex>();
            try
            {
                var transport = new VersionTransport();
                // No import is run here. The in-memory index prevents any AssetDatabase index write.
                var service = new DataManagerService(new StaticCatalogPaths(root), transport, () => index);
                Assert.That(service.LoadLocalCatalogs(), Is.Empty);
                Assert.That(transport.Urls, Is.Empty);
                var versions = await service.FetchVersionsAsync(CancellationToken.None);
                Assert.That(versions, Is.EqualTo(new[] { "16.18.2", "16.18.1" }));
                Assert.That(transport.Urls, Is.EqualTo(new[] { "https://ddragon.leagueoflegends.com/api/versions.json" }));
                Assert.That(Directory.Exists(root), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(index);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private sealed class VersionTransport : IHttpTransport
        {
            internal readonly List<string> Urls = new List<string>();
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Urls.Add(url);
                Assert.That(url, Is.EqualTo("https://ddragon.leagueoflegends.com/api/versions.json"),
                    "A version check must not request catalogs, images, or CommunityDragon.");
                return Task.FromResult(new HttpResponseData(url, 200,
                    Encoding.UTF8.GetBytes("[\"16.18.2\",\"16.18.1\"]"), "", "application/json", DateTime.UnixEpoch));
            }
        }
    }
}
