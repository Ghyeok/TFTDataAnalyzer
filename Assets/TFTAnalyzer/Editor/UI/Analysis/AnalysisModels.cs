using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        public string SummaryText { get; }
        internal CompositionDescriptor(AnonymousParticipant participant)
        {
            Fingerprint = CompositionFingerprintBuilder.Build(participant).Value;
            UnitIds = Copy(participant.Units.Select(x => x.StableId));
            TraitIds = Copy(participant.Traits.Where(x => x.IsActive).Select(x => x.StableId));
            SummaryText = string.Join(" + ", participant.Units.OrderBy(x => x.StableId, StringComparer.Ordinal)
                .Select(x => x.StableId)) +
                (TraitIds.Count == 0 ? "" : " | 활성 특성: " + string.Join(", ", TraitIds));
        }
        private static IReadOnlyList<string> Copy(IEnumerable<string> ids) =>
            Array.AsReadOnly(ids.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    public sealed class AnalysisSnapshot
    {
        public CatalogIdentity Identity { get; }
        public int MinimumSamples { get; }
        public IReadOnlyList<MetricRowViewModel> AllRows { get; }
        public IReadOnlyList<MetricRowViewModel> AllPairs { get; }
        public DateTimeOffset? FirstPlayedAt { get; }
        public DateTimeOffset? LastPlayedAt { get; }
        public IReadOnlyList<IReadOnlyList<string>> FilterOptions { get; }
        internal AnalysisData Data { get; }
        public AnalysisFilters AppliedFilters { get; }
        internal AnalysisSnapshot(CatalogIdentity identity, IEnumerable<MetricRowViewModel> rows,
            IEnumerable<MetricRowViewModel> pairs, int minimumSamples = MetricPolicy.MinimumSampleFloor,
            AnalysisData data = null, AnalysisFilters filters = null)
        {
            if (minimumSamples < MetricPolicy.MinimumSampleFloor) throw new ArgumentOutOfRangeException(nameof(minimumSamples));
            MinimumSamples = minimumSamples;
            Identity = identity;
            Data = data;
            AppliedFilters = filters ?? new AnalysisFilters();
            FilterOptions = data?.FilterOptions ?? Array.AsReadOnly(new IReadOnlyList<string>[7]);
            AllRows = Array.AsReadOnly(rows.ToArray()); AllPairs = Array.AsReadOnly(pairs.ToArray());
            if (AllRows.Count != 0)
            { FirstPlayedAt = AllRows.Min(x => x.Presentation.FirstPlayedAt); LastPlayedAt = AllRows.Max(x => x.Presentation.LastPlayedAt); }
        }
        internal AnalysisSnapshot Recalculate(AnalysisFilters filters, CancellationToken token) =>
            Data == null ? throw new InvalidOperationException("analysis_observations_unavailable") :
            AnalysisService.Recalculate(Data, filters ?? new AnalysisFilters(), token);
    }

    internal sealed class AnalysisData
    {
        internal StaticCatalog Catalog { get; }
        internal CatalogIdentity Identity { get; }
        internal AnonymousMatchObservation[] Observations { get; }
        internal MetricPolicy Policy { get; }
        internal IReadOnlyList<IReadOnlyList<string>> FilterOptions { get; }
        internal AnalysisData(StaticCatalog catalog, CatalogIdentity identity, AnonymousMatchObservation[] observations, MetricPolicy policy,
            CancellationToken token)
        {
            Catalog = catalog; Identity = identity; Observations = observations; Policy = policy;
            IEnumerable<T> Checked<T>(IEnumerable<T> values)
            {
                foreach (var value in values) { token.ThrowIfCancellationRequested(); yield return value; }
                token.ThrowIfCancellationRequested();
            }
            var participants = Checked(Checked(observations).SelectMany(x => x.Participants)).ToArray();
            IReadOnlyList<string> Values(IEnumerable<string> values)
            {
                var result = Checked(values).Where(x => !string.IsNullOrEmpty(x))
                    .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                token.ThrowIfCancellationRequested();
                return Array.AsReadOnly(result);
            }
            FilterOptions = Array.AsReadOnly(new[] {
                Values(observations.Select(x => x.Scope.Region)), Values(observations.Select(x => x.Scope.QueueId)),
                Values(observations.Select(x => x.Scope.TierBucket)), Values(participants.SelectMany(x => x.Units).Select(x => x.StableId)),
                Values(participants.SelectMany(x => x.Traits).Where(x => x.IsActive).Select(x => x.StableId)),
                Values(participants.SelectMany(x => x.Units).SelectMany(x => x.ItemIds)), Values(participants.SelectMany(x => x.AugmentIds)) });
        }
    }
}
