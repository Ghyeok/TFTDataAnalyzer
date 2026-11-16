using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Editor.Diagnostics;
using TFTAnalyzer.Editor.RiotApi;
using UnityEngine;
using static TFTAnalyzer.Tests.Diagnostics.MatchShapeTestSupport;

namespace TFTAnalyzer.Tests.Diagnostics
{
    /// <summary>TODO 5: no key, PUUID, match ID, region or raw version sentinel in reports, logs, exceptions or temp files.</summary>
    public sealed class MatchShapePrivacyTests
    {
        private static readonly string[] GameVersions =
        {
            "Version 16.18.123.456 (FAKE-BUILD-NOTE)",
            "Version 16.18.1 a3f9c2d17b4e8a6f0c5d9e2b1a7f4c8d3e6b0a9f",
            "Version 16.18.1 123e4567-e89b-12d3-a456-426614174000",
            "Version 16.18.1 (10.200.30.4)",
            "1234567890.18.1",
            "버전 16.18.1 서울서버",
            "Version 14.1.555.1234 (…) [PUBLIC] <Releases/14.1>",
        };

        private static readonly string[] Sentinels =
        {
            FakeKey, FakeSeed, SecondSeed, "KR_98765432", "PARTICIPANT-PUUID-SENTINEL", "SentinelPlayer", "SENTINEL-TAG",
            "asia", "ASIA", "KR", "FAKE-BUILD-NOTE", "a3f9c2d17b4e", "123e4567", "426614174000", "10.200.30.4",
            "1234567890", "버전", "서울", "PUBLIC", "Releases", "555.1234", "SENTINEL-DV", "SENTINEL_",
        };

        private string _root;
        private readonly List<string> _logs = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _root = CreateTempRoot();
            _logs.Clear();
            Application.logMessageReceivedThreaded += Capture;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceivedThreaded -= Capture;
            DeleteTempRoot(_root);
        }

        private void Capture(string message, string stackTrace, LogType type)
        {
            lock (_logs) _logs.Add(message + "\n" + stackTrace);
        }

        private static void AssertClean(string text, string where)
        {
            foreach (var sentinel in Sentinels)
                Assert.That(text, Does.Not.Contain(sentinel), where + " contains " + sentinel);
        }

        private static string Describe(MatchShapeRunResult result) => JsonConvert.SerializeObject(result);

        private (DiagnosticHarness Harness, MatchShapeReportFileSink Sink, List<byte[]> Temps) Harness(int ids, Func<int, Newtonsoft.Json.Linq.JObject> detail = null)
        {
            var temps = new List<byte[]>();
            var sink = new MatchShapeReportFileSink(_root, new[] { Path.Combine(_root, "Library", "TftAnalyzerStore") })
            {
                BeforeCommit = temp => temps.Add(File.ReadAllBytes(temp))
            };
            var harness = new DiagnosticHarness(ids, detail) { SinkOverride = sink };
            return (harness, sink, temps);
        }

        [Test]
        public async Task ReportTemporaryFileLogsAndResultCarryNoSentinel()
        {
            var (harness, sink, temps) = Harness(20, i =>
            {
                var match = Match(i, queue: i % 5 == 4 ? 1090 : 1100, gameVersion: GameVersions[i % GameVersions.Length],
                    dataVersion: i % 3 == 0 ? "SENTINEL-DV-77" : "2");
                var participant = match["info"]["participants"][0];
                participant["units"][0]["character_id"] = "SENTINEL_UNIT_" + i;
                participant["augments"] = new Newtonsoft.Json.Linq.JArray("SENTINEL_AUGMENT");
                return match;
            });
            var result = await harness.RunAsync();
            Assert.That(result.ReportPublished, Is.True);
            Assert.That(temps, Has.Count.EqualTo(1));
            AssertClean(Encoding.UTF8.GetString(temps[0]), "temporary file");
            AssertClean(File.ReadAllText(sink.LastReportPath, Encoding.UTF8), "report");
            AssertClean(Describe(result), "result");
            AssertClean(harness.Confirmation.Summary.Replace("지역 경로: asia", ""), "confirmation summary outside the region line");
            lock (_logs) Assert.That(_logs, Is.Empty, "The diagnostic must not log.");
        }

        [Test]
        public async Task FailurePathsCarryNoSentinelInResultsExceptionsLogsOrFiles()
        {
            var failures = new Func<Uri, int, RiotApiResponse>[]
            {
                (uri, i) => throw new IOException("native " + FakeKey + " " + uri.AbsoluteUri),
                (uri, i) => throw new RiotApiException(RiotApiFailure.TransportError),
                (uri, i) => i == 0 ? Json(IdList(3)) : throw new InvalidOperationException("detail " + uri.AbsoluteUri + " " + FakeSeed),
                (uri, i) => i == 0 ? Json(IdList(3)) : Json("{\"metadata\":{\"match_id\":\"" + MatchId(i) + "\"}, 'x':1}"),
                (uri, i) => Json("[\"" + MatchId(1) + "\", " + FakeKey + "]"),
                (uri, i) => Status(401),
            };
            foreach (var failure in failures)
            {
                var (harness, sink, temps) = Harness(3);
                harness.Transport.Respond = failure;
                var result = await harness.RunAsync();
                AssertClean(Describe(result), "result");
                foreach (var temp in temps) AssertClean(Encoding.UTF8.GetString(temp), "temporary file");
                if (sink.LastReportPath != null) AssertClean(File.ReadAllText(sink.LastReportPath), "report");
            }

            var (publish, publishSink, _) = Harness(2);
            publishSink.BeforeCommit = temp => throw new IOException("disk " + FakeKey + " " + temp);
            var publishResult = await publish.RunAsync();
            Assert.That(publishResult.ReportFailureCode, Is.EqualTo("report_publish_failed"));
            AssertClean(Describe(publishResult), "publish failure result");
            var sinkError = Assert.Throws<IOException>(() =>
                publishSink.Publish(Encoding.UTF8.GetBytes("{}"), CancellationToken.None));
            AssertClean(sinkError.ToString(), "sink exception");
            Assert.That(sinkError.InnerException, Is.Null);

            var payload = "{\"metadata\":{\"match_id\":\"" + MatchId(1) + "\",\"match_id\":\"" + FakeSeed + "\"}}";
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(payload)))
            {
                var readError = Assert.Throws<InvalidDataException>(() => new MatchShapeReader(Input()).Read(input, CancellationToken.None));
                AssertClean(readError.ToString(), "reader exception");
                Assert.That(readError.InnerException, Is.Null);
            }
            var budgetError = Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DiagnosticRequestBudget(new ScriptedTransport(new FakeClock(), null), 1, () => TimeSpan.Zero, (d, t) => Task.CompletedTask)
                    .SendAsync(new Uri("https://asia.api.riotgames.com/tft/match/v1/matches/" + MatchId(1)),
                        new Dictionary<string, string> { ["X-Riot-Token"] = FakeKey }, CancellationToken.None));
            Assert.That(budgetError.Message, Does.Not.Contain(MatchId(1)).And.Not.Contain(FakeKey));
            lock (_logs) Assert.That(_logs, Is.Empty, "The diagnostic must not log.");
        }

        [Test]
        public async Task PreconditionFailuresDoNotEchoTheRejectedKeyOrSeed()
        {
            var harness = new DiagnosticHarness { Key = () => "RGAPI-\n" + FakeKey };
            AssertClean(Describe(await harness.RunAsync()), "key precondition");
            var seeds = new DiagnosticHarness { Seeds = () => FakeSeed + " " + SecondSeed };
            AssertClean(Describe(await seeds.RunAsync()), "seed precondition");
            lock (_logs) Assert.That(_logs, Is.Empty);
        }
    }
}
