using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Consumes one bounded memory stream and admits only anonymous, catalog-bound complete lobbies.</summary>
    public sealed class MatchAnonymizer
    {
        private readonly CatalogIdentity _identity;
        private readonly Dictionary<string, HashSet<string>> _ids;
        private readonly string _release;
        private const int MaxBytes = 8 * 1024 * 1024;

        /// <summary>Requires an explicitly selected full identity matching the trusted catalog and source profile.</summary>
        public MatchAnonymizer(StaticCatalog catalog, CatalogIdentity activeCatalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (activeCatalog == null) throw new ArgumentNullException(nameof(activeCatalog));
            var actual = catalog.Manifest.Identity ?? new CatalogIdentity(catalog.Manifest.Patch, CatalogSourceProfile.OfficialOnly);
            if (!actual.Equals(activeCatalog) || actual.Patch.SetNumber != activeCatalog.Patch.SetNumber)
                throw new ArgumentException("Active catalog identity does not match.");
            _identity = activeCatalog;
            _release = Release(NumericVersion(activeCatalog.Patch.SourceGameVersion));
            _ids = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["unit"] = new HashSet<string>(catalog.Units.Select(value => value.StableId), StringComparer.Ordinal),
                ["item"] = new HashSet<string>(catalog.Items.Select(value => value.StableId), StringComparer.Ordinal),
                ["trait"] = new HashSet<string>(catalog.Traits.Select(value => value.StableId), StringComparer.Ordinal),
                ["augment"] = new HashSet<string>(catalog.Augments.Select(value => value.StableId), StringComparer.Ordinal)
            };
        }

        /// <summary>Leaves the caller's raw stream open; never returns DTOs or untrusted diagnostic values.</summary>
        public AnonymizationResult Read(Stream utf8Json, string region, string tierBucket)
        {
            RiotMatchDto payload = null;
            try
            {
                region = RiotRouting.NormalizeRegion(region);
                if (string.IsNullOrWhiteSpace(tierBucket)) throw Invalid();
                using (var bounded = new BoundedInput(utf8Json))
                using (var text = new StreamReader(bounded, new UTF8Encoding(false, true), false, 4096, true))
                using (var syntax = new StrictSyntaxReader(text))
                using (var reader = new StrictReader(syntax) { CloseInput = false, DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
                {
                    var serializer = JsonSerializer.Create(new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.None,
                        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        Culture = CultureInfo.InvariantCulture,
                        Converters = { new StrictScalarConverter() }
                    });
                    // Retain the root even on partial parse failure so finally can clear its references.
                    payload = new RiotMatchDto();
                    serializer.Populate(reader, payload);
                    if (payload?.Metadata == null || payload.Info == null || reader.Read()) throw Invalid();
                }
                return Project(payload, region, tierBucket);
            }
            catch (MatchPayloadException) { throw; }
            catch { throw Invalid(); }
            finally { payload?.Clear(); }
        }

        /// <summary>Validates admission before publishing any observation, then resolves only static catalog IDs.</summary>
        private AnonymizationResult Project(RiotMatchDto payload, string region, string tierBucket)
        {
            var info = payload.Info;
            if (payload.Metadata.DataVersion != "2") throw new MatchPayloadException(MatchPayloadFailure.UnsupportedVersion);
            if (string.IsNullOrWhiteSpace(payload.Metadata.MatchId) || info.QueueId <= 0 || info.GameDatetime < 0 ||
                info.Participants == null || info.Participants.Length != 8) throw Invalid();
            var version = NumericVersion(info.GameVersion);
            if (info.SetNumber != _identity.Patch.SetNumber || Release(version) != _release)
                throw new MatchPayloadException(MatchPayloadFailure.CatalogMismatch);
            var placements = info.Participants.Select(value => value?.Placement).ToArray();
            if (placements.Any(value => !value.HasValue || value < 1 || value > 8) || placements.Distinct().Count() != 8)
                throw Invalid();
            var unknown = new SortedDictionary<string, UnknownStaticId>(StringComparer.Ordinal);
            var participants = info.Participants.Select(participant => ProjectParticipant(participant, unknown)).ToArray();
            if (unknown.Count > 0) return new AnonymizationResult(null, unknown.Values);
            var observation = new AnonymousMatchObservation(ContentHasher.Sha256Hex("match-v1|" + payload.Metadata.MatchId),
                new MetricScope(_identity, region, info.QueueId.Value.ToString(CultureInfo.InvariantCulture), tierBucket),
                DateTimeOffset.FromUnixTimeMilliseconds(info.GameDatetime.Value), participants, payload.Metadata.DataVersion, version);
            return new AnonymizationResult(observation, null);
        }

        /// <summary>Projects final board fields with bounded collections and no personal properties.</summary>
        private AnonymousParticipant ProjectParticipant(RiotMatchParticipant participant, SortedDictionary<string, UnknownStaticId> unknown)
        {
            if (participant == null || participant.Units == null || participant.Units.Length == 0 ||
                participant.Units.Length > 64 || participant.Traits == null || participant.Traits.Length > 64 ||
                participant.Augments == null || participant.Augments.Length > 16 || participant.LastRound < 0) throw Invalid();
            var units = participant.Units.Select(unit =>
            {
                if (unit == null || unit.ItemNames == null || unit.ItemNames.Length > 16) throw Invalid();
                return new AnonymousUnit(Resolve("unit", unit.CharacterId, unknown), unit.Tier.Value,
                    unit.ItemNames.Select(id => Resolve("item", id, unknown)));
            }).ToArray();
            var traits = participant.Traits.Select(trait =>
            {
                if (trait == null) throw Invalid();
                return new AnonymousTrait(Resolve("trait", trait.Name, unknown), trait.TierCurrent.Value);
            }).ToArray();
            return new AnonymousParticipant(participant.Placement.Value, participant.LastRound.Value, units, traits,
                participant.Augments.Select(id => Resolve("augment", id, unknown)));
        }

        /// <summary>Returns known IDs only; untrusted unknown values become category-specific diagnostic hashes.</summary>
        private string Resolve(string kind, string id, SortedDictionary<string, UnknownStaticId> unknown)
        {
            if (string.IsNullOrWhiteSpace(id)) throw Invalid();
            if (_ids[kind].Contains(id)) return id;
            var digest = ContentHasher.Sha256Hex("static-id-v1|" + kind + "|" + id);
            unknown[kind + "|" + digest] = new UnknownStaticId(kind, digest);
            return "_quarantined_";
        }

        /// <summary>Retains only a numeric build prefix, discarding free-form version annotations.</summary>
        private static string NumericVersion(string value)
        {
            var match = Regex.Match(value ?? "", @"\A(?:Version )?([0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,10}(?:\.[0-9]{1,10})?)(?:\s[^\r\n]*)?\z", RegexOptions.CultureInvariant);
            if (!match.Success) throw Invalid();
            return match.Groups[1].Value;
        }
        /// <summary>Extracts the source game release independently of the product's TFT patch label.</summary>
        private static string Release(string version) => string.Join(".", version.Split('.').Take(2));
        /// <summary>Creates a constant error without carrying an original exception or payload value.</summary>
        private static MatchPayloadException Invalid() => new MatchPayloadException(MatchPayloadFailure.InvalidPayload);

        private sealed class StrictScalarConverter : JsonConverter
        {
            public override bool CanWrite => false;
            /// <summary>Validates only known scalar DTO fields, leaving ignored fields outside the contract.</summary>
            public override bool CanConvert(Type type) => type == typeof(string) || type == typeof(int?) || type == typeof(long?);
            /// <summary>Rejects numeric strings, fractional integers and numeric identifiers instead of coercing them.</summary>
            public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
            {
                if (type == typeof(string))
                {
                    if (reader.TokenType != JsonToken.String) throw Invalid();
                    return (string)reader.Value;
                }
                if (reader.TokenType != JsonToken.Integer) throw Invalid();
                var value = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture);
                return type == typeof(int?) ? (object)checked((int)value) : value;
            }
            /// <summary>Writing raw DTOs is intentionally unsupported.</summary>
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();
        }

        private sealed class StrictReader : JsonTextReader
        {
            private readonly Stack<HashSet<string>> _properties = new Stack<HashSet<string>>();
            /// <summary>Tracks only property names while consuming JSON once.</summary>
            public StrictReader(TextReader input) : base(input) { }
            /// <summary>Rejects duplicate fields and non-JSON extensions, including inside skipped identity objects.</summary>
            public override bool Read()
            {
                if (!base.Read()) return false;
                if (TokenType == JsonToken.StartObject) _properties.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                else if (TokenType == JsonToken.EndObject) _properties.Pop();
                else if (TokenType == JsonToken.PropertyName &&
                         (QuoteChar != '"' || !_properties.Peek().Add((string)Value))) throw Invalid();
                else if (TokenType == JsonToken.Comment || TokenType == JsonToken.Undefined || TokenType == JsonToken.StartConstructor) throw Invalid();
                return true;
            }
        }

        // JsonTextReader accepts JavaScript extensions and normalizes octal/hex numbers.
        // Check lexical spelling while streaming, before those original characters are lost.
        private sealed class StrictSyntaxReader : TextReader
        {
            private readonly TextReader _input;
            private readonly StringBuilder _token = new StringBuilder();
            private bool _inString;
            private bool _escape;
            private int _unicodeDigits;
            private char _previous;

            /// <summary>Wraps decoded input without buffering the full payload or taking ownership.</summary>
            public StrictSyntaxReader(TextReader input) { _input = input; }
            /// <summary>Validates each source character before Json.NET can normalize it.</summary>
            public override int Read(char[] buffer, int index, int count)
            {
                var size = _input.Read(buffer, index, count);
                for (var i = index; i < index + size; i++) Validate(buffer[i]);
                if (size == 0 && count != 0) Complete();
                return size;
            }
            /// <summary>Preserves the same validation for single-character consumers.</summary>
            public override int Read()
            {
                var value = _input.Read();
                if (value < 0) Complete(); else Validate((char)value);
                return value;
            }
            /// <summary>Rejects non-JSON quotes, escapes, whitespace and trailing separators outside strings.</summary>
            private void Validate(char value)
            {
                if (_inString)
                {
                    if (value < 0x20) throw Invalid();
                    if (_unicodeDigits > 0)
                    {
                        if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') ||
                              (value >= 'A' && value <= 'F'))) throw Invalid();
                        _unicodeDigits--;
                    }
                    else if (_escape)
                    {
                        if (value == 'u') _unicodeDigits = 4;
                        else if ("\"\\/bfnrt".IndexOf(value) < 0) throw Invalid();
                        _escape = false;
                    }
                    else if (value == '\\') _escape = true;
                    else if (value == '"') { _inString = false; _previous = '"'; }
                    return;
                }
                if (value == ' ' || value == '\t' || value == '\n' || value == '\r') { FinishToken(); return; }
                if (value == '"') { FinishToken(); _inString = true; _previous = value; return; }
                if (value == '{' || value == '[' || value == ':' || value == ',' || value == '}' || value == ']')
                {
                    FinishToken();
                    if ((value == '}' || value == ']') && _previous == ',') throw Invalid();
                    _previous = value;
                    return;
                }
                if (value == '\'' || char.IsControl(value) || char.IsWhiteSpace(value)) throw Invalid();
                _token.Append(value);
                _previous = value;
            }
            /// <summary>Accepts standard literals and decimal JSON number grammar, never octal or hexadecimal.</summary>
            private void FinishToken()
            {
                if (_token.Length == 0) return;
                var value = _token.ToString();
                _token.Clear();
                if (value != "true" && value != "false" && value != "null" &&
                    !Regex.IsMatch(value, @"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z", RegexOptions.CultureInvariant))
                    throw Invalid();
            }
            /// <summary>Rejects unfinished lexical tokens without exposing their contents.</summary>
            private void Complete()
            {
                if (_inString || _escape || _unicodeDigits != 0) throw Invalid();
                FinishToken();
            }
        }

        private sealed class BoundedInput : Stream
        {
            private readonly Stream _input;
            private int _read;
            /// <summary>Wraps a caller-owned source without taking ownership or seeking.</summary>
            public BoundedInput(Stream input) { _input = input ?? throw Invalid(); }
            public override bool CanRead => _input.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            /// <summary>Bounds all consumed bytes, including ignored fields and nonseekable inputs.</summary>
            public override int Read(byte[] buffer, int offset, int count)
            {
                var size = _input.Read(buffer, offset, Math.Min(count, MaxBytes - _read + 1));
                _read += size;
                if (_read > MaxBytes) throw Invalid();
                return size;
            }
            /// <summary>Does not flush or mutate the input.</summary>
            public override void Flush() { }
            /// <summary>Prohibits input rewinding.</summary>
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            /// <summary>Prohibits mutation of input length.</summary>
            public override void SetLength(long value) => throw new NotSupportedException();
            /// <summary>Prohibits writing to the input.</summary>
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
