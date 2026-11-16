using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Editor.Diagnostics;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Tests.Diagnostics
{
    /// <summary>Offline fakes for the match shape diagnostic; nothing here reads the environment or the network.</summary>
    internal static class MatchShapeTestSupport
    {
        internal const string FakeKey = "RGAPI-FAKE-SENTINEL-KEY-0000";
        internal const string FakeSeed = "SEED-PUUID-SENTINEL-AAAA";
        internal const string SecondSeed = "SEED-PUUID-SENTINEL-BBBB";

        internal static MatchShapeDiagnosticInput Input(int queue = 1100) => new MatchShapeDiagnosticInput(
            "asia", queue, 18, "16.18",
            new[] { "TFT18_UnitA", "TFT18_UnitB", "TFT18_UnitC" },
            new[] { "TFT_Item_A", "TFT_Item_B" },
            new[] { "TFT18_TraitA", "TFT18_TraitB" },
            new[] { "TFT18_AugmentA", "TFT18_AugmentB" });

        internal static string MatchId(int index) => "KR_98765432" + index.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

        internal static RiotApiResponse Json(string json, long status = 200, IReadOnlyDictionary<string, string> headers = null)
            => new RiotApiResponse(status, new MemoryStream(Encoding.UTF8.GetBytes(json), false), headers);

        internal static RiotApiResponse Status(long status, IReadOnlyDictionary<string, string> headers = null)
            => new RiotApiResponse(status, new MemoryStream(Array.Empty<byte>(), false), headers);

        internal static string IdList(int count)
            => new JArray(Enumerable.Range(0, count).Select(MatchId)).ToString(Formatting.None);

        /// <summary>A fabricated eight-player lobby; every identifying field carries a sentinel.</summary>
        internal static JObject Match(int index = 0, int queue = 1100, string gameVersion = "Version 16.18.123.456 (FAKE-BUILD-NOTE)",
            string dataVersion = "2")
        {
            var participants = new JArray();
            for (var i = 0; i < 8; i++)
            {
                participants.Add(new JObject
                {
                    ["puuid"] = "PARTICIPANT-PUUID-SENTINEL-" + i,
                    ["riotIdGameName"] = "SentinelPlayer" + i,
                    ["riotIdTagline"] = "SENTINEL-TAG",
                    ["placement"] = i + 1,
                    ["last_round"] = 30,
                    ["units"] = new JArray(
                        new JObject { ["character_id"] = i % 2 == 0 ? "TFT18_UnitA" : "TFT18_UnitB", ["tier"] = 2, ["itemNames"] = new JArray("TFT_Item_A") },
                        new JObject { ["character_id"] = "TFT18_UnitC", ["tier"] = 1, ["itemNames"] = new JArray() }),
                    ["traits"] = new JArray(new JObject { ["name"] = "TFT18_TraitA", ["tier_current"] = i % 3 == 0 ? 0 : 1 }),
                    ["augments"] = new JArray("TFT18_AugmentA")
                });
            }
            return new JObject
            {
                ["metadata"] = new JObject
                {
                    ["data_version"] = dataVersion,
                    ["match_id"] = MatchId(index),
                    ["participants"] = new JArray(Enumerable.Range(0, 8).Select(i => "PARTICIPANT-PUUID-SENTINEL-" + i))
                },
                ["info"] = new JObject
                {
                    ["game_version"] = gameVersion,
                    ["game_datetime"] = 1767225600000L,
                    ["queue_id"] = queue,
                    ["tft_set_number"] = 18,
                    ["participants"] = participants
                }
            };
        }

        internal static bool IsList(Uri uri) => uri.AbsolutePath.EndsWith("/ids", StringComparison.Ordinal);
    }

    internal sealed class FakeClock
    {
        public DateTimeOffset Now = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        public readonly List<TimeSpan> Delays = new List<TimeSpan>();

        public Task Delay(TimeSpan duration, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Delays.Add(duration);
            Now += duration;
            return Task.CompletedTask;
        }
    }

    internal sealed class SentRequest
    {
        public Uri Uri;
        public DateTimeOffset At;
        public bool Locked;
        public IReadOnlyDictionary<string, string> Headers;
    }

    internal sealed class ScriptedTransport : IRiotApiTransport
    {
        private readonly FakeClock _clock;
        private readonly FakeReloadLock _lock;
        public readonly List<SentRequest> Sent = new List<SentRequest>();
        public Func<Uri, int, RiotApiResponse> Respond;
        public Action<int> BeforeRespond;

        public ScriptedTransport(FakeClock clock, FakeReloadLock reloadLock, int ids = 3, Func<int, JObject> detail = null)
        {
            _clock = clock;
            _lock = reloadLock;
            Respond = (uri, index) => MatchShapeTestSupport.IsList(uri)
                ? MatchShapeTestSupport.Json(MatchShapeTestSupport.IdList(ids))
                : MatchShapeTestSupport.Json((detail ?? (i => MatchShapeTestSupport.Match(i)))(index - 1).ToString(Formatting.None));
        }

        public Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token)
        {
            Sent.Add(new SentRequest { Uri = uri, At = _clock.Now, Locked = _lock != null && _lock.Depth > 0, Headers = headers });
            var index = Sent.Count - 1;
            BeforeRespond?.Invoke(index);
            return Task.FromResult(Respond(uri, index));
        }

        public int ListSends => Sent.Count(request => MatchShapeTestSupport.IsList(request.Uri));
        public int DetailSends => Sent.Count(request => !MatchShapeTestSupport.IsList(request.Uri));
    }

    internal sealed class FakeReloadLock : IAssemblyReloadLock
    {
        public int Depth, Locks, Unlocks;
        public void Lock() { Depth++; Locks++; }
        public void Unlock() { Depth--; Unlocks++; }
    }

    internal sealed class FakeConfirmation : IMatchShapeConfirmation
    {
        public bool Restarted = true, NoCollection = true, Throw;
        public string Summary;
        public int Calls;
        public Action OnConfirm;

        public bool ConfirmUnityRestartedAfterKeyRenewal(string summary)
        {
            Calls++;
            Summary = summary;
            if (Throw) throw new InvalidOperationException("dialog failure");
            return Restarted;
        }

        public bool ConfirmNoCollectionInProgress()
        {
            Calls++;
            OnConfirm?.Invoke();
            return NoCollection;
        }
    }

    internal sealed class MemorySink : IMatchShapeReportSink
    {
        public readonly List<byte[]> Published = new List<byte[]>();
        public Action<CancellationToken> OnPublish;

        public void Publish(byte[] utf8Json, CancellationToken token)
        {
            OnPublish?.Invoke(token);
            token.ThrowIfCancellationRequested();
            Published.Add(utf8Json.ToArray());
        }

        public JObject Last => JObject.Parse(Encoding.UTF8.GetString(Published.Last()));
    }

    /// <summary>Assembles one diagnostic with offline fakes and exposes them for assertions.</summary>
    internal sealed class DiagnosticHarness
    {
        public readonly FakeClock Clock = new FakeClock();
        public readonly FakeReloadLock Lock = new FakeReloadLock();
        public readonly FakeConfirmation Confirmation = new FakeConfirmation();
        public readonly MemorySink Sink = new MemorySink();
        public readonly ScriptedTransport Transport;
        public Func<string> Key = () => MatchShapeTestSupport.FakeKey;
        public Func<string> Seeds = () => MatchShapeTestSupport.FakeSeed + "," + MatchShapeTestSupport.SecondSeed;
        public IMatchShapeReportSink SinkOverride;

        public DiagnosticHarness(int ids = 3, Func<int, JObject> detail = null)
        {
            Transport = new ScriptedTransport(Clock, Lock, ids, detail);
        }

        public MatchShapeDiagnostic Build() => new MatchShapeDiagnostic(Transport,
            new EnvironmentRiotApiKeyProvider(Key), new EnvironmentSeedPuuidProvider(Seeds),
            SinkOverride ?? Sink, Confirmation, Lock, () => Clock.Now, Clock.Delay);

        public Task<MatchShapeRunResult> RunAsync(CancellationToken token = default, MatchShapeDiagnosticInput input = null)
            => Build().RunAsync(input ?? MatchShapeTestSupport.Input(), token);
    }
}
