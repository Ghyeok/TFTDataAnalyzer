using System.Collections.Generic;

namespace TFTAnalyzer.Domain
{
    public sealed class TraitRecord
    {
        public string StableId { get; }
        public LocalizedText Name { get; }
        public LocalizedText Description { get; }
        public IReadOnlyDictionary<string, double> Effects { get; }
        public string IconPath { get; }
        public IReadOnlyList<SourceStamp> Sources { get; }

        public TraitRecord(
            string stableId,
            LocalizedText name,
            LocalizedText description,
            IReadOnlyDictionary<string, double> effects,
            string iconPath,
            IEnumerable<SourceStamp> sources)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            Name = name ?? LocalizedText.Empty;
            Description = description ?? LocalizedText.Empty;
            Effects = DomainValues.CopyEffects(effects);
            IconPath = iconPath ?? "";
            Sources = DomainValues.Copy(sources);
        }
    }
}
