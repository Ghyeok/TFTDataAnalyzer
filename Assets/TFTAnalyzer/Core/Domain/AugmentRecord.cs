using System.Collections.Generic;

namespace TFTAnalyzer.Domain
{
    public sealed class AugmentRecord
    {
        public string StableId { get; }
        public LocalizedText Name { get; }
        public string Tier { get; }
        public LocalizedText Description { get; }
        public IReadOnlyList<string> RelatedIds { get; }
        public IReadOnlyDictionary<string, double> Effects { get; }
        public string IconPath { get; }
        public IReadOnlyList<SourceStamp> Sources { get; }

        public AugmentRecord(
            string stableId,
            LocalizedText name,
            string tier,
            LocalizedText description,
            IEnumerable<string> relatedIds,
            IReadOnlyDictionary<string, double> effects,
            string iconPath,
            IEnumerable<SourceStamp> sources)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            Name = name ?? LocalizedText.Empty;
            Tier = tier ?? "";
            Description = description ?? LocalizedText.Empty;
            RelatedIds = DomainValues.Copy(relatedIds);
            Effects = DomainValues.CopyEffects(effects);
            IconPath = iconPath ?? "";
            Sources = DomainValues.Copy(sources);
        }
    }
}
