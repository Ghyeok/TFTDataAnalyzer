using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion
{
    internal sealed class StructuredExtensionBudgetException : InvalidOperationException
    {
        internal const string Code = "structured_extension_budget_exceeded";
        internal StructuredExtensionBudgetException() : base(Code) { }
    }

    // Legacy value selection stays in CatalogMerger. This layer selects schema4 values and
    // copies only their chosen document origins, without changing record/manifest Sources.
    internal sealed class StructuredCatalogMerge
    {
        private readonly StaticCatalog _official, _supplement;
        private readonly ICollection<MergeConflict> _decisions;
        private readonly List<FieldOrigin> _origins;
        private StructuredCatalogMerge(StaticCatalog official, StaticCatalog supplement,
            IEnumerable<FieldOrigin> legacyOrigins, ICollection<MergeConflict> decisions)
        {
            _official = official; _supplement = supplement; _decisions = decisions;
            CheckPointers(official); CheckPointers(supplement);
            _origins = legacyOrigins.Where(x => !Structured(x.Pointer)).ToList();
        }
        internal static StaticCatalog Merge(StaticCatalog official, StaticCatalog supplement, StaticCatalog values,
            IEnumerable<FieldOrigin> legacyOrigins, ICollection<MergeConflict> decisions)
        {
            var merge = new StructuredCatalogMerge(official, supplement, legacyOrigins, decisions);
            var units = values.Units.Select(merge.Unit).ToArray(); var traits = values.Traits.Select(merge.Trait).ToArray();
            var items = values.Items.Select(merge.Item).ToArray(); var augments = values.Augments.Select(merge.Augment).ToArray();
            return new StaticCatalog(values.Manifest, units, traits, items, augments, merge._origins);
        }

        private UnitRecord Unit(UnitRecord value)
        {
            var left = _official.Units.SingleOrDefault(x => x.StableId == value.StableId);
            var right = _supplement.Units.SingleOrDefault(x => x.StableId == value.StableId);
            var stats = Stats(value.StableId, left?.BaseStats, right?.BaseStats);
            var series = List("unit", value.StableId, "/abilityVariables", left?.AbilityVariables, right?.AbilityVariables);
            return new UnitRecord(value.StableId, value.Name, value.Cost, value.TraitIds, value.IconPath, value.Sources,
                value.Role, value.Description, value.Effects, stats, series,
                Extensions("unit", value.StableId, left?.Extensions, right?.Extensions));
        }
        private TraitRecord Trait(TraitRecord value)
        {
            var left = _official.Traits.SingleOrDefault(x => x.StableId == value.StableId);
            var right = _supplement.Traits.SingleOrDefault(x => x.StableId == value.StableId);
            return new TraitRecord(value.StableId, value.Name, value.Description, value.Effects, value.IconPath, value.Sources,
                List("trait", value.StableId, "/breakpoints", left?.Breakpoints, right?.Breakpoints),
                Extensions("trait", value.StableId, left?.Extensions, right?.Extensions));
        }
        private ItemRecord Item(ItemRecord value)
        {
            var left = _official.Items.SingleOrDefault(x => x.StableId == value.StableId);
            var right = _supplement.Items.SingleOrDefault(x => x.StableId == value.StableId);
            var a = left?.Unique; var b = right?.Unique; var supplement = !a.HasValue && b.HasValue;
            var selected = supplement ? b : a;
            if (supplement) Decision("item", value.StableId, "/unique", Json(a), Json(b), MergeResolution.SupplementFill);
            else if (a.HasValue && b.HasValue && a.Value != b.Value) Decision("item", value.StableId, "/unique", Json(a), Json(b), MergeResolution.OfficialWins);
            if (selected.HasValue) Copy("item", value.StableId, "/unique", supplement);
            return new ItemRecord(value.StableId, value.Name, value.Description, value.ComponentIds, value.Effects, value.IconPath, value.Sources,
                selected, Extensions("item", value.StableId, left?.Extensions, right?.Extensions));
        }
        private AugmentRecord Augment(AugmentRecord value)
        {
            var left = _official.Augments.SingleOrDefault(x => x.StableId == value.StableId);
            var right = _supplement.Augments.SingleOrDefault(x => x.StableId == value.StableId);
            return new AugmentRecord(value.StableId, value.Name, value.Tier, value.Description, value.RelatedIds, value.Effects,
                value.IconPath, value.Sources, Extensions("augment", value.StableId, left?.Extensions, right?.Extensions));
        }
        private IReadOnlyDictionary<string, double?> Stats(string id, IReadOnlyDictionary<string, double?> left, IReadOnlyDictionary<string, double?> right)
        {
            left = left ?? new Dictionary<string, double?>(); right = right ?? new Dictionary<string, double?>();
            var result = new SortedDictionary<string, double?>(StringComparer.Ordinal);
            foreach (var key in left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                var hasA = left.TryGetValue(key, out var a); var hasB = right.TryGetValue(key, out var b);
                var supplement = (!hasA || !a.HasValue) && hasB && (b.HasValue || !hasA);
                result.Add(key, supplement ? b : a); var path = "/baseStats/" + FieldOrigin.Escape(key);
                if (supplement) Decision("unit", id, path, hasA ? Json(a) : "missing", Json(b), MergeResolution.SupplementFill);
                else if (hasA && hasB && a.HasValue && b.HasValue && !a.Value.Equals(b.Value))
                    Decision("unit", id, path, Json(a), Json(b), MergeResolution.OfficialWins);
                Copy("unit", id, path, supplement);
            }
            return result;
        }
        private IReadOnlyList<T> List<T>(string type, string id, string path, IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            left = left ?? Array.Empty<T>(); right = right ?? Array.Empty<T>();
            var supplement = left.Count == 0 && right.Count > 0;
            if (supplement) Decision(type, id, path, Json(left), Json(right), MergeResolution.SupplementFill);
            else if (left.Count > 0 && right.Count > 0 && Json(left) != Json(right)) Decision(type, id, path, Json(left), Json(right), MergeResolution.OfficialWins);
            var selected = supplement ? right : left;
            if (selected.Count > 0) Copy(type, id, path, supplement);
            return selected;
        }
        private IReadOnlyList<StaticExtensionField> Extensions(string type, string id,
            IReadOnlyList<StaticExtensionField> left, IReadOnlyList<StaticExtensionField> right)
        {
            var entries = new Dictionary<(string Source, string Locale, string Pointer), (StaticExtensionField Value, bool Supplement, int Index)>();
            void Add(IReadOnlyList<StaticExtensionField> values, bool supplement)
            {
                if (values == null) return;
                for (var i = 0; i < values.Count; i++)
                {
                    var value = values[i]; var key = (value.SourceName, value.Locale, value.Pointer);
                    if (entries.TryGetValue(key, out var previous))
                    { if (previous.Value.CanonicalJson != value.CanonicalJson) throw new JsonSerializationException("conflicting_source_extension"); }
                    else entries.Add(key, (value, supplement, i));
                }
            }
            Add(left, false); Add(right, true);
            var sorted = entries.Values.OrderBy(x => x.Value.SourceName, StringComparer.Ordinal).ThenBy(x => x.Value.Locale, StringComparer.Ordinal)
                .ThenBy(x => x.Value.Pointer, StringComparer.Ordinal).ToArray();
            if (sorted.Sum(x => (long)System.Text.Encoding.UTF8.GetByteCount(x.Value.SourceName + x.Value.Locale + x.Value.Pointer + x.Value.CanonicalJson)) > 65536)
                throw new StructuredExtensionBudgetException();
            for (var i = 0; i < sorted.Length; i++)
            {
                var value = sorted[i]; var path = "/extensions/" + value.Index + "/canonicalJson";
                foreach (var origin in Source(value.Supplement).FieldOrigins.Where(x => x.RecordType == type && x.StableId == id && x.Pointer == path))
                    _origins.Add(new FieldOrigin(type, id, "/extensions/" + i + "/canonicalJson", origin.Sources));
            }
            return Array.AsReadOnly(sorted.Select(x => x.Value).ToArray());
        }
        private void Copy(string type, string id, string path, bool supplement)
        {
            foreach (var origin in Source(supplement).FieldOrigins.Where(x => x.RecordType == type && x.StableId == id &&
                (x.Pointer == path || x.Pointer.StartsWith(path + "/", StringComparison.Ordinal)))) _origins.Add(origin);
        }
        private StaticCatalog Source(bool supplement) => supplement ? _supplement : _official;
        private void Decision(string type, string id, string path, string a, string b, MergeResolution resolution) =>
            _decisions.Add(new MergeConflict(type, id, path, a, b, resolution));
        private static string Json(object value) => JsonConvert.SerializeObject(value, new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture, TypeNameHandling = TypeNameHandling.None });
        private static bool Structured(string pointer) => new[] { "/baseStats/", "/abilityVariables/", "/breakpoints/", "/extensions/" }
            .Any(x => pointer.StartsWith(x, StringComparison.Ordinal)) || pointer == "/unique";
        private static void CheckPointers(StaticCatalog catalog)
        {
            var expected = new HashSet<(string, string, string)>();
            void Add(string type, string id, object value) { foreach (var path in FieldOriginFields.Required(value, 4)) expected.Add((type, id, path)); }
            foreach (var r in catalog.Units) Add("unit", r.StableId, r);
            foreach (var r in catalog.Traits) Add("trait", r.StableId, r);
            foreach (var r in catalog.Items) Add("item", r.StableId, r);
            foreach (var r in catalog.Augments) Add("augment", r.StableId, r);
            if (catalog.FieldOrigins.Any(x => Structured(x.Pointer) && !expected.Contains((x.RecordType, x.StableId, x.Pointer))))
                throw new JsonSerializationException("invalid_structured_origin");
        }
    }
}
