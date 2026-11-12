using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.UI;
using TFTAnalyzer.Validation;
using UnityEngine;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class IncompleteMatchStoreTests
    {
        private string root;
        private IncompleteMatchStore store;
        private CatalogIndex index;
        private CatalogIdentity identity;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "TftIncompleteTests-" + Guid.NewGuid().ToString("N"));
            store = new IncompleteMatchStore(new AnalyzerStorePaths(Path.Combine(root, "Project")));
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(Path.GetFullPath(root), Does.StartWith(Path.Combine(Path.GetTempPath(), "TftIncompleteTests-")));
            if (index != null) UnityEngine.Object.DestroyImmediate(index);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void SavesOnlySafeDataAndDeletesOnlySelectedRecord()
        {
            var result = Save(Match(), "SYNTHETIC_SECRET_MATCH_ID", null, null, CancellationToken.None);
            Assert.That(result, Is.EqualTo(IncompleteSaveResult.Saved));
            var records = store.ReadAll(null, null, CancellationToken.None);
            Assert.That(records.Count, Is.EqualTo(1));
            Assert.That(records[0].Candidate.SetNumber.Value, Is.EqualTo(18));
            Assert.That(records[0].Candidate.Participants.Values[0].Augments.State, Is.EqualTo(CandidateFieldState.Missing));
            var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(files[0]), Does.Not.Contain("SYNTHETIC_SECRET_MATCH_ID"));
            Assert.That(store.Delete(records[0].Handle, CancellationToken.None), Is.True);
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Is.Empty);
            Assert.That(Save(Match(), "SYNTHETIC_SECRET_MATCH_ID", null, null, CancellationToken.None), Is.EqualTo(IncompleteSaveResult.Saved));
        }

        [Test]
        public void DuplicateTakesPrecedenceAt128WithoutAutomaticDeletion()
        {
            for (var i = 0; i < 128; i++)
                Assert.That(Save(Match(), "synthetic-" + i, null, null, CancellationToken.None), Is.EqualTo(IncompleteSaveResult.Saved));
            Assert.That(Save(Match(), "synthetic-0", null, null, CancellationToken.None), Is.EqualTo(IncompleteSaveResult.Duplicate));
            Assert.That(Save(Match(), "synthetic-new", null, null, CancellationToken.None), Is.EqualTo(IncompleteSaveResult.Full));
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Has.Length.EqualTo(128));
        }

        [Test]
        public void CorruptRecordCannotBypassLimitOrDuplicateProtection()
        {
            Save(Match(), "synthetic", null, null, CancellationToken.None);
            var file = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Single();
            File.WriteAllText(file, "{\"untrusted\":\"SECRET\"}");
            var error = Assert.Throws<IOException>(() => Save(Match(), "other", null, null, CancellationToken.None));
            Assert.That(error.ToString(), Does.Not.Contain("SECRET").And.Not.Contain(root));
            var damaged = store.ReadAll(null, null, CancellationToken.None).Single();
            Assert.That(damaged.IsCorrupt, Is.True);
            Assert.That(damaged.Candidate, Is.Null);
            Assert.That(store.Delete(damaged.Handle, CancellationToken.None), Is.True);
            Assert.That(Save(Match(), "other", null, null, CancellationToken.None), Is.EqualTo(IncompleteSaveResult.Saved));
        }

        [Test]
        public void CancelAndInvalidHandleCannotModifyRecords()
        {
            Save(Match(), "synthetic", null, null, CancellationToken.None);
            Assert.Throws<OperationCanceledException>(() => Save(Match(), "other", null, null, new CancellationToken(true)));
            Assert.Throws<ArgumentException>(() => store.Delete("../other", CancellationToken.None));
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void ExclusiveLeasePreventsConcurrentWritesAndPreservesExistingRecord()
        {
            Save(Match(), "first", null, null, CancellationToken.None);
            var directory = Path.Combine(root, "Project", "Library", "TftAnalyzerStore", "incomplete-matches");
            using (new FileStream(Path.Combine(directory, "store.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Throws<IOException>(() => Save(Match(), "second", null, null, CancellationToken.None));
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void CancellationAfterStagingCleansOnlyOwnedTemporaryFile()
        {
            Save(Match(), "first", null, null, CancellationToken.None);
            using (var cancellation = new CancellationTokenSource())
            {
                store = new IncompleteMatchStore(new AnalyzerStorePaths(Path.Combine(root, "Project")), () => cancellation.Cancel());
                Assert.Throws<OperationCanceledException>(() => Save(Match(), "second", null, null, cancellation.Token));
            }
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void FailedPublicationCleansStagingAndDoesNotDamagePriorRecord()
        {
            Save(Match(), "first", null, null, CancellationToken.None);
            store = new IncompleteMatchStore(new AnalyzerStorePaths(Path.Combine(root, "Project")), publish: (source, target) => throw new IOException("SYNTHETIC_SECRET"));
            var error = Assert.Throws<IOException>(() => Save(Match(), "second", null, null, CancellationToken.None));
            Assert.That(error.ToString(), Does.Not.Contain("SYNTHETIC_SECRET").And.Not.Contain(root));
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void CancellationAfterAtomicMoveReportsCommittedRecord()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                store = new IncompleteMatchStore(new AnalyzerStorePaths(Path.Combine(root, "Project")), publish: (source, target) =>
                { File.Move(source, target); cancellation.Cancel(); });
                Assert.That(Save(Match(), "first", null, null, cancellation.Token), Is.EqualTo(IncompleteSaveResult.Saved));
            }
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void StoreDirectoryJunctionCannotReachExternalFiles()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows junction fixture.");
            var paths = new AnalyzerStorePaths(Path.Combine(root, "Project"));
            Directory.CreateDirectory(paths.RootDirectory);
            var external = Path.Combine(root, "external");
            Directory.CreateDirectory(external);
            var sentinel = Path.Combine(external, "sentinel.txt");
            File.WriteAllText(sentinel, "original");
            var link = Path.Combine(paths.RootDirectory, "incomplete-matches");
            using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + external + "\"")
                { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                if (!process.WaitForExit(5000)) { process.Kill(); Assert.Fail("Fixture junction creation timed out."); }
                Assert.That(process.ExitCode, Is.Zero);
            }
            try
            {
                Assert.Throws<IOException>(() => Save(Match(), "synthetic", null, null, CancellationToken.None));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("original"));
                Assert.That(Directory.GetFiles(external), Has.Length.EqualTo(1));
            }
            finally { Directory.Delete(link); }
        }

        [Test]
        public void ReferenceCatalogMustMatchOnReloadBeforeIdsCanBeShown()
        {
            var catalog = PublishCatalog();
            var f = new IncompleteCandidateFactory(catalog, IncompleteCandidateContext.UnsupportedVersion, 1100);
            var unit = f.Unit(f.Id(CandidateIdKind.Unit, "TEST18_Unit"), f.Number(CandidateNumberKind.UnitTier, 2),
                f.Items(Array.Empty<CandidateIdField>()));
            var participant = f.Participant(1, f.Number(CandidateNumberKind.Placement, 2),
                f.Number(CandidateNumberKind.LastRound, 20), f.Units(new[] { unit }),
                f.Traits(Array.Empty<IncompleteTraitCandidate>()), f.AugmentsState(CandidateFieldState.Missing));
            var match = f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100),
                f.Participants(new[] { participant }));
            Save(match, "synthetic-match", index, identity, CancellationToken.None);
            Assert.That(store.ReadAll(index, identity, CancellationToken.None)[0].ReferenceAvailable, Is.True);
            Assert.That(store.ReadAll(index, identity, CancellationToken.None)[0].ReferenceCatalog.Units[0].StableId, Is.EqualTo("TEST18_Unit"));
            CandidateIdField First(CatalogIndex i, CatalogIdentity id) => store.ReadAll(i, id, CancellationToken.None)[0].Candidate.Participants.Values[0].Units.Values[0].StableId;
            Assert.That(First(index, identity).Value, Is.EqualTo("TEST18_Unit"));
            Assert.That(First(null, null).Value, Is.Null);
            Assert.That(First(null, null).State, Is.EqualTo(CandidateFieldState.CatalogUnconfirmed));
            File.AppendAllText(Path.Combine(new StaticCatalogPaths(Path.Combine(root, "Project")).CanonicalFinalDirectory(identity), "catalog.json"), " ");
            Assert.That(store.ReadAll(index, identity, CancellationToken.None)[0].ReferenceAvailable, Is.False);
            Assert.That(store.ReadAll(index, identity, CancellationToken.None)[0].ReferenceCatalog, Is.Null);
            Assert.That(First(index, identity).Value, Is.Null);
            Assert.That(First(index, identity).Meaning, Is.EqualTo(CandidateMeaning.Unverified));
        }

        [Test]
        public void ForeignCatalogCannotBeUsedToPublishCandidateIds()
        {
            PublishCatalog();
            var f = new IncompleteCandidateFactory(Catalog("UNIT_A"), IncompleteCandidateContext.UnsupportedVersion, 1100);
            var p = f.Participant(1, f.Number(CandidateNumberKind.Placement, 1), f.Number(CandidateNumberKind.LastRound, 1),
                f.Units(new[] { f.Unit(f.Id(CandidateIdKind.Unit, "UNIT_A"), f.Number(CandidateNumberKind.UnitTier, 1),
                    f.Items(Array.Empty<CandidateIdField>())) }), f.Traits(Array.Empty<IncompleteTraitCandidate>()),
                f.Augments(Array.Empty<CandidateIdField>()));
            var match = f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100), f.Participants(new[] { p }));
            Assert.Throws<IOException>(() => Save(match, "fake-match", index, identity, CancellationToken.None));
            Assert.That(store.ReadAll(null, null, CancellationToken.None), Is.Empty);
        }

        [Test]
        public void CandidateCannotBeReboundToAnotherPublishedCatalogWithSameId()
        {
            var first = PublishCatalog();
            var firstIdentity = identity;
            var f = new IncompleteCandidateFactory(first, IncompleteCandidateContext.UnsupportedVersion, 1100);
            var unit = f.Unit(f.Id(CandidateIdKind.Unit, "TEST18_Unit"), f.Number(CandidateNumberKind.UnitTier, 1),
                f.Items(Array.Empty<CandidateIdField>()));
            var p = f.Participant(1, f.Number(CandidateNumberKind.Placement, 1), f.Number(CandidateNumberKind.LastRound, 1),
                f.Units(new[] { unit }), f.Traits(Array.Empty<IncompleteTraitCandidate>()), f.Augments(Array.Empty<CandidateIdField>()));
            var candidate = f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100), f.Participants(new[] { p }));
            var parsed = new MatchReadWithIncompleteResult(null, null, candidate, ContentHasher.Sha256Hex("incomplete-match-v1|synthetic"),
                firstIdentity, ContentHasher.Sha256Hex(CanonicalCatalogSerializer.Serialize(first)));
            var secondIdentity = DataManagerTestSupport.Identity("16.18.2");
            var paths = new StaticCatalogPaths(Path.Combine(root, "Project"));
            var second = DataManagerTestSupport.Catalog(secondIdentity, "Different");
            var writer = new CatalogAssetWriter(paths);
            var staging = paths.CreateStagingDirectory();
            var staged = writer.WriteStagedCatalog(second, new CatalogValidator().Validate(second), staging);
            writer.Publish(staged, paths.CanonicalFinalDirectory(secondIdentity));
            writer.CleanupStaging(staging);
            index.Upsert(secondIdentity, staged.ContentHash, paths.CatalogAssetPath(secondIdentity));
            Assert.Throws<IOException>(() => store.Save(parsed, index, secondIdentity, CancellationToken.None));
            Assert.Throws<IOException>(() => store.Save(parsed, null, null, CancellationToken.None));
        }

        [Test]
        public void DuplicateFieldsAndOversizedRecordsFailClosedWithoutLeakingContent()
        {
            Save(Match(), "synthetic", null, null, CancellationToken.None);
            var file = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Single();
            File.WriteAllText(file, "{\"schema\":1,\"schema\":1,\"secret\":\"SECRET\"}");
            Assert.That(store.ReadAll(null, null, CancellationToken.None).Single().IsCorrupt, Is.True);
            using (var stream = new FileStream(file, FileMode.Create)) stream.SetLength(256 * 1024 + 1);
            Assert.Throws<IOException>(() => Save(Match(), "other", null, null, CancellationToken.None));
        }

        [TestCase("quoted-schema")]
        [TestCase("single-quotes")]
        [TestCase("octal-number")]
        [TestCase("trailing-comma")]
        public void NoncanonicalOrCoercedJsonIsReportedAsCorrupt(string mutation)
        {
            Save(Match(), "synthetic", null, null, CancellationToken.None);
            var file = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Single();
            var source = File.ReadAllText(file);
            var changed = mutation == "quoted-schema" ? source.Replace("\"schema\":1", "\"schema\":\"1\"") :
                mutation == "single-quotes" ? source.Replace("\"schema\"", "'schema'") :
                mutation == "octal-number" ? source.Replace("\"placement\":[0,1]", "\"placement\":[0,010]") :
                source.Substring(0, source.Length - 1) + ",}";
            Assert.That(changed, Is.Not.EqualTo(source));
            File.WriteAllText(file, changed);
            Assert.That(store.ReadAll(null, null, CancellationToken.None).Single().IsCorrupt, Is.True);
            Assert.Throws<IOException>(() => Save(Match(), "other", null, null, CancellationToken.None));
        }

        private static StaticCatalog Catalog(string unitId)
        {
            var name = new LocalizedText("name", "name");
            var manifest = new PatchManifest(1, new PatchKey("TEST_SET", 18, "18.2", "16.18.1"), "set",
                DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, "", "");
            return new StaticCatalog(manifest,
                new[] { new UnitRecord(unitId, name, 1, null, "", null) }, null,
                new[] { new ItemRecord("ITEM_A", name, null, null, null, "", null) }, null);
        }

        private StaticCatalog PublishCatalog()
        {
            identity = DataManagerTestSupport.Identity();
            var paths = new StaticCatalogPaths(Path.Combine(root, "Project"));
            index = ScriptableObject.CreateInstance<CatalogIndex>();
            var catalog = DataManagerTestSupport.Catalog(identity);
            var writer = new CatalogAssetWriter(paths);
            var staging = paths.CreateStagingDirectory();
            var staged = writer.WriteStagedCatalog(catalog, new CatalogValidator().Validate(catalog), staging);
            writer.Publish(staged, paths.CanonicalFinalDirectory(identity));
            writer.CleanupStaging(staging);
            index.Upsert(identity, staged.ContentHash, paths.CatalogAssetPath(identity));
            return new PublishedCatalogLoader(paths).Load(index).Single().Catalog;
        }

        private IncompleteSaveResult Save(IncompleteMatchCandidate candidate, string rawId, CatalogIndex catalogIndex,
            CatalogIdentity catalogIdentity, CancellationToken token)
        {
            var catalog = catalogIdentity == null ? null : new PublishedCatalogLoader(
                new StaticCatalogPaths(Path.Combine(root, "Project"))).Load(catalogIndex).Single(x => x.IsSelectable && x.Identity.Equals(catalogIdentity)).Catalog;
            var parsed = new MatchReadWithIncompleteResult(null, null, candidate,
                ContentHasher.Sha256Hex("incomplete-match-v1|" + rawId), catalogIdentity,
                catalog == null ? null : ContentHasher.Sha256Hex(CanonicalCatalogSerializer.Serialize(catalog)));
            return store.Save(parsed, catalogIndex, catalogIdentity, token);
        }

        private static IncompleteMatchCandidate Match()
        {
            var f = new IncompleteCandidateFactory(null, IncompleteCandidateContext.UnsupportedVersion, 1100);
            var p = f.Participant(1, f.Number(CandidateNumberKind.Placement, 1),
                f.NumberState(CandidateNumberKind.LastRound, CandidateFieldState.Null),
                f.UnitsState(CandidateFieldState.Missing), f.TraitsState(CandidateFieldState.InvalidFormat),
                f.AugmentsState(CandidateFieldState.Missing));
            return f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100),
                f.Participants(new[] { p }));
        }
    }
}
