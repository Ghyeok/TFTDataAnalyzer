using System.Collections.Generic;

namespace TFTAnalyzer.Domain
{
    public sealed class ItemRecord
    {
        public string StableId { get; }
        public LocalizedText Name { get; }
        public LocalizedText Description { get; }
        public IReadOnlyList<string> ComponentIds { get; }
        public IReadOnlyDictionary<string, double> Effects { get; }
        public string IconPath { get; }
        public IReadOnlyList<SourceStamp> Sources { get; }
        public bool? Unique { get; }
        public IReadOnlyList<StaticExtensionField> Extensions { get; }

        public ItemRecord(
            string stableId,
            LocalizedText name,
            LocalizedText description,
            IEnumerable<string> componentIds,
            IReadOnlyDictionary<string, double> effects,
            string iconPath,
            IEnumerable<SourceStamp> sources,
            bool? unique = null,
            IEnumerable<StaticExtensionField> extensions = null)
        {
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            Name = name ?? LocalizedText.Empty;
            Description = description ?? LocalizedText.Empty;
            ComponentIds = DomainValues.Copy(componentIds);
            Effects = DomainValues.CopyEffects(effects);
            IconPath = iconPath ?? "";
            Sources = DomainValues.Copy(sources);
            Unique = unique;
            Extensions = DomainValues.CopyExtensions(extensions);
        }
    }
}
