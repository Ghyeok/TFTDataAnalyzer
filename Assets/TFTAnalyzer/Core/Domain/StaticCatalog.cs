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
        public IReadOnlyList<FieldOrigin> FieldOrigins { get; }

        public StaticCatalog(
            PatchManifest manifest,
            IEnumerable<UnitRecord> units,
            IEnumerable<TraitRecord> traits,
            IEnumerable<ItemRecord> items,
            IEnumerable<AugmentRecord> augments,
            IEnumerable<FieldOrigin> fieldOrigins = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Units = DomainValues.Copy(units);
            Traits = DomainValues.Copy(traits);
            Items = DomainValues.Copy(items);
            Augments = DomainValues.Copy(augments);
            FieldOrigins = DomainValues.Copy(fieldOrigins);
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

        public static void RequireFinite(double? value)
        {
            if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
                throw new ArgumentException("Static numeric values must be finite.");
        }

        public static IReadOnlyDictionary<string, double?> CopyNullableEffects(IReadOnlyDictionary<string, double?> values)
        {
            var copy = new SortedDictionary<string, double?>(StringComparer.Ordinal);
            if (values != null) foreach (var pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) throw new ArgumentException("Static value key is required.");
                RequireFinite(pair.Value); copy.Add(pair.Key, pair.Value);
            }
            return new ReadOnlyDictionary<string, double?>(copy);
        }

        public static IReadOnlyList<StaticExtensionField> CopyExtensions(IEnumerable<StaticExtensionField> values)
        {
            var result = Copy(values);
            if (result.Any(x => x == null) || result.Select(x => (x.SourceName, x.Locale, x.Pointer)).Distinct().Count() != result.Count ||
                result.Sum(x => (long)System.Text.Encoding.UTF8.GetByteCount(x.SourceName + x.Locale + x.Pointer + x.CanonicalJson)) > 65536)
                throw new ArgumentException("Duplicate or oversized source extensions.");
            return result;
        }
    }
}
