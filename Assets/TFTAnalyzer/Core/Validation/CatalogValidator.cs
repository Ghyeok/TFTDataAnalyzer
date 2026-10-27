using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Validation
{
    public sealed class CatalogValidator
    {
        private readonly string _generatedIconPrefix;

        public CatalogValidator(string generatedAssetDirectory = "Assets/TFTAnalyzer/Data/Generated")
        {
            if (generatedAssetDirectory == null || !Regex.IsMatch(generatedAssetDirectory,
                    @"\AAssets(?:/[A-Za-z0-9_-]+)+\z"))
                throw new ArgumentException("A normalized generated asset directory is required.", nameof(generatedAssetDirectory));
            _generatedIconPrefix = generatedAssetDirectory + "/Icons/";
        }

        public ValidationReport Validate(StaticCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var issues = new List<ValidationIssue>();
            ValidatePatch(catalog, issues);
            ValidateFieldOrigins(catalog, issues);
            ValidateStructuredValues(catalog, issues);
            ValidateDuplicates(catalog.Units.Select(value => value.StableId), "unit", issues);
            ValidateDuplicates(catalog.Traits.Select(value => value.StableId), "trait", issues);
            ValidateDuplicates(catalog.Items.Select(value => value.StableId), "item", issues);
            ValidateDuplicates(catalog.Augments.Select(value => value.StableId), "augment", issues);

            var traitIds = new HashSet<string>(
                catalog.Traits.Select(value => value.StableId),
                StringComparer.Ordinal);
            var itemIds = new HashSet<string>(
                catalog.Items.Select(value => value.StableId),
                StringComparer.Ordinal);
            var allIds = new HashSet<string>(
                catalog.Units.Select(value => value.StableId)
                    .Concat(catalog.Traits.Select(value => value.StableId))
                    .Concat(catalog.Items.Select(value => value.StableId))
                    .Concat(catalog.Augments.Select(value => value.StableId)),
                StringComparer.Ordinal);

            foreach (var unit in catalog.Units)
            {
                ValidateName(unit.StableId, unit.Name, "unit", issues);
                ValidateIcon(unit.StableId, unit.IconPath, issues);
                if (unit.Cost < 1 || unit.Cost > 9)
                {
                    Error(issues, "unit_cost_out_of_range", unit.StableId,
                        "Unit cost must be between 1 and 9.");
                }

                foreach (var traitId in unit.TraitIds.Where(id => !traitIds.Contains(id)))
                {
                    Error(issues, "missing_trait_reference", unit.StableId,
                        $"Unknown trait reference: {traitId}");
                }
            }

            foreach (var trait in catalog.Traits)
            {
                ValidateName(trait.StableId, trait.Name, "trait", issues);
                ValidateIcon(trait.StableId, trait.IconPath, issues);
            }

            foreach (var item in catalog.Items)
            {
                ValidateName(item.StableId, item.Name, "item", issues);
                ValidateIcon(item.StableId, item.IconPath, issues);
                foreach (var componentId in item.ComponentIds.Where(id => !itemIds.Contains(id)))
                {
                    Error(issues, "missing_item_reference", item.StableId,
                        $"Unknown item component reference: {componentId}");
                }
            }

            foreach (var augment in catalog.Augments)
            {
                ValidateName(augment.StableId, augment.Name, "augment", issues);
                ValidateIcon(augment.StableId, augment.IconPath, issues);
                foreach (var relatedId in augment.RelatedIds.Where(id => !allIds.Contains(id)))
                {
                    Error(issues, "missing_augment_reference", augment.StableId,
                        $"Unknown augment relation: {relatedId}");
                }
            }

            return new ValidationReport(issues);
        }

        private static void ValidatePatch(
            StaticCatalog catalog,
            ICollection<ValidationIssue> issues)
        {
            var patch = catalog.Manifest.Patch;
            if (string.IsNullOrWhiteSpace(patch.SetId) ||
                string.IsNullOrWhiteSpace(patch.PatchId) ||
                string.IsNullOrWhiteSpace(patch.SourceGameVersion) ||
                patch.SetNumber <= 0)
            {
                Error(issues, "invalid_patch_identity", "", "Patch identity is incomplete.");
            }
        }

        private static void ValidateFieldOrigins(StaticCatalog catalog, ICollection<ValidationIssue> issues)
        {
            var schema = catalog.Manifest.SchemaVersion;
            if (schema < 1 || schema > 4 || (schema < 3 && catalog.FieldOrigins.Count != 0))
                Error(issues, "invalid_field_origin_schema", "", "Unsupported schema or legacy field origins.");
            if (schema < 3 || schema > 4) return;

            string SourceKey(SourceStamp s) => $"{s.SourceName}\u001f{s.Url}\u001f{s.Sha256}\u001f{s.IsSupplemental}";
            var manifestSources = new HashSet<string>(catalog.Manifest.Sources.Select(SourceKey), StringComparer.Ordinal);
            var expected = new Dictionary<(string Type, string Id, string Pointer), IReadOnlyList<SourceStamp>>();
            void Record(string type, string id, object record, IReadOnlyList<SourceStamp> sources)
            {
                foreach (var pointer in FieldOriginFields.Required(record, schema)) expected[(type, id, pointer)] = sources;
            }
            foreach (var r in catalog.Units) Record("unit", r.StableId, r, r.Sources);
            foreach (var r in catalog.Traits) Record("trait", r.StableId, r, r.Sources);
            foreach (var r in catalog.Items) Record("item", r.StableId, r, r.Sources);
            foreach (var r in catalog.Augments) Record("augment", r.StableId, r, r.Sources);
            var seen = new HashSet<(string Type, string Id, string Pointer)>();
            foreach (var origin in catalog.FieldOrigins)
            {
                if (origin == null) { Error(issues, "invalid_field_origin", "", "Null field origin."); continue; }
                var key = (origin.RecordType, origin.StableId, origin.Pointer);
                if (!seen.Add(key)) Error(issues, "duplicate_field_origin", origin.StableId, origin.Pointer);
                if (!expected.TryGetValue(key, out var recordSources))
                { Error(issues, "invalid_field_origin", origin.StableId, "Unknown record or absent field: " + origin.Pointer); continue; }
                var recordKeys = new HashSet<string>(recordSources.Select(SourceKey), StringComparer.Ordinal);
                if (origin.Sources.Count == 0 || origin.Sources.Any(s => s == null ||
                    !manifestSources.Contains(SourceKey(s)) || !recordKeys.Contains(SourceKey(s)) ||
                    (catalog.Manifest.Identity?.IncludesCommunityDragon != true && s.IsSupplemental)))
                    Error(issues, "invalid_field_origin_source", origin.StableId, origin.Pointer);
            }
            foreach (var key in expected.Keys.Where(key => !seen.Contains(key)))
                Error(issues, "missing_field_origin", key.Id, key.Pointer);
            if (schema == 4)
            {
                void Extensions(string type, string id, IReadOnlyList<StaticExtensionField> values)
                {
                    for (var i = 0; i < values.Count; i++)
                    {
                        var origin = catalog.FieldOrigins.FirstOrDefault(x => x.RecordType == type && x.StableId == id && x.Pointer == "/extensions/" + i + "/canonicalJson");
                        if (origin != null && origin.Sources.Any(x => x.SourceName != values[i].SourceName))
                            Error(issues, "invalid_extension_origin_source", id, "Extension source name differs from its document origin.");
                    }
                }
                foreach (var x in catalog.Units) Extensions("unit", x.StableId, x.Extensions);
                foreach (var x in catalog.Traits) Extensions("trait", x.StableId, x.Extensions);
                foreach (var x in catalog.Items) Extensions("item", x.StableId, x.Extensions);
                foreach (var x in catalog.Augments) Extensions("augment", x.StableId, x.Extensions);
            }
        }

        private static void ValidateStructuredValues(StaticCatalog catalog, ICollection<ValidationIssue> issues)
        {
            void Effects(string id, IReadOnlyDictionary<string, double> values)
            {
                if (values.Values.Any(x => double.IsNaN(x) || double.IsInfinity(x)))
                    Error(issues, "invalid_static_effect", id, "Static effects must contain finite numeric values.");
            }
            foreach (var unit in catalog.Units)
            {
                Effects(unit.StableId, unit.Effects);
                if (unit.AbilityVariables.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != unit.AbilityVariables.Count)
                    Error(issues, "duplicate_static_series", unit.StableId, "Source numeric series names must be unique.");
            }
            foreach (var trait in catalog.Traits)
            {
                Effects(trait.StableId, trait.Effects);
                var known = trait.Breakpoints.Where(x => x.MinUnits.HasValue && x.MaxUnits.HasValue).ToArray();
                if (known.Select(x => (x.MinUnits, x.MaxUnits)).Distinct().Count() != known.Length)
                    Error(issues, "duplicate_trait_breakpoint", trait.StableId, "Known activation ranges must be unique.");
            }
            foreach (var item in catalog.Items) Effects(item.StableId, item.Effects);
            foreach (var augment in catalog.Augments) Effects(augment.StableId, augment.Effects);
        }

        private static void ValidateDuplicates(
            IEnumerable<string> ids,
            string kind,
            ICollection<ValidationIssue> issues)
        {
            foreach (var duplicate in ids
                         .GroupBy(id => id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .Select(group => group.Key))
            {
                Error(issues, $"duplicate_{kind}_id", duplicate,
                    $"Duplicate {kind} stable ID: {duplicate}");
            }
        }

        private static void ValidateName(
            string stableId,
            LocalizedText name,
            string kind,
            ICollection<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(name.KoKr) && string.IsNullOrWhiteSpace(name.EnUs))
            {
                Error(issues, $"blank_{kind}_name", stableId,
                    "Both Korean and English names are blank.");
            }
        }

        private void ValidateIcon(
            string stableId,
            string iconPath,
            ICollection<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "missing_icon",
                    stableId,
                    "No icon metadata was supplied."));
                return;
            }

            var isWeb = Uri.TryCreate(iconPath, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
            var isGeneratedAsset = iconPath.StartsWith(_generatedIconPrefix, StringComparison.Ordinal) &&
                Regex.IsMatch(iconPath.Substring(_generatedIconPrefix.Length), @"\A[A-Za-z0-9_-]+\.png\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!isWeb && !isGeneratedAsset)
            {
                Error(issues, "invalid_icon_metadata", stableId,
                    "Icon must be an HTTP(S) URL or a generated icon asset path.");
            }
        }

        private static void Error(
            ICollection<ValidationIssue> issues,
            string code,
            string recordId,
            string message)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, code, recordId, message));
        }
    }
}
