using System;
using System.Collections.Generic;
using System.Linq;

namespace TFTAnalyzer.Analytics
{
    /// <summary>An accepted anonymous observation or a quarantined match's credential-free ID digests.</summary>
    public sealed class AnonymizationResult
    {
        public AnonymousMatchObservation Observation { get; }
        public IReadOnlyList<UnknownStaticId> UnknownStaticIds { get; }
        public bool IsAccepted => Observation != null;

        /// <summary>Snapshots quarantine diagnostics; unknown IDs never accompany a persistable observation.</summary>
        public AnonymizationResult(AnonymousMatchObservation observation, IEnumerable<UnknownStaticId> unknownStaticIds)
        {
            var ids = (unknownStaticIds ?? Enumerable.Empty<UnknownStaticId>()).ToArray();
            if (ids.Any(id => id == null) || (observation != null) == (ids.Length > 0))
                throw new ArgumentException("A result must be accepted or quarantined.");
            Observation = observation;
            UnknownStaticIds = Array.AsReadOnly(ids);
        }
    }

    /// <summary>A static category and versioned SHA-256 digest, without an untrusted raw ID.</summary>
    public sealed class UnknownStaticId
    {
        public string Kind { get; }
        public string IdHash { get; }

        /// <summary>Allows only known category labels and a validated hash in quarantine diagnostics.</summary>
        public UnknownStaticId(string kind, string idHash)
        {
            if (kind != "unit" && kind != "item" && kind != "trait" && kind != "augment")
                throw new ArgumentException("Unsupported static category.");
            Kind = kind;
            IdHash = AnalyticsHashes.RequireSha256(idHash, nameof(idHash));
        }
    }

    public enum MatchPayloadFailure { InvalidPayload, CatalogMismatch, UnsupportedVersion }

    /// <summary>Controlled diagnostics without raw JSON, identifying paths or inner parser exceptions.</summary>
    public sealed class MatchPayloadException : Exception
    {
        public string Code { get; }

        /// <summary>Maps a closed failure category to a safe public code.</summary>
        public MatchPayloadException(MatchPayloadFailure failure) : base(CodeFor(failure)) { Code = Message; }

        /// <summary>Provides constant errors even for an unknown enum value.</summary>
        private static string CodeFor(MatchPayloadFailure failure)
            => failure == MatchPayloadFailure.CatalogMismatch ? "catalog_mismatch" :
                failure == MatchPayloadFailure.UnsupportedVersion ? "unsupported_match_version" : "invalid_match_payload";
    }
}
