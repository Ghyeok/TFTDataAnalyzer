using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.UI.Analysis
{
    public sealed class AnalysisFilters
    {
        public string Region { get; }
        public string Queue { get; }
        public string Tier { get; }
        public string Unit { get; }
        public string Trait { get; }
        public string Item { get; }
        public string Augment { get; }
        public AnalysisFilters(string region = null, string queue = null, string tier = null,
            string unit = null, string trait = null, string item = null, string augment = null)
        { Region = region; Queue = queue; Tier = tier; Unit = unit; Trait = trait; Item = item; Augment = augment; }
    }

    public sealed class CompositionDescriptor
    {
        public string Fingerprint { get; }
        public IReadOnlyList<string> UnitIds { get; }
        public IReadOnlyList<string> TraitIds { get; }
        public IReadOnlyList<string> ItemIds { get; }
        public IReadOnlyList<string> AugmentIds { get; }
        public string SummaryText { get; }
        internal CompositionDescriptor(AnonymousParticipant participant)
        {
            Fingerprint = CompositionFingerprintBuilder.Build(participant).Value;
            UnitIds = Copy(participant.Units.Select(x => x.StableId));
            TraitIds = Copy(participant.Traits.Where(x => x.IsActive).Select(x => x.StableId));
            ItemIds = Copy(participant.Units.SelectMany(x => x.ItemIds));
            AugmentIds = Copy(participant.AugmentIds);
            SummaryText = string.Join(" + ", participant.Units.OrderBy(x => x.StableId, StringComparer.Ordinal)
                .ThenBy(x => x.Tier).Select(x => x.StableId + " ★" + x.Tier +
                    (x.ItemIds.Count == 0 ? "" : " (" + string.Join(", ", x.ItemIds.OrderBy(id => id, StringComparer.Ordinal)) + ")"))) +
                (TraitIds.Count == 0 ? "" : " | 활성 특성: " + string.Join(", ", TraitIds)) +
                (AugmentIds.Count == 0 ? "" : " | 증강: " + string.Join(", ", AugmentIds));
        }
        private static IReadOnlyList<string> Copy(IEnumerable<string> ids) =>
            Array.AsReadOnly(ids.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    public sealed class AnalysisSnapshot
    {
        public CatalogIdentity Identity { get; }
        public IReadOnlyList<MetricRowViewModel> AllRows { get; }
        public IReadOnlyList<MetricRowViewModel> AllPairs { get; }
        public DateTimeOffset? FirstPlayedAt { get; }
        public DateTimeOffset? LastPlayedAt { get; }
        internal AnalysisSnapshot(CatalogIdentity identity, IEnumerable<MetricRowViewModel> rows,
            IEnumerable<MetricRowViewModel> pairs)
        {
            Identity = identity;
            AllRows = Array.AsReadOnly(rows.ToArray()); AllPairs = Array.AsReadOnly(pairs.ToArray());
            if (AllRows.Count != 0)
            { FirstPlayedAt = AllRows.Min(x => x.Presentation.FirstPlayedAt); LastPlayedAt = AllRows.Max(x => x.Presentation.LastPlayedAt); }
        }
    }
}
