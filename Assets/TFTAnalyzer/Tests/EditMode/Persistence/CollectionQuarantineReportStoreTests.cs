using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class CollectionQuarantineReportStoreTests
    {
        private string _root;
        private AnalyzerStorePaths _paths;
        private static readonly string Context = new string('a', 64);
        private static readonly DateTime Completed = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftQuarantineTests-" + Guid.NewGuid().ToString("N"));
            _paths = new AnalyzerStorePaths(Path.Combine(_root, "Project"));
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(Path.GetFullPath(_root), Does.StartWith(Path.Combine(Path.GetTempPath(), "TftQuarantineTests-")));
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        /// <summary>Publishes a bounded allowlisted document, then atomically replaces only the same context.</summary>
        [Test]
        public void SaveWritesOnlySafeFieldsAndReplacesOneContext()
        {
            var source = new[] { Unknown("unit", "fake-player"), Unknown("item", "fake-key"), Unknown("unit", "fake-player") };
            var report = new CollectionQuarantineReport(Context, Completed, 2, false, source);
            var store = new CollectionQuarantineReportStore(_paths);
            store.Save(report);
            var target = _paths.QuarantineReportPath(Context);
            var text = File.ReadAllText(target);
            var value = JObject.Parse(text);
            Assert.That(value.Properties().Select(x => x.Name), Is.EquivalentTo(new[]
                { "schemaVersion", "requestContextHash", "completedUtc", "quarantinedMatches", "truncated", "entries" }));
            Assert.That((int)value["quarantinedMatches"], Is.EqualTo(2));
            Assert.That((string)value["requestContextHash"], Is.EqualTo(Context));
            Assert.That(((JArray)value["entries"]).Select(x => (string)x["kind"]), Is.EqualTo(new[] { "item", "unit" }));
            Assert.That(text, Does.Not.Contain("fake-player").And.Not.Contain("fake-key").And.Not.Contain("$type"));
            source[0] = Unknown("augment", "changed-source");
            Assert.That(report.Entries.Select(x => x.Kind), Is.EqualTo(new[] { "item", "unit" }));
            store.Save(new CollectionQuarantineReport(Context, Completed.AddMinutes(1), 3, true, report.Entries));
            value = JObject.Parse(File.ReadAllText(target));
            Assert.That((int)value["quarantinedMatches"], Is.EqualTo(3));
            Assert.That((bool)value["truncated"], Is.True);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(target)), Has.Length.EqualTo(1));
        }

        /// <summary>Does not overwrite a prior report or leave its staging file when publication fails.</summary>
        [Test]
        public void FailedReplacementPreservesPreviousBytesAndCleansOwnedTemporary()
        {
            var report = Report();
            new CollectionQuarantineReportStore(_paths).Save(report);
            var target = _paths.QuarantineReportPath(Context);
            var original = File.ReadAllBytes(target);
            var store = new CollectionQuarantineReportStore(_paths, (source, destination) => throw new IOException("fake-secret-path"));
            var error = Assert.Throws<IOException>(() => store.Save(report));
            Assert.That(error.Message, Is.EqualTo("quarantine_report_save_failed"));
            Assert.That(error.InnerException, Is.Null);
            Assert.That(File.ReadAllBytes(target), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(target)), Has.Length.EqualTo(1));
        }

        /// <summary>Counts quarantined matches separately while bounding distinct hash diagnostics.</summary>
        [Test]
        public void AccumulatorDeduplicatesAndStopsAt4096EntriesWithExplicitTruncation()
        {
            var value = new CollectionQuarantineAccumulator();
            var ids = Enumerable.Range(0, 4096).Select(x => Unknown("unit", "fake-unknown-" + x)).ToArray();
            Assert.That(value.Add(ids), Is.False);
            Assert.That(value.Add(new[] { ids[0] }), Is.False);
            Assert.That(value.Add(new[] { Unknown("item", "one-too-many") }), Is.True);
            var report = value.Freeze(Context, Completed);
            Assert.That(report.Entries.Count, Is.EqualTo(4096));
            Assert.That(report.QuarantinedMatches, Is.EqualTo(3));
            Assert.That(report.Truncated, Is.True);
            Assert.That(report.Entries.All(x => x.Kind == "unit"), Is.True);
        }

        /// <summary>Allows neither arbitrary filenames nor malformed context digests before touching disk.</summary>
        [TestCase(null)] [TestCase("../fake-key")] [TestCase("ABCDEF")] [TestCase("")]
        public void InvalidContextHashCannotCreateDirectories(string context)
        {
            Assert.Throws<ArgumentException>(() => _paths.QuarantineReportPath(context));
            Assert.That(Directory.Exists(_paths.RootDirectory), Is.False);
        }

        /// <summary>Binds diagnostics to the full requested catalog/content and filter context without persisting labels.</summary>
        [Test]
        public void ContextHashSeparatesCatalogContentAndEveryRequestedFilter()
        {
            var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            StaticCatalog Catalog(string hash) => new StaticCatalog(new PatchManifest(2, identity, "Fixture", DateTime.UnixEpoch,
                hash, null, DateTime.UnixEpoch, null, 0, 0, "", ""), null, null, null, null);
            var catalog = Catalog(new string('b', 64));
            var first = CollectionQuarantineReport.ContextHash(catalog, new MatchCollectionRequest("KR", identity, 1100, "MASTER+"));
            var other = new[]
            {
                CollectionQuarantineReport.ContextHash(Catalog(new string('c', 64)), new MatchCollectionRequest("KR", identity, 1100, "MASTER+")),
                CollectionQuarantineReport.ContextHash(catalog, new MatchCollectionRequest("NA1", identity, 1100, "MASTER+")),
                CollectionQuarantineReport.ContextHash(catalog, new MatchCollectionRequest("KR", identity, 1090, "MASTER+")),
                CollectionQuarantineReport.ContextHash(catalog, new MatchCollectionRequest("KR", identity, 1100, "ALL")),
                CollectionQuarantineReport.ContextHash(catalog, new MatchCollectionRequest("KR", new CatalogIdentity(
                    new PatchKey("TFTSet18", 18, "18.3", "16.19.1"), CatalogSourceProfile.OfficialOnly), 1100, "MASTER+"))
            };
            Assert.That(first, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(other.Distinct().Count(), Is.EqualTo(other.Length));
            Assert.That(other, Does.Not.Contain(first));
        }

        /// <summary>Rejects a linked parent and preserves the destination outside the diagnostics directory.</summary>
        [Test]
        public void JunctionAtDiagnosticsBlocksWriteWithoutTouchingDestination()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows junction fixture.");
            Directory.CreateDirectory(_paths.RootDirectory);
            var external = Path.Combine(_root, "external"); Directory.CreateDirectory(external);
            var sentinel = Path.Combine(external, "sentinel.txt"); File.WriteAllText(sentinel, "original");
            var link = Path.Combine(_paths.RootDirectory, "diagnostics");
            using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + external + "\"")
                { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                if (!process.WaitForExit(5000)) { process.Kill(); Assert.Fail("Owned junction fixture creation timed out."); }
                Assert.That(process.ExitCode, Is.Zero);
            }
            try
            {
                Assert.DoesNotThrow(() => _paths.EnsureSafe(), "A failed optional diagnostic must not block unrelated observation storage.");
                Assert.Throws<IOException>(() => new CollectionQuarantineReportStore(_paths).Save(Report()));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("original"));
                Assert.That(Directory.GetFiles(external), Has.Length.EqualTo(1));
            }
            finally { if (Directory.Exists(link)) Directory.Delete(link); }
        }

        /// <summary>Rejects an existing file link, including preserving its original external target.</summary>
        [Test]
        public void LinkedReportCannotOverwriteItsDestination()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows file-link fixture.");
            var target = _paths.QuarantineReportPath(Context);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var external = Path.Combine(_root, "external-report.txt"); File.WriteAllText(external, "original");
            if (!CreateSymbolicLink(target, external, 2)) Assert.Ignore("Host does not permit unprivileged symlinks.");
            try
            {
                Assert.Throws<IOException>(() => new CollectionQuarantineReportStore(_paths).Save(Report()));
                Assert.That(File.ReadAllText(external), Is.EqualTo("original"));
            }
            finally { File.Delete(target); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)] private static extern bool CreateSymbolicLink(string link, string target, int flags);

        private static UnknownStaticId Unknown(string kind, string raw) => new UnknownStaticId(kind,
            ContentHasher.Sha256Hex("static-id-v1|" + kind + "|" + raw));
        private static CollectionQuarantineReport Report() => new CollectionQuarantineReport(Context, Completed, 1, false,
            new[] { Unknown("unit", "fake-unknown") });
    }
}
