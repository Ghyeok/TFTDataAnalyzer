using System;
using System.Collections.Generic;

namespace TFTAnalyzer.Domain
{
    /// <summary>Source activation bounds and effects, preserving explicitly unknown bounds.</summary>
    public sealed class TraitBreakpoint
    {
        public int? MinUnits { get; }
        public int? MaxUnits { get; }
        public int? Style { get; }
        public IReadOnlyDictionary<string, double?> Effects { get; }
        public TraitBreakpoint(int? minUnits, int? maxUnits, int? style, IReadOnlyDictionary<string, double?> effects)
        {
            if (minUnits < 0 || maxUnits < 0 || style < 0 || minUnits > maxUnits)
                throw new ArgumentException("Invalid source breakpoint bounds.");
            MinUnits = minUnits; MaxUnits = maxUnits; Style = style;
            Effects = DomainValues.CopyNullableEffects(effects);
        }
    }
}
