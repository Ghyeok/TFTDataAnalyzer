using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Diff;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Persistence
{
    // Standalone manifest metadata is not covered by the catalog content hash.
    internal static class CatalogChangeSummary
    {
        private const int MaxLength = 8192;

        internal static StaticCatalog LoadBaseline(StaticCatalogPaths paths, CatalogIdentity baseline,
            string expectedHash, CatalogIdentity target)
        {
            if (baseline == null || target == null || baseline.Equals(target) ||
                baseline.Patch.SetId != target.Patch.SetId || baseline.Patch.SetNumber != target.Patch.SetNumber ||
                baseline.SourceProfile != target.SourceProfile || !IsHash(expectedHash))
                throw new InvalidDataException("Invalid comparison baseline.");
            var canonical = paths.CanonicalFinalDirectory(baseline);
            if (Directory.Exists(canonical))
            {
                try { return ReadVerified(paths, canonical, baseline, expectedHash, false); }
                catch (Exception error) when (error is InvalidDataException || error is IOException || error is JsonException ||
                    error is InvalidOperationException || error is ArgumentException) { }
            }
            var resolution = new LegacyCatalogResolver(paths).Resolve(baseline);
            if (resolution.Status != LegacyCatalogStatus.CompatibleOfficialLegacy)
                throw new InvalidDataException("Comparison baseline is unavailable or changed.");
            return ReadVerified(paths, resolution.FinalDirectory, baseline, expectedHash, true);
        }

        private static StaticCatalog ReadVerified(StaticCatalogPaths paths, string directory,
            CatalogIdentity baseline, string expectedHash, bool legacy)
        {
            var actualHash = CatalogAssetWriter.VerifyPublishedHash(directory, baseline, legacy);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Comparison baseline changed.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, "catalog.json"));
            if (!string.Equals(ContentHasher.Sha256Hex(bytes), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Comparison baseline changed while reading.");
            var catalog = CanonicalCatalogSerializer.Deserialize(System.Text.Encoding.UTF8.GetString(bytes));
            if (!new CatalogValidator(paths.GeneratedRelativeDirectory).Validate(catalog).CanPublish)
                throw new InvalidDataException("Comparison baseline is invalid.");
            return catalog;
        }

        internal static string Create(StaticCatalog baseline, CatalogIdentity baselineIdentity, string baselineHash,
            StaticCatalog target, CatalogIdentity targetIdentity, string targetHash)
        {
            var diff = new CatalogDiffer().Compare(baseline, target);
            return new JObject {
                ["version"] = 1,
                ["baseline"] = Identity(baselineIdentity, baselineHash),
                ["target"] = Identity(targetIdentity, targetHash),
                ["units"] = Counts(diff.Units), ["traits"] = Counts(diff.Traits),
                ["items"] = Counts(diff.Items), ["augments"] = Counts(diff.Augments)
            }.ToString(Formatting.None);
        }

        internal static string Describe(string raw, PatchRowViewModel target, System.Collections.Generic.IReadOnlyList<PatchRowViewModel> rows)
        {
            if (string.IsNullOrEmpty(raw)) return "변경 요약 없음";
            try
            {
                if (raw.Length > MaxLength) throw new InvalidDataException();
                JObject obj;
                using (var reader = new JsonTextReader(new StringReader(raw)))
                {
                    obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException();
                }
                if (obj.Count != 7 || obj.Value<int?>("version") != 1 ||
                    !JToken.DeepEquals(obj["target"], Identity(target.Identity, target.Entry.ContentHash)))
                    throw new InvalidDataException();
                var baselineData = obj["baseline"] as JObject;
                if (baselineData == null || baselineData.Count != 6) throw new InvalidDataException();
                var patch = new PatchKey(baselineData.Value<string>("setId"), baselineData.Value<int>("setNumber"),
                    baselineData.Value<string>("patchId"), baselineData.Value<string>("sourceGameVersion"));
                var identity = new CatalogIdentity(patch, CatalogSourceProfileIds.Parse(baselineData.Value<string>("sourceProfile")));
                var hash = baselineData.Value<string>("contentHash");
                if (!IsHash(hash) || !JToken.DeepEquals(baselineData, Identity(identity, hash)) ||
                    identity.Equals(target.Identity) || patch.SetId != target.Identity.Patch.SetId ||
                    patch.SetNumber != target.Identity.Patch.SetNumber || identity.SourceProfile != target.Identity.SourceProfile)
                    throw new InvalidDataException();
                foreach (var category in new[] { "units", "traits", "items", "augments" })
                {
                    var counts = obj[category] as JObject;
                    if (counts == null || counts.Count != 3 || new[] { "added", "changed", "removed" }.Any(key =>
                        counts[key]?.Type != JTokenType.Integer || counts.Value<long>(key) < 0 || counts.Value<long>(key) > 1000000))
                        throw new InvalidDataException();
                }
                var before = rows.FirstOrDefault(row => row.IsSelectable && row.Identity.Equals(identity) &&
                    row.Identity.Patch.SetNumber == identity.Patch.SetNumber &&
                    string.Equals(row.Entry.ContentHash, hash, StringComparison.OrdinalIgnoreCase));
                if (before == null) return "저장된 변경 요약: 기준을 찾을 수 없어 재검증할 수 없음";
                var expected = JObject.Parse(Create(before.Catalog, identity, hash, target.Catalog, target.Identity, target.Entry.ContentHash));
                if (!JToken.DeepEquals(obj, expected)) throw new InvalidDataException();
                string Label(string name, string key) => $"{name} +{obj[key]["added"]} / 변경 {obj[key]["changed"]} / -{obj[key]["removed"]}";
                return $"비교 기준: {before.DisplayIdentity} · 해시 {before.ContentHashPrefix}\n" +
                    string.Join(" · ", new[] { Label("기물", "units"), Label("시너지", "traits"), Label("아이템", "items"), Label("증강", "augments") });
            }
            catch (Exception) { return "변경 요약을 읽거나 검증할 수 없음"; }
        }

        private static JObject Identity(CatalogIdentity id, string hash) => new JObject {
            ["setId"] = id.Patch.SetId, ["setNumber"] = id.Patch.SetNumber, ["patchId"] = id.Patch.PatchId,
            ["sourceGameVersion"] = id.Patch.SourceGameVersion, ["sourceProfile"] = id.ProfileId, ["contentHash"] = hash
        };
        private static JObject Counts(EntityDiff diff) => new JObject {
            ["added"] = diff.AddedIds.Count, ["changed"] = diff.Changed.Count, ["removed"] = diff.RemovedIds.Count
        };
        private static bool IsHash(string value) => value != null && value.Length == 64 &&
            value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');
    }
}
