using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>
    /// Per-match shape facts held in memory only until aggregation. Contains no identifiers, raw versions or
    /// static IDs; composition keys are fingerprint-v2 digests that never leave the process.
    /// </summary>
    internal sealed class MatchShapeSample
    {
        public const int FieldCount = 14;
        internal static readonly string[] FieldNames =
        {
            "metadata.match_id", "metadata.data_version", "info.game_version", "info.tft_set_number", "info.queue_id",
            "info.participants", "participants[].placement", "participants[].last_round", "units[].character_id",
            "units[].tier", "units[].itemNames", "traits[].name", "traits[].tier_current", "participants[].augments"
        };
        internal static readonly string[] StaticKinds = { "unit", "item", "trait", "augment" };

        public string QueueKey { get; internal set; } = "type:missing";
        public VersionShape GameVersion { get; internal set; } = VersionShapeSignature.Describe("missing", null);
        public VersionShape DataVersion { get; internal set; } = VersionShapeSignature.Describe("missing", null);
        public bool GameVersionRegexMatch { get; internal set; }
        public string Release { get; internal set; } = "unextractable";
        public string SetNumber { get; internal set; } = "unavailable";
        public string[] FieldTypes { get; } = new string[FieldCount];
        public string AugmentState { get; internal set; } = "noParticipants";
        public int[] UnknownOccurrences { get; } = new int[StaticKinds.Length];
        public int Boards { get; internal set; }
        public int ExcludedBoards { get; internal set; }
        internal List<string> Keys { get; } = new List<string>();
        public IReadOnlyList<string> CompositionKeys => Keys;
    }

    /// <summary>
    /// Token-level reader that extracts only the diagnostic fields from one match payload. It reuses the normal
    /// path's strict lexical, duplicate-key and 8 MiB guards, never builds DTOs or observations, and keeps only one
    /// participant's character_id/tier_current values in a transient buffer.
    /// </summary>
    internal sealed class MatchShapeReader
    {
        private const int Unit = 0, Item = 1, Trait = 2, Augment = 3;
        private readonly MatchShapeDiagnosticInput _input;
        private readonly HashSet<string>[] _ids;

        public MatchShapeReader(MatchShapeDiagnosticInput input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _ids = new[] { input.UnitIds, input.ItemIds, input.TraitIds, input.AugmentIds }
                .Select(values => new HashSet<string>(values, StringComparer.Ordinal)).ToArray();
        }

        /// <summary>Leaves the caller's stream open; any malformed input becomes a fixed, value-free error.</summary>
        public MatchShapeSample Read(Stream utf8Json, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var context = new Context(token);
            try
            {
                using (var bounded = new MatchAnonymizer.BoundedInput(utf8Json))
                using (var text = new StreamReader(bounded, new UTF8Encoding(false, true), false, 4096, true))
                using (var syntax = new MatchAnonymizer.StrictSyntaxReader(text))
                using (var reader = new MatchAnonymizer.StrictReader(syntax)
                       { CloseInput = false, DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
                {
                    if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw Unreadable();
                    ReadObject(reader, context, name =>
                    {
                        if (name == "metadata") ReadMetadata(reader, context);
                        else if (name == "info") ReadInfo(reader, context);
                        else reader.Skip();
                    });
                    if (reader.Read()) throw Unreadable();
                }
                return context.Finish();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { throw Unreadable(); }
            finally { context.Clear(); }
        }

        private void ReadMetadata(JsonReader reader, Context context)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                reader.Skip();
                return;
            }
            ReadObject(reader, context, name =>
            {
                switch (name)
                {
                    case "match_id":
                        context.SetField(0, TypeOf(reader));
                        reader.Skip();
                        break;
                    case "data_version":
                        context.SetField(1, TypeOf(reader));
                        context.Sample.DataVersion = Shape(reader, out _);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            });
        }

        private void ReadInfo(JsonReader reader, Context context)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                reader.Skip();
                return;
            }
            ReadObject(reader, context, name =>
            {
                var sample = context.Sample;
                switch (name)
                {
                    case "game_version":
                        context.SetField(2, TypeOf(reader));
                        sample.GameVersion = Shape(reader, out var text);
                        sample.GameVersionRegexMatch = VersionShapeSignature.MatchesGameVersionContract(text);
                        sample.Release = VersionShapeSignature.TryExtractRelease(text, out var release)
                            ? string.Equals(release, _input.CatalogRelease, StringComparison.Ordinal) ? "match" : "mismatch"
                            : "unextractable";
                        break;
                    case "tft_set_number":
                        context.SetField(3, TypeOf(reader));
                        sample.SetNumber = reader.TokenType == JsonToken.Integer && reader.Value is long set
                            ? set == _input.SetNumber ? "match" : "mismatch"
                            : "unavailable";
                        reader.Skip();
                        break;
                    case "queue_id":
                        context.SetField(4, TypeOf(reader));
                        sample.QueueKey = reader.TokenType == JsonToken.Integer
                            ? reader.Value is long queue && queue >= 1 && queue <= 999999
                                ? queue.ToString(CultureInfo.InvariantCulture) : "out_of_range"
                            : "type:" + TypeOf(reader);
                        reader.Skip();
                        break;
                    case "participants":
                        ReadParticipants(reader, context);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            });
        }

        private void ReadParticipants(JsonReader reader, Context context)
        {
            if (reader.TokenType != JsonToken.StartArray)
            {
                context.SetField(5, TypeOf(reader));
                reader.Skip();
                return;
            }
            int count = 0, provided = 0;
            var nonObject = false;
            while (true)
            {
                if (!reader.Read()) throw Unreadable();
                if (reader.TokenType == JsonToken.EndArray) break;
                context.Token.ThrowIfCancellationRequested();
                count++;
                context.Sample.Boards++;
                if (reader.TokenType != JsonToken.StartObject)
                {
                    nonObject = true;
                    context.Sample.ExcludedBoards++;
                    reader.Skip();
                    continue;
                }
                if (ReadParticipant(reader, context)) provided++;
            }
            context.SetField(5, "array:" + (count > 16 ? "17+" : count.ToString(CultureInfo.InvariantCulture)) +
                                (nonObject ? "+nonobject" : ""));
            context.Sample.AugmentState = count == 0 ? "noParticipants"
                : provided == count ? "allProvided" : provided == 0 ? "noneProvided" : "mixed";
        }

        /// <summary>Reads one participant object; returns whether augments were provided as an array.</summary>
        private bool ReadParticipant(JsonReader reader, Context context)
        {
            var board = context.Board;
            board.Reset();
            bool placement = false, lastRound = false, augments = false, provided = false;
            ReadObject(reader, context, name =>
            {
                switch (name)
                {
                    case "placement":
                        placement = true;
                        context.AddField(6, TypeOf(reader));
                        reader.Skip();
                        break;
                    case "last_round":
                        lastRound = true;
                        context.AddField(7, TypeOf(reader));
                        reader.Skip();
                        break;
                    case "units":
                        board.UnitsSeen = true;
                        ReadArray(reader, context, () => ReadUnit(reader, context), () => board.Valid = false);
                        break;
                    case "traits":
                        board.TraitsSeen = true;
                        ReadArray(reader, context, () => ReadTrait(reader, context), () => board.Valid = false);
                        break;
                    case "augments":
                        augments = true;
                        context.AddField(13, TypeOf(reader));
                        provided = reader.TokenType == JsonToken.StartArray;
                        ReadArray(reader, context, () => CountStatic(reader, context, Augment), null);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            });
            if (!placement) context.AddField(6, "missing");
            if (!lastRound) context.AddField(7, "missing");
            if (!augments) context.AddField(13, "missing");
            if (board.Valid && board.UnitsSeen && board.TraitsSeen) context.Sample.Keys.Add(board.Fingerprint());
            else context.Sample.ExcludedBoards++;
            // The transient raw-ID buffer is cleared as soon as this participant is finished.
            board.Reset();
            return provided;
        }

        private void ReadUnit(JsonReader reader, Context context)
        {
            var board = context.Board;
            if (reader.TokenType != JsonToken.StartObject)
            {
                board.Valid = false;
                reader.Skip();
                return;
            }
            bool id = false, tier = false, items = false;
            ReadObject(reader, context, name =>
            {
                switch (name)
                {
                    case "character_id":
                        id = true;
                        context.AddField(8, TypeOf(reader));
                        if (reader.TokenType == JsonToken.String && !string.IsNullOrWhiteSpace((string)reader.Value))
                        {
                            var value = (string)reader.Value;
                            board.Units.Add(value);
                            if (!_ids[Unit].Contains(value)) context.Sample.UnknownOccurrences[Unit]++;
                        }
                        else board.Valid = false;
                        reader.Skip();
                        break;
                    case "tier":
                        tier = true;
                        context.AddField(9, TypeOf(reader));
                        reader.Skip();
                        break;
                    case "itemNames":
                        items = true;
                        context.AddField(10, TypeOf(reader));
                        ReadArray(reader, context, () => CountStatic(reader, context, Item), null);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            });
            if (!id)
            {
                context.AddField(8, "missing");
                board.Valid = false;
            }
            if (!tier) context.AddField(9, "missing");
            if (!items) context.AddField(10, "missing");
        }

        private void ReadTrait(JsonReader reader, Context context)
        {
            var board = context.Board;
            if (reader.TokenType != JsonToken.StartObject)
            {
                board.Valid = false;
                reader.Skip();
                return;
            }
            string traitName = null;
            long? tierCurrent = null;
            bool named = false, tiered = false;
            ReadObject(reader, context, name =>
            {
                switch (name)
                {
                    case "name":
                        named = true;
                        context.AddField(11, TypeOf(reader));
                        if (reader.TokenType == JsonToken.String && !string.IsNullOrWhiteSpace((string)reader.Value))
                        {
                            traitName = (string)reader.Value;
                            if (!_ids[Trait].Contains(traitName)) context.Sample.UnknownOccurrences[Trait]++;
                        }
                        reader.Skip();
                        break;
                    case "tier_current":
                        tiered = true;
                        context.AddField(12, TypeOf(reader));
                        if (reader.TokenType == JsonToken.Integer && reader.Value is long value) tierCurrent = value;
                        reader.Skip();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            });
            if (!named) context.AddField(11, "missing");
            if (!tiered) context.AddField(12, "missing");
            if (traitName == null || !tierCurrent.HasValue) board.Valid = false;
            else board.Traits.Add(new KeyValuePair<string, long>(traitName, tierCurrent.Value));
        }

        /// <summary>Counts catalog misses for string elements only; element values are never retained.</summary>
        private void CountStatic(JsonReader reader, Context context, int kind)
        {
            if (reader.TokenType == JsonToken.String && !_ids[kind].Contains((string)reader.Value))
                context.Sample.UnknownOccurrences[kind]++;
            reader.Skip();
        }

        /// <summary>Visits array elements, or skips a non-array value after invoking the optional mismatch callback.</summary>
        private static void ReadArray(JsonReader reader, Context context, Action element, Action notArray)
        {
            if (reader.TokenType != JsonToken.StartArray)
            {
                notArray?.Invoke();
                reader.Skip();
                return;
            }
            while (true)
            {
                if (!reader.Read()) throw Unreadable();
                if (reader.TokenType == JsonToken.EndArray) return;
                context.Token.ThrowIfCancellationRequested();
                element();
            }
        }

        /// <summary>Visits each property; the handler must consume the whole value.</summary>
        private static void ReadObject(JsonReader reader, Context context, Action<string> property)
        {
            while (true)
            {
                if (!reader.Read()) throw Unreadable();
                if (reader.TokenType == JsonToken.EndObject) return;
                if (reader.TokenType != JsonToken.PropertyName) throw Unreadable();
                var name = (string)reader.Value;
                if (!reader.Read()) throw Unreadable();
                property(name);
            }
        }

        /// <summary>Describes a version value by JSON type first, then its de-identified signature.</summary>
        private static VersionShape Shape(JsonReader reader, out string text)
        {
            text = null;
            VersionShape shape;
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    text = (string)reader.Value;
                    shape = VersionShapeSignature.Describe("string", text);
                    break;
                case JsonToken.Integer:
                    shape = VersionShapeSignature.Describe("integer",
                        Convert.ToString(reader.Value, CultureInfo.InvariantCulture));
                    break;
                case JsonToken.Null:
                    shape = VersionShapeSignature.Describe("null", null);
                    break;
                default:
                    shape = VersionShapeSignature.Describe("other", null);
                    break;
            }
            reader.Skip();
            return shape;
        }

        private static string TypeOf(JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String: return "string";
                case JsonToken.Integer: return "integer";
                case JsonToken.Float: return "float";
                case JsonToken.Boolean: return "boolean";
                case JsonToken.Null: return "null";
                case JsonToken.StartArray: return "array";
                case JsonToken.StartObject: return "object";
                default: throw Unreadable();
            }
        }

        private static InvalidDataException Unreadable() => new InvalidDataException("match_shape_unreadable");

        private sealed class Context
        {
            private readonly HashSet<string>[] _nested = Enumerable.Range(0, MatchShapeSample.FieldCount)
                .Select(_ => new HashSet<string>(StringComparer.Ordinal)).ToArray();
            public readonly MatchShapeSample Sample = new MatchShapeSample();
            public readonly BoardBuffer Board = new BoardBuffer();
            public readonly CancellationToken Token;

            public Context(CancellationToken token) { Token = token; }

            /// <summary>Records a top-level field type.</summary>
            public void SetField(int index, string type) => Sample.FieldTypes[index] = type;

            /// <summary>Records one occurrence type of a field nested inside participants.</summary>
            public void AddField(int index, string type) => _nested[index].Add(type);

            public MatchShapeSample Finish()
            {
                for (var i = 0; i < MatchShapeSample.FieldCount; i++)
                {
                    if (i >= 6) Sample.FieldTypes[i] = _nested[i].Count == 0 ? "none"
                        : string.Join("+", _nested[i].OrderBy(value => value, StringComparer.Ordinal));
                    else if (Sample.FieldTypes[i] == null) Sample.FieldTypes[i] = "missing";
                }
                return Sample;
            }

            public void Clear()
            {
                Board.Reset();
                foreach (var set in _nested) set.Clear();
            }
        }

        /// <summary>One participant's raw character_id and trait tier_current values, cleared after each participant.</summary>
        private sealed class BoardBuffer
        {
            public readonly List<string> Units = new List<string>();
            public readonly List<KeyValuePair<string, long>> Traits = new List<KeyValuePair<string, long>>();
            public bool Valid = true, UnitsSeen, TraitsSeen;

            public void Reset()
            {
                Units.Clear();
                Traits.Clear();
                Valid = true;
                UnitsSeen = false;
                TraitsSeen = false;
            }

            /// <summary>Mirrors CompositionFingerprintBuilder.Build (fingerprint v2) over raw IDs.</summary>
            public string Fingerprint()
            {
                var units = Units.OrderBy(id => id, StringComparer.Ordinal);
                var traits = Traits.Where(trait => trait.Value > 0).Select(trait => trait.Key)
                    .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
                var canonical = new JArray(new JArray(units), new JArray(traits));
                return ContentHasher.Sha256Hex("fingerprint-v2|" + canonical.ToString(Formatting.None));
            }
        }
    }
}
