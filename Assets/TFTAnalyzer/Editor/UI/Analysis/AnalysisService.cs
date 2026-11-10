using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Compliance;
using TFTAnalyzer.Editor.Persistence;

namespace TFTAnalyzer.Editor.UI.Analysis
{
    public interface IAnalysisService
    {
        Task<AnalysisSnapshot> LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token, int? minimumSamples = null);
    }
    public sealed class AnalysisService : IAnalysisService
    {
        private readonly ObservationStore _observations;
        private readonly AggregateStore _aggregates;
        private readonly MetricPolicy _policy;
        private readonly IAugmentPolicyMetadataProvider _provider;
        public AnalysisService(ObservationStore observations, AggregateStore aggregates = null, MetricPolicy policy = null,
            IAugmentPolicyMetadataProvider provider = null)
        {
            if (policy != null && provider != null) throw new ArgumentException("analysis_policy_configuration_invalid");
            _observations = observations ?? throw new ArgumentNullException(nameof(observations)); _aggregates = aggregates;
            _policy = policy ?? new MetricPolicy(); _provider = provider;
        }

        // Recovery/index and aggregate-cache writes happen only behind explicit local Refresh.
        public Task<AnalysisSnapshot> LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token, int? minimumSamples = null)
        {
            if (minimumSamples.HasValue && minimumSamples.Value < MetricPolicy.MinimumSampleFloor)
                throw new ArgumentOutOfRangeException(nameof(minimumSamples));
            if (!MatchesCatalog(catalog, identity)) throw new ArgumentException("analysis_catalog_invalid");
            var effectiveMinimum = Math.Max(_policy.MinimumSamples, minimumSamples ?? _policy.MinimumSamples);
            return AnalyzerStoreOperations.Shared.RunAsync(() => Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();
                var policy = _policy.WithMinimumSamples(effectiveMinimum);
                if (_provider != null)
                {
                    var read = _provider.Load(catalog, identity, token);
                    token.ThrowIfCancellationRequested();
                    policy = new MetricPolicy(effectiveMinimum, read?.State == AugmentPolicyLoadState.Loaded ? read.Metadata : null);
                }
                var observations = new List<AnonymousMatchObservation>();
                await foreach (var observation in _observations.ReadAllAsync(token))
                {
                    token.ThrowIfCancellationRequested();
                    if (SameIdentity(observation.Scope.ActiveCatalog, identity)) observations.Add(observation);
                }
                token.ThrowIfCancellationRequested();
                var data = new AnalysisData(catalog, identity, observations.ToArray(), policy, token);
                var aggregate = AggregateBuilder.Build(observations, null, token);
                var result = Present(data, observations, aggregate, new AnalysisFilters(), token);
                token.ThrowIfCancellationRequested();
                if (_aggregates != null)
                    await _aggregates.SaveGenerationAsync(aggregate.Scopes, token);
                return result;
            }, token), token);
        }

        public static AnalysisSnapshot BuildSnapshot(StaticCatalog catalog, CatalogIdentity identity,
            IEnumerable<AnonymousMatchObservation> observations, MetricPolicy policy = null, CancellationToken token = default)
        {
            if (!MatchesCatalog(catalog, identity)) throw new ArgumentException("analysis_catalog_invalid");
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            var copy = new List<AnonymousMatchObservation>();
            foreach (var observation in observations)
            {
                token.ThrowIfCancellationRequested();
                if (observation != null && SameIdentity(observation.Scope.ActiveCatalog, identity)) copy.Add(observation);
            }
            token.ThrowIfCancellationRequested();
            var data = new AnalysisData(catalog, identity, copy.ToArray(), policy ?? new MetricPolicy(), token);
            return Present(data, copy, AggregateBuilder.Build(copy, null, token), new AnalysisFilters(), token);
        }

        internal static AnalysisSnapshot Recalculate(AnalysisData data, AnalysisFilters filters, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            bool Match(string selected, string actual) => string.IsNullOrEmpty(selected) || string.Equals(selected, actual, StringComparison.Ordinal);
            var scoped = new List<AnonymousMatchObservation>();
            foreach (var observation in data.Observations)
            {
                token.ThrowIfCancellationRequested();
                if (Match(filters.Region, observation.Scope.Region) && Match(filters.Queue, observation.Scope.QueueId) &&
                    Match(filters.Tier, observation.Scope.TierBucket)) scoped.Add(observation);
            }
            token.ThrowIfCancellationRequested();
            bool ParticipantMatches(AnonymousParticipant participant)
            {
                token.ThrowIfCancellationRequested();
                var units = participant.Units.Where(unit => Match(filters.Unit, unit.StableId));
                return ((string.IsNullOrEmpty(filters.Unit) && string.IsNullOrEmpty(filters.Item)) ||
                        units.Any(unit => string.IsNullOrEmpty(filters.Item) || unit.ItemIds.Contains(filters.Item, StringComparer.Ordinal))) &&
                    (string.IsNullOrEmpty(filters.Trait) || participant.Traits.Any(trait => trait.IsActive && trait.StableId == filters.Trait)) &&
                    (string.IsNullOrEmpty(filters.Augment) || participant.AugmentIds.Contains(filters.Augment, StringComparer.Ordinal));
            }
            var aggregate = AggregateBuilder.Build(scoped, ParticipantMatches, token);
            return Present(data, scoped, aggregate, filters, token);
        }

        private static AnalysisSnapshot Present(AnalysisData data,
            IEnumerable<AnonymousMatchObservation> observations, AggregateSnapshot aggregate, AnalysisFilters filters, CancellationToken token)
        {
            var descriptors = new Dictionary<string, CompositionDescriptor>(StringComparer.Ordinal);
            foreach (var participant in observations.SelectMany(x => x.Participants))
            {
                token.ThrowIfCancellationRequested();
                var descriptor = new CompositionDescriptor(participant);
                descriptors[descriptor.Fingerprint] = descriptor;
            }
            var rows = new List<MetricRowViewModel>(); var pairs = new List<MetricRowViewModel>();
            foreach (var scope in aggregate.Scopes)
            {
                token.ThrowIfCancellationRequested();
                foreach (var metric in scope.Metrics)
                {
                    var presentation = data.Policy.Evaluate(metric, data.Catalog);
                    if (presentation.Scope == null) throw new InvalidOperationException("analysis_scope_invalid");
                    rows.Add(new MetricRowViewModel(presentation, descriptors[metric.Fingerprint.Value]));
                }
                foreach (var pair in scope.Pairs)
                {
                    var presentation = data.Policy.Evaluate(pair, data.Catalog);
                    if (presentation.Scope == null) throw new InvalidOperationException("analysis_scope_invalid");
                    pairs.Add(new MetricRowViewModel(presentation, descriptors[pair.First.Value], descriptors[pair.Second.Value]));
                }
            }
            return new AnalysisSnapshot(data.Identity, rows, pairs, data.Policy.MinimumSamples, data, filters);
        }

        internal static bool SameIdentity(CatalogIdentity first, CatalogIdentity second) =>
            first != null && second != null && first.Equals(second) && first.Patch.SetNumber == second.Patch.SetNumber;

        internal static bool MatchesCatalog(StaticCatalog catalog, CatalogIdentity identity)
        {
            if (catalog == null || identity == null) return false;
            var patch = catalog.Manifest.Patch; var supplied = identity.Patch;
            if (patch.SetNumber != supplied.SetNumber || patch.SetId != supplied.SetId ||
                patch.PatchId != supplied.PatchId || patch.SourceGameVersion != supplied.SourceGameVersion) return false;
            return catalog.Manifest.SchemaVersion == 1 ? identity.SourceProfile == CatalogSourceProfile.OfficialOnly :
                SameIdentity(catalog.Manifest.Identity, identity);
        }
    }
}
