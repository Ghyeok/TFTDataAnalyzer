using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>One unordered composition pair with eligible-lobby counts and explicit ambiguous-lobby exclusions.</summary>
    public sealed class PairwiseMetric
    {
        public MetricScope Scope { get; }
        public CompositionFingerprint First { get; }
        public CompositionFingerprint Second { get; }
        private readonly SortedSet<string> _firstAugmentIds = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> _secondAugmentIds = new SortedSet<string>(StringComparer.Ordinal);
        public IReadOnlyList<string> FirstAugmentIds => DomainValues.Copy(_firstAugmentIds);
        public IReadOnlyList<string> SecondAugmentIds => DomainValues.Copy(_secondAugmentIds);
        public int Count { get; private set; }
        public int FirstHigherCount { get; private set; }
        public int SecondHigherCount => Count - FirstHigherCount;
        public int AmbiguousLobbyCount { get; private set; }
        public DateTimeOffset FirstPlayedAt { get; private set; }
        public DateTimeOffset LastPlayedAt { get; private set; }
        public double? FirstHigherRate => Count == 0 ? (double?)null : (double)FirstHigherCount / Count;
        public ConfidenceInterval FirstHigherInterval => Count == 0 ? null : WilsonInterval.Calculate(FirstHigherCount, Count);

        /// <summary>Starts a pair accumulator with fingerprints already ordered by ordinal hash.</summary>
        internal PairwiseMetric(MetricScope scope, CompositionFingerprint first, CompositionFingerprint second, DateTimeOffset playedAt,
            IEnumerable<string> firstAugmentIds, IEnumerable<string> secondAugmentIds)
        {
            Scope = scope;
            First = first;
            Second = second;
            _firstAugmentIds.UnionWith(firstAugmentIds ?? throw new ArgumentNullException(nameof(firstAugmentIds)));
            _secondAugmentIds.UnionWith(secondAugmentIds ?? throw new ArgumentNullException(nameof(secondAugmentIds)));
            FirstPlayedAt = LastPlayedAt = playedAt;
        }

        /// <summary>Records at most one eligible result or one ambiguity exclusion for a coobserved lobby.</summary>
        internal void Add(bool? firstHigher, DateTimeOffset playedAt,
            IEnumerable<string> firstAugmentIds, IEnumerable<string> secondAugmentIds)
        {
            checked
            {
                if (!firstHigher.HasValue) AmbiguousLobbyCount++;
                else
                {
                    Count++;
                    if (firstHigher.Value) FirstHigherCount++;
                }
            }
            if (firstHigher.HasValue)
            {
                _firstAugmentIds.UnionWith(firstAugmentIds ?? throw new ArgumentNullException(nameof(firstAugmentIds)));
                _secondAugmentIds.UnionWith(secondAugmentIds ?? throw new ArgumentNullException(nameof(secondAugmentIds)));
            }
            if (playedAt < FirstPlayedAt) FirstPlayedAt = playedAt;
            if (playedAt > LastPlayedAt) LastPlayedAt = playedAt;
        }
    }
}
