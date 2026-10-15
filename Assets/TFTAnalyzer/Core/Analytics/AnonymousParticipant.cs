using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>Identity-free final board and outcome data for one participant.</summary>
    public sealed class AnonymousParticipant
    {
        public int Placement { get; }
        public int LastRound { get; }
        public IReadOnlyList<AnonymousUnit> Units { get; }
        public IReadOnlyList<AnonymousTrait> Traits { get; }
        public IReadOnlyList<string> AugmentIds { get; }

        /// <summary>Snapshots the final board and augments with placement 1–8 and a nonnegative last round.</summary>
        public AnonymousParticipant(int placement, int lastRound, IEnumerable<AnonymousUnit> units,
            IEnumerable<AnonymousTrait> traits, IEnumerable<string> augmentIds)
        {
            if (placement < 1 || placement > 8) throw new ArgumentOutOfRangeException(nameof(placement));
            if (lastRound < 0) throw new ArgumentOutOfRangeException(nameof(lastRound));
            Placement = placement;
            LastRound = lastRound;
            Units = DomainValues.Copy(units);
            if (Units.Count == 0 || Units.Any(unit => unit == null))
                throw new ArgumentException("At least one valid final unit is required.", nameof(units));
            Traits = DomainValues.Copy(traits);
            if (Traits.Any(trait => trait == null))
                throw new ArgumentException("Trait entries cannot be null.", nameof(traits));
            AugmentIds = DomainValues.Copy((augmentIds ?? Enumerable.Empty<string>())
                .Select(id => DomainValues.RequireStable(id, nameof(augmentIds))));
        }
    }
}
