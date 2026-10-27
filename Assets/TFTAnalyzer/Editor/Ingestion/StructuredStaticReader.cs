using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Ingestion
{
    internal sealed class StructuredStaticReadResult
    {
        public IReadOnlyDictionary<string, double?> BaseStats { get; internal set; } = new ReadOnlyDictionary<string, double?>(new Dictionary<string, double?>());
        public IReadOnlyList<StaticNumericSeries> AbilityVariables { get; internal set; } = Array.Empty<StaticNumericSeries>();
        public IReadOnlyList<TraitBreakpoint> Breakpoints { get; internal set; } = Array.Empty<TraitBreakpoint>();
        public bool? Unique { get; internal set; }
        public IReadOnlyList<StaticExtensionField> Extensions { get; internal set; } = Array.Empty<StaticExtensionField>();
        public IReadOnlyList<FieldOrigin> Origins { get; internal set; } = Array.Empty<FieldOrigin>();
        public IReadOnlyList<ValidationIssue> Issues { get; internal set; } = Array.Empty<ValidationIssue>();
    }

    // Reads literal source members only. It never interprets formulas, source slots, URLs,
    // restriction tokens, or unknown JSON as executable behavior or statistical policy.
    internal static class StructuredStaticReader
    {
        internal static JObject ParseDocument(string json)
        {
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw Invalid();
                return document;
            }
        }

        // Match each adapter's existing mapping, including fields it deliberately does NOT
        // map. An unmodeled official field must not disappear just because CDragon maps it.
        internal static IReadOnlyCollection<string> ConsumedFields(string type, JObject ko, JObject en, bool community)
        {
            var fields = new HashSet<string>(new[] { "/id", "/name", "/desc", "/description" }, StringComparer.Ordinal);
            if (community) fields.UnionWith(new[] { "/apiName", "/icon", "/isAugment" });
            else fields.Add("/image/full");
            if (type == "unit")
            {
                fields.UnionWith(new[] { "/cost", "/tier", "/role" });
                if (community) fields.UnionWith(new[] { "/traits", "/ability/desc" });
            }
            if (type == "item") { fields.Add("/components"); if (community) fields.Add("/composition"); }
            if (type == "augment") { fields.Add("/tier"); if (community) fields.UnionWith(new[] { "/related", "/relatedIds" }); }
            var bags = !community && (type == "unit" || type == "item") ? new[] { "stats", "effects" } : new[] { "effects" };
            foreach (var row in new[] { ko, en }.Where(x => x != null)) foreach (var bag in bags)
                if (row[bag] is JObject obj) foreach (var field in obj.Properties())
                    if (field.Value.Type == JTokenType.Integer || field.Value.Type == JTokenType.Float)
                    { Number(field.Value); fields.Add("/" + bag + "/" + FieldOrigin.Escape(field.Name)); }
            return fields;
        }

        internal static StructuredStaticReadResult Read(string recordType, string stableId, JObject ko, JObject en,
            SourceStamp koSource, SourceStamp enSource, IReadOnlyCollection<string> consumedPointers)
        {
            try { return ReadCore(recordType, stableId, ko, en, koSource, enSource, consumedPointers); }
            catch (Exception error) when (error is ArgumentException || error is JsonException || error is OverflowException)
            { throw new JsonSerializationException("invalid_structured_static"); }
        }

        private static StructuredStaticReadResult ReadCore(string type, string id, JObject ko, JObject en,
            SourceStamp koSource, SourceStamp enSource, IReadOnlyCollection<string> consumed)
        {
            var result = new StructuredStaticReadResult(); var origins = new List<FieldOrigin>(); var issues = new List<ValidationIssue>();
            void Origin(string path, SourceStamp source) => origins.Add(new FieldOrigin(type, id, path, new[] { source }));
            void Warning(string code) { if (!issues.Any(x => x.Code == code)) issues.Add(new ValidationIssue(ValidationSeverity.Warning, code, "", "원본 구조에 미상 값 또는 언어별 차이가 있습니다. 선택한 값의 필드 출처를 확인하세요.")); }
            JToken At(JObject row, string path)
            {
                JToken current = row;
                foreach (var key in path.Split('/').Skip(1)) current = (current as JObject)?[key];
                return current;
            }
            (JToken Value, SourceStamp Source) Pick(string path)
            {
                var left = At(ko, path); var right = At(en, path);
                if (left != null && right != null && !JToken.DeepEquals(left, right)) Warning("structured_locale_conflict");
                return right != null ? (right, enSource) : (left, koSource);
            }
            foreach (var row in new[] { ko, en }.Where(x => x != null))
            {
                if (type == "unit" && row["ability"] != null && !(row["ability"] is JObject)) throw Invalid();
                // Validate both documents, including the unselected locale's known structures.
                if (type == "unit" && row["stats"] != null) NumericMap(row["stats"]);
                if (type == "unit" && (row["ability"] as JObject)?["variables"] != null) ReadSeries(row["ability"]["variables"]);
                if (type == "trait" && row["effects"] != null)
                {
                    if (row["effects"] is JArray) ReadBreakpoints(row["effects"]);
                    else if (!(row["effects"] is JObject)) throw Invalid();
                }
                if (type == "item" && row["unique"] != null && row["unique"].Type != JTokenType.Boolean) throw Invalid();
            }
            if (type == "unit")
            {
                var stats = Pick("/stats");
                if (stats.Value != null)
                {
                    result.BaseStats = new ReadOnlyDictionary<string, double?>(NumericMap(stats.Value));
                    foreach (var key in result.BaseStats.Keys) Origin("/baseStats/" + FieldOrigin.Escape(key), stats.Source);
                }
                var ability = Pick("/ability/variables");
                if (ability.Value != null)
                {
                    result.AbilityVariables = ReadSeries(ability.Value);
                    for (var i = 0; i < result.AbilityVariables.Count; i++)
                    {
                        Origin("/abilityVariables/" + i + "/name", ability.Source);
                        for (var j = 0; j < result.AbilityVariables[i].Values.Count; j++) Origin("/abilityVariables/" + i + "/values/" + j, ability.Source);
                    }
                }
            }
            if (type == "trait")
            {
                var tiers = Pick("/effects");
                if (tiers.Value is JArray)
                {
                    result.Breakpoints = ReadBreakpoints(tiers.Value);
                    for (var i = 0; i < result.Breakpoints.Count; i++)
                    {
                        var item = result.Breakpoints[i]; var path = "/breakpoints/" + i;
                        foreach (var field in new[] { "minUnits", "maxUnits", "style" }) Origin(path + "/" + field, tiers.Source);
                        foreach (var key in item.Effects.Keys) Origin(path + "/effects/" + FieldOrigin.Escape(key), tiers.Source);
                        if (!item.MinUnits.HasValue || !item.MaxUnits.HasValue || !item.Style.HasValue) Warning("structured_unknown_breakpoint");
                    }
                }
                else if (tiers.Value != null && !(tiers.Value is JObject)) throw Invalid();
            }
            if (type == "item")
            {
                var unique = Pick("/unique");
                if (unique.Value != null) { result.Unique = unique.Value.Value<bool>(); Origin("/unique", unique.Source); }
            }

            var ext = new List<(StaticExtensionField Field, SourceStamp Source)>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (consumed != null) known.UnionWith(consumed);
            if (type == "unit") known.Add("/stats");
            if (type == "item") known.Add("/unique");
            bool Known(string path, JToken node, JObject root)
            {
                var parts = path.Split('/');
                if (known.Contains(path))
                {
                    if (parts.Length == 3 && (parts[1] == "effects" || parts[1] == "stats") &&
                        node.Type != JTokenType.Integer && node.Type != JTokenType.Float)
                    { Warning("structured_unmapped_effect"); return false; }
                    return true;
                }
                if (type == "unit" && parts.Length == 5 && parts[1] == "ability" && parts[2] == "variables" && int.TryParse(parts[3], out _))
                    return parts[4] == "name" || parts[4] == "value";
                if (type == "trait" && root["effects"] is JArray && parts.Length == 4 && parts[1] == "effects" && int.TryParse(parts[2], out _))
                    return new[] { "minUnits", "maxUnits", "style", "variables" }.Contains(parts[3]);
                return false;
            }
            bool Container(string path, JObject root) => path == "" || known.Any(x => x.StartsWith(path + "/", StringComparison.Ordinal)) ||
                type == "unit" && (path == "/ability" || path == "/ability/variables" || path.StartsWith("/ability/variables/", StringComparison.Ordinal) && path.Split('/').Length == 4) ||
                type == "trait" && root["effects"] is JArray && (path == "/effects" || path.StartsWith("/effects/", StringComparison.Ordinal) && path.Split('/').Length == 3);
            void Walk(JToken node, string path, string locale, SourceStamp source, JObject root)
            {
                if (Known(path, node, root)) return;
                if (Container(path, root) && node is JObject obj)
                { foreach (var p in obj.Properties()) Walk(p.Value, path + "/" + FieldOrigin.Escape(p.Name), locale, source, root); return; }
                if (Container(path, root) && node is JArray array)
                { for (var i = 0; i < array.Count; i++) Walk(array[i], path + "/" + i, locale, source, root); return; }
                ext.Add((new StaticExtensionField(source.SourceName, locale, path, node.ToString(Formatting.None)), source));
            }
            if (ko != null) Walk(ko, "", "koKr", koSource, ko);
            if (en != null) Walk(en, "", "enUs", enSource, en);
            var sorted = ext.OrderBy(x => x.Field.SourceName, StringComparer.Ordinal).ThenBy(x => x.Field.Locale, StringComparer.Ordinal).ThenBy(x => x.Field.Pointer, StringComparer.Ordinal).ToArray();
            result.Extensions = Array.AsReadOnly(sorted.Select(x => x.Field).ToArray());
            if (sorted.Sum(x => (long)EncodingSize(x.Field)) > 65536) throw Invalid();
            for (var i = 0; i < sorted.Length; i++) Origin("/extensions/" + i + "/canonicalJson", sorted[i].Source);
            result.Origins = origins.AsReadOnly(); result.Issues = issues.AsReadOnly(); return result;
        }
        private static int EncodingSize(StaticExtensionField field) => System.Text.Encoding.UTF8.GetByteCount(field.SourceName + field.Locale + field.Pointer + field.CanonicalJson);
        private static Dictionary<string, double?> NumericMap(JToken value)
        {
            if (!(value is JObject obj)) throw Invalid();
            return obj.Properties().ToDictionary(x => x.Name, x => Number(x.Value), StringComparer.Ordinal);
        }
        private static double? Number(JToken value)
        {
            if (value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) throw Invalid();
            var number = value.Value<double>(); if (double.IsNaN(number) || double.IsInfinity(number)) throw Invalid(); return number;
        }
        private static IReadOnlyList<StaticNumericSeries> ReadSeries(JToken value)
        {
            if (!(value is JArray array)) throw Invalid();
            var items = array.Select(x => {
                if (!(x is JObject row) || row["name"]?.Type != JTokenType.String || !(row["value"] is JArray values)) throw Invalid();
                return new StaticNumericSeries(row.Value<string>("name"), values.Select(Number));
            }).ToArray();
            if (items.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != items.Length) throw Invalid();
            return Array.AsReadOnly(items);
        }
        private static int? Bound(JToken token)
        {
            if (token?.Type == JTokenType.Null) return null;
            if (token?.Type != JTokenType.Integer || !int.TryParse(token.ToString(), out var value)) throw Invalid(); return value;
        }
        private static IReadOnlyList<TraitBreakpoint> ReadBreakpoints(JToken value)
        {
            if (!(value is JArray array)) throw Invalid();
            var items = array.Select(x => {
                if (!(x is JObject row)) throw Invalid();
                return new TraitBreakpoint(Bound(row["minUnits"]), Bound(row["maxUnits"]), Bound(row["style"]), NumericMap(row["variables"]));
            }).ToArray();
            var complete = items.Where(x => x.MinUnits.HasValue && x.MaxUnits.HasValue).ToArray();
            if (complete.Select(x => (x.MinUnits, x.MaxUnits)).Distinct().Count() != complete.Length) throw Invalid();
            return Array.AsReadOnly(items);
        }
        private static JsonSerializationException Invalid() => new JsonSerializationException("invalid_structured_static");
    }
}
