using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Editor.Diagnostics;
using TFTAnalyzer.Editor.RiotApi;
using static TFTAnalyzer.Tests.Diagnostics.MatchShapeTestSupport;

namespace TFTAnalyzer.Tests.Diagnostics
{
    /// <summary>TODO 2: physical send budget, single attempts, stop rules, spacing, preconditions and reload locking.</summary>
    public sealed class MatchShapeRequestBudgetTests
    {
        private static readonly Uri ListUri = new Uri("https://asia.api.riotgames.com/tft/match/v1/matches/by-puuid/seed/ids?start=0&count=20");
        private static Uri DetailUri(int i) => new Uri("https://asia.api.riotgames.com/tft/match/v1/matches/KR_" + i);
        private static readonly IReadOnlyDictionary<string, string> Headers = new Dictionary<string, string> { ["X-Riot-Token"] = FakeKey };

        [Test]
        public async Task BudgetWrapperCountsPhysicalSendsAndRejectsSendsBeyondTheLimits()
        {
            var clock = new FakeClock();
            var inner = new ScriptedTransport(clock, null, ids: 2);
            var budget = new DiagnosticRequestBudget(inner, 2, () => clock.Now, clock.Delay);
            Assert.ThrowsAsync<InvalidOperationException>(() => budget.SendAsync(DetailUri(0), Headers, default),
                "A detail send before the single list send is outside the plan.");
            (await budget.SendAsync(ListUri, Headers, default)).Dispose();
            Assert.ThrowsAsync<InvalidOperationException>(() => budget.SendAsync(ListUri, Headers, default));
            (await budget.SendAsync(DetailUri(0), Headers, default)).Dispose();
            (await budget.SendAsync(DetailUri(1), Headers, default)).Dispose();
            Assert.ThrowsAsync<InvalidOperationException>(() => budget.SendAsync(DetailUri(2), Headers, default));
            Assert.That(budget.ListSends, Is.EqualTo(1));
            Assert.That(budget.DetailSends, Is.EqualTo(2));
            Assert.That(budget.TotalSends, Is.EqualTo(3));
            Assert.That(inner.Sent, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task BudgetWrapperSpacesEverySendByAtLeastOnePointTwoSeconds()
        {
            var clock = new FakeClock();
            var inner = new ScriptedTransport(clock, null, ids: 3);
            var budget = new DiagnosticRequestBudget(inner, 3, () => clock.Now, clock.Delay);
            (await budget.SendAsync(ListUri, Headers, default)).Dispose();
            clock.Now += TimeSpan.FromMilliseconds(300);
            for (var i = 0; i < 3; i++) (await budget.SendAsync(DetailUri(i), Headers, default)).Dispose();
            var times = inner.Sent.Select(request => request.At).ToArray();
            for (var i = 1; i < times.Length; i++)
                Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(1200)));
            Assert.That(clock.Delays[0], Is.EqualTo(TimeSpan.FromMilliseconds(900)));
        }

        [Test]
        public async Task ClosedBudgetNeverSendsAgainAndFailedSendsStillCount()
        {
            var clock = new FakeClock();
            var inner = new ScriptedTransport(clock, null) { Respond = (uri, i) => throw new RiotApiException(RiotApiFailure.TransportError) };
            var budget = new DiagnosticRequestBudget(inner, 20, () => clock.Now, clock.Delay);
            Assert.ThrowsAsync<RiotApiException>(() => budget.SendAsync(ListUri, Headers, default));
            Assert.That(budget.ListSends, Is.EqualTo(1));
            budget.Close();
            Assert.ThrowsAsync<InvalidOperationException>(() => budget.SendAsync(DetailUri(0), Headers, default));
            Assert.That(inner.Sent, Has.Count.EqualTo(1));
            await Task.CompletedTask;
        }

        /// <summary>Regression: Task.Delay(TimeSpan) truncates sub-millisecond waits, which used to spin into a false stall.</summary>
        [Test]
        public async Task SubMillisecondRemaindersAreRoundedUpSoTruncatingDelaysStillComplete()
        {
            var harness = new DiagnosticHarness(ids: 5);
            harness.Clock.TruncateDelays = true;
            harness.Transport.BeforeRespond = _ => harness.Clock.Now += TimeSpan.FromTicks(3);
            var result = await harness.RunAsync();
            Assert.That(result.Status, Is.EqualTo("completed"), result.ReasonCode);
            Assert.That(harness.Transport.Sent, Has.Count.EqualTo(6));
            for (var i = 1; i < 6; i++)
                Assert.That(harness.Transport.Sent[i].At - harness.Transport.Sent[i - 1].At,
                    Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(1200)));
            Assert.That(harness.Clock.Delays, Is.Not.Empty);
            Assert.That(harness.Clock.Delays.All(delay => delay.Ticks % TimeSpan.TicksPerMillisecond == 0 &&
                delay >= TimeSpan.FromMilliseconds(1)), Is.True, "Every wait is a whole number of milliseconds, at least 1 ms.");
            Assert.That(harness.Clock.Delays.Count, Is.EqualTo(5), "One wait per detail send; no spinning.");
        }

        [Test]
        public async Task AStalledClockStopsWithItsOwnReasonInsteadOfBudgetExhausted()
        {
            var harness = new DiagnosticHarness(ids: 3);
            harness.Clock.Frozen = true;
            var result = await harness.RunAsync();
            Assert.That(harness.Transport.Sent, Has.Count.EqualTo(1));
            Assert.That(result.Status, Is.EqualTo("aborted"));
            Assert.That(result.ReasonCode, Is.EqualTo("detail_clock_stalled"));
            Assert.That((string)harness.Sink.Last["stop"]["reason"], Is.EqualTo("detail_clock_stalled"));
            Assert.That(harness.Clock.Delays.All(delay => delay >= TimeSpan.FromMilliseconds(1)), Is.True);
        }

        /// <summary>Failures outside the transport are labelled internal_error and never carry exception text.</summary>
        [TestCase(0, "list_internal_error")]
        [TestCase(2, "detail_internal_error")]
        public async Task NonTransportFailuresAreInternalErrorsWithoutExceptionText(int failingIndex, string reason)
        {
            var harness = new DiagnosticHarness(ids: 3);
            var inner = harness.Transport.Respond;
            harness.Transport.Respond = (uri, i) =>
            {
                if (i != failingIndex) return inner(uri, i);
                using (var normal = inner(uri, i))
                using (var reader = new System.IO.StreamReader(normal.TakeBody()))
                    return ThrowingDispose(reader.ReadToEnd(), "dispose failed " + FakeKey + " " + uri.AbsoluteUri);
            };
            var result = await harness.RunAsync();
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
            Assert.That(result.Status, Is.EqualTo("aborted"));
            Assert.That(harness.Transport.Sent, Has.Count.EqualTo(failingIndex + 1));
            var report = System.Text.Encoding.UTF8.GetString(harness.Sink.Published.Single());
            Assert.That(report, Does.Contain(reason));
            foreach (var forbidden in new[] { FakeKey, "dispose failed", "riotgames", MatchId(0) })
                Assert.That(report, Does.Not.Contain(forbidden));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(result), Does.Not.Contain(FakeKey));
        }

        [TestCase(401, "list_unauthorized")]
        [TestCase(403, "list_forbidden")]
        [TestCase(429, "list_rate_limited")]
        [TestCase(404, "list_not_found")]
        [TestCase(500, "list_server_error")]
        [TestCase(503, "list_server_error")]
        [TestCase(400, "list_http_error")]
        public async Task FailedListStopsAfterExactlyOneSend(int status, string reason)
        {
            var harness = new DiagnosticHarness();
            harness.Transport.Respond = (uri, i) => Status(status, status == 429
                ? new Dictionary<string, string> { ["Retry-After"] = "5" } : null);
            var result = await harness.RunAsync();
            Assert.That(harness.Transport.Sent, Has.Count.EqualTo(1));
            Assert.That(result.Status, Is.EqualTo("aborted"));
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
            Assert.That(result.ListSends, Is.EqualTo(1));
            Assert.That(result.DetailSends, Is.Zero);
            Assert.That(harness.Sink.Published, Has.Count.EqualTo(1));
            Assert.That((string)harness.Sink.Last["stop"]["reason"], Is.EqualTo(reason));
            Assert.That((string)harness.Sink.Last["stop"]["stage"], Is.EqualTo("list"));
            Assert.That((int)harness.Sink.Last["requests"]["list"], Is.EqualTo(1));
        }

        [TestCase(401, "detail_unauthorized")]
        [TestCase(403, "detail_forbidden")]
        [TestCase(429, "detail_rate_limited")]
        [TestCase(404, "detail_not_found")]
        [TestCase(500, "detail_server_error")]
        [TestCase(504, "detail_server_error")]
        [TestCase(-1, "detail_transport_error")]
        public async Task FailedDetailStopsAfterExactlyOneDetailSend(int status, string reason)
        {
            var harness = new DiagnosticHarness(ids: 5);
            var inner = harness.Transport.Respond;
            harness.Transport.Respond = (uri, i) => i < 2 ? inner(uri, i)
                : status < 0 ? throw new RiotApiException(RiotApiFailure.TransportError) : Status(status);
            var result = await harness.RunAsync();
            Assert.That(harness.Transport.ListSends, Is.EqualTo(1));
            Assert.That(harness.Transport.DetailSends, Is.EqualTo(2), "One successful detail, then one failing detail; no retry.");
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
            Assert.That(result.Status, Is.EqualTo("aborted"));
            Assert.That((string)harness.Sink.Last["stop"]["stage"], Is.EqualTo("detail"));
            Assert.That((bool)harness.Sink.Last["partial"], Is.True);
        }

        [Test]
        public async Task ListTransportErrorAndArbitraryExceptionStopWithoutRetry()
        {
            foreach (var failure in new Func<Exception>[] { () => new RiotApiException(RiotApiFailure.TransportError), () => new System.IO.IOException("native detail") })
            {
                var harness = new DiagnosticHarness();
                harness.Transport.Respond = (uri, i) => throw failure();
                var result = await harness.RunAsync();
                Assert.That(harness.Transport.Sent, Has.Count.EqualTo(1));
                Assert.That(result.ReasonCode, Is.EqualTo("list_transport_error"));
            }
        }

        [Test]
        public async Task FullRunStaysWithinTwentyOneSpacedSendsAndUsesOnlyTheFirstSeed()
        {
            var harness = new DiagnosticHarness(ids: 20);
            var result = await harness.RunAsync();
            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(harness.Transport.Sent, Has.Count.EqualTo(21));
            Assert.That(result.ListSends + result.DetailSends, Is.EqualTo(21));
            var list = harness.Transport.Sent[0].Uri.AbsoluteUri;
            Assert.That(list, Does.Contain(FakeSeed));
            Assert.That(list, Does.Contain("start=0"));
            Assert.That(list, Does.Contain("count=20"));
            Assert.That(harness.Transport.Sent.Select(request => request.Uri.AbsoluteUri), Has.None.Contains(SecondSeed));
            for (var i = 1; i < 21; i++)
                Assert.That(harness.Transport.Sent[i].At - harness.Transport.Sent[i - 1].At,
                    Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(1200)));
            Assert.That(harness.Transport.Sent.All(request => request.Headers.Count == 1 && request.Headers["X-Riot-Token"] == FakeKey));
        }

        [Test]
        public async Task OversizedOrMalformedIdListAbortsBeforeAnyDetail()
        {
            foreach (var body in new[] { IdList(21), "{\"ids\":[]}", "[\"a\",1]", "[\"a\",\"a\"]", "[\"\"]", "not json" })
            {
                var harness = new DiagnosticHarness();
                harness.Transport.Respond = (uri, i) => Json(body);
                var result = await harness.RunAsync();
                Assert.That(harness.Transport.Sent, Has.Count.EqualTo(1), body);
                Assert.That(result.ReasonCode, Is.EqualTo("list_invalid_response"), body);
            }
        }

        [Test]
        public async Task EmptyIdListCompletesWithOneSend()
        {
            var harness = new DiagnosticHarness(ids: 0);
            var result = await harness.RunAsync();
            Assert.That(harness.Transport.Sent, Has.Count.EqualTo(1));
            Assert.That(result.Status, Is.EqualTo("completed"));
        }

        [Test]
        public async Task UnmetPreconditionsSendNothingAndPublishNothing()
        {
            var cases = new List<(string Name, Action<DiagnosticHarness> Arrange, string Reason)>
            {
                ("missing key", h => h.Key = () => null, "api_key_unavailable"),
                ("blank key", h => h.Key = () => "   ", "api_key_unavailable"),
                ("control key", h => h.Key = () => "RGAPI-\nFAKE", "api_key_unavailable"),
                ("throwing key", h => h.Key = () => throw new InvalidOperationException(FakeKey), "api_key_unavailable"),
                ("missing seed", h => h.Seeds = () => null, "seed_unavailable"),
                ("restart declined", h => h.Confirmation.Restarted = false, "confirmation_declined"),
                ("collection running", h => h.Confirmation.NoCollection = false, "confirmation_declined"),
                ("dialog failure", h => h.Confirmation.Throw = true, "confirmation_declined"),
            };
            foreach (var (name, arrange, reason) in cases)
            {
                var harness = new DiagnosticHarness();
                arrange(harness);
                var result = await harness.RunAsync();
                Assert.That(harness.Transport.Sent, Is.Empty, name);
                Assert.That(harness.Sink.Published, Is.Empty, name);
                Assert.That(harness.Lock.Locks, Is.Zero, name);
                Assert.That(result.Status, Is.EqualTo("precondition_failed"), name);
                Assert.That(result.ReasonCode, Is.EqualTo(reason), name);
                Assert.That(result.RunConsumed, Is.False, name);
            }
        }

        [Test]
        public async Task ConfirmationSummaryNamesBothChecksAndTheBudget()
        {
            var harness = new DiagnosticHarness();
            await harness.RunAsync();
            Assert.That(harness.Confirmation.Calls, Is.EqualTo(2));
            Assert.That(harness.Confirmation.Summary, Does.Contain("21"));
            Assert.That(harness.Confirmation.Summary, Does.Contain("1100"));
            Assert.That(harness.Confirmation.Summary, Does.Not.Contain(FakeKey));
            Assert.That(harness.Confirmation.Summary, Does.Not.Contain(FakeSeed));
        }

        [Test]
        public async Task ReloadIsLockedForEverySendAndReleasedInFinally()
        {
            var harness = new DiagnosticHarness(ids: 3);
            await harness.RunAsync();
            Assert.That(harness.Transport.Sent.All(request => request.Locked), Is.True);
            Assert.That(harness.Lock.Locks, Is.EqualTo(1));
            Assert.That(harness.Lock.Unlocks, Is.EqualTo(1));
            Assert.That(harness.Lock.Depth, Is.Zero);

            var failing = new DiagnosticHarness(ids: 3);
            failing.Transport.Respond = (uri, i) => throw new System.IO.IOException("boom");
            failing.Sink.OnPublish = token => throw new System.IO.IOException("sink failure");
            var result = await failing.RunAsync();
            Assert.That(failing.Lock.Locks, Is.EqualTo(1));
            Assert.That(failing.Lock.Unlocks, Is.EqualTo(1));
            Assert.That(result.ReportPublished, Is.False);
            Assert.That(result.ReportFailureCode, Is.EqualTo("report_publish_failed"));
        }

        [Test]
        public async Task OnlyAListAuthorizationFailureLeavesTheRunUnconsumed()
        {
            foreach (var (status, detailStage, consumed) in new[] { (401, false, false), (403, false, false), (500, false, true), (429, false, true), (401, true, true) })
            {
                var harness = new DiagnosticHarness(ids: 3);
                var inner = harness.Transport.Respond;
                harness.Transport.Respond = (uri, i) => detailStage && i == 0 ? inner(uri, i) : Status(status);
                var result = await harness.RunAsync();
                Assert.That(result.RunConsumed, Is.EqualTo(consumed), status + "/" + detailStage);
                Assert.That((bool)harness.Sink.Last["runConsumed"], Is.EqualTo(consumed));
            }
        }

        [Test]
        public async Task OtherQueueMatchesCountTowardTheDetailLimit()
        {
            var harness = new DiagnosticHarness(ids: 20, detail: i => Match(i, queue: i % 2 == 0 ? 1100 : 1090));
            var result = await harness.RunAsync();
            Assert.That(result.DetailSends, Is.EqualTo(20));
            Assert.That(harness.Transport.Sent, Has.Count.EqualTo(21));
        }
    }
}
