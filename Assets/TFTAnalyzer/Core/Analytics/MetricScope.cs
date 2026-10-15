using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    public sealed class MetricScope : IEquatable<MetricScope>
    {
        public CatalogIdentity ActiveCatalog { get; }
        public string Region { get; }
        public string QueueId { get; }
        public string TierBucket { get; }

        public MetricScope(CatalogIdentity activeCatalog, string region, string queueId, string tierBucket)
        {
            ActiveCatalog = activeCatalog ?? throw new ArgumentNullException(nameof(activeCatalog));
            Region = DomainValues.RequireStable(region, nameof(region));
            QueueId = DomainValues.RequireStable(queueId, nameof(queueId));
            TierBucket = DomainValues.RequireStable(tierBucket, nameof(tierBucket));
        }

        public bool Equals(MetricScope other)
        {
            return other != null && ActiveCatalog.Equals(other.ActiveCatalog) &&
                   string.Equals(Region, other.Region, StringComparison.Ordinal) &&
                   string.Equals(QueueId, other.QueueId, StringComparison.Ordinal) &&
                   string.Equals(TierBucket, other.TierBucket, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as MetricScope);

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
