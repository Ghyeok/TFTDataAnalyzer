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
        public int FailedUnsupportedVersion { get; }
        public int FailedGameVersion { get; }
        public int FailedAugments { get; }
        public int FailedPayload { get; }
        public int FailedApi { get; }
        public int FailedProcessing { get; }
        public int IncompleteSaved { get; }
        public int IncompleteDuplicate { get; }
        public int IncompleteFailed { get; }
        public int IncompleteFull { get; }
        public bool Cancelled { get; }
        public int IdPages { get; }
        public bool ScanLimitReached { get; }
        public int StatusCode { get; }
        public DateTimeOffset? RetryAfterUtc { get; }
        public int Quarantined { get; }
        public bool QuarantineReportSaved { get; }
        public bool QuarantineReportFailed { get; }
        public bool QuarantineReportTruncated { get; }

        /// <summary>Copies only counters and allowlisted HTTP status from private run state.</summary>
        internal MatchCollectionSummary(int requested, int fetched, int stored, int duplicate, int filtered, int failed,
            bool cancelled, int idPages, bool scanLimitReached, int statusCode, DateTimeOffset? retryAfterUtc,
            int quarantined = 0, bool quarantineReportSaved = false, bool quarantineReportFailed = false,
            bool quarantineReportTruncated = false, int failedUnsupportedVersion = 0, int failedGameVersion = 0,
            int failedAugments = 0, int failedPayload = 0, int failedApi = 0,
            int incompleteSaved = 0, int incompleteDuplicate = 0, int incompleteFailed = 0, int incompleteFull = 0)
        {
            var classified = (long)failedUnsupportedVersion + failedGameVersion + failedAugments + failedPayload + failedApi;
            if (failed < 0 || failedUnsupportedVersion < 0 || failedGameVersion < 0 || failedAugments < 0 ||
                failedPayload < 0 || failedApi < 0 || classified > failed) throw new ArgumentOutOfRangeException(nameof(failed));
            if (incompleteSaved < 0 || incompleteDuplicate < 0 || incompleteFailed < 0 || incompleteFull < 0)
                throw new ArgumentOutOfRangeException(nameof(incompleteSaved));
            IncompleteSaved = incompleteSaved; IncompleteDuplicate = incompleteDuplicate;
            IncompleteFailed = incompleteFailed; IncompleteFull = incompleteFull;
            FailedUnsupportedVersion = failedUnsupportedVersion; FailedGameVersion = failedGameVersion;
            FailedAugments = failedAugments; FailedPayload = failedPayload; FailedApi = failedApi;
            FailedProcessing = failed - (int)classified;
            Requested = requested; Fetched = fetched; Stored = stored; Duplicate = duplicate;
            Filtered = filtered; Failed = failed; Cancelled = cancelled; IdPages = idPages;
            ScanLimitReached = scanLimitReached; StatusCode = statusCode; RetryAfterUtc = retryAfterUtc;
            Quarantined = quarantined; QuarantineReportSaved = quarantineReportSaved;
            QuarantineReportFailed = quarantineReportFailed; QuarantineReportTruncated = quarantineReportTruncated;
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
