using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Diff
{
    public sealed class CatalogDiffer
    {
        public CatalogDiff Compare(StaticCatalog previous, StaticCatalog current)
        {
            if (previous == null)
            {
                throw new ArgumentNullException(nameof(previous));
            }

            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            return new CatalogDiff(
                Compare(previous.Units, current.Units, value => value.StableId, UnitFields),
                Compare(previous.Traits, current.Traits, value => value.StableId, TraitFields),
                Compare(previous.Items, current.Items, value => value.StableId, ItemFields),
                Compare(previous.Augments, current.Augments, value => value.StableId, AugmentFields));
        }

        /// <summary>Compares records by stable ID and reports added, removed and changed fields.</summary>
        private static EntityDiff Compare<T>(
            IEnumerable<T> previous,
            IEnumerable<T> current,
            Func<T, string> id,
            Func<T, SortedDictionary<string, ComparedField>> fields)
        {
            var before = Index(previous, id);
            var after = Index(current, id);
            var added = after.Keys.Except(before.Keys, StringComparer.Ordinal);
            var removed = before.Keys.Except(after.Keys, StringComparer.Ordinal);
            var changed = before.Keys
                .Intersect(after.Keys, StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(stableId => Change(stableId, fields(before[stableId]), fields(after[stableId])))
                .Where(change => change.Fields.Count > 0);
            return new EntityDiff(added, removed, changed);
        }

        private static Dictionary<string, T> Index<T>(
            IEnumerable<T> values,
            Func<T, string> id)
        {
            return values
                .GroupBy(id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        /// <summary>Uses structural field equivalence while preserving the legacy displayed values.</summary>
        private static RecordChange Change(
            string stableId,
            SortedDictionary<string, ComparedField> before,
            SortedDictionary<string, ComparedField> after)
        {
            var names = before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
            var changes = new List<FieldChange>();
            foreach (var name in names)
            {
                before.TryGetValue(name, out var oldValue);
                after.TryGetValue(name, out var newValue);
                if (!(oldValue?.EquivalentTo(newValue) ?? newValue == null))
                {
                    changes.Add(new FieldChange(name, oldValue?.Value, newValue?.Value));
                }
            }

            return new RecordChange(stableId, changes);
        }

        /// <summary>Builds the comparable fields for a unit.</summary>
        private static SortedDictionary<string, ComparedField> UnitFields(UnitRecord value)
        {
            var result = Common(value.Name, value.IconPath, value.Sources);
            result["cost"] = value.Cost.ToString(CultureInfo.InvariantCulture);
            result["traitIds"] = StableList(value.TraitIds);
            result["role"] = value.Role;
            AddText(result, "description", value.Description);
            result["effects"] = Effects(value.Effects);
            AddStructured(result, value);
            return result;
        }

        /// <summary>Builds the comparable fields for a trait.</summary>
        private static SortedDictionary<string, ComparedField> TraitFields(TraitRecord value)
        {
            var result = Common(value.Name, value.IconPath, value.Sources);
            AddText(result, "description", value.Description);
            result["effects"] = Effects(value.Effects);
            AddStructured(result, value);
            return result;
        }

        /// <summary>Builds the comparable fields for an item.</summary>
        private static SortedDictionary<string, ComparedField> ItemFields(ItemRecord value)
        {
            var result = Common(value.Name, value.IconPath, value.Sources);
            AddText(result, "description", value.Description);
            result["componentIds"] = StableList(value.ComponentIds);
            result["effects"] = Effects(value.Effects);
            AddStructured(result, value);
            return result;
        }

        /// <summary>Builds the comparable fields for an augment.</summary>
        private static SortedDictionary<string, ComparedField> AugmentFields(AugmentRecord value)
        {
            var result = Common(value.Name, value.IconPath, value.Sources);
            result["tier"] = value.Tier;
            AddText(result, "description", value.Description);
            result["relatedIds"] = StableList(value.RelatedIds);
            result["effects"] = Effects(value.Effects);
            AddStructured(result, value);
            return result;
        }

        /// <summary>Adds structured fields and deterministically ordered extension data for the record type.</summary>
        private static void AddStructured(SortedDictionary<string, ComparedField> fields, object value)
        {
            string Json(object item) => Newtonsoft.Json.JsonConvert.SerializeObject(item);
            IReadOnlyList<StaticExtensionField> extensions;
            switch (value)
            {
                case UnitRecord unit:
                    fields["/baseStats"] = Json(unit.BaseStats); fields["/abilityVariables"] = Json(unit.AbilityVariables); extensions = unit.Extensions; break;
                case TraitRecord trait:
                    fields["/breakpoints"] = Json(trait.Breakpoints); extensions = trait.Extensions; break;
                case ItemRecord item:
                    fields["/unique"] = Json(item.Unique); extensions = item.Extensions; break;
                case AugmentRecord augment: extensions = augment.Extensions; break;
                default: throw new ArgumentException("Unknown static record.");
            }
            fields["/extensions"] = Json(extensions.OrderBy(x => x.SourceName, StringComparer.Ordinal).ThenBy(x => x.Locale, StringComparer.Ordinal)
                .ThenBy(x => x.Pointer, StringComparer.Ordinal).ToArray());
        }

        /// <summary>Compares source components individually in stable name and URL order.</summary>
        private static SortedDictionary<string, ComparedField> Common(
            LocalizedText name,
            string iconPath,
            IEnumerable<SourceStamp> sources)
        {
            var result = new SortedDictionary<string, ComparedField>(StringComparer.Ordinal);
            AddText(result, "name", name);
            result["iconPath"] = iconPath ?? "";
            var sourceParts = sources
                .OrderBy(source => source.SourceName, StringComparer.Ordinal)
                .ThenBy(source => source.Url, StringComparer.Ordinal)
                .Select(source => new[]
                {
                    source.SourceName,
                    source.Url,
                    source.ETag,
                    source.Sha256,
                    source.IsSupplemental ? "1" : "0"
                }).ToArray();
            result["sources"] = new ComparedField(
                string.Join("\u001e", sourceParts.Select(parts => string.Join("\u001f", parts))),
                sourceParts.SelectMany(parts => parts));
            return result;
        }

        private static void AddText(
            IDictionary<string, ComparedField> values,
            string prefix,
            LocalizedText text)
        {
            values[$"{prefix}.koKr"] = text.KoKr;
            values[$"{prefix}.enUs"] = text.EnUs;
        }

        /// <summary>Ignores relationship order while retaining duplicate elements and their boundaries.</summary>
        private static ComparedField StableList(IEnumerable<string> values)
        {
            var parts = values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return new ComparedField(string.Join("\u001f", parts), parts);
        }

        /// <summary>Compares ordinal effect keys and invariant round-trip values without joining their boundaries.</summary>
        private static ComparedField Effects(IEnumerable<KeyValuePair<string, double>> values)
        {
            var parts = values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new[] { pair.Key, pair.Value.ToString("R", CultureInfo.InvariantCulture) })
                .ToArray();
            return new ComparedField(
                string.Join("\u001f", parts.Select(pair => $"{pair[0]}={pair[1]}")),
                parts.SelectMany(pair => pair));
        }

        private sealed class ComparedField
        {
            public string Value { get; }
            private readonly string[] _parts;

            public ComparedField(string value, IEnumerable<string> parts = null)
            {
                Value = value;
                _parts = (parts ?? new[] { value }).ToArray();
            }

            /// <summary>Checks each stored component using ordinal string equality.</summary>
            public bool EquivalentTo(ComparedField other)
            {
                return other != null && _parts.SequenceEqual(other._parts, StringComparer.Ordinal);
            }

            public static implicit operator ComparedField(string value)
            {
                return new ComparedField(value);
            }
        }
    }
}
