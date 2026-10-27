using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Serialization
{
    public static class CanonicalCatalogSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None
        };

        public static string Serialize(StaticCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var json = JsonConvert.SerializeObject(ToDto(catalog), Formatting.Indented, Settings);
            if (catalog.Manifest.SchemaVersion >= 4)
            {
                var structured = StructuredCatalogJson.Write(catalog,json);
                return catalog.Manifest.SchemaVersion == 5 ? LocalizedCatalogJson.Write(catalog,structured) : structured;
            }
            return json.Replace("\r\n", "\n") + "\n";
        }

        public static StaticCatalog Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Catalog JSON cannot be blank.", nameof(json));
            }

            var root = StructuredCatalogJson.ReadRoot(json);
            var schemaToken = (root["manifest"] as Newtonsoft.Json.Linq.JObject)?["schemaVersion"];
            var separated = schemaToken?.Type == Newtonsoft.Json.Linq.JTokenType.Integer && schemaToken.ToString() == "5";
            if (separated)
            {
                root = LocalizedCatalogJson.Expand(root);
                StructuredCatalogJson.Guard(root,4); // Strict literal metadata/record types before DTO coercion.
            }
            else if (root.Properties().Any(x=>string.Equals(x.Name,"localizations",StringComparison.OrdinalIgnoreCase)))
                throw new JsonSerializationException("Invalid localized catalog schema.");
            var dto = JsonConvert.DeserializeObject<CatalogDto>(separated ? root.ToString(Formatting.None) : json, Settings);
            if (dto == null || dto.Manifest == null)
            {
                throw new JsonSerializationException("Catalog manifest is required.");
            }

            if (dto.Manifest.SchemaVersion < 1 || dto.Manifest.SchemaVersion > 5 ||
                (dto.Manifest.SchemaVersion == 5 && !separated) ||
                (separated && dto.Manifest.SchemaVersion != 5) ||
                (dto.Manifest.SchemaVersion < 3 && root.Properties().Any(x =>
                    string.Equals(x.Name, "fieldOrigins", StringComparison.OrdinalIgnoreCase))) ||
                (dto.Manifest.SchemaVersion >= 3 && !(root["fieldOrigins"] is Newtonsoft.Json.Linq.JArray)))
                throw new JsonSerializationException("Invalid schema or fieldOrigins contract.");
            if (!separated) StructuredCatalogJson.Guard(root, dto.Manifest.SchemaVersion);
            try { return dto.Manifest.SchemaVersion >= 4 ? StructuredCatalogJson.Attach(FromDto(dto), root) : FromDto(dto); }
            catch (ArgumentException error) { throw new JsonSerializationException("Invalid structured static values.", error); }
        }

        private static CatalogDto ToDto(StaticCatalog catalog)
        {
            if (catalog.Manifest.SchemaVersion < 1 || catalog.Manifest.SchemaVersion > 5 ||
                (catalog.Manifest.SchemaVersion < 3 && catalog.FieldOrigins.Count > 0) ||
                (catalog.Manifest.SchemaVersion < 4 && StructuredCatalogJson.HasValues(catalog)))
                throw new JsonSerializationException("Invalid schema or fieldOrigins contract.");
            return new CatalogDto
            {
                Manifest = ToDto(catalog.Manifest),
                Units = Stable(catalog.Units, value => value.StableId).Select(ToDto).ToList(),
                Traits = Stable(catalog.Traits, value => value.StableId).Select(ToDto).ToList(),
                Items = Stable(catalog.Items, value => value.StableId).Select(ToDto).ToList(),
                Augments = Stable(catalog.Augments, value => value.StableId).Select(ToDto).ToList(),
                FieldOrigins = catalog.Manifest.SchemaVersion >= 3 ? catalog.FieldOrigins
                    .OrderBy(x => x.RecordType, StringComparer.Ordinal).ThenBy(x => x.StableId, StringComparer.Ordinal)
                    .ThenBy(x => x.Pointer, StringComparer.Ordinal).Select(x => new FieldOriginDto
                    { RecordType = x.RecordType, StableId = x.StableId, Pointer = x.Pointer,
                        Sources = StableSources(x.Sources) }).ToList() : null
            };
        }

        private static StaticCatalog FromDto(CatalogDto dto)
        {
            return new StaticCatalog(
                FromDto(dto.Manifest),
                (dto.Units ?? new List<UnitDto>()).Select(FromDto),
                (dto.Traits ?? new List<TraitDto>()).Select(FromDto),
                (dto.Items ?? new List<ItemDto>()).Select(FromDto),
                (dto.Augments ?? new List<AugmentDto>()).Select(FromDto),
                (dto.FieldOrigins ?? new List<FieldOriginDto>()).Select(x => new FieldOrigin(
                    x.RecordType, x.StableId, x.Pointer, FromDto(x.Sources))));
        }

        private static ManifestDto ToDto(PatchManifest manifest)
        {
            return new ManifestDto
            {
                SchemaVersion = manifest.SchemaVersion,
                SetId = manifest.Patch.SetId,
                SetNumber = manifest.Patch.SetNumber,
                SetName = manifest.SetName,
                PatchId = manifest.Patch.PatchId,
                SourceGameVersion = manifest.Patch.SourceGameVersion,
                SourceProfile = manifest.Identity?.ProfileId,
                CreatedUtc = manifest.CreatedUtc,
                ContentHash = manifest.ContentHash,
                Sources = manifest.Sources
                    .OrderBy(value => value.SourceName, StringComparer.Ordinal)
                    .ThenBy(value => value.Url, StringComparer.Ordinal)
                    .Select(ToDto)
                    .ToList(),
                PolicyReviewDate = manifest.PolicyReviewDate,
                PolicyUrls = manifest.PolicyUrls.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                ValidationErrorCount = manifest.ValidationErrorCount,
                ValidationWarningCount = manifest.ValidationWarningCount,
                PreviousPatchSummary = manifest.PreviousPatchSummary,
                CommunityDragonVersion = manifest.CommunityDragonVersion
            };
        }

        private static PatchManifest FromDto(ManifestDto dto)
        {
            var patch = new PatchKey(dto.SetId, dto.SetNumber, dto.PatchId, dto.SourceGameVersion);
            var sources = (dto.Sources ?? new List<SourceDto>()).Select(FromDto);
            if (dto.SchemaVersion == 1)
            {
                if (dto.SourceProfile != null)
                {
                    throw new JsonSerializationException("Schema 1 must not declare a source profile.");
                }

                return new PatchManifest(1, patch, dto.SetName, dto.CreatedUtc, dto.ContentHash,
                    sources, dto.PolicyReviewDate, dto.PolicyUrls, dto.ValidationErrorCount,
                    dto.ValidationWarningCount, dto.PreviousPatchSummary, dto.CommunityDragonVersion);
            }

            try
            {
                var identity = new CatalogIdentity(patch, CatalogSourceProfileIds.Parse(dto.SourceProfile));
                return new PatchManifest(dto.SchemaVersion, identity, dto.SetName, dto.CreatedUtc,
                    dto.ContentHash, sources, dto.PolicyReviewDate, dto.PolicyUrls,
                    dto.ValidationErrorCount, dto.ValidationWarningCount,
                    dto.PreviousPatchSummary, dto.CommunityDragonVersion);
            }
            catch (ArgumentException error)
            {
                throw new JsonSerializationException("Invalid catalog identity in manifest.", error);
            }
        }

        private static SourceDto ToDto(SourceStamp source)
        {
            return new SourceDto
            {
                SourceName = source.SourceName,
                Url = source.Url,
                RetrievedUtc = source.RetrievedUtc,
                ETag = source.ETag,
                Sha256 = source.Sha256,
                IsSupplemental = source.IsSupplemental
            };
        }

        private static SourceStamp FromDto(SourceDto dto)
        {
            return new SourceStamp(
                dto.SourceName,
                dto.Url,
                dto.RetrievedUtc,
                dto.ETag,
                dto.Sha256,
                dto.IsSupplemental);
        }

        private static UnitDto ToDto(UnitRecord value)
        {
            return new UnitDto
            {
                StableId = value.StableId,
                Name = ToDto(value.Name),
                Cost = value.Cost,
                TraitIds = value.TraitIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                IconPath = value.IconPath,
                Sources = StableSources(value.Sources),
                Role = value.Role,
                Description = ToDto(value.Description),
                Effects = StableEffects(value.Effects)
            };
        }

        private static UnitRecord FromDto(UnitDto dto)
        {
            return new UnitRecord(
                dto.StableId,
                FromDto(dto.Name),
                dto.Cost,
                dto.TraitIds,
                dto.IconPath,
                FromDto(dto.Sources),
                dto.Role,
                FromDto(dto.Description),
                dto.Effects);
        }

        private static TraitDto ToDto(TraitRecord value)
        {
            return new TraitDto
            {
                StableId = value.StableId,
                Name = ToDto(value.Name),
                Description = ToDto(value.Description),
                Effects = StableEffects(value.Effects),
                IconPath = value.IconPath,
                Sources = StableSources(value.Sources)
            };
        }

        private static TraitRecord FromDto(TraitDto dto)
        {
            return new TraitRecord(
                dto.StableId,
                FromDto(dto.Name),
                FromDto(dto.Description),
                dto.Effects,
                dto.IconPath,
                FromDto(dto.Sources));
        }

        private static ItemDto ToDto(ItemRecord value)
        {
            return new ItemDto
            {
                StableId = value.StableId,
                Name = ToDto(value.Name),
                Description = ToDto(value.Description),
                ComponentIds = value.ComponentIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                Effects = StableEffects(value.Effects),
                IconPath = value.IconPath,
                Sources = StableSources(value.Sources)
            };
        }

        private static ItemRecord FromDto(ItemDto dto)
        {
            return new ItemRecord(
                dto.StableId,
                FromDto(dto.Name),
                FromDto(dto.Description),
                dto.ComponentIds,
                dto.Effects,
                dto.IconPath,
                FromDto(dto.Sources));
        }

        private static AugmentDto ToDto(AugmentRecord value)
        {
            return new AugmentDto
            {
                StableId = value.StableId,
                Name = ToDto(value.Name),
                Tier = value.Tier,
                Description = ToDto(value.Description),
                RelatedIds = value.RelatedIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                Effects = StableEffects(value.Effects),
                IconPath = value.IconPath,
                Sources = StableSources(value.Sources)
            };
        }

        private static AugmentRecord FromDto(AugmentDto dto)
        {
            return new AugmentRecord(
                dto.StableId,
                FromDto(dto.Name),
                dto.Tier,
                FromDto(dto.Description),
                dto.RelatedIds,
                dto.Effects,
                dto.IconPath,
                FromDto(dto.Sources));
        }

        private static LocalizedDto ToDto(LocalizedText value)
        {
            return new LocalizedDto { KoKr = value.KoKr, EnUs = value.EnUs };
        }

        private static LocalizedText FromDto(LocalizedDto dto)
        {
            return dto == null ? LocalizedText.Empty : new LocalizedText(dto.KoKr, dto.EnUs);
        }

        private static List<SourceDto> StableSources(IEnumerable<SourceStamp> values)
        {
            return values
                .OrderBy(value => value.SourceName, StringComparer.Ordinal)
                .ThenBy(value => value.Url, StringComparer.Ordinal)
                .Select(ToDto)
                .ToList();
        }

        private static IEnumerable<SourceStamp> FromDto(IEnumerable<SourceDto> values)
        {
            return (values ?? Enumerable.Empty<SourceDto>()).Select(FromDto);
        }

        private static SortedDictionary<string, double> StableEffects(
            IEnumerable<KeyValuePair<string, double>> values)
        {
            var result = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        private static IEnumerable<T> Stable<T>(IEnumerable<T> values, Func<T, string> key)
        {
            return values.OrderBy(key, StringComparer.Ordinal);
        }

        private sealed class CatalogDto
        {
            [JsonProperty("manifest", Order = 1)] public ManifestDto Manifest { get; set; }
            [JsonProperty("units", Order = 2)] public List<UnitDto> Units { get; set; }
            [JsonProperty("traits", Order = 3)] public List<TraitDto> Traits { get; set; }
            [JsonProperty("items", Order = 4)] public List<ItemDto> Items { get; set; }
            [JsonProperty("augments", Order = 5)] public List<AugmentDto> Augments { get; set; }
            [JsonProperty("fieldOrigins", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
            public List<FieldOriginDto> FieldOrigins { get; set; }
        }

        private sealed class FieldOriginDto
        {
            [JsonProperty("recordType", Order = 1)] public string RecordType { get; set; }
            [JsonProperty("stableId", Order = 2)] public string StableId { get; set; }
            [JsonProperty("pointer", Order = 3)] public string Pointer { get; set; }
            [JsonProperty("sources", Order = 4)] public List<SourceDto> Sources { get; set; }
        }

        private sealed class ManifestDto
        {
            [JsonProperty("schemaVersion", Order = 1)] public int SchemaVersion { get; set; }
            [JsonProperty("setId", Order = 2)] public string SetId { get; set; }
            [JsonProperty("setNumber", Order = 3)] public int SetNumber { get; set; }
            [JsonProperty("setName", Order = 4)] public string SetName { get; set; }
            [JsonProperty("patchId", Order = 5)] public string PatchId { get; set; }
            [JsonProperty("sourceGameVersion", Order = 6)] public string SourceGameVersion { get; set; }
            [JsonProperty("sourceProfile", Order = 7, NullValueHandling = NullValueHandling.Ignore)] public string SourceProfile { get; set; }
            [JsonProperty("createdUtc", Order = 7)] public DateTime CreatedUtc { get; set; }
            [JsonProperty("contentHash", Order = 8)] public string ContentHash { get; set; }
            [JsonProperty("sources", Order = 9)] public List<SourceDto> Sources { get; set; }
            [JsonProperty("policyReviewDate", Order = 10)] public DateTime PolicyReviewDate { get; set; }
            [JsonProperty("policyUrls", Order = 11)] public List<string> PolicyUrls { get; set; }
            [JsonProperty("validationErrorCount", Order = 12)] public int ValidationErrorCount { get; set; }
            [JsonProperty("validationWarningCount", Order = 13)] public int ValidationWarningCount { get; set; }
            [JsonProperty("previousPatchSummary", Order = 14)] public string PreviousPatchSummary { get; set; }
            [JsonProperty("communityDragonVersion", Order = 15)] public string CommunityDragonVersion { get; set; }
        }

        private sealed class SourceDto
        {
            [JsonProperty("sourceName", Order = 1)] public string SourceName { get; set; }
            [JsonProperty("url", Order = 2)] public string Url { get; set; }
            [JsonProperty("retrievedUtc", Order = 3)] public DateTime RetrievedUtc { get; set; }
            [JsonProperty("etag", Order = 4)] public string ETag { get; set; }
            [JsonProperty("sha256", Order = 5)] public string Sha256 { get; set; }
            [JsonProperty("isSupplemental", Order = 6)] public bool IsSupplemental { get; set; }
        }

        private sealed class LocalizedDto
        {
            [JsonProperty("koKr", Order = 1)] public string KoKr { get; set; }
            [JsonProperty("enUs", Order = 2)] public string EnUs { get; set; }
        }

        private sealed class UnitDto
        {
            [JsonProperty("stableId", Order = 1)] public string StableId { get; set; }
            [JsonProperty("name", Order = 2)] public LocalizedDto Name { get; set; }
            [JsonProperty("cost", Order = 3)] public int Cost { get; set; }
            [JsonProperty("traitIds", Order = 4)] public List<string> TraitIds { get; set; }
            [JsonProperty("iconPath", Order = 5)] public string IconPath { get; set; }
            [JsonProperty("sources", Order = 6)] public List<SourceDto> Sources { get; set; }
            [JsonProperty("role", Order = 7)] public string Role { get; set; }
            [JsonProperty("description", Order = 8)] public LocalizedDto Description { get; set; }
            [JsonProperty("effects", Order = 9)] public SortedDictionary<string, double> Effects { get; set; }
        }

        private sealed class TraitDto
        {
            [JsonProperty("stableId", Order = 1)] public string StableId { get; set; }
            [JsonProperty("name", Order = 2)] public LocalizedDto Name { get; set; }
            [JsonProperty("description", Order = 3)] public LocalizedDto Description { get; set; }
            [JsonProperty("effects", Order = 4)] public SortedDictionary<string, double> Effects { get; set; }
            [JsonProperty("iconPath", Order = 5)] public string IconPath { get; set; }
            [JsonProperty("sources", Order = 6)] public List<SourceDto> Sources { get; set; }
        }

        private sealed class ItemDto
        {
            [JsonProperty("stableId", Order = 1)] public string StableId { get; set; }
            [JsonProperty("name", Order = 2)] public LocalizedDto Name { get; set; }
            [JsonProperty("description", Order = 3)] public LocalizedDto Description { get; set; }
            [JsonProperty("componentIds", Order = 4)] public List<string> ComponentIds { get; set; }
            [JsonProperty("effects", Order = 5)] public SortedDictionary<string, double> Effects { get; set; }
            [JsonProperty("iconPath", Order = 6)] public string IconPath { get; set; }
            [JsonProperty("sources", Order = 7)] public List<SourceDto> Sources { get; set; }
        }

        private sealed class AugmentDto
        {
            [JsonProperty("stableId", Order = 1)] public string StableId { get; set; }
            [JsonProperty("name", Order = 2)] public LocalizedDto Name { get; set; }
            [JsonProperty("tier", Order = 3)] public string Tier { get; set; }
            [JsonProperty("description", Order = 4)] public LocalizedDto Description { get; set; }
            [JsonProperty("relatedIds", Order = 5)] public List<string> RelatedIds { get; set; }
            [JsonProperty("effects", Order = 6)] public SortedDictionary<string, double> Effects { get; set; }
            [JsonProperty("iconPath", Order = 7)] public string IconPath { get; set; }
            [JsonProperty("sources", Order = 8)] public List<SourceDto> Sources { get; set; }
        }
    }
}
