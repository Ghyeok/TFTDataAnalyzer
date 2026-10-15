using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>A value key separating statistics by catalog identity and collection filters.</summary>
    public sealed class MetricScope : IEquatable<MetricScope>
    {
        public CatalogIdentity ActiveCatalog { get; }
        public string Region { get; }
        public string QueueId { get; }
        public string TierBucket { get; }

        /// <summary>Binds statistics to a full catalog identity and nonblank region, queue and tier filters.</summary>
        public MetricScope(CatalogIdentity activeCatalog, string region, string queueId, string tierBucket)
        {
            ActiveCatalog = activeCatalog ?? throw new ArgumentNullException(nameof(activeCatalog));
            Region = DomainValues.RequireStable(region, nameof(region));
            QueueId = DomainValues.RequireStable(queueId, nameof(queueId));
            TierBucket = DomainValues.RequireStable(tierBucket, nameof(tierBucket));
        }

        /// <summary>Compares the complete catalog identity and every filter using ordinal text equality.</summary>
        public bool Equals(MetricScope other)
        {
            return other != null && ActiveCatalog.Equals(other.ActiveCatalog) &&
                   string.Equals(Region, other.Region, StringComparison.Ordinal) &&
                   string.Equals(QueueId, other.QueueId, StringComparison.Ordinal) &&
                   string.Equals(TierBucket, other.TierBucket, StringComparison.Ordinal);
        }

        /// <summary>Compares the complete catalog identity and every filter using ordinal text equality.</summary>
        public override bool Equals(object obj) => Equals(obj as MetricScope);

        /// <summary>Returns a collection hash consistent with equality; this is not a persisted identifier.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ActiveCatalog.GetHashCode();
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Region);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(QueueId);
                return hash * 31 + StringComparer.Ordinal.GetHashCode(TierBucket);
            }
        }
    }
}
