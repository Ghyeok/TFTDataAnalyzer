using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.DataDragon;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class DataDragonNetworkSmokeTests
    {
        [Test, Explicit("Uses the public Riot Data Dragon endpoint")]
        public async Task VersionsEndpoint_ReturnsAtLeastOneSemanticVersion()
        {
            var adapter = new DataDragonAdapter(new UnityWebRequestTransport());

            var versions = await adapter.FetchVersionsAsync(CancellationToken.None);

            Assert.That(versions, Is.Not.Empty);
            Assert.That(versions.Any(value => Version.TryParse(value, out _)), Is.True);
        }
    }
}
