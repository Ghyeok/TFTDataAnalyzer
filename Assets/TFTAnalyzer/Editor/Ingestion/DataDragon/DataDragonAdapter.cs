using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

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
                1,
                patch,
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

            return new StaticCatalog(
                manifest,
                ParseUnits(snapshot.Champions, patch),
                ParseTraits(snapshot.Traits, patch),
                ParseItems(snapshot.Items, patch),
                ParseAugments(snapshot.Augments, patch));
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
            DataDragonPayloadPair pair,
            PatchKey patch)
        {
            foreach (var record in Join(pair, patch))
            {
                yield return new UnitRecord(
                    record.Id,
                    record.Name,
                    ReadInt(record.Primary, "cost", "tier"),
                    null,
                    ReadIcon(patch, DataDragonDocumentKind.Champions, record.Primary),
                    PairSources(pair),
                    ReadString(record.Primary, "role"),
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    ReadNumericBag(record.Primary, "stats", "effects"));
            }
        }

        private static IEnumerable<TraitRecord> ParseTraits(
            DataDragonPayloadPair pair,
            PatchKey patch)
        {
            foreach (var record in Join(pair, patch))
            {
                yield return new TraitRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    ReadNumericBag(record.Primary, "effects"),
                    ReadIcon(patch, DataDragonDocumentKind.Traits, record.Primary),
                    PairSources(pair));
            }
        }

        private static IEnumerable<ItemRecord> ParseItems(
            DataDragonPayloadPair pair,
            PatchKey patch)
        {
            foreach (var record in Join(pair, patch))
            {
                yield return new ItemRecord(
                    record.Id,
                    record.Name,
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    ReadStringArray(record.Primary, "components"),
                    ReadNumericBag(record.Primary, "stats", "effects"),
                    ReadIcon(patch, DataDragonDocumentKind.Items, record.Primary),
                    PairSources(pair));
            }
        }

        private static IEnumerable<AugmentRecord> ParseAugments(
            DataDragonPayloadPair pair,
            PatchKey patch)
        {
            foreach (var record in Join(pair, patch))
            {
                yield return new AugmentRecord(
                    record.Id,
                    record.Name,
                    ReadString(record.Primary, "tier"),
                    ReadLocalized(record.KoKr, record.EnUs, "description", "desc"),
                    null,
                    ReadNumericBag(record.Primary, "effects"),
                    ReadIcon(patch, DataDragonDocumentKind.Augments, record.Primary),
                    PairSources(pair));
            }
        }

        private static IEnumerable<JoinedRecord> Join(
            DataDragonPayloadPair pair,
            PatchKey patch)
        {
            var ko = ReadRecords(pair.KoKr);
            var en = ReadRecords(pair.EnUs);
            return ko.Keys
                .Concat(en.Keys)
                .Distinct(StringComparer.Ordinal)
                .Where(id => BelongsToSet(id, patch))
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id =>
                {
                    ko.TryGetValue(id, out var koRecord);
                    en.TryGetValue(id, out var enRecord);
                    return new JoinedRecord(
                        id,
                        koRecord,
                        enRecord,
                        new LocalizedText(
                            ReadString(koRecord, "name"),
                            ReadString(enRecord, "name")));
                });
        }

        private static Dictionary<string, JObject> ReadRecords(HttpResponseData response)
        {
            var root = JObject.Parse(response.Utf8Body);
            var data = root["data"] as JObject;
            if (data == null)
            {
                throw new JsonSerializationException($"Data Dragon response has no data object: {response.Url}");
            }

            var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var property in data.Properties())
            {
                var record = property.Value as JObject;
                if (record == null)
                {
                    continue;
                }

                var id = ReadString(record, "id");
                result[string.IsNullOrWhiteSpace(id) ? property.Name : id] = record;
            }

            return result;
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

            public JoinedRecord(
                string id,
                JObject koKr,
                JObject enUs,
                LocalizedText name)
            {
                Id = id;
                KoKr = koKr;
                EnUs = enUs;
                Name = name;
            }
        }
    }
}
