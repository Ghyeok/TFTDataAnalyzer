using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Editor.Ingestion;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class StaticVersionCheckSchedulerTests
    {
        private string _root, _state;
        private DateTimeOffset _now;
        private RecordingTransport _transport;
        private StaticVersionCheckScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftVersion_" + Guid.NewGuid().ToString("N"));
            _state = Path.Combine(_root, "Library", "TftAnalyzerStore", "version-check.json");
            _now = new DateTimeOffset(2026, 10, 19, 1, 0, 0, TimeSpan.Zero);
            _transport = new RecordingTransport();
            _scheduler = Create();
        }

        [TearDown]
        public void TearDown()
        {
            _scheduler.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private StaticVersionCheckScheduler Create(TimeSpan? timeout = null) =>
            new StaticVersionCheckScheduler(_transport, () => _now, _root, () => "16.18.1", timeout);

        [Test]
        public async Task FirstCheckReservesBeforeOnlyTheVersionListRequestAndPersistsExactlyTwoFields()
        {
            _transport.OnRequest = token =>
            {
                Assert.That(File.Exists(_state), Is.True, "Reserve the attempt before network access.");
                Assert.That(ReadCheckedUtc(), Is.EqualTo(_now));
                Assert.That(token.CanBeCanceled, Is.True);
                return Task.FromResult(RecordingTransport.Response("[\"16.18.2\",\"16.18.1\"]"));
            };
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Is.EqualTo(new[] { "https://ddragon.leagueoflegends.com/api/versions.json" }));
            Assert.That(_scheduler.LatestVersion, Is.EqualTo("16.18.2"));
            Assert.That(_scheduler.Notice, Does.Contain("16.18.2").And.Contain("16.18.1").And.Contain("가져오기"));
            var state = JObject.Parse(File.ReadAllText(_state));
            Assert.That(state.Properties().Select(x => x.Name), Is.EquivalentTo(new[] { "lastCheckedUtc", "lastSeenVersion" }));
            Assert.That(state["lastSeenVersion"].Value<string>(), Is.EqualTo("16.18.2"));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_state)), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task ReloadSkipsBeforeTwentyFourHoursAndChecksAtTheExactBoundary()
        {
            await _scheduler.CheckAsync(default);
            _scheduler.Dispose();
            _now = _now.AddHours(24).AddTicks(-1);
            _scheduler = Create();
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
            _now = _now.AddTicks(1);
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ConcurrentChecksShareOneTaskAndOneReservation()
        {
            var pending = new TaskCompletionSource<HttpResponseData>();
            _transport.OnRequest = token => pending.Task;
            var first = _scheduler.CheckAsync(default);
            var second = _scheduler.CheckAsync(default);
            Assert.That(second, Is.SameAs(first));
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
            pending.SetResult(RecordingTransport.Response("[\"16.18.2\"]"));
            await first;
            Assert.That(_scheduler.LatestVersion, Is.EqualTo("16.18.2"));
        }

        [Test]
        public async Task CancellationBeforeReservationDoesNotConsumeTheDay()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                await _scheduler.CheckAsync(cancellation.Token);
            }
            Assert.That(File.Exists(_state), Is.False);
            Assert.That(_transport.Urls, Is.Empty);
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task FailureOrCancellationAfterReservationConsumesTheDayAndPreservesPreviousSuccess(bool cancel)
        {
            WriteState(_now.AddHours(-24), "16.18.0");
            using (var cancellation = new CancellationTokenSource())
            {
                _transport.OnRequest = token =>
                {
                    if (cancel) { cancellation.Cancel(); throw new OperationCanceledException(token); }
                    throw new IOException("private exception must not reach the UI");
                };
                await _scheduler.CheckAsync(cancellation.Token);
            }
            var state = JObject.Parse(File.ReadAllText(_state));
            Assert.That(state["lastSeenVersion"].Value<string>(), Is.EqualTo("16.18.0"));
            Assert.That(ReadCheckedUtc(), Is.EqualTo(_now));
            Assert.That(_scheduler.Notice, Does.Not.Contain("private"));
            _scheduler.Dispose(); _scheduler = Create();
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
        }

        [TestCase("not-json", "")]
        [TestCase("{\"lastCheckedUtc\":\"bad\",\"lastSeenVersion\":\"16.18.0\"}", "16.18.0")]
        [TestCase("{\"lastCheckedUtc\":\"2099-01-01T00:00:00Z\",\"lastSeenVersion\":\"16.18.0\"}", "16.18.0")]
        [TestCase("{\"lastCheckedUtc\":\"2026-10-18T00:00:00Z\",\"lastSeenVersion\":\"https://evil.test\"}", "")]
        public async Task CorruptOrFutureStateIsRepairedWithoutARequestThenDeferredForADay(string json, string retained)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_state)); File.WriteAllText(_state, json);
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Is.Empty);
            var state = JObject.Parse(File.ReadAllText(_state));
            Assert.That(ReadCheckedUtc(), Is.EqualTo(_now));
            Assert.That(state["lastSeenVersion"].Value<string>(), Is.EqualTo(retained));
            _now = _now.AddHours(24);
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task UnreadableOrUnwritableStateFailsClosedWithoutARequest()
        {
            Directory.CreateDirectory(_state);
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Is.Empty);
            Assert.That(_scheduler.Notice, Is.Empty);
            Directory.Delete(_state);
            File.WriteAllText(Path.Combine(_root, "Library", "TftAnalyzerStore", "barrier"), "preserve");
            using (var lockFile = new FileStream(_state, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                await _scheduler.CheckAsync(default);
                Assert.That(_transport.Urls, Is.Empty);
            }
        }

        [Test]
        public async Task ReloadCancellationDiscardsALateResponseWithoutPublishingAnEventOrSuccess()
        {
            var pending = new TaskCompletionSource<HttpResponseData>();
            _transport.OnRequest = token => pending.Task;
            var events = 0; _scheduler.Changed += () => events++;
            var operation = _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
            _scheduler.CancelPending();
            var before = events;
            pending.SetResult(RecordingTransport.Response("[\"16.18.2\"]"));
            await operation;
            Assert.That(events, Is.EqualTo(before));
            Assert.That(JObject.Parse(File.ReadAllText(_state))["lastSeenVersion"].Value<string>(), Is.Empty);
            Assert.That(_scheduler.LatestVersion, Is.Empty);
        }

        [Test]
        public async Task CancellationDeadlineSettlesEvenWhenTheTransportIgnoresCancellation()
        {
            _scheduler.Dispose(); _scheduler = Create(TimeSpan.FromMilliseconds(30));
            var pending = new TaskCompletionSource<HttpResponseData>();
            _transport.OnRequest = token => pending.Task;
            var operation = _scheduler.CheckAsync(default);
            Assert.That(await Task.WhenAny(operation, Task.Delay(2000)), Is.SameAs(operation));
            await operation;
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
            pending.SetResult(RecordingTransport.Response("[\"16.18.2\"]"));
            Assert.That(_scheduler.LatestVersion, Is.Empty);
        }

        [TestCase("[]")]
        [TestCase("[\"bad\",\"https://evil.test/path\",\"../catalog\"]")]
        public async Task EmptyOrAbnormalVersionsNeverBecomeANoticeOrRequestTarget(string payload)
        {
            _transport.OnRequest = token => Task.FromResult(RecordingTransport.Response(payload));
            await _scheduler.CheckAsync(default);
            Assert.That(_scheduler.Notice, Is.Empty);
            Assert.That(_scheduler.LatestVersion, Is.Empty);
            Assert.That(_transport.Urls, Has.Count.EqualTo(1));
        }

        [TestCase("16.18.2")]
        [TestCase(null)]
        public async Task MatchingOrUnknownImportedVersionDoesNotInventAnUpdate(string imported)
        {
            _scheduler.Dispose();
            _scheduler = new StaticVersionCheckScheduler(_transport, () => _now, _root, () => imported);
            await _scheduler.CheckAsync(default);
            Assert.That(_scheduler.LatestVersion, Is.EqualTo("16.18.2"));
            Assert.That(_scheduler.Notice, Is.Empty);
        }

        [Test]
        public async Task CachedLastSeenRestoresAfterReloadAndTracksManualImportsWithoutAnotherRequest()
        {
            var imported = "16.18.1";
            WriteState(_now.AddHours(-1), "16.18.2");
            _scheduler.Dispose();
            _scheduler = new StaticVersionCheckScheduler(_transport, () => _now, _root, () => imported);
            var updates = 0; _scheduler.Changed += () => updates++;
            await _scheduler.CheckAsync(default);
            Assert.That(_scheduler.LatestVersion, Is.EqualTo("16.18.2"));
            Assert.That(_scheduler.Notice, Does.Contain("16.18.2").And.Contain("16.18.1"));
            Assert.That(updates, Is.EqualTo(1), "A window already open must receive the restored cached notice.");
            imported = "16.18.2";
            Assert.That(_scheduler.Notice, Is.Empty);
            imported = "16.18.0";
            Assert.That(_scheduler.Notice, Does.Contain("16.18.0"));
            await _scheduler.CheckAsync(default);
            Assert.That(_transport.Urls, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task RepairedOrExpiredStateNotifiesAnOpenWindowEvenWithoutHttpSuccess(bool future)
        {
            WriteState(future ? _now.AddHours(1) : _now.AddHours(-24), "16.18.2");
            _transport.OnRequest = token => throw new IOException("offline");
            var updates = 0;
            _scheduler.Changed += () => updates++;
            await _scheduler.CheckAsync(default);
            Assert.That(_scheduler.LatestVersion, Is.EqualTo("16.18.2"));
            Assert.That(_scheduler.Notice, Does.Contain("16.18.2").And.Contain("16.18.1"));
            Assert.That(_transport.Urls.Count, Is.EqualTo(future ? 0 : 1));
            Assert.That(updates, Is.EqualTo(1), "Restoring cached state must update an already open window.");
        }

        private void WriteState(DateTimeOffset time, string version)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_state));
            File.WriteAllText(_state, new JObject { ["lastCheckedUtc"] = time.ToString("O"), ["lastSeenVersion"] = version }.ToString());
        }

        private DateTimeOffset ReadCheckedUtc()
        {
            using (var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(File.ReadAllText(_state)))
                { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
                return DateTimeOffset.Parse(JObject.Load(reader)["lastCheckedUtc"].Value<string>(),
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        internal sealed class RecordingTransport : IHttpTransport
        {
            internal readonly List<string> Urls = new List<string>();
            internal Func<CancellationToken, Task<HttpResponseData>> OnRequest = token =>
                Task.FromResult(Response("[\"16.18.2\",\"16.18.1\"]"));
            public Task<HttpResponseData> GetAsync(string url, CancellationToken token)
            { Urls.Add(url); return OnRequest(token); }
            internal static HttpResponseData Response(string body) => new HttpResponseData(
                "https://ddragon.leagueoflegends.com/api/versions.json", 200, Encoding.UTF8.GetBytes(body), "", "application/json", DateTime.UnixEpoch);
        }
    }
}
