using System;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.UI.Collection
{
    public sealed class CollectionProgressViewModel
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
        public int IdPages { get; }
        public bool Cancelled { get; }
        public bool ScanLimitReached { get; }
        public DateTimeOffset? RetryAfterUtc { get; }
        public int Quarantined { get; }
        public bool QuarantineReportSaved { get; }
        public bool QuarantineReportFailed { get; }
        public bool QuarantineReportTruncated { get; }

        /// <summary>Copies an immutable numeric snapshot, or initializes empty counters for a new collection run.</summary>
        internal CollectionProgressViewModel(MatchCollectionSummary summary = null)
        {
            if (summary == null) return;
            Requested = summary.Requested;
            Fetched = summary.Fetched;
            Stored = summary.Stored;
            Duplicate = summary.Duplicate;
            Filtered = summary.Filtered;
            Failed = summary.Failed;
            FailedUnsupportedVersion = summary.FailedUnsupportedVersion;
            FailedGameVersion = summary.FailedGameVersion;
            FailedAugments = summary.FailedAugments;
            FailedPayload = summary.FailedPayload;
            FailedApi = summary.FailedApi;
            FailedProcessing = summary.FailedProcessing;
            IncompleteSaved = summary.IncompleteSaved;
            IncompleteDuplicate = summary.IncompleteDuplicate;
            IncompleteFailed = summary.IncompleteFailed;
            IncompleteFull = summary.IncompleteFull;
            IdPages = summary.IdPages;
            Cancelled = summary.Cancelled;
            ScanLimitReached = summary.ScanLimitReached;
            RetryAfterUtc = summary.RetryAfterUtc;
            Quarantined = summary.Quarantined;
            QuarantineReportSaved = summary.QuarantineReportSaved;
            QuarantineReportFailed = summary.QuarantineReportFailed;
            QuarantineReportTruncated = summary.QuarantineReportTruncated;
        }
    }
}
