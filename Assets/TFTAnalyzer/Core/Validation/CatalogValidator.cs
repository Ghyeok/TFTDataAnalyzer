using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Validation
{
    public sealed class CatalogValidator
    {
        public ValidationReport Validate(StaticCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var issues = new List<ValidationIssue>();
            ValidatePatch(catalog, issues);
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

        private static void ValidateIcon(
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
            var isGeneratedAsset = iconPath.StartsWith(
                "Assets/TFTAnalyzer/Data/Generated/Icons/",
                StringComparison.Ordinal) && iconPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
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
