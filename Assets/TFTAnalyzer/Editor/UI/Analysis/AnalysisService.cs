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
                var aggregate = AggregateBuilder.Build(observations);
                var result = Present(catalog, identity, observations, aggregate, policy, token);
                if (_aggregates != null)
                    foreach (var scope in aggregate.Scopes) await _aggregates.SaveAsync(scope, token);
                token.ThrowIfCancellationRequested();
                return result;
            }, token), token);
        }

        public static AnalysisSnapshot BuildSnapshot(StaticCatalog catalog, CatalogIdentity identity,
            IEnumerable<AnonymousMatchObservation> observations, MetricPolicy policy = null)
        {
            if (!MatchesCatalog(catalog, identity)) throw new ArgumentException("analysis_catalog_invalid");
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            var copy = observations.Where(x => x != null && SameIdentity(x.Scope.ActiveCatalog, identity)).ToArray();
            return Present(catalog, identity, copy, AggregateBuilder.Build(copy), policy ?? new MetricPolicy(), CancellationToken.None);
        }

        private static AnalysisSnapshot Present(StaticCatalog catalog, CatalogIdentity identity,
            IEnumerable<AnonymousMatchObservation> observations, AggregateSnapshot aggregate, MetricPolicy policy, CancellationToken token)
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
                    var presentation = policy.Evaluate(metric, catalog);
                    if (presentation.Scope == null) throw new InvalidOperationException("analysis_scope_invalid");
                    rows.Add(new MetricRowViewModel(presentation, descriptors[metric.Fingerprint.Value]));
                }
                foreach (var pair in scope.Pairs)
                {
                    var presentation = policy.Evaluate(pair, catalog);
                    if (presentation.Scope == null) throw new InvalidOperationException("analysis_scope_invalid");
                    pairs.Add(new MetricRowViewModel(presentation, descriptors[pair.First.Value], descriptors[pair.Second.Value]));
                }
            }
            return new AnalysisSnapshot(identity, rows, pairs, policy.MinimumSamples);
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
