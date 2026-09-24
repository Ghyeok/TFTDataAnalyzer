using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.Persistence
{
    public enum LegacyCatalogStatus
    {
        NotFound,
        CompatibleOfficialLegacy,
        AmbiguousLegacy
    }

    public sealed class LegacyCatalogResolution
    {
        public LegacyCatalogStatus Status { get; }
        public string FinalDirectory { get; }
        public string CatalogAssetPath { get; }

        public LegacyCatalogResolution(LegacyCatalogStatus status, string finalDirectory, string catalogAssetPath)
        {
            Status = status;
            FinalDirectory = finalDirectory;
            CatalogAssetPath = catalogAssetPath;
        }
    }

    public sealed class LegacyCatalogResolver
    {
        private readonly StaticCatalogPaths _paths;

        public LegacyCatalogResolver(StaticCatalogPaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public LegacyCatalogResolution Resolve(CatalogIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            var canonical = new LegacyCatalogResolution(LegacyCatalogStatus.NotFound,
                _paths.CanonicalFinalDirectory(identity), _paths.CatalogAssetPath(identity));
            if (identity.SourceProfile != CatalogSourceProfile.OfficialOnly) return canonical;

            var legacy = _paths.LegacyFinalDirectory(identity.Patch);
            if (!Directory.Exists(legacy)) return canonical;

            var legacyAsset = _paths.LegacyCatalogAssetPath(identity.Patch);
            var ambiguous = new LegacyCatalogResolution(LegacyCatalogStatus.AmbiguousLegacy, legacy, legacyAsset);
            var manifestPath = Path.Combine(legacy, "manifest.json");
            var catalogPath = Path.Combine(legacy, "catalog.json");
            if (!File.Exists(manifestPath) && !File.Exists(catalogPath)) return canonical;
            if (!File.Exists(manifestPath) || !File.Exists(catalogPath)) return ambiguous;

            try
            {
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                if (manifest.Value<int?>("schemaVersion") != 1 ||
                    !Equal(manifest, "setId", identity.Patch.SetId) ||
                    manifest.Value<int?>("setNumber") != identity.Patch.SetNumber ||
                    !Equal(manifest, "patchId", identity.Patch.PatchId))
                {
                    return ambiguous;
                }

                var sourceVersion = manifest.Value<string>("sourceGameVersion");
                if (string.IsNullOrWhiteSpace(sourceVersion)) return ambiguous;
                if (!string.Equals(sourceVersion, identity.Patch.SourceGameVersion, StringComparison.Ordinal))
                    return canonical;

                if (!string.IsNullOrWhiteSpace(manifest.Value<string>("communityDragonVersion")))
                    return ambiguous;
                if (manifest["sourceProfile"] != null) return ambiguous;
                var sources = manifest["sources"] as JArray;
                if (sources == null || sources.Count == 0 || sources.Any(source => !IsOfficial(source as JObject)))
                    return ambiguous;
                var declaredHash = manifest.Value<string>("contentHash");
                var catalogBytes = File.ReadAllBytes(catalogPath);
                var actualHash = ContentHasher.Sha256Hex(catalogBytes);
                if (!string.Equals(declaredHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    return ambiguous;

                var embedded = JObject.Parse(System.Text.Encoding.UTF8.GetString(catalogBytes))["manifest"] as JObject;
                if (embedded == null || !IsMatchingOfficialManifest(embedded, identity))
                    return ambiguous;

                return new LegacyCatalogResolution(LegacyCatalogStatus.CompatibleOfficialLegacy,
                    legacy, legacyAsset);
            }
            catch (Exception error) when (error is Newtonsoft.Json.JsonException ||
                                          error is ArgumentException || error is InvalidCastException)
            {
                return ambiguous;
            }
        }

        private static bool Equal(JObject value, string name, string expected)
        {
            return string.Equals(value.Value<string>(name), expected, StringComparison.Ordinal);
        }

        private static bool IsMatchingOfficialManifest(JObject manifest, CatalogIdentity identity)
        {
            var sources = manifest["sources"] as JArray;
            return manifest.Value<int?>("schemaVersion") == 1 &&
                   Equal(manifest, "setId", identity.Patch.SetId) &&
                   manifest.Value<int?>("setNumber") == identity.Patch.SetNumber &&
                   Equal(manifest, "patchId", identity.Patch.PatchId) &&
                   Equal(manifest, "sourceGameVersion", identity.Patch.SourceGameVersion) &&
                   manifest["sourceProfile"] == null &&
                   string.IsNullOrWhiteSpace(manifest.Value<string>("communityDragonVersion")) &&
                   sources != null && sources.Count > 0 &&
                   sources.All(source => IsOfficial(source as JObject));
        }

        private static bool IsOfficial(JObject source)
        {
            if (source == null) return false;
            var name = source.GetValue("sourceName", StringComparison.OrdinalIgnoreCase)?.ToString();
            var supplemental = source.GetValue("isSupplemental", StringComparison.OrdinalIgnoreCase);
            var url = source.GetValue("url", StringComparison.OrdinalIgnoreCase)?.ToString();
            return string.Equals(name, "Riot Data Dragon", StringComparison.Ordinal) &&
                   supplemental?.Type == JTokenType.Boolean && !supplemental.Value<bool>() &&
                   Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   uri.Scheme == Uri.UriSchemeHttps &&
                   string.Equals(uri.Host, "ddragon.leagueoflegends.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}
