using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>An immutable final unit snapshot containing only static IDs, tier and equipped items.</summary>
    public sealed class AnonymousUnit
    {
        public string StableId { get; }
        public int Tier { get; }
        public IReadOnlyList<string> ItemIds { get; }

        /// <summary>Copies final unit state, rejecting blank stable IDs or item IDs and nonpositive tiers.</summary>
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
