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
        public int IdPages { get; }
        public bool Cancelled { get; }
        public bool ScanLimitReached { get; }
        public DateTimeOffset? RetryAfterUtc { get; }

        internal CollectionProgressViewModel(MatchCollectionSummary summary = null)
        {
            if (summary == null) return;
            Requested = summary.Requested;
            Fetched = summary.Fetched;
            Stored = summary.Stored;
            Duplicate = summary.Duplicate;
            Filtered = summary.Filtered;
            Failed = summary.Failed;
            IdPages = summary.IdPages;
            Cancelled = summary.Cancelled;
            ScanLimitReached = summary.ScanLimitReached;
            RetryAfterUtc = summary.RetryAfterUtc;
        }
    }
}
