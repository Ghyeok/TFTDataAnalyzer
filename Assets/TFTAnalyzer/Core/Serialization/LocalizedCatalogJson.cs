using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Serialization
{
    // Physical schema5 text storage. Logical domain fields and provenance paths stay unchanged.
    internal static class LocalizedCatalogJson
    {
        private static readonly (string Type,string Plural)[] Kinds = {
            ("unit","units"),("trait","traits"),("item","items"),("augment","augments") };
        private static readonly string[] TextFields = { "name", "description" };
        private static readonly string[] ManifestFields = { "schemaVersion", "setId", "setNumber", "setName", "patchId",
            "sourceGameVersion", "sourceProfile", "createdUtc", "contentHash", "sources", "policyReviewDate", "policyUrls",
            "validationErrorCount", "validationWarningCount", "previousPatchSummary", "communityDragonVersion" };
        private static readonly string[] EntryFields = { "recordType","stableId","field","koKr","enUs","usedEnglishFallback" };

        internal static string Write(StaticCatalog catalog,string structuredJson)
        {
            if (catalog.Manifest.SchemaVersion != 5) throw Invalid();
            var root = StructuredCatalogJson.ReadRoot(structuredJson);
            var entries = new List<(string Type,string Id,string Field,JObject Value)>();
            var keys = new HashSet<(string,string,string)>();
            foreach (var record in Records(root))
            {
                var id = Id(record.Row["stableId"]);
                foreach (var field in TextFields)
                {
                    if (!keys.Add((record.Type,id,field))) throw Invalid();
                    var text = record.Row[field] as JObject ?? throw Invalid();
                    if (text.Properties().Count() != 2) throw Invalid();
                    var ko = Text(text["koKr"]); var en = Text(text["enUs"]);
                    entries.Add((record.Type,id,field,new JObject {
                        ["recordType"] = record.Type,["stableId"] = id,["field"] = field,
                        ["koKr"] = ko,["enUs"] = en,["usedEnglishFallback"] = new LocalizedText(ko,en).UsedEnglishFallback }));
                    record.Row.Remove(field);
                }
            }
            root["localizations"] = new JArray(entries.OrderBy(x=>x.Type,StringComparer.Ordinal)
                .ThenBy(x=>x.Id,StringComparer.Ordinal).ThenBy(x=>x.Field,StringComparer.Ordinal).Select(x=>x.Value));
            return root.ToString(Formatting.Indented).Replace("\r\n","\n") + "\n";
        }

        internal static JObject Expand(JObject root)
        {
            // Hydrate a copy only after checking all keys and literal values; no inline fallback.
            var manifest = root["manifest"] as JObject ?? throw Invalid();
            if (manifest.Properties().Count() != ManifestFields.Length ||
                manifest.Properties().Any(p => !ManifestFields.Contains(p.Name,StringComparer.Ordinal))) throw Invalid();
            var copy = (JObject)root.DeepClone();
            var table = copy["localizations"] as JArray ?? throw Invalid();
            var expected = new Dictionary<(string Type,string Id,string Field),JObject>();
            foreach (var record in Records(copy))
            {
                if (record.Row.Properties().Any(p=>TextFields.Contains(p.Name,StringComparer.OrdinalIgnoreCase))) throw Invalid();
                var id = Id(record.Row["stableId"]);
                foreach (var field in TextFields)
                {
                    var key = (record.Type,id,field);
                    if (expected.ContainsKey(key)) throw Invalid();
                    expected.Add(key,record.Row);
                }
            }
            if (table.Count != expected.Count) throw Invalid();
            var seen = new HashSet<(string,string,string)>();
            foreach (var token in table)
            {
                var row = token as JObject ?? throw Invalid();
                if (row.Properties().Count()!=EntryFields.Length || row.Properties().Any(p=>!EntryFields.Contains(p.Name,StringComparer.Ordinal))) throw Invalid();
                var key = (Text(row["recordType"]),Id(row["stableId"]),Text(row["field"]));
                if (!seen.Add(key) || !expected.TryGetValue(key,out var record)) throw Invalid();
                var ko = Text(row["koKr"]); var en = Text(row["enUs"]);
                if (row["usedEnglishFallback"]?.Type != JTokenType.Boolean ||
                    row.Value<bool>("usedEnglishFallback") != new LocalizedText(ko,en).UsedEnglishFallback) throw Invalid();
                record[key.Item3] = new JObject { ["koKr"] = ko,["enUs"] = en };
            }
            copy.Remove("localizations");
            return copy;
        }

        private static IEnumerable<(string Type,JObject Row)> Records(JObject root)
        {
            foreach (var kind in Kinds)
            {
                if (!(root[kind.Plural] is JArray records)) throw Invalid();
                foreach (var value in records)
                {
                    if (!(value is JObject row)) throw Invalid();
                    yield return (kind.Type,row);
                }
            }
        }
        private static string Id(JToken token)
        {
            var id = Text(token);
            if (id.Length == 0 || id != id.Trim()) throw Invalid();
            return id;
        }
        private static string Text(JToken token) => token?.Type == JTokenType.String ? token.Value<string>() : throw Invalid();
        private static JsonSerializationException Invalid() => new JsonSerializationException("Invalid localized catalog table.");
    }
}
