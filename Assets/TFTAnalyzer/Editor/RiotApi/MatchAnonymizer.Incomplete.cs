using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.RiotApi
{
    public sealed partial class MatchAnonymizer
    {
        /// <summary>
        /// Consumes the caller's source once. The existing parser runs first; only fully validated,
        /// request-bound input can produce an additional incomplete candidate. Raw capture is erased on exit.
        /// </summary>
        public MatchReadWithIncompleteResult ReadWithIncomplete(Stream utf8Json, string region, string tierBucket,
            int requestedQueueId, string expectedMatchId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (requestedQueueId <= 0) throw new ArgumentOutOfRangeException(nameof(requestedQueueId));
            cancellationToken.ThrowIfCancellationRequested();
            using (var capture = new CapturedInput(utf8Json, cancellationToken))
            {
                AnonymizationResult normal = null;
                MatchPayloadException failure = null;
                try { normal = Read(capture, region, tierBucket); }
                catch (MatchPayloadException error) { failure = error; }
                cancellationToken.ThrowIfCancellationRequested();
                if (normal?.IsAccepted == true)
                    return new MatchReadWithIncompleteResult(normal, null);
                try
                {
                    // A failed I/O read is never retried. Early DTO errors may leave a readable tail to validate.
                    if (capture.Failed) return new MatchReadWithIncompleteResult(normal, failure);
                    capture.Complete();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(expectedMatchId))
                        return new MatchReadWithIncompleteResult(normal, failure);
                    var context = InspectCompleteCapture(capture, expectedMatchId, cancellationToken);
                    if (!context.IdentityMatches)
                        return new MatchReadWithIncompleteResult(normal, failure);
                    var factory = new IncompleteCandidateFactory(_referenceCatalog, context.MeaningContext, requestedQueueId);
                    IncompleteMatchCandidate candidate;
                    using (var input = capture.OpenMemory())
                    using (var text = new StreamReader(input, new UTF8Encoding(false, true), false, 4096, true))
                    using (var syntax = new StrictSyntaxReader(text))
                    using (var reader = CreateCandidateReader(syntax))
                        candidate = ExtractCandidate(reader, factory, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new MatchReadWithIncompleteResult(normal, failure, candidate,
                        ContentHasher.Sha256Hex("incomplete-match-v1|" + expectedMatchId), _identity,
                        ContentHasher.Sha256Hex(CanonicalCatalogSerializer.Serialize(_referenceCatalog)));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // New validation failures cannot replace the first result/diagnostic of the existing parser.
                    return new MatchReadWithIncompleteResult(normal, failure);
                }
            }
        }

        private static StrictReader CreateCandidateReader(TextReader input) => new StrictReader(input)
        { CloseInput = false, DateParseHandling = DateParseHandling.None, MaxDepth = 32 };

        private struct CaptureContext
        {
            public bool IdentityMatches;
            public IncompleteCandidateContext MeaningContext;
        }

        private CaptureContext InspectCompleteCapture(CapturedInput capture, string expectedMatchId, CancellationToken token)
        {
            bool identityMatches = false, supported = false, setMatches = false, versionMatches = false;
            using (var input = capture.OpenMemory())
            using (var text = new StreamReader(input, new UTF8Encoding(false, true), false, 4096, true))
            using (var syntax = new StrictSyntaxReader(text))
            using (var reader = CreateCandidateReader(syntax))
            {
                RequireStart(reader, token);
                while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
                {
                    var property = Property(reader, token);
                    if (Same(property, "metadata") && reader.TokenType == JsonToken.StartObject)
                    {
                        while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
                        {
                            var name = Property(reader, token);
                            if (Same(name, "match_id") && reader.TokenType == JsonToken.String)
                                identityMatches = string.Equals((string)reader.Value, expectedMatchId, StringComparison.Ordinal);
                            else if (Same(name, "data_version") && reader.TokenType == JsonToken.String)
                                supported = (string)reader.Value == "2";
                            Skip(reader, token);
                        }
                    }
                    else if (Same(property, "info") && reader.TokenType == JsonToken.StartObject)
                    {
                        while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
                        {
                            var name = Property(reader, token);
                            if (Same(name, "tft_set_number") && reader.TokenType == JsonToken.Integer)
                            {
                                try { setMatches = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture) == _identity.Patch.SetNumber; }
                                catch { setMatches = false; }
                            }
                            else if (Same(name, "game_version") && reader.TokenType == JsonToken.String)
                            {
                                try { versionMatches = Release(NumericVersion((string)reader.Value)) == _release; }
                                catch (MatchPayloadException) { versionMatches = false; }
                            }
                            Skip(reader, token);
                        }
                    }
                    else Skip(reader, token);
                }
                RequireEnd(reader, token);
            }
            return new CaptureContext
            {
                IdentityMatches = identityMatches,
                MeaningContext = !supported ? IncompleteCandidateContext.UnsupportedVersion :
                    setMatches && versionMatches ? IncompleteCandidateContext.SupportedVersionPatchConfirmed :
                    IncompleteCandidateContext.PatchUnconfirmed
            };
        }

        private static IncompleteMatchCandidate ExtractCandidate(JsonReader reader, IncompleteCandidateFactory f,
            CancellationToken token)
        {
            var set = f.NumberState(CandidateNumberKind.SetNumber, CandidateFieldState.Missing);
            var queue = f.NumberState(CandidateNumberKind.QueueId, CandidateFieldState.Missing);
            var participants = f.ParticipantsState(CandidateFieldState.Missing);
            RequireStart(reader, token);
            while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
            {
                var property = Property(reader, token);
                if (!Same(property, "info")) { Skip(reader, token); continue; }
                if (reader.TokenType != JsonToken.StartObject)
                {
                    var state = reader.TokenType == JsonToken.Null ? CandidateFieldState.Null : CandidateFieldState.InvalidFormat;
                    set = f.NumberState(CandidateNumberKind.SetNumber, state);
                    queue = f.NumberState(CandidateNumberKind.QueueId, state);
                    participants = f.ParticipantsState(state);
                    Skip(reader, token);
                    continue;
                }
                while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
                {
                    var name = Property(reader, token);
                    if (Same(name, "tft_set_number")) set = ReadNumber(reader, f, CandidateNumberKind.SetNumber, token);
                    else if (Same(name, "queue_id")) queue = ReadNumber(reader, f, CandidateNumberKind.QueueId, token);
                    else if (Same(name, "participants"))
                        participants = ReadArray(reader, 8, true, (r, ordinal) => ReadParticipant(r, f, ordinal, token),
                            f.Participants, f.ParticipantsState, token);
                    else Skip(reader, token);
                }
            }
            RequireEnd(reader, token);
            return f.Match(set, queue, participants);
        }

        private static IncompleteParticipantCandidate ReadParticipant(JsonReader reader, IncompleteCandidateFactory f,
            int ordinal, CancellationToken token)
        {
            var placement = f.NumberState(CandidateNumberKind.Placement, CandidateFieldState.Missing);
            var lastRound = f.NumberState(CandidateNumberKind.LastRound, CandidateFieldState.Missing);
            var units = f.UnitsState(CandidateFieldState.Missing);
            var traits = f.TraitsState(CandidateFieldState.Missing);
            var augments = f.AugmentsState(CandidateFieldState.Missing);
            while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
            {
                var name = Property(reader, token);
                if (Same(name, "placement")) placement = ReadNumber(reader, f, CandidateNumberKind.Placement, token);
                else if (Same(name, "last_round")) lastRound = ReadNumber(reader, f, CandidateNumberKind.LastRound, token);
                else if (Same(name, "units"))
                    units = ReadArray(reader, 64, true, (r, _) => ReadUnit(r, f, token), f.Units, f.UnitsState, token);
                else if (Same(name, "traits"))
                    traits = ReadArray(reader, 64, true, (r, _) => ReadTrait(r, f, token), f.Traits, f.TraitsState, token);
                else if (Same(name, "augments"))
                    augments = ReadArray(reader, 16, false, (r, _) => ReadId(r, f, CandidateIdKind.Augment, token),
                        f.Augments, f.AugmentsState, token);
                else Skip(reader, token);
            }
            if (reader.TokenType != JsonToken.EndObject) throw Invalid();
            return f.Participant(ordinal, placement, lastRound, units, traits, augments);
        }

        private static IncompleteUnitCandidate ReadUnit(JsonReader reader, IncompleteCandidateFactory f, CancellationToken token)
        {
            var id = f.IdState(CandidateIdKind.Unit, CandidateFieldState.Missing);
            var tier = f.NumberState(CandidateNumberKind.UnitTier, CandidateFieldState.Missing);
            var items = f.ItemsState(CandidateFieldState.Missing);
            while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
            {
                var name = Property(reader, token);
                if (Same(name, "character_id")) id = ReadId(reader, f, CandidateIdKind.Unit, token);
                else if (Same(name, "tier")) tier = ReadNumber(reader, f, CandidateNumberKind.UnitTier, token);
                else if (Same(name, "itemNames"))
                    items = ReadArray(reader, 16, false, (r, _) => ReadId(r, f, CandidateIdKind.Item, token),
                        f.Items, f.ItemsState, token);
                else Skip(reader, token);
            }
            if (reader.TokenType != JsonToken.EndObject) throw Invalid();
            return f.Unit(id, tier, items);
        }

        private static IncompleteTraitCandidate ReadTrait(JsonReader reader, IncompleteCandidateFactory f, CancellationToken token)
        {
            var id = f.IdState(CandidateIdKind.Trait, CandidateFieldState.Missing);
            var tier = f.NumberState(CandidateNumberKind.TraitTier, CandidateFieldState.Missing);
            while (Next(reader, token) && reader.TokenType != JsonToken.EndObject)
            {
                var name = Property(reader, token);
                if (Same(name, "name")) id = ReadId(reader, f, CandidateIdKind.Trait, token);
                else if (Same(name, "tier_current")) tier = ReadNumber(reader, f, CandidateNumberKind.TraitTier, token);
                else Skip(reader, token);
            }
            if (reader.TokenType != JsonToken.EndObject) throw Invalid();
            return f.Trait(id, tier);
        }

        private static CandidateList<T> ReadArray<T>(JsonReader reader, int max, bool objectsOnly,
            Func<JsonReader, int, T> item, Func<IEnumerable<T>, CandidateList<T>> values,
            Func<CandidateFieldState, CandidateList<T>> status, CancellationToken token)
        {
            if (reader.TokenType == JsonToken.Null) return status(CandidateFieldState.Null);
            if (reader.TokenType != JsonToken.StartArray)
            {
                Skip(reader, token);
                return status(CandidateFieldState.InvalidFormat);
            }
            var result = new List<T>();
            var count = 0;
            var invalid = false;
            while (Next(reader, token) && reader.TokenType != JsonToken.EndArray)
            {
                count++;
                if (count > max) { Skip(reader, token); continue; }
                if (objectsOnly && reader.TokenType != JsonToken.StartObject)
                {
                    invalid = true; Skip(reader, token); continue;
                }
                result.Add(item(reader, count));
            }
            if (reader.TokenType != JsonToken.EndArray) throw Invalid();
            return count > max ? status(CandidateFieldState.LimitExceeded) :
                invalid ? status(CandidateFieldState.InvalidFormat) : values(result);
        }

        private static CandidateNumberField ReadNumber(JsonReader reader, IncompleteCandidateFactory f,
            CandidateNumberKind kind, CancellationToken token)
        {
            if (reader.TokenType == JsonToken.Null) return f.NumberState(kind, CandidateFieldState.Null);
            if (reader.TokenType == JsonToken.Integer)
            {
                try { return f.Number(kind, Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture)); }
                catch (Exception error) when (error is OverflowException || error is InvalidCastException || error is FormatException)
                { return f.NumberState(kind, CandidateFieldState.OutOfRange); }
            }
            Skip(reader, token);
            return f.NumberState(kind, CandidateFieldState.InvalidFormat);
        }

        private static CandidateIdField ReadId(JsonReader reader, IncompleteCandidateFactory f,
            CandidateIdKind kind, CancellationToken token)
        {
            if (reader.TokenType == JsonToken.Null) return f.IdState(kind, CandidateFieldState.Null);
            if (reader.TokenType == JsonToken.String) return f.Id(kind, (string)reader.Value);
            Skip(reader, token);
            return f.IdState(kind, CandidateFieldState.InvalidFormat);
        }

        private static bool Same(string value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        private static bool Next(JsonReader reader, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return reader.Read(); }
        private static string Property(JsonReader reader, CancellationToken token)
        {
            if (reader.TokenType != JsonToken.PropertyName) throw Invalid();
            var name = (string)reader.Value;
            if (!Next(reader, token)) throw Invalid();
            return name;
        }
        private static void Skip(JsonReader reader, CancellationToken token)
        { token.ThrowIfCancellationRequested(); reader.Skip(); token.ThrowIfCancellationRequested(); }
        private static void RequireStart(JsonReader reader, CancellationToken token)
        { if (!Next(reader, token) || reader.TokenType != JsonToken.StartObject) throw Invalid(); }
        private static void RequireEnd(JsonReader reader, CancellationToken token)
        { if (reader.TokenType != JsonToken.EndObject || Next(reader, token)) throw Invalid(); }

        /// <summary>A bounded, erase-on-dispose tee that never seeks or owns the caller's source.</summary>
        private sealed class CapturedInput : Stream
        {
            private readonly Stream _input;
            private readonly CancellationToken _token;
            private readonly MemoryStream _bytes = new MemoryStream(4096);
            private bool _complete;
            internal bool Failed { get; private set; }
            internal CapturedInput(Stream input, CancellationToken token) { _input = input; _token = token; }
            public override bool CanRead => _input?.CanRead == true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (Failed) throw Invalid();
                if (_complete || count == 0) return 0;
                try
                {
                    _token.ThrowIfCancellationRequested();
                    var size = _input.Read(buffer, offset, Math.Min(count, MaxBytes - (int)_bytes.Length + 1));
                    if (_bytes.Length + size > MaxBytes) throw Invalid();
                    if (size == 0) _complete = true;
                    else _bytes.Write(buffer, offset, size);
                    return size;
                }
                catch { Failed = true; throw; }
            }

            internal void Complete()
            {
                var buffer = new byte[4096];
                try { while (!_complete) Read(buffer, 0, buffer.Length); }
                finally { Array.Clear(buffer, 0, buffer.Length); }
            }
            internal MemoryStream OpenMemory()
            {
                if (Failed || !_complete) throw Invalid();
                return new MemoryStream(_bytes.GetBuffer(), 0, (int)_bytes.Length, false, false);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Array.Clear(_bytes.GetBuffer(), 0, _bytes.Capacity);
                    _bytes.Dispose();
                }
                base.Dispose(disposing);
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
