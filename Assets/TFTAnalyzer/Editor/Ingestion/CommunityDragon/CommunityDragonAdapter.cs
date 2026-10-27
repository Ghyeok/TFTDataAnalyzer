using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

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

        /// <summary>Preserves the scoped legacy entry point while refusing diagnostic errors.</summary>
        public StaticCatalog Parse(
            string koJson,
            string enJson,
            PatchKey patch)
        {
            var parsed = ParseWithDiagnostics(koJson, enJson, patch);
            if (parsed.Issues.Any(issue => issue.Severity == ValidationSeverity.Error))
                throw new JsonSerializationException("CommunityDragon shape rejected: " +
                    string.Join(", ", parsed.Issues.Where(x => x.Severity == ValidationSeverity.Error).Select(x => x.Code)));
            return parsed.Catalog;
        }

        /// <summary>Reads explicit set membership and bounds global rows by a validated official catalog.</summary>
        public CommunityDragonParseResult ParseWithDiagnostics(
            string koJson, string enJson, PatchKey patch, StaticCatalog official = null)
        {
            if (string.IsNullOrWhiteSpace(koJson) || string.IsNullOrWhiteSpace(enJson))
            {
                throw new ArgumentException("Both CommunityDragon locales are required.");
            }

            if (patch == null)
            {
                throw new ArgumentNullException(nameof(patch));
            }

            var version = CommunityDragonEndpoints.VersionFromDataDragon(patch.SourceGameVersion);
            var sources = new[]
            {
                Source(version, "ko_kr", koJson),
                Source(version, "en_us", enJson)
            };
            PatchManifest Manifest(string setName) => new PatchManifest(
                5,
                new CatalogIdentity(patch, CatalogSourceProfile.OfficialPlusCommunityDragon),
                setName,
                DateTime.UnixEpoch,
                "",
                sources,
                new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
                new[] { "https://communitydragon.org/documentation/assets" },
                0,
                0,
                "",
                version);
            var manifest = Manifest("");

            var issues = new List<ValidationIssue>();
            var warnings = new Dictionary<string, int>(StringComparer.Ordinal);
            var excludedUnitIds = new List<string>();
            try
            {
                var koRoot = StructuredStaticReader.ParseDocument(koJson);
                var enRoot = StructuredStaticReader.ParseDocument(enJson);
                var koSet = SelectSet(koRoot, patch.SetNumber);
                var enSet = SelectSet(enRoot, patch.SetNumber);
                manifest = Manifest(ReadString(enSet, "name"));
                var koTraits = Records(koRoot, koSet, "traits");
                var enTraits = Records(enRoot, enSet, "traits");
                var koItems = Records(koRoot, koSet, "items");
                var enItems = Records(enRoot, enSet, "items");
                var koAugments = Records(koRoot, koSet, "augments");
                var enAugments = Records(enRoot, enSet, "augments");
                void FillGlobal(JObject root, ref JArray items, ref JArray augments)
                {
                    if (items.Count > 0 && augments.Count > 0) return;
                    var global = GlobalRecords(root, official, patch, warnings);
                    if (items.Count == 0) items = global.Items;
                    if (augments.Count == 0) augments = global.Augments;
                }
                FillGlobal(koRoot, ref koItems, ref koAugments);
                FillGlobal(enRoot, ref enItems, ref enAugments);
                var itemIds = new HashSet<string>(Index(koItems).Keys.Concat(Index(enItems).Keys), StringComparer.Ordinal);
                if (Index(koAugments).Keys.Concat(Index(enAugments).Keys).Any(itemIds.Contains))
                    throw new JsonSerializationException("record_kind_conflict");
                CheckKind(koItems, false); CheckKind(enItems, false);
                CheckKind(koAugments, true); CheckKind(enAugments, true);
                var origins = new List<FieldOrigin>();
                var catalog = new StaticCatalog(manifest,
                    ParseUnits(origins, Records(koRoot, koSet, "champions"), Records(enRoot, enSet, "champions"),
                        koTraits, enTraits, patch, sources, version, official, warnings, excludedUnitIds, issues),
                    ParseTraits(origins, koTraits, enTraits, patch, sources, version, issues),
                    ParseItems(origins, koItems, enItems, patch, sources, version, issues),
                    ParseAugments(origins, koAugments, enAugments, patch, sources, version, issues));
                var units = Records(koRoot, koSet, "champions").Concat(Records(enRoot, enSet, "champions")).OfType<JObject>();
                if (catalog.Units.Count + catalog.Traits.Count + catalog.Items.Count + catalog.Augments.Count == 0)
                    throw new JsonSerializationException("no_supported_records");
                AddWarnings(issues, warnings, excludedUnitIds);
                return new CommunityDragonParseResult(new StaticCatalog(manifest, catalog.Units,
                    catalog.Traits, catalog.Items, catalog.Augments, origins), issues);
            }
            catch (JsonException error)
            {
                // Shape errors contain fixed codes only. Never copy a raw JSON error/body into reports.
                var code = error is JsonReaderException ? "invalid_json" : error.Message;
                var message = code == "official_scope_required" ? "전역 보완 항목의 범위를 확인할 공식 카탈로그가 필요합니다." :
                    code == "invalid_official_scope" ? "공식 카탈로그의 패치·출처·검증 결과가 요청과 일치하지 않습니다." :
                    code == "no_supported_records" ? "선택 세트에서 지원 형식의 항목을 읽지 못했습니다. 원본 자료 형식을 확인해 주세요." :
                    code == "unresolved_trait_reference" ? "기물의 시너지 참조를 같은 언어·선택 세트의 유일한 안정 ID로 확인할 수 없습니다." :
                    "보완 자료의 세트·ID·필수 값 또는 종류가 손상되거나 모호합니다. 가져오기를 게시하지 않습니다.";
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "cdragon_" + code, "", message));
                AddWarnings(issues, warnings, excludedUnitIds);
                return new CommunityDragonParseResult(new StaticCatalog(manifest, null, null, null, null), issues);
            }
        }

        private static JObject SelectSet(JObject root, int setNumber)
        {
            var setData = root["setData"] as JArray;
            if (setData == null || setData.Any(x => !(x is JObject) || x["number"]?.Type != JTokenType.Integer ||
                !int.TryParse(x["number"].ToString(), out _)))
                throw new JsonSerializationException("invalid_set_data");
            var selected = setData
                .OfType<JObject>()
                .Where(value => value["number"]?.Value<int>() == setNumber).ToArray();
            if (selected.Length != 1)
            {
                throw new JsonSerializationException("missing_or_duplicate_set");
            }
            return selected[0];
        }

        private static JArray Records(JObject root, JObject set, string name)
        {
            if (set[name] != null && !(set[name] is JArray)) throw new JsonSerializationException("invalid_record_array");
            return (set[name] as JArray) ?? new JArray();
        }

        private static IEnumerable<UnitRecord> ParseUnits(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            JArray koTraits,
            JArray enTraits,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version,
            StaticCatalog official,
            Dictionary<string, int> warnings,
            List<string> excludedUnitIds, List<ValidationIssue> issues)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var cost = UnitCost(record.Primary);
                if (cost < 1 || cost > 9)
                {
                    if (official != null && official.Units.Any(x => x.StableId == record.Id))
                        throw new JsonSerializationException("official_unit_excluded");
                    Warn(warnings, "excluded_unit_cost", 1);
                    if (excludedUnitIds.Count < 5) excludedUnitIds.Add(DiagnosticId(record.Id));
                    continue;
                }
                var structured = StructuredStaticReader.Read("unit", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("unit", record.KoKr, record.EnUs, true));
                var value = new UnitRecord(
                    record.Id,
                    record.Name,
                    cost,
                    TraitReferences(record.Primary, record.EnUs != null ? enTraits : koTraits, koTraits, enTraits),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources,
                    ReadString(record.Primary, "role"),
                    new LocalizedText(UnitDescription(record.KoKr), UnitDescription(record.EnUs)),
                    ReadEffects(record.Primary), structured.BaseStats, structured.AbilityVariables, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "unit", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static string UnitDescription(JObject row)
        {
            var flat = ReadFirst(row, "desc", "description");
            return string.IsNullOrWhiteSpace(flat) ? ReadString(row?["ability"] as JObject, "desc") : flat;
        }

        private static int UnitCost(JObject row)
        {
            foreach (var name in new[] { "cost", "tier" })
            {
                if (row[name] == null) continue;
                if (row[name].Type != JTokenType.Integer || !int.TryParse(row[name].ToString(), out var cost))
                    throw new JsonSerializationException("invalid_unit_cost");
                return cost;
            }
            throw new JsonSerializationException("missing_unit_cost");
        }

        private static IReadOnlyList<string> TraitReferences(JObject row, JArray localeTraits, JArray koTraits, JArray enTraits)
        {
            if (row["traits"] == null) return Array.Empty<string>();
            if (!(row["traits"] is JArray refs)) throw new JsonSerializationException("invalid_trait_references");
            var local = Index(localeTraits);
            var ids = new HashSet<string>(Index(koTraits).Keys.Concat(Index(enTraits).Keys), StringComparer.Ordinal);
            return refs.Select(token =>
            {
                var text = token.Type == JTokenType.String ? token.ToString() :
                    token is JObject obj ? StableId(obj) : throw new JsonSerializationException("invalid_trait_reference");
                if (string.IsNullOrWhiteSpace(text)) throw new JsonSerializationException("invalid_trait_reference");
                if (ids.Contains(text)) return text;
                var matches = local.Where(pair => ReadString(pair.Value, "name") == text).Select(pair => pair.Key).ToArray();
                if (matches.Length != 1) throw new JsonSerializationException("unresolved_trait_reference");
                return matches[0];
            }).ToArray();
        }

        private static void Warn(Dictionary<string, int> warnings, string code, int count)
        {
            if (count <= 0) return;
            warnings.TryGetValue(code, out var old);
            warnings[code] = old + count;
        }

        private static string DiagnosticId(string id)
        {
            var length = Math.Min(id.Length, 64);
            if (length < id.Length && char.IsHighSurrogate(id[length - 1])) length--;
            var sample = id.Substring(0, length);
            return new string(sample.Select(c => char.IsControl(c) ? ' ' : c).ToArray()) + (length < id.Length ? "…" : "");
        }

        private static void AddWarnings(List<ValidationIssue> issues, Dictionary<string, int> warnings, IReadOnlyList<string> excludedUnitIds)
        {
            foreach (var entry in warnings.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var label = entry.Key == "unsupported_unit_structure" ? "새 모델이 필요한 기물 능력치·스킬 변수는 읽지 않았습니다" :
                    entry.Key == "unsupported_trait_tiers" ? "새 모델이 필요한 시너지 활성 구간은 읽지 않았습니다" :
                    entry.Key == "excluded_global_records" ? "공식 ID 범위에 속하지 않는 전역 보완 항목은 제외했습니다" :
                    "비용 1~9 범위 밖의 보완 기물은 제외했습니다";
                var samples = entry.Key == "excluded_unit_cost" && excludedUnitIds.Count > 0
                    ? " ID 예시: " + string.Join(", ", excludedUnitIds) +
                      (entry.Value > excludedUnitIds.Count ? " 외 " + (entry.Value - excludedUnitIds.Count) + "건" : "") : "";
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "cdragon_" + entry.Key, "",
                    label + ": " + entry.Value + "건 (" + (entry.Key == "excluded_unit_cost" ? "병합 기물 기준" : "언어별 원본 레코드 기준") + ")." + samples));
            }
        }

        private static void CheckKind(JArray rows, bool augment)
        {
            foreach (var row in Index(rows).Values)
                if (row["isAugment"] != null && (row["isAugment"].Type != JTokenType.Boolean || row.Value<bool>("isAugment") != augment))
                    throw new JsonSerializationException("record_kind_conflict");
        }

        private static (JArray Items, JArray Augments) GlobalRecords(JObject root, StaticCatalog official,
            PatchKey patch, Dictionary<string, int> warnings)
        {
            var items = new JArray(); var augments = new JArray();
            if (root["items"] == null || root["items"].Type == JTokenType.Null) return (items, augments);
            if (!(root["items"] is JArray rows)) throw new JsonSerializationException("invalid_global_array");
            if (rows.Count == 0) return (items, augments);
            if (official == null) throw new JsonSerializationException("official_scope_required");
            var manifest = official.Manifest;
            var expected = new CatalogIdentity(patch, CatalogSourceProfile.OfficialOnly);
            var actual = manifest.Identity ?? new CatalogIdentity(manifest.Patch, CatalogSourceProfile.OfficialOnly);
            if (!expected.Equals(actual) || manifest.Patch.SetNumber != patch.SetNumber || manifest.Sources.Any(x => x.IsSupplemental) ||
                !string.IsNullOrEmpty(manifest.CommunityDragonVersion) || !new CatalogValidator().Validate(official).CanPublish)
                throw new JsonSerializationException("invalid_official_scope");
            var itemIds = new HashSet<string>(official.Items.Select(x => x.StableId), StringComparer.Ordinal);
            var augmentIds = new HashSet<string>(official.Augments.Select(x => x.StableId), StringComparer.Ordinal);
            foreach (var token in rows)
            {
                var row = token as JObject;
                var idToken = row?["apiName"] ?? row?["id"];
                var id = idToken?.Type == JTokenType.String ? idToken.ToString() : "";
                var isItem = itemIds.Contains(id); var isAugment = augmentIds.Contains(id);
                if (!isItem && !isAugment) { Warn(warnings, "excluded_global_records", 1); continue; }
                if (isItem && isAugment || row["isAugment"]?.Type != JTokenType.Boolean || row.Value<bool>("isAugment") != isAugment)
                    throw new JsonSerializationException("record_kind_conflict");
                (isAugment ? augments : items).Add(row.DeepClone());
            }
            return (items, augments);
        }

        private static IEnumerable<TraitRecord> ParseTraits(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version, List<ValidationIssue> issues)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var structured = StructuredStaticReader.Read("trait", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("trait", record.KoKr, record.EnUs, true));
                var value = new TraitRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record, "desc", "description"),
                    ReadEffects(record.Primary),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources, structured.Breakpoints, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "trait", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static IEnumerable<ItemRecord> ParseItems(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version, List<ValidationIssue> issues)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var structured = StructuredStaticReader.Read("item", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("item", record.KoKr, record.EnUs, true));
                var value = new ItemRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record, "desc", "description"),
                    ReadStringArray(record.Primary, true, "components", "composition"),
                    ReadEffects(record.Primary),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources, structured.Unique, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "item", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static IEnumerable<AugmentRecord> ParseAugments(
            List<FieldOrigin> origins,
            JArray ko,
            JArray en,
            PatchKey patch,
            IReadOnlyList<SourceStamp> sources,
            string version, List<ValidationIssue> issues)
        {
            foreach (var record in Join(ko, en, patch))
            {
                var structured = StructuredStaticReader.Read("augment", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("augment", record.KoKr, record.EnUs, true));
                var value = new AugmentRecord(
                    record.Id,
                    record.Name,
                    ReadString(record.Primary, "tier"),
                    ReadLocalized(record, "desc", "description"),
                    ReadStringArray(record.Primary, false, "related", "relatedIds"),
                    ReadEffects(record.Primary),
                    Icon(version, ReadString(record.Primary, "icon")),
                    sources, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "augment", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static IEnumerable<JoinedRecord> Join(JArray ko, JArray en, PatchKey patch)
        {
            var koRecords = Index(ko);
            var enRecords = Index(en);
            return koRecords.Keys.Concat(enRecords.Keys)
                .Distinct(StringComparer.Ordinal)
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
            foreach (var token in values)
            {
                if (!(token is JObject value)) throw new JsonSerializationException("invalid_record");
                var id = StableId(value);
                if (result.ContainsKey(id)) throw new JsonSerializationException("duplicate_record_id");
                result.Add(id, value);
            }

            return result;
        }

        private static string StableId(JObject row)
        {
            var token = row["apiName"] ?? row["id"];
            if (token?.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.ToString()))
                throw new JsonSerializationException("invalid_record_id");
            return token.ToString();
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

        private static IReadOnlyList<string> ReadStringArray(JObject value, bool stringsOnly, params string[] names)
        {
            foreach (var name in names)
            {
                if (value?[name] == null) continue;
                if (!(value[name] is JArray array)) throw new JsonSerializationException("invalid_reference_array");
                return array.Select(token =>
                {
                    var id = token.Type == JTokenType.String ? token.ToString() :
                        !stringsOnly && token is JObject obj ? StableId(obj) : throw new JsonSerializationException("invalid_reference_token");
                    if (string.IsNullOrWhiteSpace(id)) throw new JsonSerializationException("invalid_reference_token");
                    return id;
                }).ToArray();
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
