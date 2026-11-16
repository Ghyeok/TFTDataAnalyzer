using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Editor.Diagnostics;
using UnityEngine;
using static TFTAnalyzer.Tests.Diagnostics.MatchShapeTestSupport;

namespace TFTAnalyzer.Tests.Diagnostics
{
    /// <summary>TODO 4: cancellation, atomic publication, allowlist schema and report location.</summary>
    public sealed class MatchShapeReportTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "TftDiagnosticsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string StoreRoot => Path.Combine(_root, "Library", "TftAnalyzerStore");
        private MatchShapeReportFileSink Sink() => new MatchShapeReportFileSink(_root, new[] { StoreRoot });
        private string ReportDirectory => Path.Combine(_root, "Library", "TftAnalyzerDiagnostics", "match-shape");

        [Test]
        public async Task CancellationBeforeTheListSendsNothingAndRecordsACancelledPartialReport()
        {
            var harness = new DiagnosticHarness();
            using (var cancellation = new CancellationTokenSource())
            {
                harness.Confirmation.OnConfirm = cancellation.Cancel;
                var result = await harness.RunAsync(cancellation.Token);
                Assert.That(harness.Transport.Sent, Is.Empty);
                Assert.That(result.Status, Is.EqualTo("cancelled"));
                Assert.That(result.RunConsumed, Is.False);
                Assert.That((string)harness.Sink.Last["status"], Is.EqualTo("cancelled"));
                Assert.That((bool)harness.Sink.Last["partial"], Is.True);
                Assert.That((int)harness.Sink.Last["requests"]["total"], Is.Zero);
                Assert.That((string)harness.Sink.Last["classification"]["result"], Is.EqualTo("undecidable"));
            }
        }

        [Test]
        public async Task CancellationDuringADetailStopsFurtherSends()
        {
            var harness = new DiagnosticHarness(ids: 10);
            using (var cancellation = new CancellationTokenSource())
            {
                harness.Transport.BeforeRespond = index => { if (index == 2) cancellation.Cancel(); };
                var result = await harness.RunAsync(cancellation.Token);
                Assert.That(harness.Transport.Sent, Has.Count.EqualTo(3));
                Assert.That(result.Status, Is.EqualTo("cancelled"));
                Assert.That(result.ReasonCode, Is.EqualTo("cancelled"));
                Assert.That((int)harness.Sink.Last["requests"]["detail"], Is.EqualTo(2));
                Assert.That((string)harness.Sink.Last["stop"]["stage"], Is.EqualTo("detail"));
                Assert.That((bool)harness.Sink.Last["partial"], Is.True);
                Assert.That(MatchShapeReportSchema.IsValid(harness.Sink.Last), Is.True);
            }
        }

        [Test]
        public async Task CancellationDuringReportWriteLeavesNoReportOrTemporaryFileAndSendsNothingMore()
        {
            var harness = new DiagnosticHarness(ids: 3);
            using (var cancellation = new CancellationTokenSource())
            {
                var sink = Sink();
                sink.BeforeCommit = temp =>
                {
                    Assert.That(File.Exists(temp), Is.True);
                    cancellation.Cancel();
                };
                harness.SinkOverride = sink;
                var result = await harness.RunAsync(cancellation.Token);
                Assert.That(harness.Transport.Sent, Has.Count.EqualTo(4));
                Assert.That(result.ReportPublished, Is.False);
                Assert.That(result.ReportFailureCode, Is.EqualTo("report_publish_cancelled"));
                Assert.That(result.Status, Is.EqualTo("cancelled"));
                Assert.That(Directory.GetFiles(ReportDirectory), Is.Empty);
            }
        }

        [Test]
        public async Task InjectedPublicationFailureLeavesNoPartialOrTemporaryFileAndKeepsThePreviousReport()
        {
            var first = new DiagnosticHarness(ids: 2) { SinkOverride = Sink() };
            Assert.That((await first.RunAsync()).ReportPublished, Is.True);
            var path = Path.Combine(ReportDirectory, MatchShapeReportFileSink.FileName);
            var previous = File.ReadAllBytes(path);

            var sink = Sink();
            sink.BeforeCommit = temp => throw new IOException("injected " + FakeKey);
            var failing = new DiagnosticHarness(ids: 3) { SinkOverride = sink };
            var result = await failing.RunAsync();
            Assert.That(result.ReportPublished, Is.False);
            Assert.That(result.ReportFailureCode, Is.EqualTo("report_publish_failed"));
            Assert.That(Directory.GetFiles(ReportDirectory).Select(Path.GetFileName), Is.EqualTo(new[] { MatchShapeReportFileSink.FileName }));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(previous));
        }

        [Test]
        public async Task SuccessfulPublicationWritesOneSchemaValidReportAtomically()
        {
            var harness = new DiagnosticHarness(ids: 12) { SinkOverride = Sink() };
            var result = await harness.RunAsync();
            Assert.That(result.ReportPublished, Is.True);
            Assert.That(result.Classification, Is.EqualTo("shape_conditions_met_pending_external_evidence"));
            var files = Directory.GetFiles(ReportDirectory);
            Assert.That(files.Select(Path.GetFileName), Is.EqualTo(new[] { MatchShapeReportFileSink.FileName }));
            var bytes = File.ReadAllBytes(files[0]);
            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            var report = JObject.Parse(Encoding.UTF8.GetString(bytes));
            Assert.That(MatchShapeReportSchema.IsValid(report), Is.True);
            Assert.That((string)report["status"], Is.EqualTo("completed"));
            Assert.That((bool)report["partial"], Is.False);
            Assert.That((int)report["requests"]["total"], Is.EqualTo(13));
            Assert.That((double)report["classification"]["unobservedShapeUpper95"], Is.EqualTo(0.25));
        }

        [Test]
        public async Task ClassificationFollowsTheApprovedOrder()
        {
            async Task<JObject> Run(int ids, Func<int, JObject> detail)
            {
                var harness = new DiagnosticHarness(ids, detail);
                await harness.RunAsync();
                return harness.Sink.Last;
            }
            var small = await Run(9, i => Match(i));
            Assert.That((string)small["classification"]["result"], Is.EqualTo("undecidable"));
            Assert.That((string)small["classification"]["reason"], Is.EqualTo("insufficient_sample"));
            var otherQueues = await Run(20, i => Match(i, queue: i < 11 ? 1090 : 1100));
            Assert.That((int)otherQueues["classification"]["targetSampleCount"], Is.EqualTo(9));
            Assert.That((string)otherQueues["classification"]["reason"], Is.EqualTo("insufficient_sample"));
            var mixed = await Run(12, i => Match(i, gameVersion: i == 0 ? "Version 16.18.1.2 (FAKE-BUILD-NOTE)" : "Version 16.18.123.456 (FAKE-BUILD-NOTE)"));
            Assert.That((string)mixed["classification"]["reason"], Is.EqualTo("multiple_game_version_signatures"));
            var mismatch = await Run(12, i => Match(i, gameVersion: "Version 16.17.123.456 (FAKE-BUILD-NOTE)"));
            Assert.That((string)mismatch["classification"]["result"], Is.EqualTo("c_or_d"));
            Assert.That((string)mismatch["classification"]["reason"], Is.EqualTo("release_mismatch"));
            var unextractable = await Run(12, i => Match(i, gameVersion: "Build 16.18 (FAKE)"));
            Assert.That((string)unextractable["classification"]["reason"], Is.EqualTo("release_unextractable"));
            var dataVersions = await Run(12, i => Match(i, dataVersion: i % 2 == 0 ? "2" : "SENTINEL-DV-77"));
            Assert.That((string)dataVersions["classification"]["result"], Is.EqualTo("shape_conditions_met_pending_external_evidence"));
            Assert.That((int)dataVersions["classification"]["dataVersionSignatureCount"], Is.EqualTo(2));
            var partial = new DiagnosticHarness(12);
            var inner = partial.Transport.Respond;
            partial.Transport.Respond = (uri, i) => i == 12 ? Status(500) : inner(uri, i);
            await partial.RunAsync();
            Assert.That((string)partial.Sink.Last["classification"]["reason"], Is.EqualTo("partial_run"));
        }

        [Test]
        public async Task SchemaAcceptsGeneratedReportsAndRejectsEveryOutOfAllowlistMutation()
        {
            var harness = new DiagnosticHarness(ids: 4, detail: i => Match(i, queue: i == 3 ? 1090 : 1100));
            await harness.RunAsync();
            var valid = harness.Sink.Last;
            Assert.That(MatchShapeReportSchema.IsValid(valid), Is.True);
            var mutations = new (string Name, Action<JObject> Mutate)[]
            {
                ("extra root key", r => r["matchIds"] = new JArray("KR_1")),
                ("missing root key", r => r.Remove("queues")),
                ("unknown status", r => r["status"] = "ok"),
                ("unknown reason", r => r["stop"]["reason"] = "asia"),
                ("negative count", r => r["requests"]["list"] = -1),
                ("budget overflow", r => { r["requests"]["detail"] = 21; r["requests"]["total"] = 22; }),
                ("string count", r => r["unreadableDetails"] = "0"),
                ("region queue", r => r["queues"][0]["queue"] = "asia"),
                ("extra queue key", r => r["queues"][0]["region"] = "asia"),
                ("raw signature", r => r["queues"][0]["gameVersion"]["signatures"][0]["signature"] = "Version 16.18"),
                ("long groups", r => r["queues"][0]["gameVersion"]["leadingGroups"][0]["value"] = "1618.1"),
                ("unknown type", r => r["queues"][0]["gameVersion"]["signatures"][0]["type"] = "boolean"),
                ("field type value", r => r["queues"][0]["requiredFieldRows"][0]["fields"]["metadata.match_id"] = "KR_1"),
                ("extra field", r => ((JObject)r["queues"][0]["requiredFieldRows"][0]["fields"])["puuid"] = "string"),
                ("top5 overflow", r => r["compositionDiversity"]["top5Frequencies"] = new JArray(1, 1, 1, 1, 1, 1)),
                ("composition content", r => r["compositionDiversity"]["examples"] = new JArray("TFT18_UnitA")),
                ("unknown classification", r => r["classification"]["result"] = "b"),
                ("upper bound", r => r["classification"]["unobservedShapeUpper95"] = 2.0),
                ("unknown kind", r => ((JObject)r["queues"][0]["unknownStaticIds"])["ids"] = new JObject()),
            };
            foreach (var (name, mutate) in mutations)
            {
                var copy = (JObject)valid.DeepClone();
                mutate(copy);
                Assert.That(MatchShapeReportSchema.IsValid(copy), Is.False, name);
            }
            Assert.That(MatchShapeReportSchema.IsValid(null), Is.False);
            Assert.That(MatchShapeReportSchema.IsValid(new JArray()), Is.False);
        }

        [Test]
        public void ReportDirectoryIsOutsideStoreRootsAndAssets()
        {
            var sink = Sink();
            Assert.That(sink.ReportDirectory, Is.EqualTo(Path.GetFullPath(ReportDirectory)));
            Assert.That(sink.ReportDirectory, Does.Not.StartWith(Path.GetFullPath(StoreRoot) + Path.DirectorySeparatorChar));
            Assert.That(sink.ReportDirectory, Does.Not.StartWith(Path.Combine(_root, "Assets")));
            var external = Path.Combine(_root, "external-store");
            Assert.DoesNotThrow(() => new MatchShapeReportFileSink(_root, new[] { StoreRoot, external }));
            foreach (var overlapping in new[]
            {
                Path.Combine(_root, "Library", "TftAnalyzerDiagnostics"), ReportDirectory,
                Path.Combine(ReportDirectory, "nested"), Path.Combine(_root, "Library"), _root
            })
                Assert.Throws<ArgumentException>(() => new MatchShapeReportFileSink(_root, new[] { overlapping }), overlapping);
            Assert.Throws<ArgumentException>(() => new MatchShapeReportFileSink(_root, null));
        }

        [Test]
        public void JunctionOnTheReportPathIsRejectedWithoutTouchingItsTarget()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows junction fixture.");
            var external = Path.Combine(_root, "external");
            Directory.CreateDirectory(external);
            File.WriteAllText(Path.Combine(external, "sentinel.txt"), "original");
            var link = Path.Combine(_root, "Library", "TftAnalyzerDiagnostics");
            Directory.CreateDirectory(Path.GetDirectoryName(link));
            using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + external + "\"")
                { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                if (!process.WaitForExit(5000)) { process.Kill(); Assert.Fail("Owned junction fixture creation timed out."); }
                Assert.That(process.ExitCode, Is.Zero);
            }
            try
            {
                Assert.Throws<IOException>(() => Sink().Publish(Encoding.UTF8.GetBytes("{}"), CancellationToken.None));
                Assert.That(Directory.GetFileSystemEntries(external).Select(Path.GetFileName), Is.EqualTo(new[] { "sentinel.txt" }));
                Assert.That(File.ReadAllText(Path.Combine(external, "sentinel.txt")), Is.EqualTo("original"));
            }
            finally { if (Directory.Exists(link)) Directory.Delete(link); }
        }
    }
}
