using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Persistence
{
    public sealed class StagedCatalog
    {
        public string StagingRoot { get; }
        public string PatchDirectory { get; }
        public string IconsDirectory { get; }
        public string ContentHash { get; }
        public string LegacyContentHash { get; }
        public CatalogIdentity Identity { get; }

        public StagedCatalog(
            string stagingRoot,
            string patchDirectory,
            string iconsDirectory,
            string contentHash,
            string legacyContentHash = null,
            CatalogIdentity identity = null)
        {
            StagingRoot = stagingRoot;
            PatchDirectory = patchDirectory;
            IconsDirectory = iconsDirectory;
            ContentHash = contentHash;
            LegacyContentHash = legacyContentHash ?? contentHash;
            Identity = identity;
        }
    }

    public sealed class CatalogAssetWriter
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private readonly StaticCatalogPaths _paths;

        public CatalogAssetWriter(StaticCatalogPaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public void StoreRawSources(CatalogIdentity identity, IEnumerable<HttpResponseData> responses)
        {
            var directory = _paths.RawDirectory(identity);
            foreach (var response in responses ?? Enumerable.Empty<HttpResponseData>())
            {
                Directory.CreateDirectory(directory);
                var hash = ContentHasher.Sha256Hex(response.Body);
                var path = Path.Combine(directory, hash + ".json");
                if (!File.Exists(path))
                {
                    File.WriteAllBytes(path, response.Body);
                }
            }
        }

        public async Task<StaticCatalog> StageIconsAsync(
            StaticCatalog catalog,
            IHttpTransport transport,
            string stagingRoot,
            CancellationToken token)
        {
            var stagedIcons = Path.Combine(stagingRoot, "icons");
            var cache = new Dictionary<string, string>(StringComparer.Ordinal);
            async Task<string> Stage(string source)
            {
                if (!IsWebUrl(source))
                {
                    return source ?? "";
                }

                if (cache.TryGetValue(source, out var cached))
                {
                    return cached;
                }

                token.ThrowIfCancellationRequested();
                var response = await transport.GetAsync(source, token);
                if (response.StatusCode < 200 || response.StatusCode >= 300 || !HasPngSignature(response.Body))
                {
                    throw new InvalidDataException($"Icon response is not a valid PNG: {source}");
                }

                var hash = ContentHasher.Sha256Hex(response.Body);
                Directory.CreateDirectory(stagedIcons);
                var path = Path.Combine(stagedIcons, hash + ".png");
                if (!File.Exists(path))
                {
                    File.WriteAllBytes(path, response.Body);
                }

                var assetPath = $"{StaticCatalogPaths.GeneratedAssetDirectory}/Icons/{hash}.png";
                cache[source] = assetPath;
                return assetPath;
            }

            var units = new List<UnitRecord>();
            foreach (var value in catalog.Units)
            {
                units.Add(new UnitRecord(
                    value.StableId,
                    value.Name,
                    value.Cost,
                    value.TraitIds,
                    await Stage(value.IconPath),
                    value.Sources,
                    value.Role,
                    value.Description,
                    value.Effects));
            }

            var traits = new List<TraitRecord>();
            foreach (var value in catalog.Traits)
            {
                traits.Add(new TraitRecord(
                    value.StableId,
                    value.Name,
                    value.Description,
                    value.Effects,
                    await Stage(value.IconPath),
                    value.Sources));
            }

            var items = new List<ItemRecord>();
            foreach (var value in catalog.Items)
            {
                items.Add(new ItemRecord(
                    value.StableId,
                    value.Name,
                    value.Description,
                    value.ComponentIds,
                    value.Effects,
                    await Stage(value.IconPath),
                    value.Sources));
            }

            var augments = new List<AugmentRecord>();
            foreach (var value in catalog.Augments)
            {
                augments.Add(new AugmentRecord(
                    value.StableId,
                    value.Name,
                    value.Tier,
                    value.Description,
                    value.RelatedIds,
                    value.Effects,
                    await Stage(value.IconPath),
                    value.Sources));
            }

            return new StaticCatalog(catalog.Manifest, units, traits, items, augments);
        }

        public StagedCatalog WriteStagedCatalog(
            StaticCatalog catalog,
            ValidationReport validation,
            string stagingRoot)
        {
            var patchDirectory = Path.Combine(stagingRoot, "patch");
            Directory.CreateDirectory(patchDirectory);
            var catalogJson = CanonicalCatalogSerializer.Serialize(
                NormalizeForContentSnapshot(catalog));
            var contentHash = ContentHasher.Sha256Hex(catalogJson);
            var legacyContentHash = catalog.Manifest.Identity?.SourceProfile == CatalogSourceProfile.OfficialOnly
                ? ContentHasher.Sha256Hex(CanonicalCatalogSerializer.Serialize(
                    NormalizeForContentSnapshot(AsSchemaOne(catalog))))
                : contentHash;
            File.WriteAllText(
                Path.Combine(patchDirectory, "catalog.json"),
                catalogJson,
                Utf8WithoutBom);

            var manifest = new
            {
                schemaVersion = catalog.Manifest.SchemaVersion,
                setId = catalog.Manifest.Patch.SetId,
                setNumber = catalog.Manifest.Patch.SetNumber,
                setName = catalog.Manifest.SetName,
                patchId = catalog.Manifest.Patch.PatchId,
                sourceGameVersion = catalog.Manifest.Patch.SourceGameVersion,
                sourceProfile = catalog.Manifest.Identity?.ProfileId,
                contentHash,
                createdUtc = catalog.Manifest.CreatedUtc,
                sources = catalog.Manifest.Sources
                    .OrderBy(source => source.SourceName, StringComparer.Ordinal)
                    .ThenBy(source => source.Url, StringComparer.Ordinal),
                policyReviewDate = catalog.Manifest.PolicyReviewDate,
                policyUrls = catalog.Manifest.PolicyUrls.OrderBy(value => value, StringComparer.Ordinal),
                validationErrorCount = validation.ErrorCount,
                validationWarningCount = validation.WarningCount,
                previousPatchSummary = catalog.Manifest.PreviousPatchSummary,
                communityDragonVersion = catalog.Manifest.CommunityDragonVersion
            };
            var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                .Replace("\r\n", "\n") + "\n";
            File.WriteAllText(
                Path.Combine(patchDirectory, "manifest.json"),
                manifestJson,
                Utf8WithoutBom);
            return new StagedCatalog(
                stagingRoot,
                patchDirectory,
                Path.Combine(stagingRoot, "icons"),
                contentHash,
                legacyContentHash,
                catalog.Manifest.Identity);
        }

        public bool Publish(StagedCatalog staged, string finalDirectory, bool compatibleLegacy = false)
        {
            if (Directory.Exists(finalDirectory))
            {
                var existingHash = VerifyPublishedHash(finalDirectory, staged.Identity, compatibleLegacy);
                var expectedHash = compatibleLegacy ? staged.LegacyContentHash : staged.ContentHash;
                if (!string.Equals(existingHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Immutable catalog already exists with different content: {finalDirectory}");
                }

                return true;
            }

            PublishIcons(staged.IconsDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(finalDirectory));
            Directory.Move(staged.PatchDirectory, finalDirectory);
            return false;
        }

        public void CleanupStaging(string stagingRoot)
        {
            if (!string.IsNullOrWhiteSpace(stagingRoot) && Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, true);
            }
        }

        private void PublishIcons(string stagedIcons)
        {
            if (!Directory.Exists(stagedIcons))
            {
                return;
            }

            Directory.CreateDirectory(_paths.IconsDirectory);
            foreach (var source in Directory.GetFiles(stagedIcons, "*.png").OrderBy(value => value))
            {
                var target = Path.Combine(_paths.IconsDirectory, Path.GetFileName(source));
                if (File.Exists(target))
                {
                    var existingHash = ContentHasher.Sha256Hex(File.ReadAllBytes(target));
                    var expectedHash = Path.GetFileNameWithoutExtension(source);
                    if (!string.Equals(existingHash, expectedHash, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Existing icon hash does not match filename: {target}");
                    }

                    continue;
                }

                File.Move(source, target);
            }
        }

        public static string VerifyPublishedHash(string finalDirectory,
            CatalogIdentity expectedIdentity, bool compatibleLegacy)
        {
            var manifestPath = Path.Combine(finalDirectory, "manifest.json");
            var catalogPath = Path.Combine(finalDirectory, "catalog.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException($"Published catalog has no manifest: {finalDirectory}");
            }

            if (!File.Exists(catalogPath))
            {
                throw new InvalidDataException($"Published catalog has no catalog: {finalDirectory}");
            }

            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            if (expectedIdentity != null &&
                (manifest.Value<int?>("schemaVersion") != (compatibleLegacy ? 1 : 2) ||
                 !string.Equals(manifest.Value<string>("setId"), expectedIdentity.Patch.SetId, StringComparison.Ordinal) ||
                 manifest.Value<int?>("setNumber") != expectedIdentity.Patch.SetNumber ||
                 !string.Equals(manifest.Value<string>("patchId"), expectedIdentity.Patch.PatchId, StringComparison.Ordinal) ||
                 !string.Equals(manifest.Value<string>("sourceGameVersion"), expectedIdentity.Patch.SourceGameVersion, StringComparison.Ordinal) ||
                 (!compatibleLegacy && !string.Equals(manifest.Value<string>("sourceProfile"), expectedIdentity.ProfileId, StringComparison.Ordinal)) ||
                 (compatibleLegacy && manifest["sourceProfile"] != null)))
            {
                throw new InvalidDataException($"Published manifest identity does not match target: {finalDirectory}");
            }

            var declaredHash = manifest["contentHash"]?.ToString() ?? "";
            var catalogBytes = File.ReadAllBytes(catalogPath);
            var actualHash = ContentHasher.Sha256Hex(catalogBytes);
            if (string.IsNullOrWhiteSpace(declaredHash) ||
                !string.Equals(declaredHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Published catalog hash does not match its bytes: {finalDirectory}");
            }

            if (expectedIdentity != null)
            {
                JObject embedded;
                try
                {
                    embedded = JObject.Parse(Encoding.UTF8.GetString(catalogBytes))["manifest"] as JObject;
                }
                catch (JsonException error)
                {
                    throw new InvalidDataException($"Published catalog manifest is malformed: {finalDirectory}", error);
                }

                if (embedded == null ||
                    embedded.Value<int?>("schemaVersion") != (compatibleLegacy ? 1 : 2) ||
                    !string.Equals(embedded.Value<string>("setId"), expectedIdentity.Patch.SetId, StringComparison.Ordinal) ||
                    embedded.Value<int?>("setNumber") != expectedIdentity.Patch.SetNumber ||
                    !string.Equals(embedded.Value<string>("patchId"), expectedIdentity.Patch.PatchId, StringComparison.Ordinal) ||
                    !string.Equals(embedded.Value<string>("sourceGameVersion"), expectedIdentity.Patch.SourceGameVersion, StringComparison.Ordinal) ||
                    (!compatibleLegacy && !string.Equals(embedded.Value<string>("sourceProfile"), expectedIdentity.ProfileId, StringComparison.Ordinal)) ||
                    (compatibleLegacy && embedded["sourceProfile"] != null) ||
                    !SameProvenance(manifest, embedded, expectedIdentity))
                {
                    throw new InvalidDataException($"Published manifest provenance differs from catalog: {finalDirectory}");
                }
            }

            return declaredHash;
        }

        private static bool SameProvenance(JObject standalone, JObject embedded, CatalogIdentity identity)
        {
            var outerSources = standalone["sources"] as JArray;
            var innerSources = embedded["sources"] as JArray;
            if (outerSources == null || innerSources == null ||
                outerSources.Count != innerSources.Count)
                return false;

            var outer = outerSources.Select(SourceProvenance).ToArray();
            var inner = innerSources.Select(SourceProvenance).ToArray();
            if (outer.Any(value => value == null) || inner.Any(value => value == null) ||
                !outer.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(inner.OrderBy(value => value, StringComparer.Ordinal)))
                return false;

            var version = standalone.Value<string>("communityDragonVersion") ?? "";
            if (!string.Equals(version, embedded.Value<string>("communityDragonVersion") ?? "", StringComparison.Ordinal))
                return false;
            var supplemental = outerSources.Any(source =>
                (source as JObject)?.GetValue("isSupplemental", StringComparison.OrdinalIgnoreCase)?.Value<bool>() == true);
            return identity.IncludesCommunityDragon
                ? !string.IsNullOrWhiteSpace(version) && supplemental
                : string.IsNullOrWhiteSpace(version) && !supplemental;
        }

        private static string SourceProvenance(JToken token)
        {
            var source = token as JObject;
            var name = source?.GetValue("sourceName", StringComparison.OrdinalIgnoreCase)?.ToString();
            var url = source?.GetValue("url", StringComparison.OrdinalIgnoreCase)?.ToString();
            var sha = source?.GetValue("sha256", StringComparison.OrdinalIgnoreCase)?.ToString();
            var supplemental = source?.GetValue("isSupplemental", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) ||
                string.IsNullOrWhiteSpace(sha) || supplemental?.Type != JTokenType.Boolean)
                return null;
            return JsonConvert.SerializeObject(new object[] { name, url, sha, supplemental.Value<bool>() });
        }

        private static bool IsWebUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        private static bool HasPngSignature(byte[] bytes)
        {
            var signature = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
            return bytes != null && bytes.Length >= signature.Length &&
                   !signature.Where((value, index) => bytes[index] != value).Any();
        }

        private static StaticCatalog NormalizeForContentSnapshot(StaticCatalog catalog)
        {
            SourceStamp NormalizeSource(SourceStamp source)
            {
                return new SourceStamp(
                    source.SourceName,
                    source.Url,
                    DateTime.UnixEpoch,
                    "",
                    source.Sha256,
                    source.IsSupplemental);
            }

            var manifest = catalog.Manifest.Identity == null
                ? new PatchManifest(
                    1, catalog.Manifest.Patch, catalog.Manifest.SetName, DateTime.UnixEpoch, "",
                    catalog.Manifest.Sources.Select(NormalizeSource), DateTime.UnixEpoch,
                    catalog.Manifest.PolicyUrls, 0, 0, "", catalog.Manifest.CommunityDragonVersion)
                : new PatchManifest(
                catalog.Manifest.SchemaVersion,
                catalog.Manifest.Identity,
                catalog.Manifest.SetName,
                DateTime.UnixEpoch,
                "",
                catalog.Manifest.Sources.Select(NormalizeSource),
                DateTime.UnixEpoch,
                catalog.Manifest.PolicyUrls,
                0,
                0,
                "",
                catalog.Manifest.CommunityDragonVersion);
            var units = catalog.Units.Select(value => new UnitRecord(
                value.StableId,
                value.Name,
                value.Cost,
                value.TraitIds,
                value.IconPath,
                value.Sources.Select(NormalizeSource),
                value.Role,
                value.Description,
                value.Effects));
            var traits = catalog.Traits.Select(value => new TraitRecord(
                value.StableId,
                value.Name,
                value.Description,
                value.Effects,
                value.IconPath,
                value.Sources.Select(NormalizeSource)));
            var items = catalog.Items.Select(value => new ItemRecord(
                value.StableId,
                value.Name,
                value.Description,
                value.ComponentIds,
                value.Effects,
                value.IconPath,
                value.Sources.Select(NormalizeSource)));
            var augments = catalog.Augments.Select(value => new AugmentRecord(
                value.StableId,
                value.Name,
                value.Tier,
                value.Description,
                value.RelatedIds,
                value.Effects,
                value.IconPath,
                value.Sources.Select(NormalizeSource)));
            return new StaticCatalog(manifest, units, traits, items, augments);
        }

        private static StaticCatalog AsSchemaOne(StaticCatalog catalog)
        {
            var source = catalog.Manifest;
            var manifest = new PatchManifest(1, source.Patch, source.SetName, source.CreatedUtc,
                source.ContentHash, source.Sources, source.PolicyReviewDate, source.PolicyUrls,
                source.ValidationErrorCount, source.ValidationWarningCount,
                source.PreviousPatchSummary, source.CommunityDragonVersion);
            return new StaticCatalog(manifest, catalog.Units, catalog.Traits, catalog.Items, catalog.Augments);
        }
    }
}
