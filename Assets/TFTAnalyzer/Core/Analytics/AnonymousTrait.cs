using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>An immutable final trait record with its activation tier.</summary>
    public sealed class AnonymousTrait
    {
        public string StableId { get; }
        public int TierCurrent { get; }
        public bool IsActive => TierCurrent > 0;

        /// <summary>Stores a stable trait ID and nonnegative tier; tier zero represents an inactive trait.</summary>
        public AnonymousTrait(string stableId, int tierCurrent)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            if (tierCurrent < 0) throw new ArgumentOutOfRangeException(nameof(tierCurrent));
            TierCurrent = tierCurrent;
        }
    }
}
