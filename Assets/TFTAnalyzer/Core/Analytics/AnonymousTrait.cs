using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    public sealed class AnonymousTrait
    {
        public string StableId { get; }
        public int TierCurrent { get; }
        public bool IsActive => TierCurrent > 0;

        public AnonymousTrait(string stableId, int tierCurrent)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            if (tierCurrent < 0) throw new ArgumentOutOfRangeException(nameof(tierCurrent));
            TierCurrent = tierCurrent;
        }
    }
}
