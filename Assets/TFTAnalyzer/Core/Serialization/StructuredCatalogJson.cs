using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Serialization
{
    // Schema4 is layered on the frozen legacy DTO representation. Old schemas never pass
    // through JObject output, so their formatting, numeric tokens and hashes stay unchanged.
    internal static class StructuredCatalogJson
    {
        private static readonly string[] NewKeys = { "baseStats", "abilityVariables", "breakpoints", "unique", "extensions" };
        internal static JObject ReadRoot(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw Invalid();
                return root;
            }
        }

        internal static void Guard(JObject root, int schema)
        {
            if (schema == 4) Metadata(root);
            foreach (var kind in new[] { "units", "traits", "items", "augments" })
            {
                if (!(root[kind] is JArray rows)) { if (schema == 4) throw Invalid(); else continue; }
                foreach (var token in rows)
                {
                    if (!(token is JObject row)) throw Invalid();
                    if (schema < 4)
                    {
                        if (row.Properties().Any(p => NewKeys.Contains(p.Name, StringComparer.OrdinalIgnoreCase))) throw Invalid();
                        continue;
                    }
                    var known = new List<string> { "stableId", "name", "description", "effects", "iconPath", "sources", "extensions" };
                    if (kind == "units") known.AddRange(new[] { "cost", "role", "traitIds", "baseStats", "abilityVariables" });
                    if (kind == "traits") known.Add("breakpoints");
                    if (kind == "items") known.AddRange(new[] { "componentIds", "unique" });
                    if (kind == "augments") known.AddRange(new[] { "tier", "relatedIds" });
                    if (row.Properties().Any(p => !known.Contains(p.Name, StringComparer.Ordinal))) throw Invalid();
                    foreach (var name in new[] { "stableId", "iconPath" }) String(row[name]);
                    foreach (var name in new[] { "name", "description" })
                    {
                        var text = Object(row[name]); String(text["koKr"]); String(text["enUs"]);
                    }
                    Map(row["effects"], false);
                    Sources(row["sources"]);
                    if (kind == "units") { Integer(row["cost"], false); String(row["role"]); Strings(row["traitIds"]); }
                    if (kind == "items") Strings(row["componentIds"]);
                    if (kind == "augments") { String(row["tier"]); Strings(row["relatedIds"]); }
                }
            }
        }

        private static void Metadata(JObject root)
        {
            var top = new[] { "manifest", "units", "traits", "items", "augments", "fieldOrigins" };
            if (root.Properties().Any(p => !top.Contains(p.Name, StringComparer.Ordinal))) throw Invalid();
            var manifest = Object(root["manifest"]);
            foreach (var key in new[] { "schemaVersion", "setNumber", "validationErrorCount", "validationWarningCount" }) Integer(manifest[key], false);
            foreach (var key in new[] { "setId", "setName", "patchId", "sourceGameVersion", "sourceProfile", "createdUtc", "contentHash", "policyReviewDate", "previousPatchSummary", "communityDragonVersion" }) String(manifest[key]);
            Strings(manifest["policyUrls"]); Sources(manifest["sources"]);
            if (!(root["fieldOrigins"] is JArray origins)) throw Invalid();
            foreach (var token in origins)
            {
                var origin = Object(token);
                if (origin.Properties().Count() != 4) throw Invalid();
                foreach (var key in new[] { "recordType", "stableId", "pointer" }) String(origin[key]);
                Sources(origin["sources"]);
            }
        }

        private static void Sources(JToken value)
        {
            if (!(value is JArray sources)) throw Invalid();
            foreach (var token in sources)
            {
                var source = Object(token);
                if (source.Properties().Count() != 6) throw Invalid();
                foreach (var key in new[] { "sourceName", "url", "retrievedUtc", "etag", "sha256" }) String(source[key]);
                if (source["isSupplemental"]?.Type != JTokenType.Boolean) throw Invalid();
            }
        }

        internal static bool HasValues(StaticCatalog c) =>
            c.Units.Any(x => x.BaseStats.Count > 0 || x.AbilityVariables.Count > 0 || x.Extensions.Count > 0) ||
            c.Traits.Any(x => x.Breakpoints.Count > 0 || x.Extensions.Count > 0) ||
            c.Items.Any(x => x.Unique.HasValue || x.Extensions.Count > 0) || c.Augments.Any(x => x.Extensions.Count > 0);

        internal static StaticCatalog Attach(StaticCatalog c, JObject root)
        {
            return new StaticCatalog(c.Manifest,
                c.Units.Select((x, i) => {
                    var r = Object(root["units"][i]);
                    return new UnitRecord(x.StableId, x.Name, x.Cost, x.TraitIds, x.IconPath, x.Sources, x.Role, x.Description, x.Effects,
                        Map(r["baseStats"], true), Series(r["abilityVariables"]), Extensions(r["extensions"]));
                }),
                c.Traits.Select((x, i) => {
                    var r = Object(root["traits"][i]);
                    return new TraitRecord(x.StableId, x.Name, x.Description, x.Effects, x.IconPath, x.Sources,
                        Breakpoints(r["breakpoints"]), Extensions(r["extensions"]));
                }),
                c.Items.Select((x, i) => {
                    var r = Object(root["items"][i]); var unique = r["unique"];
                    if (unique == null || unique.Type != JTokenType.Boolean && unique.Type != JTokenType.Null) throw Invalid();
                    return new ItemRecord(x.StableId, x.Name, x.Description, x.ComponentIds, x.Effects, x.IconPath, x.Sources,
                        unique.Type == JTokenType.Null ? (bool?)null : unique.Value<bool>(), Extensions(r["extensions"]));
                }),
                c.Augments.Select((x, i) => new AugmentRecord(x.StableId, x.Name, x.Tier, x.Description, x.RelatedIds,
                    x.Effects, x.IconPath, x.Sources, Extensions(root["augments"][i]["extensions"]))), c.FieldOrigins);
        }

        internal static string Write(StaticCatalog c, string legacyJson)
        {
            var root = ReadRoot(legacyJson);
            var mappings = new Dictionary<(string Type, string Id, int Index), int>();
            void Add(string kind, string type, string id, IReadOnlyList<StaticExtensionField> extensions, Action<JObject> fill)
            {
                var row = root[kind].OfType<JObject>().Single(x => x.Value<string>("stableId") == id); fill(row);
                var sorted = extensions.Select((x, i) => (Value: x, Index: i)).OrderBy(x => x.Value.SourceName, StringComparer.Ordinal)
                    .ThenBy(x => x.Value.Locale, StringComparer.Ordinal).ThenBy(x => x.Value.Pointer, StringComparer.Ordinal).ToArray();
                row["extensions"] = new JArray(sorted.Select((x, i) => {
                    mappings.Add((type, id, x.Index), i);
                    return new JObject { ["sourceName"] = x.Value.SourceName, ["locale"] = x.Value.Locale,
                        ["pointer"] = x.Value.Pointer, ["canonicalJson"] = x.Value.CanonicalJson };
                }));
            }
            foreach (var x in c.Units) Add("units", "unit", x.StableId, x.Extensions, r => {
                r["baseStats"] = NumericMap(x.BaseStats);
                r["abilityVariables"] = new JArray(x.AbilityVariables.Select(s => new JObject { ["name"] = s.Name, ["values"] = NumericArray(s.Values) }));
            });
            foreach (var x in c.Traits) Add("traits", "trait", x.StableId, x.Extensions, r => {
                r["breakpoints"] = new JArray(x.Breakpoints.Select(b => new JObject { ["minUnits"] = IntToken(b.MinUnits),
                    ["maxUnits"] = IntToken(b.MaxUnits), ["style"] = IntToken(b.Style), ["effects"] = NumericMap(b.Effects) }));
            });
            foreach (var x in c.Items) Add("items", "item", x.StableId, x.Extensions, r => r["unique"] = x.Unique.HasValue ? new JValue(x.Unique.Value) : JValue.CreateNull());
            foreach (var x in c.Augments) Add("augments", "augment", x.StableId, x.Extensions, r => { });
            foreach (var origin in root["fieldOrigins"].OfType<JObject>())
            {
                var parts = origin.Value<string>("pointer").Split('/');
                if (parts.Length == 4 && parts[1] == "extensions" && int.TryParse(parts[2], out var old) &&
                    mappings.TryGetValue((origin.Value<string>("recordType"), origin.Value<string>("stableId"), old), out var next))
                    origin["pointer"] = "/extensions/" + next + "/" + parts[3];
            }
            root["fieldOrigins"] = new JArray(root["fieldOrigins"].OfType<JObject>().OrderBy(x => x.Value<string>("recordType"), StringComparer.Ordinal)
                .ThenBy(x => x.Value<string>("stableId"), StringComparer.Ordinal).ThenBy(x => x.Value<string>("pointer"), StringComparer.Ordinal));
            return root.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }

        private static JObject NumericMap(IReadOnlyDictionary<string, double?> values) => new JObject(values.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new JProperty(x.Key, NumberToken(x.Value))));
        private static JArray NumericArray(IEnumerable<double?> values) => new JArray(values.Select(NumberToken));
        private static JValue NumberToken(double? value) => value.HasValue ? new JValue(value.Value) : JValue.CreateNull();
        private static JValue IntToken(int? value) => value.HasValue ? new JValue(value.Value) : JValue.CreateNull();
        private static JObject Object(JToken value) => value as JObject ?? throw Invalid();
        private static string String(JToken value) => value?.Type == JTokenType.String ? value.Value<string>() : throw Invalid();
        private static void Strings(JToken value)
        { if (!(value is JArray a)) throw Invalid(); foreach (var x in a) String(x); }
        private static int? Integer(JToken value, bool nullable)
        {
            if (nullable && value?.Type == JTokenType.Null) return null;
            if (value?.Type != JTokenType.Integer || !int.TryParse(value.ToString(), out var result)) throw Invalid();
            return result;
        }
        private static Dictionary<string, double?> Map(JToken value, bool nullable)
        {
            var result = new Dictionary<string, double?>(StringComparer.Ordinal);
            foreach (var p in Object(value).Properties())
            {
                if (nullable && p.Value.Type == JTokenType.Null) { result.Add(p.Name, null); continue; }
                if (p.Value.Type != JTokenType.Integer && p.Value.Type != JTokenType.Float) throw Invalid();
                var number = p.Value.Value<double>();
                if (double.IsNaN(number) || double.IsInfinity(number)) throw Invalid();
                result.Add(p.Name, number);
            }
            return result;
        }
        private static IReadOnlyList<StaticNumericSeries> Series(JToken value)
        {
            if (!(value is JArray array)) throw Invalid();
            var list = new List<StaticNumericSeries>();
            foreach (var token in array)
            {
                var r = Object(token); if (r.Properties().Count() != 2 || !(r["values"] is JArray values)) throw Invalid();
                var numbers = values.Select(x => {
                    if (x.Type == JTokenType.Null) return (double?)null;
                    if (x.Type != JTokenType.Integer && x.Type != JTokenType.Float) throw Invalid();
                    return (double?)x.Value<double>();
                }).ToArray();
                list.Add(new StaticNumericSeries(String(r["name"]), numbers));
            }
            if (list.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != list.Count) throw Invalid();
            return list.AsReadOnly();
        }
        private static IReadOnlyList<TraitBreakpoint> Breakpoints(JToken value)
        {
            if (!(value is JArray array)) throw Invalid();
            return Array.AsReadOnly(array.Select(x => {
                var r = Object(x); if (r.Properties().Count() != 4) throw Invalid();
                return new TraitBreakpoint(Integer(r["minUnits"], true), Integer(r["maxUnits"], true), Integer(r["style"], true), Map(r["effects"], true));
            }).ToArray());
        }
        private static IReadOnlyList<StaticExtensionField> Extensions(JToken value)
        {
            if (!(value is JArray array)) throw Invalid();
            return Array.AsReadOnly(array.Select(x => {
                var r = Object(x); if (r.Properties().Count() != 4) throw Invalid();
                return new StaticExtensionField(String(r["sourceName"]), String(r["locale"]), String(r["pointer"]), String(r["canonicalJson"]));
            }).ToArray());
        }
        private static JsonSerializationException Invalid() => new JsonSerializationException("Invalid structured static data contract.");
    }
}
