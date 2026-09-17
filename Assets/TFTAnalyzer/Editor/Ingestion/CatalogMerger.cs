using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion
{
    public enum MergeResolution
    {
        SupplementFill,
        OfficialWins
    }

    public sealed class MergeConflict
    {
        public string RecordType { get; }
        public string StableId { get; }
        public string FieldName { get; }
        public string OfficialValue { get; }
        public string SupplementValue { get; }
        public MergeResolution Resolution { get; }

        public MergeConflict(
            string recordType,
            string stableId,
            string fieldName,
            string officialValue,
            string supplementValue,
            MergeResolution resolution)
        {
            RecordType = recordType;
            StableId = stableId;
            FieldName = fieldName;
            OfficialValue = officialValue ?? "";
            SupplementValue = supplementValue ?? "";
            Resolution = resolution;
        }
    }

    public sealed class MergeResult
    {
        public StaticCatalog Catalog { get; }
        public IReadOnlyList<MergeConflict> Conflicts { get; }

        public MergeResult(StaticCatalog catalog, IEnumerable<MergeConflict> conflicts)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Conflicts = Array.AsReadOnly((conflicts ?? Enumerable.Empty<MergeConflict>())
                .OrderBy(value => value.RecordType, StringComparer.Ordinal)
                .ThenBy(value => value.StableId, StringComparer.Ordinal)
                .ThenBy(value => value.FieldName, StringComparer.Ordinal)
                .ThenBy(value => value.Resolution)
                .ToArray());
        }
    }

    public sealed class CatalogMerger
    {
        public MergeResult Merge(StaticCatalog official, StaticCatalog supplement)
        {
            if (official == null || supplement == null)
            {
                throw new ArgumentNullException(official == null ? nameof(official) : nameof(supplement));
            }

            var decisions = new List<MergeConflict>();
            var units = MergeRecords(
                official.Units,
                supplement.Units,
                value => value.StableId,
                (left, right) => MergeUnit(left, right, decisions),
                "unit",
                decisions);
            var traits = MergeRecords(
                official.Traits,
                supplement.Traits,
                value => value.StableId,
                (left, right) => MergeTrait(left, right, decisions),
                "trait",
                decisions);
            var items = MergeRecords(
                official.Items,
                supplement.Items,
                value => value.StableId,
                (left, right) => MergeItem(left, right, decisions),
                "item",
                decisions);
            var augments = MergeRecords(
                official.Augments,
                supplement.Augments,
                value => value.StableId,
                (left, right) => MergeAugment(left, right, decisions),
                "augment",
                decisions);

            var manifest = new PatchManifest(
                official.Manifest.SchemaVersion,
                official.Manifest.Patch,
                string.IsNullOrWhiteSpace(official.Manifest.SetName)
                    ? supplement.Manifest.SetName
                    : official.Manifest.SetName,
                official.Manifest.CreatedUtc,
                official.Manifest.ContentHash,
                MergeSources(official.Manifest.Sources, supplement.Manifest.Sources),
                official.Manifest.PolicyReviewDate,
                official.Manifest.PolicyUrls.Concat(supplement.Manifest.PolicyUrls),
                official.Manifest.ValidationErrorCount,
                official.Manifest.ValidationWarningCount,
                official.Manifest.PreviousPatchSummary,
                supplement.Manifest.CommunityDragonVersion);
            return new MergeResult(
                new StaticCatalog(manifest, units, traits, items, augments),
                decisions);
        }

        private static IEnumerable<T> MergeRecords<T>(
            IEnumerable<T> official,
            IEnumerable<T> supplement,
            Func<T, string> id,
            Func<T, T, T> merge,
            string recordType,
            ICollection<MergeConflict> decisions)
        {
            var left = official.ToDictionary(id, StringComparer.Ordinal);
            var right = supplement.ToDictionary(id, StringComparer.Ordinal);
            foreach (var stableId in left.Keys.Concat(right.Keys)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                var hasLeft = left.TryGetValue(stableId, out var officialRecord);
                var hasRight = right.TryGetValue(stableId, out var supplementRecord);
                if (!hasLeft)
                {
                    decisions.Add(new MergeConflict(
                        recordType,
                        stableId,
                        "record",
                        "",
                        "present",
                        MergeResolution.SupplementFill));
                    yield return supplementRecord;
                }
                else if (!hasRight)
                {
                    yield return officialRecord;
                }
                else
                {
                    yield return merge(officialRecord, supplementRecord);
                }
            }
        }

        private static UnitRecord MergeUnit(
            UnitRecord official,
            UnitRecord supplement,
            ICollection<MergeConflict> decisions)
        {
            var type = "unit";
            return new UnitRecord(
                official.StableId,
                MergeText(type, official.StableId, "name", official.Name, supplement.Name, decisions),
                ChooseNumber(type, official.StableId, "cost", official.Cost, supplement.Cost, decisions),
                ChooseList(type, official.StableId, "traitIds", official.TraitIds, supplement.TraitIds, decisions),
                ChooseText(type, official.StableId, "iconPath", official.IconPath, supplement.IconPath, decisions),
                MergeSources(official.Sources, supplement.Sources),
                ChooseText(type, official.StableId, "role", official.Role, supplement.Role, decisions),
                MergeText(type, official.StableId, "description", official.Description, supplement.Description, decisions),
                MergeEffects(type, official.StableId, official.Effects, supplement.Effects, decisions));
        }

        private static TraitRecord MergeTrait(
            TraitRecord official,
            TraitRecord supplement,
            ICollection<MergeConflict> decisions)
        {
            var type = "trait";
            return new TraitRecord(
                official.StableId,
                MergeText(type, official.StableId, "name", official.Name, supplement.Name, decisions),
                MergeText(type, official.StableId, "description", official.Description, supplement.Description, decisions),
                MergeEffects(type, official.StableId, official.Effects, supplement.Effects, decisions),
                ChooseText(type, official.StableId, "iconPath", official.IconPath, supplement.IconPath, decisions),
                MergeSources(official.Sources, supplement.Sources));
        }

        private static ItemRecord MergeItem(
            ItemRecord official,
            ItemRecord supplement,
            ICollection<MergeConflict> decisions)
        {
            var type = "item";
            return new ItemRecord(
                official.StableId,
                MergeText(type, official.StableId, "name", official.Name, supplement.Name, decisions),
                MergeText(type, official.StableId, "description", official.Description, supplement.Description, decisions),
                ChooseList(type, official.StableId, "componentIds", official.ComponentIds, supplement.ComponentIds, decisions),
                MergeEffects(type, official.StableId, official.Effects, supplement.Effects, decisions),
                ChooseText(type, official.StableId, "iconPath", official.IconPath, supplement.IconPath, decisions),
                MergeSources(official.Sources, supplement.Sources));
        }

        private static AugmentRecord MergeAugment(
            AugmentRecord official,
            AugmentRecord supplement,
            ICollection<MergeConflict> decisions)
        {
            var type = "augment";
            return new AugmentRecord(
                official.StableId,
                MergeText(type, official.StableId, "name", official.Name, supplement.Name, decisions),
                ChooseText(type, official.StableId, "tier", official.Tier, supplement.Tier, decisions),
                MergeText(type, official.StableId, "description", official.Description, supplement.Description, decisions),
                ChooseList(type, official.StableId, "relatedIds", official.RelatedIds, supplement.RelatedIds, decisions),
                MergeEffects(type, official.StableId, official.Effects, supplement.Effects, decisions),
                ChooseText(type, official.StableId, "iconPath", official.IconPath, supplement.IconPath, decisions),
                MergeSources(official.Sources, supplement.Sources));
        }

        private static LocalizedText MergeText(
            string type,
            string id,
            string field,
            LocalizedText official,
            LocalizedText supplement,
            ICollection<MergeConflict> decisions)
        {
            return new LocalizedText(
                ChooseText(type, id, $"{field}.koKr", official.KoKr, supplement.KoKr, decisions),
                ChooseText(type, id, $"{field}.enUs", official.EnUs, supplement.EnUs, decisions));
        }

        private static string ChooseText(
            string type,
            string id,
            string field,
            string official,
            string supplement,
            ICollection<MergeConflict> decisions)
        {
            if (string.IsNullOrWhiteSpace(official) && !string.IsNullOrWhiteSpace(supplement))
            {
                decisions.Add(Decision(type, id, field, official, supplement, MergeResolution.SupplementFill));
                return supplement;
            }

            if (!string.IsNullOrWhiteSpace(official) &&
                !string.IsNullOrWhiteSpace(supplement) &&
                !string.Equals(official, supplement, StringComparison.Ordinal))
            {
                decisions.Add(Decision(type, id, field, official, supplement, MergeResolution.OfficialWins));
            }

            return official ?? "";
        }

        private static int ChooseNumber(
            string type,
            string id,
            string field,
            int official,
            int supplement,
            ICollection<MergeConflict> decisions)
        {
            if (official <= 0 && supplement > 0)
            {
                decisions.Add(Decision(type, id, field, official.ToString(), supplement.ToString(), MergeResolution.SupplementFill));
                return supplement;
            }

            if (official > 0 && supplement > 0 && official != supplement)
            {
                decisions.Add(Decision(type, id, field, official.ToString(), supplement.ToString(), MergeResolution.OfficialWins));
            }

            return official;
        }

        private static IReadOnlyList<string> ChooseList(
            string type,
            string id,
            string field,
            IReadOnlyList<string> official,
            IReadOnlyList<string> supplement,
            ICollection<MergeConflict> decisions)
        {
            var officialValue = StableList(official);
            var supplementValue = StableList(supplement);
            if (official.Count == 0 && supplement.Count > 0)
            {
                decisions.Add(Decision(type, id, field, officialValue, supplementValue, MergeResolution.SupplementFill));
                return supplement;
            }

            if (official.Count > 0 && supplement.Count > 0 && officialValue != supplementValue)
            {
                decisions.Add(Decision(type, id, field, officialValue, supplementValue, MergeResolution.OfficialWins));
            }

            return official;
        }

        private static IReadOnlyDictionary<string, double> MergeEffects(
            string type,
            string id,
            IReadOnlyDictionary<string, double> official,
            IReadOnlyDictionary<string, double> supplement,
            ICollection<MergeConflict> decisions)
        {
            var merged = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in official)
            {
                merged[pair.Key] = pair.Value;
            }

            foreach (var pair in supplement.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (!merged.TryGetValue(pair.Key, out var officialValue))
                {
                    merged[pair.Key] = pair.Value;
                    decisions.Add(Decision(
                        type,
                        id,
                        $"effects.{pair.Key}",
                        "",
                        Format(pair.Value),
                        MergeResolution.SupplementFill));
                }
                else if (!officialValue.Equals(pair.Value))
                {
                    decisions.Add(Decision(
                        type,
                        id,
                        $"effects.{pair.Key}",
                        Format(officialValue),
                        Format(pair.Value),
                        MergeResolution.OfficialWins));
                }
            }

            return merged;
        }

        private static IReadOnlyList<SourceStamp> MergeSources(
            IEnumerable<SourceStamp> official,
            IEnumerable<SourceStamp> supplement)
        {
            return official.Concat(supplement)
                .GroupBy(source => $"{source.SourceName}\u001f{source.Url}\u001f{source.Sha256}", StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(source => source.SourceName, StringComparer.Ordinal)
                .ThenBy(source => source.Url, StringComparer.Ordinal)
                .ToArray();
        }

        private static MergeConflict Decision(
            string type,
            string id,
            string field,
            string official,
            string supplement,
            MergeResolution resolution)
        {
            return new MergeConflict(type, id, field, official, supplement, resolution);
        }

        private static string StableList(IEnumerable<string> values)
        {
            return string.Join(",", values.OrderBy(value => value, StringComparer.Ordinal));
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
