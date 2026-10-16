using System;
using System.Collections.Generic;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Nonidentifying, fully catalog-bound limits and labels for one explicit collection run.</summary>
    public sealed class MatchCollectionRequest
    {
        public CatalogIdentity ActiveCatalog { get; }
        public string Region { get; }
        public int QueueId { get; }
        public string TierBucket { get; }
        public int MaximumUniqueMatches { get; }
        private static readonly HashSet<string> Tiers = new HashSet<string>(new[]
        {
            "IRON", "BRONZE", "SILVER", "GOLD", "PLATINUM", "EMERALD", "DIAMOND", "MASTER", "GRANDMASTER",
            "CHALLENGER", "MASTER+", "DIAMOND+", "ALL", "UNKNOWN", "UNRANKED"
        }, StringComparer.Ordinal);

        /// <summary>Validates a bounded request before credentials, seeds or transport are accessed.</summary>
        public MatchCollectionRequest(string platform, CatalogIdentity activeCatalog, int queueId,
            string tierBucket, int maximumUniqueMatches = 100)
        {
            ActiveCatalog = activeCatalog ?? throw new ArgumentNullException(nameof(activeCatalog));
            Region = RiotRouting.ForPlatform(platform);
            if (queueId < 1 || queueId > 999999) throw new ArgumentOutOfRangeException(nameof(queueId));
            if (tierBucket == null || !Tiers.Contains(tierBucket)) throw new ArgumentException("Unsupported tier bucket.");
            if (maximumUniqueMatches < 1 || maximumUniqueMatches > 1000)
                throw new ArgumentOutOfRangeException(nameof(maximumUniqueMatches));
            QueueId = queueId;
            TierBucket = tierBucket;
            MaximumUniqueMatches = maximumUniqueMatches;
        }
    }
}
