using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    public sealed class AnonymousMatchObservation
    {
        public string MatchHash { get; }
        public MetricScope Scope { get; }
        public DateTimeOffset PlayedAt { get; }
        public IReadOnlyList<AnonymousParticipant> Participants { get; }

        public AnonymousMatchObservation(string matchHash, MetricScope scope, DateTimeOffset playedAt,
            IReadOnlyList<AnonymousParticipant> participants)
        {
            MatchHash = AnalyticsHashes.RequireSha256(matchHash, nameof(matchHash));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            PlayedAt = playedAt.ToUniversalTime();
            Participants = DomainValues.Copy(participants);
            if (Participants.Count == 0 || Participants.Any(participant => participant == null))
                throw new ArgumentException("At least one valid participant is required.", nameof(participants));
        }
    }
}
