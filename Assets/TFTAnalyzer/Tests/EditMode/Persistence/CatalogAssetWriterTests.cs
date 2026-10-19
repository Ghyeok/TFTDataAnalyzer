using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class CatalogAssetWriterTests
    {
        [Test]
        public void RawSourcesAreSeparatedByVersionAndProfile()
        {
            var root = Path.Combine(Path.GetTempPath(), "TftRawCacheTests", Guid.NewGuid().ToString("N"));
            try
            {
                var paths = new StaticCatalogPaths(root);
                var writer = new CatalogAssetWriter(paths);
                var directories = new System.Collections.Generic.List<string>();
                var counter = 0;
                foreach (var version in new[] { "16.18.1", "16.18.2" })
                foreach (var profile in new[] { CatalogSourceProfile.OfficialOnly, CatalogSourceProfile.OfficialPlusCommunityDragon })
                {
                    var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", version), profile);
                    var bytes = Encoding.UTF8.GetBytes("{\"versionProfile\":" + counter++ + "}");
                    writer.StoreRawSources(identity, new[]
                    {
                        new HttpResponseData("https://example.test/raw.json", 200, bytes, "", "application/json", DateTime.UnixEpoch)
                    });
                    var directory = paths.RawDirectory(identity);
                    directories.Add(directory);
                    var files = Directory.GetFiles(directory, "*.json");
                    Assert.That(files.Length, Is.EqualTo(1));
                    Assert.That(File.ReadAllBytes(files[0]), Is.EqualTo(bytes));
                }

                Assert.That(directories.Distinct().Count(), Is.EqualTo(4));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void PublishRejectsTamperedExistingCatalogEvenWhenManifestHashMatchesStagedHash()
        {
            var root = Path.Combine(Path.GetTempPath(), "TftPublishTests", Guid.NewGuid().ToString("N"));
            var paths = new StaticCatalogPaths(root);
            var finalDirectory = paths.AbsoluteAssetPath(paths.GeneratedRelativeDirectory + "/final");
            var stageRoot = paths.CreateStagingDirectory();
            var stageDirectory = Path.Combine(stageRoot, "patch");
            try
            {
                Directory.CreateDirectory(finalDirectory);
                Directory.CreateDirectory(stageDirectory);
                var goodBytes = new byte[] { 1, 2, 3 };
                var hash = ContentHasher.Sha256Hex(goodBytes);
                File.WriteAllBytes(Path.Combine(stageDirectory, "catalog.json"), goodBytes);
                File.WriteAllText(Path.Combine(stageDirectory, "manifest.json"), "{\"contentHash\":\"" + hash + "\"}");
                File.WriteAllBytes(Path.Combine(finalDirectory, "catalog.json"), new byte[] { 9, 9, 9 });
                File.WriteAllText(Path.Combine(finalDirectory, "manifest.json"), "{\"contentHash\":\"" + hash + "\"}");

                var staged = new StagedCatalog(stageRoot, stageDirectory, Path.Combine(stageRoot, "icons"), hash);
                Assert.Throws<InvalidDataException>(() => new CatalogAssetWriter(new StaticCatalogPaths(root)).Publish(staged, finalDirectory));
                Assert.That(File.ReadAllBytes(Path.Combine(finalDirectory, "catalog.json")), Is.EqualTo(new byte[] { 9, 9, 9 }));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
