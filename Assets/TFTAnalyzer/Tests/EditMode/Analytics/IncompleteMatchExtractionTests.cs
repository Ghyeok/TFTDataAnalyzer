using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    public sealed class IncompleteMatchExtractionTests
    {
        private const string RawId = "KR1_123456";
        private const string Sentinel = "SYNTHETIC_PRIVATE_EXTRACTION_SENTINEL";

        [Test]
        public void AcceptedObservationKeepsExistingResultAndDoesNotCreateAnIncompleteRecord()
        {
            var result = Read(Fixture());
            Assert.That(result.NormalResult.IsAccepted, Is.True);
            Assert.That(result.NormalResult.Observation.MatchHash, Is.EqualTo(ContentHasher.Sha256Hex("match-v1|" + RawId)));
            Assert.That(result.Failure, Is.Null);
            Assert.That(result.IncompleteCandidate, Is.Null);
            Assert.That(result.IncompleteMatchHash, Is.Null);
        }

        [TestCase("16.19.1", "Synthetic")]
        [TestCase("16.18.1", "Different reference content")]
        public void ReferenceOriginBindsIdentityAndContentEvenWhenStableIdsAreShared(string version, string unitName)
        {
            var firstCatalog = Catalog();
            var secondCatalog = Catalog(version, unitName);
            MatchReadWithIncompleteResult Parse(StaticCatalog catalog)
            {
                using (var stream = new OnePassStream(Encoding.UTF8.GetBytes(IncompleteFixture().ToString()), 127))
                    return new MatchAnonymizer(catalog, catalog.Manifest.Identity).ReadWithIncomplete(stream,
                        "asia", "MASTER+", 1100, RawId);
            }
            var first = Parse(firstCatalog);
            var second = Parse(secondCatalog);
            Assert.That(first.IncompleteCandidate.Participants.Values[0].Units.Values[0].StableId.Value, Is.EqualTo("TEST_UNIT"));
            Assert.That(second.IncompleteCandidate.Participants.Values[0].Units.Values[0].StableId.Value, Is.EqualTo("TEST_UNIT"));
            Assert.That(first.ReferenceCatalogIdentity, Is.EqualTo(firstCatalog.Manifest.Identity));
            Assert.That(second.ReferenceCatalogIdentity, Is.EqualTo(secondCatalog.Manifest.Identity));
            Assert.That(first.ReferenceCatalogHash, Is.EqualTo(ContentHasher.Sha256Hex(CanonicalCatalogSerializer.Serialize(firstCatalog))));
            Assert.That(second.ReferenceCatalogHash, Is.EqualTo(ContentHasher.Sha256Hex(CanonicalCatalogSerializer.Serialize(secondCatalog))));
            Assert.That(second.ReferenceCatalogHash, Is.Not.EqualTo(first.ReferenceCatalogHash));
            Assert.That(JsonConvert.SerializeObject(first), Does.Not.Contain(first.ReferenceCatalogHash));
            Assert.That(JsonConvert.SerializeObject(second), Does.Not.Contain(second.ReferenceCatalogHash));
            Assert.That(JsonConvert.SerializeObject(second), Does.Not.Contain(unitName));
        }

        [Test]
        public void UnsupportedResponseKeepsItsFirstFailureAndExtractsUnverifiedSafeValues()
        {
            var json = Fixture();
            json["metadata"]["data_version"] = "999";
            ((JObject)json["info"]["participants"][0]).Property("augments").Remove();
            json["ignored"] = Sentinel;
            json["info"]["game_version"] = Sentinel;
            var result = Read(json);
            Assert.That(result.Failure.ReasonCode, Is.EqualTo("missing_match_augments"));
            Assert.That(result.IncompleteMatchHash, Is.EqualTo(ContentHasher.Sha256Hex("incomplete-match-v1|" + RawId)));
            Assert.That(result.IncompleteMatchHash, Is.Not.EqualTo(ContentHasher.Sha256Hex("match-v1|" + RawId)));
            var match = result.IncompleteCandidate;
            Assert.That(match.SetNumber.Value, Is.EqualTo(18));
            Assert.That(match.Meaning, Is.EqualTo(CandidateMeaning.Unverified));
            Assert.That(match.Participants.Values[0].Units.Values[0].StableId.Value, Is.EqualTo("TEST_UNIT"));
            Assert.That(match.Participants.Values[0].Units.Values[0].StableId.Meaning, Is.EqualTo(CandidateMeaning.Unverified));
            Assert.That(match.Participants.Values[0].Augments.State, Is.EqualTo(CandidateFieldState.Missing));
            AssertSafe(JsonConvert.SerializeObject(result));
            Assert.That(JsonConvert.SerializeObject(result), Does.Not.Contain(result.IncompleteMatchHash));
        }

        [TestCase("Version 16.19.123.456", CandidateMeaning.Unverified)]
        [TestCase(Sentinel, CandidateMeaning.Unverified)]
        [TestCase("Version 16.18.123.456", CandidateMeaning.Confirmed)]
        public void SupportedSchemaSeparatesPatchConfirmationFromArrayPresence(string version, CandidateMeaning expected)
        {
            var json = Fixture();
            json["info"]["game_version"] = version;
            ((JObject)json["info"]["participants"][0]).Property("augments").Remove();
            var result = Read(json);
            Assert.That(result.Failure.ReasonCode, Is.EqualTo("missing_match_augments"));
            Assert.That(result.IncompleteCandidate.SetNumber.Meaning, Is.EqualTo(expected));
            Assert.That(result.IncompleteCandidate.Participants.Values[0].Augments.State, Is.EqualTo(CandidateFieldState.Missing));
        }

        [TestCase("null", CandidateFieldState.Null)]
        [TestCase("\"3\"", CandidateFieldState.InvalidFormat)]
        [TestCase("3.0", CandidateFieldState.InvalidFormat)]
        [TestCase("true", CandidateFieldState.InvalidFormat)]
        [TestCase("0", CandidateFieldState.OutOfRange)]
        [TestCase("9", CandidateFieldState.OutOfRange)]
        [TestCase("9223372036854775808", CandidateFieldState.OutOfRange)]
        public void InvalidNumericCandidatePreservesStateWithoutCoercion(string replacement, CandidateFieldState state)
        {
            var json = IncompleteFixture();
            json["info"]["participants"][0]["placement"] = JToken.Parse(replacement);
            var field = Read(json).IncompleteCandidate.Participants.Values[0].Placement;
            Assert.That(field.State, Is.EqualTo(state));
            Assert.That(field.Value, Is.Null);
        }

        [Test]
        public void MissingNumericAndMismatchedQueueValuesDoNotBecomeDefaults()
        {
            var json = IncompleteFixture();
            ((JObject)json["info"]["participants"][0]).Property("last_round").Remove();
            json["info"]["queue_id"] = 1099;
            var candidate = Read(json).IncompleteCandidate;
            Assert.That(candidate.QueueId.State, Is.EqualTo(CandidateFieldState.OutOfRange));
            Assert.That(candidate.QueueId.Value, Is.Null);
            Assert.That(candidate.Participants.Values[0].LastRound.State, Is.EqualTo(CandidateFieldState.Missing));
        }

        [TestCase("null", CandidateFieldState.Null)]
        [TestCase("[]", CandidateFieldState.EmptyArray)]
        [TestCase("{}", CandidateFieldState.InvalidFormat)]
        [TestCase("\"" + Sentinel + "\"", CandidateFieldState.InvalidFormat)]
        public void ArrayEnvelopeStatesStaySeparate(string replacement, CandidateFieldState state)
        {
            var json = IncompleteFixture();
            json["info"]["participants"][0]["augments"] = JToken.Parse(replacement);
            var field = Read(json).IncompleteCandidate.Participants.Values[0].Augments;
            Assert.That(field.State, Is.EqualTo(state));
            Assert.That(field.Values, Is.Empty);
            Assert.That(field.Meaning, Is.EqualTo(CandidateMeaning.Unverified));
        }

        [Test]
        public void UnknownAndWrongKindIdsAreDiscardedWhileKnownIdsComeFromTheReferenceCatalog()
        {
            var json = IncompleteFixture();
            var unit = json["info"]["participants"][0]["units"][0];
            unit["character_id"] = Sentinel;
            unit["itemNames"] = new JArray("TEST_UNIT", "TEST_ITEM", 42, JValue.CreateNull());
            var match = Read(json).IncompleteCandidate;
            var candidate = match.Participants.Values[0].Units.Values[0];
            Assert.That(candidate.StableId.State, Is.EqualTo(CandidateFieldState.CatalogUnconfirmed));
            Assert.That(candidate.StableId.Value, Is.Null);
            Assert.That(candidate.Items.Values.Select(x => x.State), Is.EqualTo(new[] {
                CandidateFieldState.CatalogUnconfirmed, CandidateFieldState.Value,
                CandidateFieldState.InvalidFormat, CandidateFieldState.Null }));
            Assert.That(candidate.Items.Values[1].Value, Is.EqualTo("TEST_ITEM"));
            AssertSafe(JsonConvert.SerializeObject(match));
        }

        [TestCase("units", 64)]
        [TestCase("traits", 64)]
        [TestCase("augments", 16)]
        [TestCase("items", 16)]
        [TestCase("participants", 8)]
        public void OverflowDiscardsTheWholeFieldRatherThanKeepingATruncatedArray(string kind, int max)
        {
            foreach (var count in new[] { max, max + 1 })
            {
                var json = IncompleteFixture();
                var participant = json["info"]["participants"][0];
                var target = kind == "participants" ? (JArray)json["info"]["participants"] :
                    kind == "items" ? (JArray)participant["units"][0]["itemNames"] : (JArray)participant[kind];
                var sample = target[0].DeepClone();
                target.Clear();
                for (var i = 0; i < count; i++) target.Add(sample.DeepClone());
                var match = Read(json).IncompleteCandidate;
                object field;
                if (kind == "participants") field = match.Participants;
                else
                {
                    var p = match.Participants.Values[0];
                    field = kind == "units" ? (object)p.Units : kind == "traits" ? p.Traits :
                        kind == "augments" ? p.Augments : p.Units.Values[0].Items;
                }
                Assert.That((CandidateFieldState)field.GetType().GetProperty("State").GetValue(field),
                    Is.EqualTo(count == max ? CandidateFieldState.Value : CandidateFieldState.LimitExceeded));
                Assert.That(((System.Collections.IEnumerable)field.GetType().GetProperty("Values").GetValue(field)).Cast<object>().Count(),
                    Is.EqualTo(count == max ? max : 0));
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("KR1_OTHER")]
        public void MissingOrMismatchedRequestedIdentityNeverProducesARecord(string requestedId)
        {
            var result = Read(IncompleteFixture(), requestedId);
            Assert.That(result.IncompleteCandidate, Is.Null);
            Assert.That(result.IncompleteMatchHash, Is.Null);
            Assert.That(result.Failure.Code, Is.EqualTo("unsupported_match_version"));
        }

        [TestCase("tail")]
        [TestCase("duplicate")]
        [TestCase("ignored_duplicate")]
        [TestCase("trailing_comma")]
        [TestCase("depth")]
        public void FullSyntaxValidationRejectsCandidatesEvenAfterAnEarlierSemanticFailure(string corruption)
        {
            var json = IncompleteFixture();
            json["info"]["participants"][0]["augments"] = JValue.CreateNull();
            json["ignored_padding"] = new string('x', 20000);
            var text = json.ToString(Formatting.None);
            switch (corruption)
            {
                case "tail": text += "{\"raw\":\"" + Sentinel + "\""; break;
                case "duplicate": text = text.Replace("\"queue_id\":1100", "\"queue_id\":1100,\"QUEUE_ID\":1100"); break;
                case "ignored_duplicate": text = text.Insert(text.Length - 1, ",\"ignored\":{\"secret\":1,\"secret\":2}"); break;
                case "trailing_comma": text = text.Insert(text.Length - 1, ","); break;
                default: text = text.Insert(text.Length - 1, ",\"deep\":" + new string('[', 40) + "0" + new string(']', 40)); break;
            }
            var expected = ExistingFailure(text);
            var result = ReadText(text);
            Assert.That(result.Failure.ReasonCode, Is.EqualTo(expected.ReasonCode));
            Assert.That(result.IncompleteCandidate, Is.Null);
            Assert.That(result.IncompleteMatchHash, Is.Null);
            AssertSafe(JsonConvert.SerializeObject(result));
        }

        [Test]
        public void OnePassNonseekableInputIsFullyConsumedWithoutClosingTheCallerStream()
        {
            var bytes = Encoding.UTF8.GetBytes(IncompleteFixture().ToString(Formatting.None));
            foreach (var chunk in new[] { 1, 127, 4096 })
            using (var stream = new OnePassStream(bytes, chunk))
            {
                var result = Parser().ReadWithIncomplete(stream, "asia", "MASTER+", 1100, RawId);
                Assert.That(result.IncompleteCandidate, Is.Not.Null);
                Assert.That(stream.BytesRead, Is.EqualTo(bytes.Length));
                Assert.That(stream.WasDisposed, Is.False);
                Assert.That(stream.CanRead, Is.True);
            }
        }

        [Test]
        public void LateIoFailureKeepsTheOriginalFirstFailureAndDoesNotPublishAPartialCandidate()
        {
            var json = IncompleteFixture();
            json["info"]["participants"][0]["augments"] = JValue.CreateNull();
            json["ignored_padding"] = new string('x', 20000);
            var bytes = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            using (var stream = new OnePassStream(bytes, 128, 12000))
            {
                var result = Parser().ReadWithIncomplete(stream, "asia", "MASTER+", 1100, RawId);
                Assert.That(result.Failure.ReasonCode, Is.EqualTo("null_match_augments"));
                Assert.That(result.IncompleteCandidate, Is.Null);
                Assert.That(stream.FailedReads, Is.EqualTo(1));
                Assert.That(stream.WasDisposed, Is.False);
                AssertSafe(JsonConvert.SerializeObject(result));
            }
        }

        [Test]
        public void InitialIoFailureIsSanitizedAndNeverRetried()
        {
            using (var stream = new OnePassStream(Array.Empty<byte>(), 128, 0))
            {
                var result = Parser().ReadWithIncomplete(stream, "asia", "MASTER+", 1100, RawId);
                Assert.That(result.Failure.Code, Is.EqualTo("invalid_match_payload"));
                Assert.That(result.IncompleteCandidate, Is.Null);
                Assert.That(stream.FailedReads, Is.EqualTo(1));
                AssertSafe(result.Failure.ToString());
            }
        }

        [Test]
        public void CancellationProducesNoCandidateAndDoesNotCloseTheCallerStream()
        {
            using (var cancellation = new CancellationTokenSource())
            using (var stream = new OnePassStream(Encoding.UTF8.GetBytes(IncompleteFixture().ToString()), 128))
            {
                stream.AfterRead = () => cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() =>
                    Parser().ReadWithIncomplete(stream, "asia", "MASTER+", 1100, RawId, cancellation.Token));
                Assert.That(stream.WasDisposed, Is.False);
            }
        }

        [Test]
        public void OversizedOrInvalidUtf8PayloadNeverProducesCandidates()
        {
            var json = IncompleteFixture();
            json["ignored"] = new string('x', 8 * 1024 * 1024);
            var result = Read(json);
            Assert.That(result.IncompleteCandidate, Is.Null);
            Assert.That(result.IncompleteMatchHash, Is.Null);
            var bytes = Encoding.UTF8.GetBytes(IncompleteFixture().ToString());
            bytes[bytes.Length - 2] = 0xff;
            using (var stream = new OnePassStream(bytes, 127))
                Assert.That(Parser().ReadWithIncomplete(stream, "asia", "MASTER+", 1100, RawId).IncompleteCandidate, Is.Null);
        }

        [Test]
        public void ExistingQuarantineIsPreservedAlongsideACatalogFilteredIncompleteCandidate()
        {
            var json = Fixture();
            json["info"]["participants"][0]["units"][0]["character_id"] = Sentinel;
            var result = Read(json);
            Assert.That(result.NormalResult.IsAccepted, Is.False);
            Assert.That(result.NormalResult.UnknownStaticIds.Count, Is.EqualTo(1));
            Assert.That(result.Failure, Is.Null);
            Assert.That(result.IncompleteCandidate.Participants.Values[0].Units.Values[0].StableId.Value, Is.Null);
            AssertSafe(JsonConvert.SerializeObject(result));
        }

        private static JObject Fixture() => JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,
            "TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json")));
        private static JObject IncompleteFixture()
        {
            var json = Fixture();
            json["metadata"]["data_version"] = "999";
            return json;
        }
        private static MatchAnonymizer Parser()
        {
            var catalog = Catalog();
            return new MatchAnonymizer(catalog, catalog.Manifest.Identity);
        }
        private static StaticCatalog Catalog(string version = "16.18.1", string unitName = "Synthetic")
        {
            var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", version), CatalogSourceProfile.OfficialOnly);
            var name = new LocalizedText("", "Synthetic");
            return new StaticCatalog(new PatchManifest(2, identity, "Test", DateTime.UnixEpoch, "", null,
                DateTime.UnixEpoch, null, 0, 0, "", ""),
                new[] { new UnitRecord("TEST_UNIT", new LocalizedText("", unitName), 1, null, "", null) },
                new[] { new TraitRecord("TEST_TRAIT", name, null, null, "", null) },
                new[] { new ItemRecord("TEST_ITEM", name, null, null, null, "", null) },
                new[] { new AugmentRecord("TEST_AUGMENT", name, "", null, null, null, "", null) });
        }
        private static MatchReadWithIncompleteResult Read(JObject json, string requestedId = RawId) =>
            ReadText(json.ToString(Formatting.None), requestedId);
        private static MatchReadWithIncompleteResult ReadText(string text, string requestedId = RawId)
        {
            using (var stream = new OnePassStream(Encoding.UTF8.GetBytes(text), 127))
                return Parser().ReadWithIncomplete(stream, "asia", "MASTER+", 1100, requestedId);
        }
        private static MatchPayloadException ExistingFailure(string text)
        {
            using (var stream = new OnePassStream(Encoding.UTF8.GetBytes(text), 127))
                return Assert.Throws<MatchPayloadException>(() => Parser().Read(stream, "asia", "MASTER+"));
        }
        private static void AssertSafe(string text)
        {
            foreach (var forbidden in new[] { RawId, Sentinel, "FAKE-PUUID", "FakePlayer", "FAKE-BUILD-NOTE" })
                Assert.That(text, Does.Not.Contain(forbidden));
        }

        private sealed class OnePassStream : Stream
        {
            private readonly byte[] _bytes;
            private readonly int _chunk, _failAfter;
            private int _position;
            public int BytesRead { get; private set; }
            public int FailedReads { get; private set; }
            public bool WasDisposed { get; private set; }
            public Action AfterRead;
            public OnePassStream(byte[] bytes, int chunk, int failAfter = int.MaxValue)
            { _bytes = bytes; _chunk = chunk; _failAfter = failAfter; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _failAfter) { FailedReads++; throw new IOException(Sentinel); }
                var size = Math.Min(Math.Min(count, _chunk), _bytes.Length - _position);
                Array.Copy(_bytes, _position, buffer, offset, size);
                _position += size; BytesRead += size;
                AfterRead?.Invoke();
                return size;
            }
            public override bool CanRead => !WasDisposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override long Length => throw new NotSupportedException();
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
        }
    }
}
