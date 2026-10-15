using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>An immutable anonymous match snapshot bound to a complete statistics scope.</summary>
    public sealed class AnonymousMatchObservation
    {
        public string MatchHash { get; }
        public MetricScope Scope { get; }
        public DateTimeOffset PlayedAt { get; }
        public IReadOnlyList<AnonymousParticipant> Participants { get; }
        public string MatchDataVersion { get; }
        public string MatchGameVersion { get; }

        /// <summary>Snapshots participants with a complete scope, a validated SHA-256 match hash and UTC time.</summary>
        public AnonymousMatchObservation(string matchHash, MetricScope scope, DateTimeOffset playedAt,
            IReadOnlyList<AnonymousParticipant> participants, string matchDataVersion = "", string matchGameVersion = "")
        {
            MatchHash = AnalyticsHashes.RequireSha256(matchHash, nameof(matchHash));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            PlayedAt = playedAt.ToUniversalTime();
            Participants = DomainValues.Copy(participants);
            MatchDataVersion = matchDataVersion ?? "";
            MatchGameVersion = matchGameVersion ?? "";
            if ((MatchDataVersion.Length != 0 && !Regex.IsMatch(MatchDataVersion, @"\A[0-9]{1,5}\z")) ||
                (MatchGameVersion.Length != 0 && !Regex.IsMatch(MatchGameVersion, @"\A[0-9]{1,10}(?:\.[0-9]{1,10}){2,3}\z")))
                throw new ArgumentException("Invalid match version metadata.");
            if (Participants.Count == 0 || Participants.Any(participant => participant == null))
                throw new ArgumentException("At least one valid participant is required.", nameof(participants));
        }
    }
}
