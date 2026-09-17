using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TFTAnalyzer.Domain
{
    public sealed class StaticCatalog
    {
        public PatchManifest Manifest { get; }
        public IReadOnlyList<UnitRecord> Units { get; }
        public IReadOnlyList<TraitRecord> Traits { get; }
        public IReadOnlyList<ItemRecord> Items { get; }
        public IReadOnlyList<AugmentRecord> Augments { get; }

        public StaticCatalog(
            PatchManifest manifest,
            IEnumerable<UnitRecord> units,
            IEnumerable<TraitRecord> traits,
            IEnumerable<ItemRecord> items,
            IEnumerable<AugmentRecord> augments)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Units = DomainValues.Copy(units);
            Traits = DomainValues.Copy(traits);
            Items = DomainValues.Copy(items);
            Augments = DomainValues.Copy(augments);
        }
    }

    internal static class DomainValues
    {
        public static string RequireStable(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be blank.", parameterName);
            }

            return value.Trim();
        }

        public static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            return Array.AsReadOnly((values ?? Enumerable.Empty<T>()).ToArray());
        }

        public static IReadOnlyDictionary<string, double> CopyEffects(
            IReadOnlyDictionary<string, double> values)
        {
            var copy = new SortedDictionary<string, double>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (var pair in values)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return new ReadOnlyDictionary<string, double>(copy);
        }

        public static DateTime AsUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }
    }
}
