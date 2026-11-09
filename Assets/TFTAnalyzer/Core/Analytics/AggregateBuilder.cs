using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>A deterministic collection of scope snapshots with no match identifiers or wall-clock build metadata.</summary>
    public sealed class AggregateSnapshot
    {
        public const int CurrentSchemaVersion = 3;
        public int SchemaVersion => CurrentSchemaVersion;
        public IReadOnlyList<ScopeAggregate> Scopes { get; }

        /// <summary>Snapshots scope records in the builder's canonical order.</summary>
        internal AggregateSnapshot(IEnumerable<ScopeAggregate> scopes) { Scopes = DomainValues.Copy(scopes); }
    }

    /// <summary>Counts, observed match-time ranges and data versions belonging to one complete scope.</summary>
    public sealed class ScopeAggregate
    {
        public MetricScope Scope { get; }
        public int MatchCount { get; }
        public DateTimeOffset FirstPlayedAt { get; }
        public DateTimeOffset LastPlayedAt { get; }
        public IReadOnlyList<string> MatchDataVersions { get; }
        public IReadOnlyList<string> MatchGameVersions { get; }
        public IReadOnlyList<AggregateMetric> Metrics { get; }
        public IReadOnlyList<PairwiseMetric> Pairs { get; }

        /// <summary>Copies the final sorted metadata and metrics after all input validation has completed.</summary>
        internal ScopeAggregate(MetricScope scope, int matchCount, DateTimeOffset firstPlayedAt, DateTimeOffset lastPlayedAt,
            IEnumerable<string> dataVersions, IEnumerable<string> gameVersions,
            IEnumerable<AggregateMetric> metrics, IEnumerable<PairwiseMetric> pairs)
        {
            Scope = scope;
            MatchCount = matchCount;
            FirstPlayedAt = firstPlayedAt;
            LastPlayedAt = lastPlayedAt;
            MatchDataVersions = DomainValues.Copy(dataVersions);
            MatchGameVersions = DomainValues.Copy(gameVersions);
            Metrics = DomainValues.Copy(metrics);
            Pairs = DomainValues.Copy(pairs);
        }
    }

    /// <summary>Rebuilds historical statistics only from complete, identity-free final match observations.</summary>
    public static class AggregateBuilder
    {
        /// <summary>Deduplicates equivalent hashes, rejects conflicts and sorts all scope/composition outputs ordinally.</summary>
        public static AggregateSnapshot Build(IEnumerable<AnonymousMatchObservation> observations)
            => Build(observations, null, CancellationToken.None);

        /// <summary>Filters participant observations while preserving the original lobby for pair ambiguity checks.</summary>
        public static AggregateSnapshot Build(IEnumerable<AnonymousMatchObservation> observations,
            Func<AnonymousParticipant, bool> participantFilter)
            => Build(observations, participantFilter, CancellationToken.None);

        /// <summary>Allows callers to cancel a rebuild without publishing a partial snapshot.</summary>
        public static AggregateSnapshot Build(IEnumerable<AnonymousMatchObservation> observations,
            Func<AnonymousParticipant, bool> participantFilter, CancellationToken cancellationToken)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            cancellationToken.ThrowIfCancellationRequested();
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var scopes = new SortedDictionary<string, ScopeAccumulator>(StringComparer.Ordinal);
            foreach (var observation in observations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (observation == null || observation.Participants.Count != 8 ||
                    observation.Participants.Select(value => value.Placement).Distinct().Count() != 8)
                    throw new ArgumentException("A complete eight-placement observation is required.");
                var boards = new List<Board>();
                foreach (var participant in observation.Participants.OrderBy(value => value.Placement))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    boards.Add(new Board(participant, CompositionFingerprintBuilder.Build(participant),
                        CompositionFingerprintBuilder.BuildStrict(participant)));
                }
                var scopeKey = ScopeKey(observation.Scope);
                var signature = new JArray(scopeKey, observation.PlayedAt.ToString("O", CultureInfo.InvariantCulture),
                    observation.MatchDataVersion, observation.MatchGameVersion,
                    new JArray(boards.Select(board => new JArray(board.Participant.Placement,
                        board.Participant.LastRound, board.StrictFingerprint.Value)))).ToString(Formatting.None);
                if (seen.TryGetValue(observation.MatchHash, out var previous))
                {
                    if (!string.Equals(previous, signature, StringComparison.Ordinal))
                        throw new ArgumentException("Conflicting duplicate match observations.");
                    continue;
                }
                seen.Add(observation.MatchHash, signature);
                if (!scopes.TryGetValue(scopeKey, out var accumulator))
                {
                    accumulator = new ScopeAccumulator(observation.Scope, observation.PlayedAt);
                    scopes.Add(scopeKey, accumulator);
                }
                accumulator.Add(observation, boards.ToArray(), participantFilter, cancellationToken);
            }
            return new AggregateSnapshot(scopes.Values.Select(value => value.Finish()));
        }

        /// <summary>Encodes all catalog and filter dimensions without delimiter ambiguity or culture dependence.</summary>
        private static string ScopeKey(MetricScope scope)
        {
            var patch = scope.ActiveCatalog.Patch;
            return new JArray(patch.SetId, patch.SetNumber, patch.PatchId, patch.SourceGameVersion,
                scope.ActiveCatalog.ProfileId, scope.Region, scope.QueueId, scope.TierBucket).ToString(Formatting.None);
        }

        /// <summary>Pairs a validated participant with its outcome-independent composition fingerprint.</summary>
        private sealed class Board
        {
            internal AnonymousParticipant Participant { get; }
            internal CompositionFingerprint Fingerprint { get; }
            internal CompositionFingerprint StrictFingerprint { get; }
            internal bool Selected { get; set; }

            /// <summary>Retains only the current observation's board references while building.</summary>
            internal Board(AnonymousParticipant participant, CompositionFingerprint fingerprint, CompositionFingerprint strictFingerprint)
            { Participant = participant; Fingerprint = fingerprint; StrictFingerprint = strictFingerprint; }
        }

        /// <summary>Accumulates counts privately until an immutable public scope snapshot is produced.</summary>
        private sealed class ScopeAccumulator
        {
            private readonly MetricScope _scope;
            private readonly SortedDictionary<string, AggregateMetric> _metrics = new SortedDictionary<string, AggregateMetric>(StringComparer.Ordinal);
            private readonly SortedDictionary<string, PairwiseMetric> _pairs = new SortedDictionary<string, PairwiseMetric>(StringComparer.Ordinal);
            private readonly SortedSet<string> _dataVersions = new SortedSet<string>(StringComparer.Ordinal);
            private readonly SortedSet<string> _gameVersions = new SortedSet<string>(StringComparer.Ordinal);
            private int _matchCount;
            private DateTimeOffset _firstPlayedAt;
            private DateTimeOffset _lastPlayedAt;

            /// <summary>Initializes a scope's observed time bounds from its first accepted match.</summary>
            internal ScopeAccumulator(MetricScope scope, DateTimeOffset playedAt)
            { _scope = scope; _firstPlayedAt = _lastPlayedAt = playedAt; }

            /// <summary>Adds every participant and one result or exclusion per distinct composition pair in this lobby.</summary>
            internal void Add(AnonymousMatchObservation observation, Board[] boards,
                Func<AnonymousParticipant, bool> participantFilter, CancellationToken cancellationToken)
            {
                checked { _matchCount++; }
                if (observation.PlayedAt < _firstPlayedAt) _firstPlayedAt = observation.PlayedAt;
                if (observation.PlayedAt > _lastPlayedAt) _lastPlayedAt = observation.PlayedAt;
                _dataVersions.Add(observation.MatchDataVersion);
                _gameVersions.Add(observation.MatchGameVersion);
                foreach (var board in boards)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    board.Selected = participantFilter == null || participantFilter(board.Participant);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!board.Selected) continue;
                    if (!_metrics.TryGetValue(board.Fingerprint.Value, out var metric))
                    {
                        metric = new AggregateMetric(_scope, board.Fingerprint, observation.PlayedAt, board.Participant.AugmentIds);
                        _metrics.Add(board.Fingerprint.Value, metric);
                    }
                    metric.Add(board.Participant.Placement, observation.PlayedAt, board.Participant.AugmentIds);
                }
                var groups = boards.GroupBy(board => board.Fingerprint.Value, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => group.ToArray()).ToArray();
                for (var i = 0; i < groups.Length; i++)
                for (var j = i + 1; j < groups.Length; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!groups[i].Any(board => board.Selected) && !groups[j].Any(board => board.Selected)) continue;
                    var first = groups[i][0];
                    var second = groups[j][0];
                    var key = first.Fingerprint.Value + "|" + second.Fingerprint.Value;
                    if (!_pairs.TryGetValue(key, out var pair))
                    {
                        pair = new PairwiseMetric(_scope, first.Fingerprint, second.Fingerprint, observation.PlayedAt,
                            Array.Empty<string>(), Array.Empty<string>());
                        _pairs.Add(key, pair);
                    }
                    pair.Add(groups[i].Length == 1 && groups[j].Length == 1
                        ? (bool?)(first.Participant.Placement < second.Participant.Placement) : null, observation.PlayedAt,
                        first.Participant.AugmentIds, second.Participant.AugmentIds);
                }
            }

            /// <summary>Publishes sorted copies without retaining the input match hash dictionary or raw boards.</summary>
            internal ScopeAggregate Finish() => new ScopeAggregate(_scope, _matchCount, _firstPlayedAt, _lastPlayedAt,
                _dataVersions, _gameVersions, _metrics.Values, _pairs.Values);
        }
    }
}
