using System;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Immutable numeric outcome; no request identifiers, payloads, exceptions or credentials are retained.</summary>
    public sealed class MatchCollectionSummary
    {
        public int Requested { get; }
        public int Fetched { get; }
        public int Stored { get; }
        public int Duplicate { get; }
        public int Filtered { get; }
        public int Failed { get; }
        public bool Cancelled { get; }
        public int IdPages { get; }
        public bool ScanLimitReached { get; }
        public int StatusCode { get; }
        public DateTimeOffset? RetryAfterUtc { get; }

        /// <summary>Copies only counters and allowlisted HTTP status from private run state.</summary>
        internal MatchCollectionSummary(int requested, int fetched, int stored, int duplicate, int filtered, int failed,
            bool cancelled, int idPages, bool scanLimitReached, int statusCode, DateTimeOffset? retryAfterUtc)
        {
            Requested = requested; Fetched = fetched; Stored = stored; Duplicate = duplicate;
            Filtered = filtered; Failed = failed; Cancelled = cancelled; IdPages = idPages;
            ScanLimitReached = scanLimitReached; StatusCode = statusCode; RetryAfterUtc = retryAfterUtc;
        }
    }

    /// <summary>Progress exposes only an immutable nonidentifying snapshot.</summary>
    public sealed class CollectionProgress
    {
        public MatchCollectionSummary Summary { get; }
        /// <summary>Captures one run-state snapshot without exposing mutable state.</summary>
        internal CollectionProgress(MatchCollectionSummary summary) { Summary = summary; }
    }
}
