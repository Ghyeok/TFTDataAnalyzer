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
        public IReadOnlyDictionary<string, double?> BaseStats { get; }
        public IReadOnlyList<StaticNumericSeries> AbilityVariables { get; }
        public IReadOnlyList<StaticExtensionField> Extensions { get; }

        public UnitRecord(
            string stableId,
            LocalizedText name,
            int cost,
            IEnumerable<string> traitIds,
            string iconPath,
            IEnumerable<SourceStamp> sources,
            string role = "",
            LocalizedText description = null,
            IReadOnlyDictionary<string, double> effects = null,
            IReadOnlyDictionary<string, double?> baseStats = null,
            IEnumerable<StaticNumericSeries> abilityVariables = null,
            IEnumerable<StaticExtensionField> extensions = null)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            Name = name ?? LocalizedText.Empty;
            Cost = cost;
            TraitIds = DomainValues.Copy(traitIds);
            IconPath = iconPath ?? "";
            Sources = DomainValues.Copy(sources);
            BaseStats = DomainValues.CopyNullableEffects(baseStats);
            AbilityVariables = DomainValues.Copy(abilityVariables);
            Extensions = DomainValues.CopyExtensions(extensions);
            Role = role ?? "";
            Description = description ?? LocalizedText.Empty;
            Effects = DomainValues.CopyEffects(effects);
        }
    }
}
