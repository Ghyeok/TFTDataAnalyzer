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

        private static EntityDiff Compare<T>(
            IEnumerable<T> previous,
            IEnumerable<T> current,
            Func<T, string> id,
            Func<T, SortedDictionary<string, string>> fields)
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

        private static RecordChange Change(
            string stableId,
            SortedDictionary<string, string> before,
            SortedDictionary<string, string> after)
        {
            var names = before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
            var changes = new List<FieldChange>();
            foreach (var name in names)
            {
                before.TryGetValue(name, out var oldValue);
                after.TryGetValue(name, out var newValue);
                if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                {
                    changes.Add(new FieldChange(name, oldValue, newValue));
                }
            }

            return new RecordChange(stableId, changes);
        }

        private static SortedDictionary<string, string> UnitFields(UnitRecord value)
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

        private static SortedDictionary<string, string> TraitFields(TraitRecord value)
        {
            var result = Common(value.Name, value.IconPath, value.Sources);
            AddText(result, "description", value.Description);
            result["effects"] = Effects(value.Effects);
            AddStructured(result, value);
            return result;
        }

        private static SortedDictionary<string, string> ItemFields(ItemRecord value)
        {
            var result = Common(value.Name, value.IconPath, value.Sources);
            AddText(result, "description", value.Description);
            result["componentIds"] = StableList(value.ComponentIds);
            result["effects"] = Effects(value.Effects);
            AddStructured(result, value);
            return result;
        }

        private static SortedDictionary<string, string> AugmentFields(AugmentRecord value)
        {
            var result = Common(value.Name, value.IconPath, value.Sources);
            result["tier"] = value.Tier;
            AddText(result, "description", value.Description);
            result["relatedIds"] = StableList(value.RelatedIds);
            result["effects"] = Effects(value.Effects);
            AddStructured(result, value);
            return result;
        }

        private static void AddStructured(SortedDictionary<string, string> fields, object value)
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

        private static SortedDictionary<string, string> Common(
            LocalizedText name,
            string iconPath,
            IEnumerable<SourceStamp> sources)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            AddText(result, "name", name);
            result["iconPath"] = iconPath ?? "";
            result["sources"] = string.Join("\u001e", sources
                .OrderBy(source => source.SourceName, StringComparer.Ordinal)
                .ThenBy(source => source.Url, StringComparer.Ordinal)
                .Select(source => string.Join("\u001f", new[]
                {
                    source.SourceName,
                    source.Url,
                    source.ETag,
                    source.Sha256,
                    source.IsSupplemental ? "1" : "0"
                })));
            return result;
        }

        private static void AddText(
            IDictionary<string, string> values,
            string prefix,
            LocalizedText text)
        {
            values[$"{prefix}.koKr"] = text.KoKr;
            values[$"{prefix}.enUs"] = text.EnUs;
        }

        private static string StableList(IEnumerable<string> values)
        {
            return string.Join("\u001f", values.OrderBy(value => value, StringComparer.Ordinal));
        }

        private static string Effects(IEnumerable<KeyValuePair<string, double>> values)
        {
            return string.Join("\u001f", values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value.ToString("R", CultureInfo.InvariantCulture)}"));
        }
    }
}
