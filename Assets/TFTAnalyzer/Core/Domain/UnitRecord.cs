using System.Collections.Generic;

namespace TFTAnalyzer.Domain
{
    public sealed class UnitRecord
    {
        public string StableId { get; }
        public LocalizedText Name { get; }
        public int Cost { get; }
        public IReadOnlyList<string> TraitIds { get; }
        public string IconPath { get; }
        public IReadOnlyList<SourceStamp> Sources { get; }
        public string Role { get; }
        public LocalizedText Description { get; }
        public IReadOnlyDictionary<string, double> Effects { get; }

        public UnitRecord(
            string stableId,
            LocalizedText name,
            int cost,
            IEnumerable<string> traitIds,
            string iconPath,
            IEnumerable<SourceStamp> sources,
            string role = "",
            LocalizedText description = null,
            IReadOnlyDictionary<string, double> effects = null)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            Name = name ?? LocalizedText.Empty;
            Cost = cost;
            TraitIds = DomainValues.Copy(traitIds);
            IconPath = iconPath ?? "";
            Sources = DomainValues.Copy(sources);
            Role = role ?? "";
            Description = description ?? LocalizedText.Empty;
            Effects = DomainValues.CopyEffects(effects);
        }
    }
}
