using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.Ingestion.CommunityDragon
{
    public sealed class CommunityDragonAdapter
    {
        public async Task<CommunityDragonSnapshot> FetchAsync(
            CommunityDragonRequest request,
            IHttpTransport transport,
            CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            if (!request.IncludeCommunityDragon)
            {
                throw new InvalidOperationException(
                    "CommunityDragon access requires explicit opt-in for this import.");
            }

            var version = CommunityDragonEndpoints.VersionFromDataDragon(
                request.Patch.SourceGameVersion);
            var koTask = transport.GetAsync(
                CommunityDragonEndpoints.Document(version, "ko_kr", true),
                token);
            var enTask = transport.GetAsync(
                CommunityDragonEndpoints.Document(version, "en_us", true),
                token);
            await Task.WhenAll(koTask, enTask);
            return new CommunityDragonSnapshot(koTask.Result, enTask.Result);
        }

        public StaticCatalog Parse(
            string koJson,
            string enJson,
            PatchKey patch)
        {
            if (string.IsNullOrWhiteSpace(koJson) || string.IsNullOrWhiteSpace(enJson))
            {
                throw new ArgumentException("Both CommunityDragon locales are required.");
            }

            if (patch == null)
            {
                throw new ArgumentNullException(nameof(patch));
            }

            var koRoot = JObject.Parse(koJson);
            var enRoot = JObject.Parse(enJson);
            var koSet = SelectSet(koRoot, patch.SetNumber);
            var enSet = SelectSet(enRoot, patch.SetNumber);
            var version = CommunityDragonEndpoints.VersionFromDataDragon(patch.SourceGameVersion);
            var sources = new[]
            {
                Source(version, "ko_kr", koJson),
                Source(version, "en_us", enJson)
            };
            var manifest = new PatchManifest(
                3,
                new CatalogIdentity(patch, CatalogSourceProfile.OfficialPlusCommunityDragon),
                ReadString(enSet, "name"),
                DateTime.UnixEpoch,
                "",
                sources,
                new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
                new[] { "https://communitydragon.org/documentation/assets" },
                0,
                0,
                "",
                version);

            var origins = new List<FieldOrigin>();
            var catalog = new StaticCatalog(
                manifest,
                ParseUnits(origins, Records(koRoot, koSet, "champions"), Records(enRoot, enSet, "champions"), patch, sources, version),
                ParseTraits(origins, Records(koRoot, koSet, "traits"), Records(enRoot, enSet, "traits"), patch, sources, version),
                ParseItems(origins, Records(koRoot, koSet, "items"), Records(enRoot, enSet, "items"), patch, sources, version),
                ParseAugments(origins, Records(koRoot, koSet, "augments"), Records(enRoot, enSet, "augments"), patch, sources, version));
            return new StaticCatalog(catalog.Manifest, catalog.Units, catalog.Traits, catalog.Items, catalog.Augments, origins);
        }

        private static JObject SelectSet(JObject root, int setNumber)
        {
            var setData = root["setData"] as JArray;
            var selected = setData?
                .OfType<JObject>()
                .FirstOrDefault(value => value["number"]?.Value<int>() == setNumber);
            if (selected == null)
            {
                throw new JsonSerializationException($"CommunityDragon set {setNumber} was not found.");
            }

            return selected;
        }

        private static JArray Records(JObject root, JObject set, string name)
        {
            return (set[name] as JArray) ?? (root[name] as JArray) ?? new JArray();
        }

        private static IEnumerable<UnitRecord> ParseUnits(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var value = new UnitRecord(
                    record.Id,
                    record.Name,
                    ReadInt(record.Primary, "cost", "tier"),
                    ReadStringArray(record.Primary, "traits"),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources,
                    ReadString(record.Primary, "role"),
                    ReadLocalized(record, "desc", "description"),
                    ReadEffects(record.Primary));
                ParsedFieldOrigins.Add(origins, "unit", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                yield return value;
            }
        }

        private static IEnumerable<TraitRecord> ParseTraits(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var value = new TraitRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record, "desc", "description"),
                    ReadEffects(record.Primary),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources);
                ParsedFieldOrigins.Add(origins, "trait", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                yield return value;
            }
        }

        private static IEnumerable<ItemRecord> ParseItems(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var value = new ItemRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record, "desc", "description"),
                    ReadStringArray(record.Primary, "components", "composition"),
                    ReadEffects(record.Primary),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources);
                ParsedFieldOrigins.Add(origins, "item", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                yield return value;
            }
        }

        private static IEnumerable<AugmentRecord> ParseAugments(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var value = new AugmentRecord(
                    record.Id,
                    record.Name,
                    ReadString(record.Primary, "tier"),
                    ReadLocalized(record, "desc", "description"),
                    ReadStringArray(record.Primary, "related", "relatedIds"),
                    ReadEffects(record.Primary),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources);
                ParsedFieldOrigins.Add(origins, "augment", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                yield return value;
            }
        }

        private static IEnumerable<JoinedRecord> Join(JArray ko, JArray en, PatchKey patch)
        {
            var koRecords = Index(ko);
            var enRecords = Index(en);
            return koRecords.Keys.Concat(enRecords.Keys)
                .Distinct(StringComparer.Ordinal)
                .Where(id => BelongsToSet(id, patch))
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id =>
                {
                    koRecords.TryGetValue(id, out var koRecord);
                    enRecords.TryGetValue(id, out var enRecord);
                    return new JoinedRecord(
                        id,
                        koRecord,
                        enRecord,
                        new LocalizedText(
                            ReadString(koRecord, "name"),
                            ReadString(enRecord, "name")));
                });
        }

        private static Dictionary<string, JObject> Index(JArray values)
        {
            var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var value in values.OfType<JObject>())
            {
                var id = ReadString(value, "apiName");
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = ReadString(value, "id");
                }

                if (!string.IsNullOrWhiteSpace(id))
                {
                    result[id] = value;
                }
            }

            return result;
        }

        private static bool BelongsToSet(string id, PatchKey patch)
        {
            return id.StartsWith(patch.SetId, StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith($"TFT{patch.SetNumber}_", StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith($"TEST{patch.SetNumber}_", StringComparison.OrdinalIgnoreCase);
        }

        private static LocalizedText ReadLocalized(JoinedRecord record, params string[] names)
        {
            return new LocalizedText(
                ReadFirst(record.KoKr, names),
                ReadFirst(record.EnUs, names));
        }

        private static string ReadFirst(JObject value, params string[] names)
        {
            return names.Select(name => ReadString(value, name))
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? "";
        }

        private static string ReadString(JObject value, string name)
        {
            return value?[name]?.Type == JTokenType.Null ? "" : value?[name]?.ToString() ?? "";
        }

        private static int ReadInt(JObject value, params string[] names)
        {
            foreach (var name in names)
            {
                if (value?[name]?.Type == JTokenType.Integer)
                {
                    return value[name].Value<int>();
                }
            }

            return 0;
        }

        private static IReadOnlyList<string> ReadStringArray(JObject value, params string[] names)
        {
            foreach (var name in names)
            {
                var array = value?[name] as JArray;
                if (array == null)
                {
                    continue;
                }

                return array.Select(token => token.Type == JTokenType.String
                        ? token.ToString()
                        : token["apiName"]?.ToString() ?? token["id"]?.ToString() ?? "")
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyDictionary<string, double> ReadEffects(JObject value)
        {
            var result = new SortedDictionary<string, double>(StringComparer.Ordinal);
            var effects = value?["effects"] as JObject;
            if (effects != null)
            {
                foreach (var property in effects.Properties())
                {
                    if (property.Value.Type == JTokenType.Integer || property.Value.Type == JTokenType.Float)
                    {
                        result[property.Name] = property.Value.Value<double>();
                    }
                }
            }

            return result;
        }

        private static string Icon(string version, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return "";
            }

            var normalized = sourcePath.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
            if (normalized.EndsWith(".tex", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 4) + ".png";
            }

            return $"https://raw.communitydragon.org/{version}/plugins/rcp-be-lol-game-data/global/default/{normalized}";
        }

        private static SourceStamp Source(string version, string locale, string json)
        {
            return new SourceStamp(
                "CommunityDragon",
                CommunityDragonEndpoints.Document(version, locale, true),
                DateTime.UnixEpoch,
                "",
                ContentHasher.Sha256Hex(json),
                true);
        }

        private sealed class JoinedRecord
        {
            public string Id { get; }
            public JObject KoKr { get; }
            public JObject EnUs { get; }
            public JObject Primary => EnUs ?? KoKr;
            public LocalizedText Name { get; }

            public JoinedRecord(string id, JObject koKr, JObject enUs, LocalizedText name)
            {
                Id = id;
                KoKr = koKr;
                EnUs = enUs;
                Name = name;
            }
        }
    }
}
