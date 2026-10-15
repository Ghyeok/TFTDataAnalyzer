using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Tests.RiotApi
{
    /// <summary>Offline privacy, routing, response ownership and rate-limit regressions.</summary>
    public sealed class RiotApiClientTests
    {
        private const string Secret = "RGAPI-fabricated-test-secret";
        private const string Seed = "FAKE_PRIVATE_PUUID";
        private const string Match = "FAKE_PRIVATE_MATCH";

        /// <summary>Checks the approved TFT platform-to-regional-cluster mapping.</summary>
        [TestCase("KR", "asia")]
        [TestCase("JP1", "asia")]
        [TestCase("PH2", "asia")]
        [TestCase("SG2", "asia")]
        [TestCase("TH2", "asia")]
        [TestCase("TW2", "asia")]
        [TestCase("VN2", "asia")]
        [TestCase("NA1", "americas")]
        [TestCase("BR1", "americas")]
        [TestCase("LA1", "americas")]
        [TestCase("LA2", "americas")]
        [TestCase("OC1", "americas")]
        [TestCase("EUW1", "europe")]
        [TestCase("EUN1", "europe")]
        [TestCase("TR1", "europe")]
        [TestCase("RU", "europe")]
        public void PlatformRoutingUsesTftClusters(string platform, string expected)
            => Assert.That(RiotRouting.ForPlatform(platform.ToLowerInvariant()), Is.EqualTo(expected));

        /// <summary>Keeps the key in one auth header and encodes identifying path values as single segments.</summary>
        [Test]
        public async Task AuthenticationIsHeaderOnlyAndPathsAreEncoded()
        {
            var f = new Fixture();
            f.Transport.Enqueue(200, "[\"FAKE_MATCH\"]");
            var ids = await f.Client.GetMatchIdsAsync("ASIA", "fake/seed?x=1&y=2", 10, 100, default);
            var request = f.Transport.Requests.Single();
            Assert.That(request.Uri.Host, Is.EqualTo("asia.api.riotgames.com"));
            Assert.That(request.Uri.AbsoluteUri, Does.Contain("fake%2Fseed%3Fx%3D1%26y%3D2"));
            Assert.That(request.Uri.Query, Is.EqualTo("?start=10&count=100"));
            Assert.That(request.Uri.AbsoluteUri, Does.Not.Contain(Secret));
            Assert.That(request.Headers.Count, Is.EqualTo(1));
            Assert.That(request.Headers["X-Riot-Token"], Is.EqualTo(Secret));
            Assert.That(ids, Is.EqualTo(new[] { "FAKE_MATCH" }));
            Assert.That(f.Transport.Bodies.Single().Disposed, Is.True);
        }

        /// <summary>Transfers the successful stream to the caller without consuming its body.</summary>
        [Test]
        public async Task MatchStreamBelongsToCaller()
        {
            var f = new Fixture();
            f.Transport.Enqueue(200, "{\"fabricated\":true}");
            var stream = await f.Client.GetMatchStreamAsync("asia", "fake/id?x=1", default);
            Assert.That(f.Transport.Requests.Single().Uri.AbsoluteUri, Does.EndWith("fake%2Fid%3Fx%3D1"));
            Assert.That(f.Transport.Bodies.Single().Disposed, Is.False);
            Assert.That(f.Transport.Bodies.Single().ReadCount, Is.Zero);
            using (stream)
            using (var reader = new StreamReader(stream))
                Assert.That(await reader.ReadToEndAsync(), Does.Contain("fabricated"));
            Assert.That(f.Transport.Bodies.Single().Disposed, Is.True);
        }

        /// <summary>Terminal failures disclose neither identifying URLs nor server error bodies.</summary>
        [TestCase(400)]
        [TestCase(401)]
        [TestCase(403)]
        [TestCase(404)]
        [TestCase(302)]
        [TestCase(418)]
        public void TerminalErrorsAreSafeAndNeverRetried(int status)
        {
            var f = new Fixture();
            f.Transport.Enqueue(status, Secret + Seed + Match);
            var error = Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(error.StatusCode, Is.EqualTo(status));
            Assert.That(error.Code, Is.EqualTo("http_error"));
            AssertSafe(error);
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(1));
            Assert.That(f.Delays, Is.Empty);
            Assert.That(f.Transport.Bodies.Single().Disposed, Is.True);
            Assert.That(f.Transport.Bodies.Single().ReadCount, Is.Zero);
        }

        /// <summary>Untrusted transport exceptions and cancellation messages cannot escape the client boundary.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TransportFailuresDiscardSensitiveDetails(bool cancelled)
        {
            var f = new Fixture();
            f.Transport.Throw = cancelled
                ? new OperationCanceledException(Secret + Seed + Match)
                : new IOException(Secret + Seed + Match, new Exception(Secret));
            Exception error = cancelled
                ? Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), () => Ids(f))
                : (Exception)Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            AssertSafe(error);
            Assert.That(error.InnerException, Is.Null);
            Assert.That(f.Delays, Is.Empty);
        }

        /// <summary>No call is sent before Retry-After, and the exact deadline permits another attempt.</summary>
        [Test]
        public async Task RetryAfterGatesTheEntireRoute()
        {
            var f = new Fixture();
            f.Transport.Enqueue(429, Secret, Header("retry-after", "7"));
            var start = f.Now;
            var first = Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(first.Code, Is.EqualTo("rate_limited"));
            Assert.That(first.RetryAfterUtc, Is.EqualTo(start.AddSeconds(7)));
            AssertSafe(first);
            f.Now = start.AddSeconds(6.9);
            Assert.ThrowsAsync<RiotApiException>(() => f.Client.GetMatchStreamAsync("asia", Match, default));
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(1));
            Assert.That(f.Transport.Bodies.Single().Disposed, Is.True);
            f.Now = start.AddSeconds(7);
            f.Transport.Enqueue(200, "[]");
            await Ids(f);
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(2));
        }

        /// <summary>Parses an HTTP-date retry value while keeping other regions usable.</summary>
        [Test]
        public async Task HttpDateRetryDoesNotBlockOtherRegions()
        {
            var f = new Fixture();
            var deadline = f.Now.AddSeconds(12);
            f.Transport.Enqueue(429, "", Header("Retry-After", deadline.ToString("R")));
            var error = Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(error.RetryAfterUtc, Is.EqualTo(deadline));
            f.Transport.Enqueue(200, "[]");
            await f.Client.GetMatchIdsAsync("europe", Seed, 0, 1, default);
            Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(2));
        }

        /// <summary>Missing or malformed retry headers fail closed with a conservative cooldown.</summary>
        [TestCase(null)]
        [TestCase("invalid")]
        [TestCase("-1")]
        public void InvalidRetryAfterBlocksForSixtySeconds(string value)
        {
            var f = new Fixture();
            f.Transport.Enqueue(429, "", value == null ? null : Header("Retry-After", value));
            var error = Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(error.RetryAfterUtc, Is.EqualTo(f.Now.AddSeconds(60)));
            Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(1));
        }

        /// <summary>Application windows gate both endpoints and reopen at expiry.</summary>
        [Test]
        public async Task ApplicationLimitsApplyAcrossMethods()
        {
            var f = new Fixture();
            f.Transport.Enqueue(200, "[]", new Dictionary<string, string>
            {
                ["x-app-rate-limit"] = "1:10,5:120", ["x-app-rate-limit-count"] = "1:10,1:120"
            });
            await Ids(f);
            var error = Assert.ThrowsAsync<RiotApiException>(() => f.Client.GetMatchStreamAsync("asia", Match, default));
            Assert.That(error.RetryAfterUtc, Is.EqualTo(f.Now.AddSeconds(10)));
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(1));
            f.Now = f.Now.AddSeconds(10);
            f.Transport.Enqueue(200, "{}");
            using (await f.Client.GetMatchStreamAsync("asia", Match, default)) { }
        }

        /// <summary>Method quotas remain endpoint-specific.</summary>
        [Test]
        public async Task MethodLimitsDoNotBlockOtherMethods()
        {
            var f = new Fixture();
            f.Transport.Enqueue(200, "[]", new Dictionary<string, string>
            {
                ["X-Method-Rate-Limit"] = "1:20", ["X-Method-Rate-Limit-Count"] = "1:20"
            });
            await Ids(f);
            f.Transport.Enqueue(200, "{}");
            using (await f.Client.GetMatchStreamAsync("asia", Match, default)) { }
            Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(2));
        }

        /// <summary>Clients sharing state reserve known quotas even when later responses omit headers.</summary>
        [Test]
        public async Task SharedLimitsReserveRequests()
        {
            var f = new Fixture();
            f.Transport.Enqueue(200, "[]", new Dictionary<string, string>
            {
                ["X-App-Rate-Limit"] = "2:10", ["X-App-Rate-Limit-Count"] = "1:10"
            });
            await Ids(f);
            f.Transport.Enqueue(200, "[]");
            await f.CreateClient().GetMatchIdsAsync("asia", Seed, 0, 1, default);
            Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(2));
        }

        /// <summary>Retries only the planned transient statuses at 1,2,4 seconds, disposing every failed body.</summary>
        [TestCase(500)]
        [TestCase(502)]
        [TestCase(503)]
        [TestCase(504)]
        public void ServerRetriesAreBounded(int status)
        {
            var f = new Fixture();
            for (var i = 0; i < 4; i++) f.Transport.Enqueue(status, Secret);
            AssertSafe(Assert.ThrowsAsync<RiotApiException>(() => Ids(f)));
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(4));
            Assert.That(f.Delays.Select(d => d.TotalSeconds), Is.EqualTo(new[] { 1d, 2d, 4d }));
            Assert.That(f.Transport.Bodies.All(b => b.Disposed && b.ReadCount == 0), Is.True);
        }

        /// <summary>Returns a successful response after a transient error and a single backoff.</summary>
        [Test]
        public async Task ServerRetryCanRecover()
        {
            var f = new Fixture();
            f.Transport.Enqueue(503, Secret);
            f.Transport.Enqueue(200, "[\"FAKE_MATCH\"]");
            Assert.That(await Ids(f), Is.EqualTo(new[] { "FAKE_MATCH" }));
            Assert.That(f.Delays.Single(), Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(f.Transport.Bodies.All(b => b.Disposed), Is.True);
        }

        /// <summary>Malformed ID lists produce safe errors and close the response stream.</summary>
        [TestCase("{\"private\":\"RGAPI-fabricated-test-secret\"}")]
        [TestCase("[null]")]
        [TestCase("[12]")]
        [TestCase("[\" \"]")]
        [TestCase("[] {\"private\":true}")]
        public void InvalidIdResponsesAreSanitized(string body)
        {
            var f = new Fixture();
            f.Transport.Enqueue(200, body);
            var error = Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
            Assert.That(error.Code, Is.EqualTo("invalid_response"));
            AssertSafe(error);
            Assert.That(f.Transport.Bodies.Single().Disposed, Is.True);
        }

        /// <summary>Cancelled or invalid requests perform no key lookup or transport call.</summary>
        [Test]
        public void InvalidAndCancelledRequestsDoNotAuthenticate()
        {
            var f = new Fixture();
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), () => f.Client.GetMatchIdsAsync("asia", Seed, 0, 1, cancelled.Token));
            }
            Assert.ThrowsAsync<ArgumentException>(() => f.Client.GetMatchIdsAsync("https://evil.test", Seed, 0, 1, default));
            Assert.ThrowsAsync<ArgumentException>(() => f.Client.GetMatchIdsAsync("asia", " ", 0, 1, default));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Client.GetMatchIdsAsync("asia", Seed, -1, 1, default));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Client.GetMatchIdsAsync("asia", Seed, 0, 0, default));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Client.GetMatchIdsAsync("asia", Seed, 0, 101, default));
            Assert.ThrowsAsync<ArgumentException>(() => f.Client.GetMatchStreamAsync("asia", " ", default));
            Assert.That(f.KeyReads, Is.Zero);
            Assert.That(f.Transport.Requests, Is.Empty);
        }

        /// <summary>Rejects unsafe production targets before creating an authenticated request.</summary>
        [TestCase("http://asia.api.riotgames.com/tft/match/v1/matches/test")]
        [TestCase("https://asia.api.riotgames.com.evil.test/tft/match/v1/matches/test")]
        [TestCase("https://user@asia.api.riotgames.com/tft/match/v1/matches/test")]
        [TestCase("https://asia.api.riotgames.com:444/tft/match/v1/matches/test")]
        [TestCase("https://asia.api.riotgames.com/lol/spectator/v5/test")]
        public void ProductionTransportRejectsUnsafeTargets(string target)
        {
            Assert.ThrowsAsync<ArgumentException>(() => new UnityRiotApiTransport().SendAsync(
                new Uri(target), Header("X-Riot-Token", Secret), default));
        }

        /// <summary>Key lookup trims values and does not expose them through provider inspection.</summary>
        [Test]
        public void ProviderTrimsWithoutExposingKey()
        {
            var provider = new EnvironmentRiotApiKeyProvider(() => "  " + Secret + "  ");
            Assert.That(provider.IsConfigured, Is.True);
            Assert.That(provider.GetRequiredKey(), Is.EqualTo(Secret));
            Assert.That(provider.ToString(), Does.Not.Contain(Secret));
        }

        /// <summary>Missing, blank and header-injection keys fail without sending.</summary>
        [TestCase(null)]
        [TestCase(" ")]
        [TestCase("RGAPI-fabricated-test-secret\r\nInjected: value")]
        public void InvalidKeysDoNotSend(string key)
        {
            var provider = new EnvironmentRiotApiKeyProvider(() => key);
            Assert.That(provider.IsConfigured, Is.False);
            AssertSafe(Assert.Throws<RiotApiException>(() => provider.GetRequiredKey()));
            var f = new Fixture();
            var client = new RiotApiClient(provider, f.Transport);
            AssertSafe(Assert.ThrowsAsync<RiotApiException>(() => client.GetMatchIdsAsync("asia", Seed, 0, 1, default)));
            Assert.That(f.Transport.Requests, Is.Empty);
        }

        /// <summary>Reader failures cannot disclose credentials in provider errors.</summary>
        [Test]
        public void ProviderExceptionsAreSanitized()
        {
            var provider = new EnvironmentRiotApiKeyProvider(() => throw new InvalidOperationException(Secret));
            Assert.That(provider.IsConfigured, Is.False);
            AssertSafe(Assert.Throws<RiotApiException>(() => provider.GetRequiredKey()));
        }

        /// <summary>Cancellation after a response must not erase server cooldown metadata.</summary>
        [Test]
        public void CancellationAfterResponseRetainsRetryAfter()
        {
            var f = new Fixture();
            using (var cancellation = new CancellationTokenSource())
            {
                f.Transport.Enqueue(429, Secret, Header("Retry-After", "30"));
                f.Transport.AfterResponse = cancellation.Cancel;
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(),
                    () => f.Client.GetMatchIdsAsync("asia", Seed, 0, 1, cancellation.Token));
                f.Transport.AfterResponse = null;
                var error = Assert.ThrowsAsync<RiotApiException>(() => Ids(f));
                Assert.That(error.Code, Is.EqualTo("rate_limited"));
                Assert.That(error.RetryAfterUtc, Is.EqualTo(f.Now.AddSeconds(30)));
                Assert.That(f.Transport.Requests.Count, Is.EqualTo(1));
                Assert.That(f.Transport.Bodies.Single().Disposed, Is.True);
            }
        }

        /// <summary>Cancellation interrupts bounded backoff without issuing another authenticated request.</summary>
        [Test]
        public void CancellationDuringBackoffStopsRetry()
        {
            var f = new Fixture();
            f.Transport.Enqueue(503, Secret);
            var client = new RiotApiClient(new EnvironmentRiotApiKeyProvider(() => Secret), f.Transport,
                f.Limits, () => f.Now, (duration, token) => throw new OperationCanceledException(Secret));
            var error = Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(),
                () => client.GetMatchIdsAsync("asia", Seed, 0, 1, default));
            AssertSafe(error);
            Assert.That(f.Transport.Requests.Count, Is.EqualTo(1));
            Assert.That(f.Transport.Bodies.Single().Disposed, Is.True);
        }

        /// <summary>Native completion must retain HTTP metadata when cancellation races its continuation.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void NativeWaitRetainsReceivedHeadersOnCancellation(bool cancellationWins)
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var completion = cancellationWins ? Task.FromCanceled(cancellation.Token) : Task.CompletedTask;
                var aborted = false;
                Assert.DoesNotThrowAsync(() => UnityRiotApiTransport.WaitForResponseAsync(
                    completion, () => true, () => aborted = true, cancellation.Token));
                Assert.That(aborted, Is.False, "Capture received headers before aborting the native request.");
            }
        }

        /// <summary>Cancellation before headers aborts the native request with a sanitized exception.</summary>
        [Test]
        public void NativeWaitAbortsCancellationWithoutResponse()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var aborted = false;
                var error = Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(),
                    () => UnityRiotApiTransport.WaitForResponseAsync(Task.FromCanceled(cancellation.Token),
                        () => false, () => aborted = true, cancellation.Token));
                Assert.That(aborted, Is.True);
                AssertSafe(error);
            }
        }

        /// <summary>Creates fabricated case-insensitive headers.</summary>
        private static Dictionary<string, string> Header(string name, string value)
            => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [name] = value };
        /// <summary>Requests only fabricated match IDs.</summary>
        private static Task<IReadOnlyList<string>> Ids(Fixture f)
            => f.Client.GetMatchIdsAsync("asia", Seed, 0, 100, default);
        /// <summary>Asserts credentials and identifying fixture values are absent from errors.</summary>
        private static void AssertSafe(Exception error)
        {
            foreach (var value in new[] { Secret, Seed, Match })
                Assert.That(error.ToString(), Does.Not.Contain(value));
        }

        private sealed class Fixture
        {
            public DateTimeOffset Now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            public readonly FakeTransport Transport = new FakeTransport();
            public readonly RateLimitState Limits = new RateLimitState();
            public readonly List<TimeSpan> Delays = new List<TimeSpan>();
            public int KeyReads;
            public RiotApiClient Client { get; }
            /// <summary>Creates a fixture without reading process environment or contacting the network.</summary>
            public Fixture() { Client = CreateClient(); }
            /// <summary>Creates a client sharing deterministic time and quota state.</summary>
            public RiotApiClient CreateClient() => new RiotApiClient(
                new EnvironmentRiotApiKeyProvider(() => { KeyReads++; return Secret; }), Transport, Limits,
                () => Now, (duration, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    Delays.Add(duration);
                    Now = Now.Add(duration);
                    return Task.CompletedTask;
                });
        }

        private sealed class Request
        {
            public Uri Uri;
            public IReadOnlyDictionary<string, string> Headers;
        }

        private sealed class FakeTransport : IRiotApiTransport
        {
            public readonly List<Request> Requests = new List<Request>();
            public readonly List<TrackedStream> Bodies = new List<TrackedStream>();
            private readonly Queue<RiotApiResponse> _responses = new Queue<RiotApiResponse>();
            public Exception Throw;
            public Action AfterResponse;
            /// <summary>Queues a fabricated owned response stream.</summary>
            public void Enqueue(int status, string body, IReadOnlyDictionary<string, string> headers = null)
            {
                var stream = new TrackedStream(body);
                Bodies.Add(stream);
                _responses.Enqueue(new RiotApiResponse(status, stream, headers));
            }
            /// <summary>Records the request without sending any network traffic.</summary>
            public Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Requests.Add(new Request { Uri = uri, Headers = headers });
                if (Throw != null) throw Throw;
                var response = _responses.Dequeue();
                AfterResponse?.Invoke();
                return Task.FromResult(response);
            }
        }

        private sealed class TrackedStream : MemoryStream
        {
            public bool Disposed;
            public int ReadCount;
            /// <summary>Creates an in-memory fixture response.</summary>
            public TrackedStream(string value) : base(Encoding.UTF8.GetBytes(value), false) { }
            /// <summary>Tracks body consumption.</summary>
            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadCount++;
                return base.Read(buffer, offset, count);
            }
            /// <summary>Tracks ownership release.</summary>
            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
