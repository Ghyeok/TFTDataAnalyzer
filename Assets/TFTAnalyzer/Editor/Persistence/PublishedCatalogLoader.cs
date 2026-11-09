using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Persistence
{
    public sealed class PublishedCatalogLoader
    {
        private readonly StaticCatalogPaths _paths;

        public PublishedCatalogLoader(StaticCatalogPaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public IReadOnlyList<PatchRowViewModel> Load(CatalogIndex index)
        {
            if (index == null) return Array.Empty<PatchRowViewModel>();
            var rows = index.Entries.Select(Read).ToArray();
            return Array.AsReadOnly(rows.Select(row => WithSummary(row, rows)).ToArray());
        }

        private static PatchRowViewModel WithSummary(PatchRowViewModel row, IReadOnlyList<PatchRowViewModel> rows)
        {
            if (!row.IsSelectable) return row;
            string summary;
            try
            {
                var metadata = JObject.Parse(File.ReadAllText(Path.Combine(row.Directory, "manifest.json")));
                var token = metadata["previousPatchSummary"];
                summary = token == null || token.Type == JTokenType.Null ? "변경 요약 없음" :
                    token.Type == JTokenType.String ? CatalogChangeSummary.Describe(token.Value<string>(), row, rows) :
                    "변경 요약을 읽거나 검증할 수 없음";
            }
            catch (Exception) { summary = "변경 요약을 읽거나 검증할 수 없음"; }
            return new PatchRowViewModel(row.Entry, row.Identity, row.Directory, row.Catalog, row.RetrievedUtc,
                row.ValidationErrorCount, row.ValidationWarningCount, row.ErrorCode, summary);
        }

        private PatchRowViewModel Read(CatalogIndexEntry entry)
        {
            CatalogIdentity identity = null;
            var directory = "";
            try
            {
                var resolver = new LegacyCatalogResolver(_paths);
                identity = entry.ResolveIdentity(resolver);
                var canonical = _paths.CatalogAssetPath(identity);
                var legacy = false;
                if (!string.Equals(entry.CatalogAssetPath, canonical, StringComparison.Ordinal))
                {
                    var resolution = resolver.Resolve(identity);
                    if (resolution.Status != LegacyCatalogStatus.CompatibleOfficialLegacy ||
                        !string.Equals(entry.CatalogAssetPath, resolution.CatalogAssetPath, StringComparison.Ordinal))
                        throw new InvalidDataException();
                    directory = resolution.FinalDirectory;
                    legacy = true;
                }
                else directory = _paths.CanonicalFinalDirectory(identity);

                var hash = CatalogAssetWriter.VerifyPublishedHash(directory, identity, legacy);
                if (!string.Equals(hash, entry.ContentHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException();
                var catalog = CanonicalCatalogSerializer.Deserialize(File.ReadAllText(Path.Combine(directory, "catalog.json")));
                var validation = new CatalogValidator(_paths.GeneratedRelativeDirectory).Validate(catalog);
                var metadata = JObject.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
                var time = metadata.GetValue("createdUtc", StringComparison.OrdinalIgnoreCase);
                if (time == null || (time.Type != JTokenType.Date && time.Type != JTokenType.String))
                    throw new InvalidDataException();
                var retrieved = time.Value<DateTime>();
                if (retrieved.Kind == DateTimeKind.Unspecified) throw new InvalidDataException();
                var errors = ReadCount(metadata, "validationErrorCount");
                var warnings = ReadCount(metadata, "validationWarningCount");
                return new PatchRowViewModel(entry, identity, directory,
                    validation.CanPublish && errors == 0 ? catalog : null, retrieved.ToUniversalTime(),
                    Math.Max(errors, validation.ErrorCount), warnings,
                    validation.CanPublish && errors == 0 ? "" : "LocalValidationFailed");
            }
            catch (Exception)
            {
                // Local failures stay isolated to the row. Exception text can contain private paths/data.
                return new PatchRowViewModel(entry, identity, directory, null, DateTime.UnixEpoch,
                    0, 0, "LocalIntegrityFailed");
            }
        }

        private static int ReadCount(JObject metadata, string name)
        {
            var token = metadata.GetValue(name, StringComparison.OrdinalIgnoreCase);
            if (token?.Type != JTokenType.Integer) throw new InvalidDataException();
            var count = token.Value<int>();
            if (count < 0) throw new InvalidDataException();
            return count;
        }
    }
}
