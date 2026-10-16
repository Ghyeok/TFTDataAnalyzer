using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Serialization;
using UnityEngine;

namespace TFTAnalyzer.Tests.Analytics
{
    public sealed class MatchAnonymizerTests
    {
        private const string RawMatch = "KR1_123456";

        /// <summary>Projects a fabricated complete lobby without retaining personal fields or raw build notes.</summary>
        [Test]
        public void ReadRemovesIdentityAndRetainsSafeScopeAndVersions()
        {
            var result = Read(Fixture());
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.UnknownStaticIds, Is.Empty);
            var observation = result.Observation;
            Assert.That(observation.MatchHash, Is.EqualTo(ContentHasher.Sha256Hex("match-v1|" + RawMatch)));
            Assert.That(observation.Participants.Count, Is.EqualTo(8));
            Assert.That(observation.Scope.ActiveCatalog, Is.EqualTo(Identity()));
            Assert.That(observation.Scope.QueueId, Is.EqualTo("1100"));
            Assert.That(observation.Scope.Region, Is.EqualTo("asia"));
            Assert.That(observation.PlayedAt, Is.EqualTo(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(observation.MatchDataVersion, Is.EqualTo("2"));
            Assert.That(observation.MatchGameVersion, Is.EqualTo("16.18.123.456"));
            Assert.That(observation.Participants[0].Units[0].ItemIds, Is.EqualTo(new[] { "TEST_ITEM" }));
            AssertSafe(JsonConvert.SerializeObject(result));
            var restored = JsonConvert.DeserializeObject<AnonymousMatchObservation>(JsonConvert.SerializeObject(observation));
            Assert.That(restored.MatchGameVersion, Is.EqualTo(observation.MatchGameVersion));
            Assert.That(restored.MatchDataVersion, Is.EqualTo(observation.MatchDataVersion));
        }

        /// <summary>Unknown IDs in every static category quarantine the whole match and expose digests only.</summary>
        [TestCase("unit")]
        [TestCase("item")]
        [TestCase("trait")]
        [TestCase("augment")]
        public void UnknownIdsQuarantineWithoutLeakingUntrustedStrings(string kind)
        {
            var json = Fixture();
            var participant = json["info"]["participants"][0];
            var value = "FakePlayer-FAKE-PUUID-unknown";
            switch (kind)
            {
                case "unit": participant["units"][0]["character_id"] = value; break;
                case "item": participant["units"][0]["itemNames"][0] = value; break;
                case "trait": participant["traits"][0]["name"] = value; break;
                default: participant["augments"][0] = value; break;
            }
            var result = Read(json);
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Observation, Is.Null);
            Assert.That(result.UnknownStaticIds.Count, Is.EqualTo(1));
            Assert.That(result.UnknownStaticIds[0].Kind, Is.EqualTo(kind));
            Assert.That(result.UnknownStaticIds[0].IdHash,
                Is.EqualTo(ContentHasher.Sha256Hex("static-id-v1|" + kind + "|" + value)));
            AssertSafe(JsonConvert.SerializeObject(result));
        }

        /// <summary>Missing required fields are rejected instead of silently defaulting to valid-looking values.</summary>
        [TestCase("metadata.match_id")]
        [TestCase("metadata.data_version")]
        [TestCase("info.game_version")]
        [TestCase("info.game_datetime")]
        [TestCase("info.queue_id")]
        [TestCase("info.tft_set_number")]
        [TestCase("info.participants")]
        [TestCase("info.participants[0].placement")]
        [TestCase("info.participants[0].last_round")]
        [TestCase("info.participants[0].units")]
        [TestCase("info.participants[0].units[0].character_id")]
        [TestCase("info.participants[0].units[0].tier")]
        public void MissingRequiredFieldFailsSafely(string path)
        {
            var json = Fixture();
            ((JProperty)json.SelectToken(path).Parent).Remove();
            Reject(json.ToString());
        }

        /// <summary>Rejects fractional, string-coerced and out-of-range outcomes and incomplete or inconsistent lobbies.</summary>
        [TestCase("info.participants[0].placement", "0")]
        [TestCase("info.participants[0].placement", "9")]
        [TestCase("info.participants[0].placement", "1.1")]
        [TestCase("info.participants[0].placement", "\"1\"")]
        [TestCase("info.participants[0].placement", "2")]
        [TestCase("info.participants[0].last_round", "-1")]
        [TestCase("info.participants[0].units", "[]")]
        [TestCase("info.participants[0].units[0].character_id", "\" \"")]
        [TestCase("info.participants[0].units[0].character_id", "123")]
        [TestCase("info.participants[0].units[0].tier", "0")]
        [TestCase("info.game_datetime", "-1")]
        [TestCase("info.queue_id", "0")]
        [TestCase("info.participants", "[]")]
        public void InvalidPayloadFailsSafely(string path, string replacement)
        {
            var json = Fixture();
            json.SelectToken(path).Replace(JToken.Parse(replacement));
            Reject(json.ToString());
        }

        /// <summary>Supports standard eight-player placement ordering only.</summary>
        [TestCase(7)]
        [TestCase(9)]
        public void IncompleteOrOversizedLobbyFails(int count)
        {
            var json = Fixture();
            var players = (JArray)json["info"]["participants"];
            if (count == 7) players.RemoveAt(7); else players.Add(players[0].DeepClone());
            Reject(json.ToString());
        }

        /// <summary>Payload set, game release and supported schema version must agree with the selected catalog.</summary>
        [TestCase("info.tft_set_number", "17", "catalog_mismatch")]
        [TestCase("info.game_version", "\"Version 16.19.123.456\"", "catalog_mismatch")]
        [TestCase("info.game_version", "\"FakePlayer\"", "invalid_match_payload")]
        [TestCase("metadata.data_version", "\"999\"", "unsupported_match_version")]
        public void MismatchedVersionsFail(string path, string replacement, string code)
        {
            var json = Fixture();
            json.SelectToken(path).Replace(JToken.Parse(replacement));
            Reject(json.ToString(), code);
        }

        /// <summary>Syntax errors, extra content and duplicate fields never expose parser paths or payload fragments.</summary>
        [TestCase("{\"FakePlayer-FAKE-PUUID\":")]
        [TestCase("null")]
        [TestCase("[]")]
        public void MalformedJsonFailsSafely(string payload) => Reject(payload);

        /// <summary>Rejects ambiguous duplicate fields and trailing content after an otherwise valid match.</summary>
        [Test]
        public void DuplicateFieldsAndTrailingContentFail()
        {
            var payload = Fixture().ToString(Formatting.None);
            Reject(payload + "{}");
            Reject(payload.Replace("\"queue_id\":1100", "\"queue_id\":1100,\"queue_id\":1100"));
        }

        /// <summary>Rejects permissive Json.NET syntax before octal or hexadecimal values can change queue scope.</summary>
        [TestCase("octal")]
        [TestCase("hex")]
        [TestCase("single_quotes")]
        [TestCase("unquoted_key")]
        [TestCase("trailing_comma")]
        [TestCase("invalid_escape")]
        [TestCase("raw_control")]
        public void NonstandardJsonFailsSafely(string syntax)
        {
            var payload = Fixture().ToString(Formatting.None);
            switch (syntax)
            {
                case "octal": payload = payload.Replace("\"queue_id\":1100", "\"queue_id\":010"); break;
                case "hex": payload = payload.Replace("\"queue_id\":1100", "\"queue_id\":0x44c"); break;
                case "single_quotes": payload = payload.Replace('"', '\''); break;
                case "unquoted_key": payload = payload.Replace("\"queue_id\":", "queue_id:"); break;
                case "trailing_comma": payload = payload.Insert(payload.Length - 1, ","); break;
                case "invalid_escape": payload = payload.Replace("FakePlayer1", "Fake\\'Player1"); break;
                default: payload = payload.Replace("FakePlayer1", "Fake\u0001Player1"); break;
            }
            Reject(payload);
        }

        /// <summary>Lexical validation preserves valid escapes, delimiters inside strings, whitespace and ignored fractions.</summary>
        [Test]
        public void ValidJsonEscapesAndIgnoredNumericFieldsRemainSupported()
        {
            var payload = Fixture();
            payload["ignored"] = "FakePlayer quote \" slash \\ newline\n apostrophe ' ,} 010 NaN";
            payload["fraction"] = 1.25;
            Assert.That(Read(payload).IsAccepted, Is.True);
            var bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            foreach (var chunk in new[] { 1, 17 })
            using (var input = new NonseekableStream(bytes, chunk))
                Assert.That(Parser().Read(input, "asia", "MASTER+").IsAccepted, Is.True);
        }

        /// <summary>Unknown optional identity objects are ignored while the input is consumed once without seeking or closing.</summary>
        [Test]
        public void CallerOwnsNonseekableInputOnSuccessAndFailure()
        {
            foreach (var payload in new[] { Fixture().ToString(), "{\"FakePlayer\":" })
            using (var input = new NonseekableStream(Encoding.UTF8.GetBytes(payload)))
            {
                try { Parser().Read(input, "asia", "MASTER+"); } catch (MatchPayloadException) { }
                Assert.That(input.WasDisposed, Is.False);
                Assert.That(input.CanRead, Is.True);
            }
        }

        /// <summary>Untrusted I/O diagnostics are replaced with a controlled error and no inner exception.</summary>
        [Test]
        public void InputFailureIsSanitized()
        {
            using (var input = new FailingStream())
            {
                var error = Assert.Throws<MatchPayloadException>(() => Parser().Read(input, "asia", "MASTER+"));
                Assert.That(error.Code, Is.EqualTo("invalid_match_payload"));
                Assert.That(error.InnerException, Is.Null);
                AssertSafe(error.ToString());
                Assert.That(input.CanRead, Is.True);
            }
        }

        /// <summary>Rejects invalid UTF-8, excessive nesting and oversized payloads without owning the stream.</summary>
        [Test]
        public void ResourceAndEncodingLimitsFailSafely()
        {
            using (var input = new MemoryStream(new byte[] { 0xff, 0xfe, 0xff }))
                Assert.Throws<MatchPayloadException>(() => Parser().Read(input, "asia", "MASTER+"));
            Reject(new string('[', 40) + "0" + new string(']', 40));
            Reject("{\"ignored\":\"" + new string('x', 8 * 1024 * 1024) + "\"}");
        }

        /// <summary>Full catalog profile and patch identity cannot be substituted using patch label alone.</summary>
        [Test]
        public void ConstructorRequiresMatchingFullCatalogIdentity()
        {
            Assert.Throws<ArgumentException>(() => new MatchAnonymizer(Catalog(), Identity(CatalogSourceProfile.OfficialPlusCommunityDragon)));
        }

        /// <summary>Loads only a committed fabricated fixture; never reads credentials or live match responses.</summary>
        private static JObject Fixture() => JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,
            "TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json")));
        /// <summary>Creates the selected complete catalog identity.</summary>
        private static CatalogIdentity Identity(CatalogSourceProfile profile = CatalogSourceProfile.OfficialOnly)
            => new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), profile);
        /// <summary>Creates a tiny trusted catalog with one ID in each static category.</summary>
        private static StaticCatalog Catalog() => new StaticCatalog(
            new PatchManifest(2, Identity(), "Test", DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, "", ""),
            new[] { new UnitRecord("TEST_UNIT", null, 1, null, "", null) },
            new[] { new TraitRecord("TEST_TRAIT", null, null, null, "", null) },
            new[] { new ItemRecord("TEST_ITEM", null, null, null, null, "", null) },
            new[] { new AugmentRecord("TEST_AUGMENT", null, "", null, null, null, "", null) });
        /// <summary>Builds the memory-only parser without any API client or persistence.</summary>
        private static MatchAnonymizer Parser() => new MatchAnonymizer(Catalog(), Identity());
        /// <summary>Feeds one fabricated UTF-8 stream to the parser.</summary>
        private static AnonymizationResult Read(JObject json)
        {
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString(Formatting.None))))
                return Parser().Read(input, "asia", "MASTER+");
        }
        /// <summary>Asserts a controlled payload error without retaining an identifying parser exception.</summary>
        private static void Reject(string payload, string code = "invalid_match_payload")
        {
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(payload)))
            {
                var error = Assert.Throws<MatchPayloadException>(() => Parser().Read(input, "asia", "MASTER+"));
                Assert.That(error.Code, Is.EqualTo(code));
                Assert.That(error.InnerException, Is.Null);
                AssertSafe(error.ToString());
                Assert.That(input.CanRead, Is.True);
            }
        }
        /// <summary>Checks known fabricated identity markers and raw version notes never escape.</summary>
        private static void AssertSafe(string value)
        {
            foreach (var forbidden in new[] { RawMatch, "FAKE-PUUID", "FakePlayer", "FAKE-TAG", "FAKE-BUILD-NOTE" })
                Assert.That(value, Does.Not.Contain(forbidden));
        }

        private class NonseekableStream : MemoryStream
        {
            public bool WasDisposed;
            private readonly int _chunk;
            /// <summary>Creates a one-pass fabricated source.</summary>
            public NonseekableStream(byte[] bytes, int chunk = int.MaxValue) : base(bytes, false) { _chunk = chunk; }
            public override bool CanSeek => false;
            /// <summary>Can split escapes and lexical tokens across arbitrarily small input reads.</summary>
            public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, _chunk));
            /// <summary>Rejects attempts to rewind or inspect stream position.</summary>
            public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
            /// <summary>Records disposal so caller ownership can be checked.</summary>
            protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
        }
        private sealed class FailingStream : NonseekableStream
        {
            /// <summary>Creates a source whose read failure contains identifying diagnostics.</summary>
            public FailingStream() : base(Array.Empty<byte>()) { }
            /// <summary>Injects an untrusted read error without contacting external systems.</summary>
            public override int Read(byte[] buffer, int offset, int count)
                => throw new IOException("FakePlayer FAKE-PUUID " + RawMatch);
        }
    }
}
