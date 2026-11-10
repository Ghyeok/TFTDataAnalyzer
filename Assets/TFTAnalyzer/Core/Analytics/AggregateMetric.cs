using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>Raw participant-level historical counts for one complete scope and composition.</summary>
    public sealed class AggregateMetric
    {
        public MetricScope Scope { get; }
        public CompositionFingerprint Fingerprint { get; }
        private readonly SortedSet<string> _augmentIds = new SortedSet<string>(StringComparer.Ordinal);
        public IReadOnlyList<string> AugmentIds => DomainValues.Copy(_augmentIds);
        public int Count { get; private set; }
        public long PlacementSum { get; private set; }
        public int TopFourCount { get; private set; }
        public int FirstPlaceCount { get; private set; }
        public DateTimeOffset FirstPlayedAt { get; private set; }
        public DateTimeOffset LastPlayedAt { get; private set; }
        public double AveragePlacement => (double)PlacementSum / Count;
        public double TopFourRate => (double)TopFourCount / Count;
        public double FirstPlaceRate => (double)FirstPlaceCount / Count;
        public ConfidenceInterval TopFourInterval => WilsonInterval.Calculate(TopFourCount, Count);
        public ConfidenceInterval FirstPlaceInterval => WilsonInterval.Calculate(FirstPlaceCount, Count);

        /// <summary>Starts an internal accumulator that is published only after a complete build.</summary>
        internal AggregateMetric(MetricScope scope, CompositionFingerprint fingerprint, DateTimeOffset playedAt,
            IEnumerable<string> augmentIds)
        {
            Scope = scope;
            Fingerprint = fingerprint;
            _augmentIds.UnionWith(augmentIds ?? throw new ArgumentNullException(nameof(augmentIds)));
            FirstPlayedAt = LastPlayedAt = playedAt;
        }

        /// <summary>Adds one validated participant and updates its observed match-time range.</summary>
        internal void Add(int placement, DateTimeOffset playedAt, IEnumerable<string> augmentIds)
        {
            checked
            {
                Count++;
                PlacementSum += placement;
                if (placement <= 4) TopFourCount++;
                if (placement == 1) FirstPlaceCount++;
            }
            _augmentIds.UnionWith(augmentIds ?? throw new ArgumentNullException(nameof(augmentIds)));
            if (playedAt < FirstPlayedAt) FirstPlayedAt = playedAt;
            if (playedAt > LastPlayedAt) LastPlayedAt = playedAt;
        }
    }
}
