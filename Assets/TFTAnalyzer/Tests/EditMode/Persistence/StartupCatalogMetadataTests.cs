using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Tests.UI;
using UnityEngine;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class StartupCatalogMetadataTests
    {
        [Test]
        public void CompletionRecordIsOptionalAndDoesNotUseTheCatalogSourceTimestamp()
        {
            var identity = DataManagerTestSupport.Identity();
            var legacy = new CatalogIndexEntry(identity, "hash", "Assets/catalog.json");
            Assert.That(legacy.ImportCompletedUtc, Is.Null.Or.Empty);
            var row = Row("16.18.1", null);
            Assert.That(row.RetrievedUtc, Is.Not.EqualTo(DateTime.UnixEpoch));
            Assert.That(row.Entry.ImportCompletedUtc, Is.Null.Or.Empty);
            var completed = Row("16.18.2", "2026-10-19T01:00:00.0000000+00:00");
            Assert.That(completed.Entry.ImportCompletedUtc, Is.EqualTo("2026-10-19T01:00:00.0000000+00:00"));
        }

        [Test]
        public void LatestValidImportWinsIndependentlyOfSelectedRowsOrSourceTimestamps()
        {
            var rows = new[] { Row("16.18.2", "2026-10-18T01:00:00Z"),
                Row("16.18.1", "2026-10-19T01:00:00Z"), Row("16.18.9", "2026-10-20T01:00:00Z", false) };
            Assert.That(LastImportedSourceVersion.Select(rows), Is.EqualTo("16.18.1"));
            Assert.That(LastImportedSourceVersion.Select(rows.Reverse()), Is.EqualTo("16.18.1"));
        }

        [Test]
        public void EqualCompletionTimesUseIdentityOrdinalForDeterministicSelection()
        {
            var rows = new[] { Row("16.18.1", "2026-10-19T01:00:00Z"), Row("16.18.2", "2026-10-19T01:00:00Z") };
            Assert.That(LastImportedSourceVersion.Select(rows), Is.EqualTo("16.18.2"));
            Assert.That(LastImportedSourceVersion.Select(rows.Reverse()), Is.EqualTo("16.18.2"));
        }

        [Test]
        public void UnknownCompletionOnlyFallsBackWhenAllVerifiedVersionsAgree()
        {
            Assert.That(LastImportedSourceVersion.Select(new[] { Row("16.18.1", null), Row("16.18.1", "bad") }), Is.EqualTo("16.18.1"));
            Assert.That(LastImportedSourceVersion.Select(new[] { Row("16.18.1", null), Row("16.18.2", null) }), Is.Null);
            Assert.That(LastImportedSourceVersion.Select(Array.Empty<PatchRowViewModel>()), Is.Null);
        }

        [Test]
        public void ExactReimportUpdatesOnlyTheIndexCompletionAndRollbackRestoresIt()
        {
            var index = ScriptableObject.CreateInstance<CatalogIndex>();
            var file = Path.Combine(Path.GetTempPath(), "TftIndex_" + Guid.NewGuid().ToString("N"));
            var identity = DataManagerTestSupport.Identity();
            try
            {
                index.Upsert(identity, "unchanged-hash", "Assets/catalog.json", importCompletedUtc: "2026-10-18T01:00:00Z");
                File.WriteAllText(file, "unchanged-index-bytes");
                Assert.Throws<IOException>(() => CatalogIndexWriter.SaveWithRollback(index, identity, "unchanged-hash", "Assets/catalog.json",
                    false, file, () => { File.WriteAllText(file, "partial"); throw new IOException(); }, "2026-10-19T01:00:00Z"));
                Assert.That(index.Entries.Single().ImportCompletedUtc, Is.EqualTo("2026-10-18T01:00:00Z"));
                Assert.That(File.ReadAllText(file), Is.EqualTo("unchanged-index-bytes"));
                CatalogIndexWriter.SaveWithRollback(index, identity, "unchanged-hash", "Assets/catalog.json", false, file, () => { }, "2026-10-19T01:00:00Z");
                Assert.That(index.Entries.Single().ImportCompletedUtc, Is.EqualTo("2026-10-19T01:00:00Z"));
                Assert.That(index.Entries.Single().ContentHash, Is.EqualTo("unchanged-hash"));
            }
            finally { UnityEngine.Object.DestroyImmediate(index); if (File.Exists(file)) File.Delete(file); }
        }

        [Test]
        public void IsolatedPathsKeepEveryCatalogAndCacheConsumerInsideTheirInjectedRoots()
        {
            var root = Path.Combine(Path.GetTempPath(), "TftPaths_" + Guid.NewGuid().ToString("N"));
            var paths = new StaticCatalogPaths(root, "Assets/TftAnalyzerE2E_fixture/Generated", "Library/TftAnalyzerValidation/e2e_fixture/cache");
            var identity = DataManagerTestSupport.Identity();
            var generated = Path.Combine(root, "Assets", "TftAnalyzerE2E_fixture", "Generated");
            var cache = Path.Combine(root, "Library", "TftAnalyzerValidation", "e2e_fixture", "cache");
            Assert.That(paths.CanonicalFinalDirectory(identity), Does.StartWith(generated + Path.DirectorySeparatorChar));
            Assert.That(paths.LegacyFinalDirectory(identity.Patch), Does.StartWith(generated + Path.DirectorySeparatorChar));
            Assert.That(paths.IconsDirectory, Is.EqualTo(Path.Combine(generated, "Icons")));
            Assert.That(paths.IndexAssetPath, Is.EqualTo("Assets/TftAnalyzerE2E_fixture/Generated/CatalogIndex.asset"));
            Assert.That(paths.RawDirectory(identity), Does.StartWith(cache + Path.DirectorySeparatorChar));
            Assert.That(paths.StagingDirectory, Is.EqualTo(Path.Combine(cache, "staging")));
            Assert.That(paths.CatalogAssetPath(identity), Does.StartWith("Assets/TftAnalyzerE2E_fixture/Generated/"));
        }

        [TestCase("Assets/../escape", "Library/cache")]
        [TestCase("Library/generated", "Library/cache")]
        [TestCase("Assets/generated", "Assets/cache")]
        [TestCase("C:/escape", "Library/cache")]
        public void InjectedPathsRejectEscapesAndWrongProjectAreas(string generated, string cache)
        {
            Assert.Throws<ArgumentException>(() => new StaticCatalogPaths(Path.GetTempPath(), generated, cache));
        }

        private static PatchRowViewModel Row(string version, string completed, bool valid = true)
        {
            var row = DataManagerTestSupport.Row(DataManagerTestSupport.Identity(version), valid: valid);
            return new PatchRowViewModel(new CatalogIndexEntry(row.Identity, row.Entry.ContentHash, row.Entry.CatalogAssetPath, completed),
                row.Identity, row.Directory, row.Catalog, row.RetrievedUtc, row.ValidationErrorCount, row.ValidationWarningCount, row.ErrorCode);
        }
    }
}
