using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>Explicit reviewed augment classifications; absence of a rule remains unknown, never implicitly standard.</summary>
    public enum AugmentPolicyKind { Unknown, Standard, LegendBased, Legend }

    /// <summary>Immutable policy classification metadata bound to one complete trusted catalog identity.</summary>
    public sealed class MetricPolicyMetadata
    {
        public CatalogIdentity Catalog { get; }
        public IReadOnlyDictionary<string, AugmentPolicyKind> Augments { get; }

        /// <summary>Copies reviewed classifications and rejects invalid codes or normalized duplicate IDs.</summary>
        public MetricPolicyMetadata(CatalogIdentity catalog, IReadOnlyDictionary<string, AugmentPolicyKind> augments)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (augments == null) throw new ArgumentNullException(nameof(augments));
            var copy = new SortedDictionary<string, AugmentPolicyKind>(StringComparer.Ordinal);
            foreach (var pair in augments)
            {
                if (!Enum.IsDefined(typeof(AugmentPolicyKind), pair.Value)) throw new ArgumentOutOfRangeException(nameof(augments));
                var id = DomainValues.RequireStable(pair.Key, nameof(augments));
                if (copy.ContainsKey(id)) throw new ArgumentException("Duplicate policy classification.");
                copy.Add(id, pair.Value);
            }
            Augments = new ReadOnlyDictionary<string, AugmentPolicyKind>(copy);
        }
    }

    /// <summary>Applies the repository's approved historical-metric rules before any display or export DTO is created.</summary>
    public sealed class MetricPolicy
    {
        public const int MinimumSampleFloor = 100;
        public int MinimumSamples { get; }
        private readonly MetricPolicyMetadata _augmentMetadata;
        private static readonly HashSet<string> Regions = new HashSet<string>(new[] { "asia", "americas", "europe" }, StringComparer.Ordinal);
        private static readonly HashSet<string> Tiers = new HashSet<string>(new[]
        {
            "IRON", "BRONZE", "SILVER", "GOLD", "PLATINUM", "EMERALD", "DIAMOND", "MASTER", "GRANDMASTER",
            "CHALLENGER", "MASTER+", "DIAMOND+", "ALL", "UNKNOWN", "UNRANKED"
        }, StringComparer.Ordinal);

        /// <summary>Allows users to raise the sample minimum and optionally supply reviewed catalog-bound augment rules.</summary>
        public MetricPolicy(int minimumSamples = MinimumSampleFloor, MetricPolicyMetadata augmentMetadata = null)
        {
            if (minimumSamples < MinimumSampleFloor) throw new ArgumentOutOfRangeException(nameof(minimumSamples));
            MinimumSamples = minimumSamples;
            _augmentMetadata = augmentMetadata;
        }

        /// <summary>Returns approved averages and only policy-visible participant ratios, without retaining the raw metric.</summary>
        public MetricPresentation Evaluate(AggregateMetric metric, StaticCatalog catalog)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));
            var decision = Decide(metric.Scope, metric.AugmentIds, metric.Count, catalog);
            var ratios = decision.State == MetricVisibility.Visible ? new[]
            {
                new PresentedRatio("top_four_rate", metric.TopFourCount, metric.Count),
                new PresentedRatio("first_place_rate", metric.FirstPlaceCount, metric.Count)
            } : null;
            return new MetricPresentation(decision.State, decision.Reason, "historical_observation",
                decision.ContextValid ? metric.Scope : null, decision.ContextValid ? metric.Count : 0,
                decision.AllowAverage ? (double?)metric.AveragePlacement : null,
                decision.ContextValid ? metric.Fingerprint.Value : null, null, 0,
                decision.ContextValid ? (DateTimeOffset?)metric.FirstPlayedAt : null,
                decision.ContextValid ? (DateTimeOffset?)metric.LastPlayedAt : null, ratios);
        }

        /// <summary>Uses eligible lobbies and both compositions' augment rules for historical relative-placement output.</summary>
        public MetricPresentation Evaluate(PairwiseMetric metric, StaticCatalog catalog)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));
            var decision = Decide(metric.Scope, metric.FirstAugmentIds.Concat(metric.SecondAugmentIds), metric.Count, catalog);
            var ratios = decision.State == MetricVisibility.Visible ? new[]
            {
                new PresentedRatio("relative_placement_advantage", metric.FirstHigherCount, metric.Count)
            } : null;
            return new MetricPresentation(decision.State, decision.Reason, "relative_placement_advantage",
                decision.ContextValid ? metric.Scope : null, decision.ContextValid ? metric.Count : 0, null,
                decision.ContextValid ? metric.First.Value : null, decision.ContextValid ? metric.Second.Value : null,
                decision.ContextValid ? metric.AmbiguousLobbyCount : 0,
                decision.ContextValid ? (DateTimeOffset?)metric.FirstPlayedAt : null,
                decision.ContextValid ? (DateTimeOffset?)metric.LastPlayedAt : null, ratios);
        }

        /// <summary>Validates trusted context first, then policy exclusions, then the immutable sample threshold.</summary>
        private Decision Decide(MetricScope scope, IEnumerable<string> augmentIds, int count, StaticCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var actual = catalog.Manifest.Identity ?? new CatalogIdentity(catalog.Manifest.Patch, CatalogSourceProfile.OfficialOnly);
            if (!SameCatalog(actual, scope.ActiveCatalog))
                return new Decision(MetricVisibility.PolicyBlocked, "catalog_mismatch", false, false);
            if (!Regions.Contains(scope.Region) || !Tiers.Contains(scope.TierBucket) ||
                !Regex.IsMatch(scope.QueueId, @"\A[0-9]{1,6}\z") ||
                !int.TryParse(scope.QueueId, NumberStyles.None, CultureInfo.InvariantCulture, out var queue) || queue <= 0)
                return new Decision(MetricVisibility.PolicyBlocked, "invalid_metric_scope", false, false);
            var restriction = AugmentRestriction(augmentIds, actual, catalog);
            if (restriction != null)
                return new Decision(MetricVisibility.PolicyBlocked, restriction, true,
                    restriction != "unknown_augment" && restriction != "legend");
            return count < MinimumSamples
                ? new Decision(MetricVisibility.InsufficientSample, "insufficient_sample", true, true)
                : new Decision(MetricVisibility.Visible, null, true, true);
        }

        /// <summary>Fails closed without inferring policy tags from IDs, descriptions or incomplete classification metadata.</summary>
        private string AugmentRestriction(IEnumerable<string> augmentIds, CatalogIdentity actual, StaticCatalog catalog)
        {
            var ids = augmentIds.Distinct(StringComparer.Ordinal).ToArray();
            if (ids.Length == 0) return null;
            var known = new HashSet<string>(catalog.Augments.Select(value => value.StableId), StringComparer.Ordinal);
            if (ids.Any(id => !known.Contains(id))) return "unknown_augment";
            if (_augmentMetadata == null) return "unclassified_augment";
            if (!SameCatalog(actual, _augmentMetadata.Catalog)) return "policy_catalog_mismatch";
            var kinds = ids.Select(id => _augmentMetadata.Augments.TryGetValue(id, out var kind) ? kind : AugmentPolicyKind.Unknown).ToArray();
            if (kinds.Contains(AugmentPolicyKind.Legend)) return "legend";
            if (kinds.Contains(AugmentPolicyKind.LegendBased)) return "legend_based_augment";
            return kinds.Contains(AugmentPolicyKind.Unknown) ? "unclassified_augment" : null;
        }

        /// <summary>Includes set number in addition to the catalog identity's existing value equality.</summary>
        private static bool SameCatalog(CatalogIdentity a, CatalogIdentity b) => a.Equals(b) && a.Patch.SetNumber == b.Patch.SetNumber;

        /// <summary>Keeps transient policy decisions separate from the allowlisted presentation payload.</summary>
        private readonly struct Decision
        {
            internal MetricVisibility State { get; }
            internal string Reason { get; }
            internal bool ContextValid { get; }
            internal bool AllowAverage { get; }

            /// <summary>Records whether context and averages are safe independently of ratio visibility.</summary>
            internal Decision(MetricVisibility state, string reason, bool contextValid, bool allowAverage)
            { State = state; Reason = reason; ContextValid = contextValid; AllowAverage = allowAverage; }
        }
    }
}
