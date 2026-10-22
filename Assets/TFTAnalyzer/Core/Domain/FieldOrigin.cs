using System;
using System.Collections.Generic;
using System.Linq;

namespace TFTAnalyzer.Domain
{
    /// <summary>The document lineage of one selected catalog value, identified by a JSON pointer.</summary>
    public sealed class FieldOrigin
    {
        public string RecordType { get; }
        public string StableId { get; }
        public string Pointer { get; }
        public IReadOnlyList<SourceStamp> Sources { get; }

        public FieldOrigin(string recordType, string stableId, string pointer, IEnumerable<SourceStamp> sources)
        {
            RecordType = DomainValues.RequireStable(recordType, nameof(recordType));
            StableId = DomainValues.RequireStable(stableId, nameof(stableId));
            Pointer = DomainValues.RequireStable(pointer, nameof(pointer));
            Sources = DomainValues.Copy(sources);
        }

        public static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
    }

    /// <summary>Fixed schema 3 required pointers, independent of the origins supplied by a producer.</summary>
    public static class FieldOriginFields
    {
        public static IReadOnlyList<string> Required(object record)
        {
            var fields = new List<string> { "/stableId" };
            void Text(string path, string text) { if (!string.IsNullOrWhiteSpace(text)) fields.Add(path); }
            void List(string path, IReadOnlyList<string> list) { if (list.Count > 0) fields.Add(path); }
            void Common(LocalizedText name, LocalizedText description, string icon, IReadOnlyDictionary<string, double> effects)
            {
                Text("/name/koKr", name.KoKr); Text("/name/enUs", name.EnUs);
                Text("/description/koKr", description.KoKr); Text("/description/enUs", description.EnUs);
                Text("/iconPath", icon);
                fields.AddRange(effects.Keys.Select(key => "/effects/" + FieldOrigin.Escape(key)));
            }
            switch (record)
            {
                case UnitRecord unit:
                    Common(unit.Name, unit.Description, unit.IconPath, unit.Effects);
                    fields.Add("/cost"); Text("/role", unit.Role); List("/traitIds", unit.TraitIds); break;
                case TraitRecord trait:
                    Common(trait.Name, trait.Description, trait.IconPath, trait.Effects); break;
                case ItemRecord item:
                    Common(item.Name, item.Description, item.IconPath, item.Effects);
                    List("/componentIds", item.ComponentIds); break;
                case AugmentRecord augment:
                    Common(augment.Name, augment.Description, augment.IconPath, augment.Effects);
                    Text("/tier", augment.Tier); List("/relatedIds", augment.RelatedIds); break;
                default: throw new ArgumentException("Unknown catalog record.", nameof(record));
            }
            return Array.AsReadOnly(fields.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }
    }
}
