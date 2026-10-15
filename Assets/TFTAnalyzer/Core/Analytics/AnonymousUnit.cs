using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    public sealed class AnonymousUnit
    {
        public string StableId { get; }
        public int Tier { get; }
        public IReadOnlyList<string> ItemIds { get; }

        public AnonymousUnit(string stableId, int tier, IEnumerable<string> itemIds)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            if (tier < 1) throw new ArgumentOutOfRangeException(nameof(tier));
            Tier = tier;
            ItemIds = DomainValues.Copy((itemIds ?? Enumerable.Empty<string>())
                .Select(id => DomainValues.RequireStable(id, nameof(itemIds))));
        }
    }
}
