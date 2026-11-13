using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Ingestion.DataDragon
{
    public sealed class DataDragonAdapter
    {
        private static readonly DateTime PolicyReviewDate =
            new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string[] PolicyUrls =
        {
            "https://developer.riotgames.com/docs/tft",
            "https://support-developer.riotgames.com/hc/en-us/articles/22698591841939-General-Policies"
        };

        private readonly IHttpTransport _transport;

        public DataDragonAdapter(IHttpTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token)
        {
            var response = await _transport.GetAsync(DataDragonEndpoints.Versions, token);
            EnsureSuccess(response);
            var versions = JArray.Parse(response.Utf8Body)
                .Values<string>()
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            return Array.AsReadOnly(versions);
        }

        public async Task<DataDragonSnapshot> FetchAsync(
            DataDragonRequest request,
            CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var version = request.Patch.SourceGameVersion;
            var champions = FetchPairAsync(version, DataDragonDocumentKind.Champions, token);
            var traits = FetchPairAsync(version, DataDragonDocumentKind.Traits, token);
            var items = FetchPairAsync(version, DataDragonDocumentKind.Items, token);
            var augments = FetchPairAsync(version, DataDragonDocumentKind.Augments, token);
            await Task.WhenAll(champions, traits, items, augments);
            return new DataDragonSnapshot(
                champions.Result,
                traits.Result,
                items.Result,
                augments.Result);
        }

        public StaticCatalog Parse(DataDragonSnapshot snapshot, PatchKey patch)
        {
            var result = ParseWithDiagnostics(snapshot, patch);
            if (result.Issues.Any(x => x.Severity == ValidationSeverity.Error))
                throw new JsonSerializationException("Data Dragon static document rejected.");
            return result.Catalog;
        }

        public DataDragonParseResult ParseWithDiagnostics(DataDragonSnapshot snapshot, PatchKey patch)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (patch == null) throw new ArgumentNullException(nameof(patch));
            var issues = new List<ValidationIssue>();
            try { return new DataDragonParseResult(ParseCore(snapshot, patch, issues), issues); }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is OverflowException)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "ddragon_invalid_static_data", "", "공식 정적 자료의 형식 또는 값이 잘못되어 가져오기를 중단했습니다."));
                var manifest = new PatchManifest(5, new CatalogIdentity(patch, CatalogSourceProfile.OfficialOnly), "", DateTime.UnixEpoch, "",
                    snapshot.AllResponses.Select(ToSourceStamp), PolicyReviewDate, PolicyUrls, 0, 0, "", "");
                return new DataDragonParseResult(new StaticCatalog(manifest, null, null, null, null), issues);
            }
        }

        private StaticCatalog ParseCore(DataDragonSnapshot snapshot, PatchKey patch, List<ValidationIssue> issues)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (patch == null)
            {
                throw new ArgumentNullException(nameof(patch));
            }

            var stamps = snapshot.AllResponses.Select(ToSourceStamp).ToArray();
            var createdUtc = snapshot.AllResponses.Max(response => response.RetrievedUtc);
            var manifest = new PatchManifest(
                5,
                new CatalogIdentity(patch, CatalogSourceProfile.OfficialOnly),
                "",
                createdUtc,
                "",
                stamps,
                PolicyReviewDate,
                PolicyUrls,
                0,
                0,
                "",
                "");
            // Keep every source row until both locales have been checked. An ID-keyed
            // dictionary here would erase the evidence needed for scope and prefix checks.
            var pairs = new[]
            {
                new SourcePair("unit", snapshot.Champions, false),
                new SourcePair("trait", snapshot.Traits, false),
                new SourcePair("item", snapshot.Items, true),
                new SourcePair("augment", snapshot.Augments, true, issues)
            };
            var setPrefixes = DiscoverSetPrefixes(pairs[0], patch);
            ValidateSourceIds(pairs, patch, setPrefixes, issues);
            if (issues.Any(x => x.Severity == ValidationSeverity.Error))
                return new StaticCatalog(manifest, null, null, null, null);
            // Out-of-scope collision groups must not participate in later discovery or joins.
            setPrefixes = DiscoverSetPrefixes(pairs[0], patch);

            var origins = new List<FieldOrigin>();
            var catalog = new StaticCatalog(
                manifest,
                ParseUnits(origins, pairs[0], patch, setPrefixes, issues),
                ParseTraits(origins, pairs[1], patch, setPrefixes, issues),
                ParseItems(origins, pairs[2], patch, setPrefixes, issues),
                ParseAugments(origins, pairs[3], patch, setPrefixes, issues));
            return new StaticCatalog(catalog.Manifest, catalog.Units, catalog.Traits, catalog.Items, catalog.Augments, origins);
        }

        private async Task<DataDragonPayloadPair> FetchPairAsync(
            string version,
            DataDragonDocumentKind kind,
            CancellationToken token)
        {
            var koTask = _transport.GetAsync(
                DataDragonEndpoints.Document(version, "ko_KR", kind),
                token);
            var enTask = _transport.GetAsync(
                DataDragonEndpoints.Document(version, "en_US", kind),
                token);
            await Task.WhenAll(koTask, enTask);
            EnsureSuccess(koTask.Result);
            EnsureSuccess(enTask.Result);
            return new DataDragonPayloadPair(koTask.Result, enTask.Result);
        }

        private static IEnumerable<UnitRecord> ParseUnits(
            List<FieldOrigin> origins,
            SourcePair pair,
            PatchKey patch,
            IReadOnlyCollection<string> setPrefixes, List<ValidationIssue> issues)
        {
            var sources = PairSources(pair.Payload);
            foreach (var record in Join(pair, patch, setPrefixes, false))
            {
                var structured = StructuredStaticReader.Read("unit", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("unit", record.KoKr, record.EnUs, false));
                var value = new UnitRecord(
                    record.Id,
                    record.Name,
                    ReadInt(record.Primary, "cost", "tier"),
                    null,
                    ReadIcon(patch, DataDragonDocumentKind.Champions, record.Primary),
                    sources,
                    ReadString(record.Primary, "role"),
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    ReadNumericBag(record.Primary, "stats", "effects"), structured.BaseStats, structured.AbilityVariables, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "unit", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static IEnumerable<TraitRecord> ParseTraits(
            List<FieldOrigin> origins,
            SourcePair pair,
            PatchKey patch,
            IReadOnlyCollection<string> setPrefixes, List<ValidationIssue> issues)
        {
            var sources = PairSources(pair.Payload);
            foreach (var record in Join(pair, patch, setPrefixes, false))
            {
                var structured = StructuredStaticReader.Read("trait", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("trait", record.KoKr, record.EnUs, false));
                var value = new TraitRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    ReadNumericBag(record.Primary, "effects"),
                    ReadIcon(patch, DataDragonDocumentKind.Traits, record.Primary),
                    sources, structured.Breakpoints, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "trait", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static IEnumerable<ItemRecord> ParseItems(
            List<FieldOrigin> origins,
            SourcePair pair,
            PatchKey patch,
            IReadOnlyCollection<string> setPrefixes, List<ValidationIssue> issues)
        {
            var sources = PairSources(pair.Payload);
            foreach (var record in Join(pair, patch, setPrefixes, true))
            {
                var structured = StructuredStaticReader.Read("item", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("item", record.KoKr, record.EnUs, false));
                var value = new ItemRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    ReadStringArray(record.Primary, "components"),
                    ReadNumericBag(record.Primary, "stats", "effects"),
                    ReadIcon(patch, DataDragonDocumentKind.Items, record.Primary),
                    sources, structured.Unique, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "item", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static IEnumerable<AugmentRecord> ParseAugments(
            List<FieldOrigin> origins,
            SourcePair pair,
            PatchKey patch,
            IReadOnlyCollection<string> setPrefixes, List<ValidationIssue> issues)
        {
            var sources = PairSources(pair.Payload);
            foreach (var record in Join(pair, patch, setPrefixes, true))
            {
                var structured = StructuredStaticReader.Read("augment", record.Id, record.KoKr, record.EnUs, sources[0], sources[1], StructuredStaticReader.ConsumedFields("augment", record.KoKr, record.EnUs, false));
                var value = new AugmentRecord(
                    record.Id,
                    record.Name,
                    ReadString(record.Primary, "tier"),
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    null,
                    ReadNumericBag(record.Primary, "effects"),
                    ReadIcon(patch, DataDragonDocumentKind.Augments, record.Primary),
                    sources, structured.Extensions);
                ParsedFieldOrigins.Add(origins, "augment", value, record.KoKr, record.EnUs, sources[0], sources[1]);
                origins.AddRange(structured.Origins); issues.AddRange(structured.Issues);
                yield return value;
            }
        }

        private static IEnumerable<JoinedRecord> Join(
            SourcePair pair,
            PatchKey patch,
            IReadOnlyCollection<string> setPrefixes,
            bool includeGenericRecords)
        {
            var ko = pair.KoKr.Where(x => !pair.ExcludedIds.Contains(x.Id)).ToDictionary(x => x.Id, StringComparer.Ordinal);
            var en = pair.EnUs.Where(x => !pair.ExcludedIds.Contains(x.Id)).ToDictionary(x => x.Id, StringComparer.Ordinal);
            return ko.Keys
                .Concat(en.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id =>
                {
                    ko.TryGetValue(id, out var koRecord);
                    en.TryGetValue(id, out var enRecord);
                    return new JoinedRecord(
                        id,
                        koRecord?.Value,
                        enRecord?.Value,
                        new LocalizedText(
                            ReadString(koRecord?.Value, "name"),
                            ReadString(enRecord?.Value, "name")),
                        SourceBelongsToSet(koRecord, patch) ||
                        SourceBelongsToSet(enRecord, patch));
                })
                .Where(record =>
                    record.SourceBelongsToSet ||
                    BelongsToSet(record.Id, patch) ||
                    BelongsToPrefix(record.Id, setPrefixes) ||
                    includeGenericRecords && !HasExplicitSetPrefix(record.Id))
                .Where(record => !string.IsNullOrWhiteSpace(record.Name.DisplayValue));
        }

        private static IReadOnlyList<SourceRecord> ReadRecords(HttpResponseData response, List<ValidationIssue> augmentIssues = null)
        {
            var root = augmentIssues == null ? StructuredStaticReader.ParseDocument(response.Utf8Body) : ParseAugmentDocument(response.Utf8Body, augmentIssues);
            var data = root["data"] as JObject;
            if (data == null)
            {
                throw new JsonSerializationException($"Data Dragon response has no data object: {response.Url}");
            }

            var result = new List<SourceRecord>();
            foreach (var property in data.Properties())
            {
                var record = property.Value as JObject;
                if (record == null)
                {
                    continue;
                }

                var id = ReadString(record, "id");
                id = string.IsNullOrWhiteSpace(id) ? property.Name : id;
                result.Add(new SourceRecord(id, property.Name, record));
            }

            return result;
        }

        private static void ValidateSourceIds(IEnumerable<SourcePair> pairs, PatchKey patch,
            IReadOnlyCollection<string> setPrefixes, List<ValidationIssue> issues)
        {
            const int maxDetails = 20;
            var total = 0;
            var errors = 0;
            var errorDetails = new List<KeyValuePair<int, ValidationIssue>>();
            var warningDetails = new List<KeyValuePair<int, ValidationIssue>>();
            foreach (var pair in pairs.OrderBy(x => x.Kind, StringComparer.Ordinal))
            {
                var allById = pair.KoKr.Concat(pair.EnUs).ToLookup(x => x.Id, StringComparer.Ordinal);
                foreach (var locale in new[] { "en_US", "ko_KR" })
                foreach (var group in (locale == "ko_KR" ? pair.KoKr : pair.EnUs)
                    .GroupBy(x => x.Id, StringComparer.Ordinal).Where(x => x.Count() > 1)
                    .OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    var id = group.Key;
                    var rows = allById[id].ToArray();
                    var selected = rows.Any(x => SourceBelongsToSet(x, patch)) ||
                        BelongsToSet(id, patch) || BelongsToPrefix(id, setPrefixes) ||
                        pair.IncludeGeneric && !HasExplicitSetPrefix(id);
                    var blocks = selected || !ClearlyOneOtherSet(rows, patch);
                    total++;
                    if (blocks) errors++;
                    // Remove both locales for this ID rather than choosing a representative.
                    pair.ExcludedIds.Add(id);
                    var details = blocks ? errorDetails : warningDetails;
                    if (details.Count >= maxDetails) continue;
                    var keys = group.Select(x => x.SourceKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    var keyText = string.Join(", ", keys.Take(4).Select(DiagnosticText));
                    details.Add(new KeyValuePair<int, ValidationIssue>(total,
                        new ValidationIssue(blocks ? ValidationSeverity.Error : ValidationSeverity.Warning,
                        "ddragon_duplicate_source_id", DiagnosticText(id),
                        $"범위 {DiagnosticText(patch.SetId)} · {pair.Kind} · {locale} · ID {DiagnosticText(id)} · " +
                        (blocks ? "선택 범위 또는 경계 불명확으로 중단" : "확정된 비선택 세트의 충돌 그룹 전체 제외") +
                        $" · 원본 키 {keys.Length}개: {keyText}" + (keys.Length > 4 ? " … (일부 생략)" : ""))));
                }
            }
            if (total > 0)
            {
                // Reserve room for blocking evidence, then retain canonical ordering of
                // the displayed subset so warnings cannot hide a later failing ID.
                issues.AddRange(errorDetails.Concat(warningDetails.Take(maxDetails - errorDetails.Count))
                    .OrderBy(x => x.Key).Select(x => x.Value));
                issues.Add(new ValidationIssue(errors > 0 ? ValidationSeverity.Error : ValidationSeverity.Warning,
                    "ddragon_duplicate_source_id_summary", "",
                    $"범위 {DiagnosticText(patch.SetId)} · 공식 원본 ID 충돌 그룹 총 {total}개 (오류 {errors}개, 경고 {total - errors}개). " +
                    $"오류 상세 우선, 종류·언어·ID·원본 키 순으로 상세 {Math.Min(total, maxDetails)}개 표시 (최대 {maxDetails}개)."));
            }
        }

        private static bool ClearlyOneOtherSet(IEnumerable<SourceRecord> rows, PatchKey patch)
        {
            int? otherSet = null;
            foreach (var row in rows)
            {
                var evidence = new HashSet<int>();
                foreach (Match match in Regex.Matches(row.SourceKey, @"/(?:TFTSet|TFT|TEST)([0-9]+)(?=/)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var set)) return false;
                    evidence.Add(set);
                }
                if (HasExplicitSetPrefix(row.Id))
                {
                    var match = Regex.Match(row.Id, @"^(?:TFTSet|TFT|TEST)([0-9]+)_",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var set)) return false;
                    evidence.Add(set);
                }
                if (evidence.Count != 1 || evidence.Contains(patch.SetNumber)) return false;
                var current = evidence.Single();
                if (otherSet.HasValue && otherSet.Value != current) return false;
                otherSet = current;
            }
            return otherSet.HasValue;
        }

        // Source keys and IDs are untrusted display text. Escape markup/control/format
        // characters and bound the final text, without splitting UTF-16 surrogate pairs.
        private static string DiagnosticText(string value)
        {
            const int limit = 128;
            var result = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                var category = char.GetUnicodeCategory(ch);
                var escaped = char.IsControl(ch) || category == UnicodeCategory.Format ||
                    category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator ||
                    ch == '<' || ch == '>' || char.IsSurrogate(ch) &&
                    !(char.IsHighSurrogate(ch) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]));
                var text = escaped ? "\\u" + ((int)ch).ToString("x4", CultureInfo.InvariantCulture) :
                    char.IsHighSurrogate(ch) ? value.Substring(i++, 2) : ch.ToString();
                if (result.Length + text.Length > limit - 1) { result.Append('…'); break; }
                result.Append(text);
            }
            return result.ToString();
        }

        // Data Dragon's augment-container is unused metadata, not a catalog record.
        // Keep the rest of the document strict, including duplicate root and data keys.
        private static JObject ParseAugmentDocument(string json, List<ValidationIssue> issues)
        {
            const int maxCharacters = 16 * 1024 * 1024;
            if (json.Length > maxCharacters) throw new JsonSerializationException("augment_document_too_large");
            StrictJsonSyntax.Validate(json);
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("invalid_augment_document");
                var root = new JObject();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("invalid_augment_document");
                    var name = (string)reader.Value;
                    if (!seen.Add(name) || !reader.Read()) throw new JsonSerializationException("invalid_augment_document");
                    if (name == "augment-container")
                    {
                        if (reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("invalid_augment_container");
                        reader.Skip();
                        if (!issues.Any(x => x.Code == "ddragon_augment_metadata_skipped"))
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "ddragon_augment_metadata_skipped", "",
                                "공식 증강 자료의 사용하지 않는 부가 정보를 제외했습니다. 증강 목록은 별도로 검증합니다."));
                    }
                    else
                    {
                        root.Add(name, JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }));
                    }
                }
                if (reader.TokenType != JsonToken.EndObject || reader.Read()) throw new JsonSerializationException("invalid_augment_document");
                return root;
            }
        }

        // JsonTextReader accepts extensions such as comments and trailing commas. Validate
        // standard JSON separately before skipping the one unused metadata object.
        private sealed class StrictJsonSyntax
        {
            private readonly string _text;
            private int _offset;
            private StrictJsonSyntax(string text) { _text = text; }
            internal static void Validate(string text)
            {
                var parser = new StrictJsonSyntax(text);
                parser.Value(0); parser.Space();
                if (parser._offset != text.Length) throw Invalid();
            }
            private static JsonSerializationException Invalid() => new JsonSerializationException("invalid_augment_json_syntax");
            private void Space() { while (_offset < _text.Length && (_text[_offset] == ' ' || _text[_offset] == '\t' || _text[_offset] == '\r' || _text[_offset] == '\n')) _offset++; }
            private bool Take(char value) { if (_offset < _text.Length && _text[_offset] == value) { _offset++; return true; } return false; }
            private void Require(char value) { if (!Take(value)) throw Invalid(); }
            private void Value(int depth)
            {
                if (depth > 64) throw Invalid();
                Space();
                if (_offset >= _text.Length) throw Invalid();
                var ch = _text[_offset];
                if (ch == '{') { Object(depth + 1); return; }
                if (ch == '[') { Array(depth + 1); return; }
                if (ch == '"') { String(); return; }
                if (ch == 't') { Literal("true"); return; }
                if (ch == 'f') { Literal("false"); return; }
                if (ch == 'n') { Literal("null"); return; }
                Number();
            }
            private void Object(int depth)
            {
                Require('{'); Space(); if (Take('}')) return;
                while (true)
                {
                    Space(); String(); Space(); Require(':'); Value(depth); Space();
                    if (Take('}')) return;
                    Require(','); Space();
                }
            }
            private void Array(int depth)
            {
                Require('['); Space(); if (Take(']')) return;
                while (true)
                {
                    Value(depth); Space(); if (Take(']')) return;
                    Require(','); Space();
                }
            }
            private void String()
            {
                Require('"');
                while (_offset < _text.Length)
                {
                    var ch = _text[_offset++];
                    if (ch == '"') return;
                    if (ch < 0x20) throw Invalid();
                    if (ch != '\\') continue;
                    if (_offset >= _text.Length) throw Invalid();
                    ch = _text[_offset++];
                    if (ch == 'u')
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            if (_offset >= _text.Length || !IsHex(_text[_offset++])) throw Invalid();
                        }
                    }
                    else if (ch != '"' && ch != '\\' && ch != '/' && ch != 'b' && ch != 'f' && ch != 'n' && ch != 'r' && ch != 't') throw Invalid();
                }
                throw Invalid();
            }
            private static bool IsHex(char ch) => ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f' || ch >= 'A' && ch <= 'F';
            private static bool Digit(char ch) => ch >= '0' && ch <= '9';
            private void Literal(string value)
            {
                foreach (var ch in value) Require(ch);
            }
            private void Number()
            {
                Take('-');
                if (_offset >= _text.Length) throw Invalid();
                if (Take('0')) { if (_offset < _text.Length && Digit(_text[_offset])) throw Invalid(); }
                else
                {
                    if (_text[_offset] < '1' || _text[_offset] > '9') throw Invalid();
                    while (_offset < _text.Length && Digit(_text[_offset])) _offset++;
                }
                if (Take('.')) Digits();
                if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); Digits(); }
            }
            private void Digits()
            {
                var start = _offset;
                while (_offset < _text.Length && Digit(_text[_offset])) _offset++;
                if (_offset == start) throw Invalid();
            }
        }

        private static IReadOnlyCollection<string> DiscoverSetPrefixes(
            SourcePair champions,
            PatchKey patch)
        {
            return champions.KoKr.Concat(champions.EnUs)
                .Where(record => !champions.ExcludedIds.Contains(record.Id))
                .Where(record => SourceBelongsToSet(record, patch))
                .Select(record => ReadNamespacePrefix(record.Id))
                .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ReadNamespacePrefix(string stableId)
        {
            var separator = stableId.IndexOf('_');
            return separator > 0 ? stableId.Substring(0, separator) : "";
        }

        private static bool SourceBelongsToSet(SourceRecord record, PatchKey patch)
        {
            if (record == null)
            {
                return false;
            }

            return record.SourceKey.IndexOf(
                $"/{patch.SetId}/",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool BelongsToPrefix(
            string stableId,
            IEnumerable<string> setPrefixes)
        {
            return setPrefixes.Any(prefix => stableId.StartsWith(
                prefix + "_",
                StringComparison.OrdinalIgnoreCase));
        }

        private static bool BelongsToSet(string stableId, PatchKey patch)
        {
            if (stableId.StartsWith(patch.SetId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return stableId.StartsWith($"TFT{patch.SetNumber}_", StringComparison.OrdinalIgnoreCase) ||
                   stableId.StartsWith($"TEST{patch.SetNumber}_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasExplicitSetPrefix(string stableId)
        {
            return HasNumberedPrefix(stableId, "TFTSet") ||
                   HasNumberedPrefix(stableId, "TFT") ||
                   HasNumberedPrefix(stableId, "TEST");
        }

        private static bool HasNumberedPrefix(string stableId, string prefix)
        {
            if (!stableId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var index = prefix.Length;
            var firstDigit = index;
            while (index < stableId.Length && char.IsDigit(stableId[index]))
            {
                index++;
            }

            return index > firstDigit && index < stableId.Length && stableId[index] == '_';
        }

        private static LocalizedText ReadLocalized(
            JObject ko,
            JObject en,
            params string[] names)
        {
            return new LocalizedText(ReadFirstString(ko, names), ReadFirstString(en, names));
        }

        private static string ReadFirstString(JObject value, params string[] names)
        {
            foreach (var name in names)
            {
                var text = ReadString(value, name);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return "";
        }

        private static string ReadString(JObject value, string name)
        {
            return value?[name]?.Type == JTokenType.Null
                ? ""
                : value?[name]?.ToString() ?? "";
        }

        private static int ReadInt(JObject value, params string[] names)
        {
            foreach (var name in names)
            {
                var token = value?[name];
                if (token != null && token.Type == JTokenType.Integer)
                {
                    return token.Value<int>();
                }
            }

            return 0;
        }

        private static IReadOnlyList<string> ReadStringArray(JObject value, string name)
        {
            var array = value?[name] as JArray;
            return array == null
                ? Array.Empty<string>()
                : array.Values<string>().Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        }

        private static IReadOnlyDictionary<string, double> ReadNumericBag(
            JObject value,
            params string[] names)
        {
            var result = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                var bag = value?[name] as JObject;
                if (bag == null)
                {
                    continue;
                }

                foreach (var property in bag.Properties())
                {
                    if (property.Value.Type == JTokenType.Integer ||
                        property.Value.Type == JTokenType.Float)
                    {
                        result[property.Name] = property.Value.Value<double>();
                    }
                }
            }

            return result;
        }

        private static string ReadIcon(
            PatchKey patch,
            DataDragonDocumentKind kind,
            JObject value)
        {
            var filename = value?["image"]?["full"]?.ToString() ?? "";
            return DataDragonEndpoints.Image(patch.SourceGameVersion, kind, filename);
        }

        private static IReadOnlyList<SourceStamp> PairSources(DataDragonPayloadPair pair)
        {
            return new[] { ToSourceStamp(pair.KoKr), ToSourceStamp(pair.EnUs) };
        }

        private static SourceStamp ToSourceStamp(HttpResponseData response)
        {
            return new SourceStamp(
                "Riot Data Dragon",
                response.Url,
                response.RetrievedUtc,
                response.ETag,
                ContentHasher.Sha256Hex(response.Body));
        }

        private static void EnsureSuccess(HttpResponseData response)
        {
            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                throw new HttpTransportException(response.Url, response.StatusCode, "request failed");
            }
        }

        private sealed class JoinedRecord
        {
            public string Id { get; }
            public JObject KoKr { get; }
            public JObject EnUs { get; }
            public JObject Primary => EnUs ?? KoKr;
            public LocalizedText Name { get; }
            public bool SourceBelongsToSet { get; }

            public JoinedRecord(
                string id,
                JObject koKr,
                JObject enUs,
                LocalizedText name,
                bool sourceBelongsToSet)
            {
                Id = id;
                KoKr = koKr;
                EnUs = enUs;
                Name = name;
                SourceBelongsToSet = sourceBelongsToSet;
            }
        }

        private sealed class SourceRecord
        {
            public string Id { get; }
            public string SourceKey { get; }
            public JObject Value { get; }

            public SourceRecord(string id, string sourceKey, JObject value)
            {
                Id = id;
                SourceKey = sourceKey;
                Value = value;
            }
        }

        private sealed class SourcePair
        {
            public string Kind { get; }
            public DataDragonPayloadPair Payload { get; }
            public bool IncludeGeneric { get; }
            public IReadOnlyList<SourceRecord> KoKr { get; }
            public IReadOnlyList<SourceRecord> EnUs { get; }
            public HashSet<string> ExcludedIds { get; } = new HashSet<string>(StringComparer.Ordinal);

            public SourcePair(string kind, DataDragonPayloadPair payload, bool includeGeneric,
                List<ValidationIssue> augmentIssues = null)
            {
                Kind = kind;
                Payload = payload;
                IncludeGeneric = includeGeneric;
                KoKr = ReadRecords(payload.KoKr, augmentIssues);
                EnUs = ReadRecords(payload.EnUs, augmentIssues);
            }
        }
    }
}
