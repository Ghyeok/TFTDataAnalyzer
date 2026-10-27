using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.UI.Data
{
    public sealed class CatalogStructuredRow
    {
        public string Field { get; }
        public string Label { get; }
        public string Value { get; }
        public IReadOnlyList<SourceStamp> Sources { get; }
        public string ExtensionJson { get; }
        public string SourceText => Sources.Count == 0 ? "선택한 원본 값의 출처 정보 없음" : string.Join("\n", Sources.Select(s =>
            (s.IsSupplemental ? "보완" : "공식") + " · " + s.SourceName + "\n" + s.Url + "\nSHA-256: " + s.Sha256));
        internal CatalogStructuredRow(string field, string label, string value, IEnumerable<SourceStamp> sources, string extensionJson = null)
        {
            Field = field; Label = label; Value = value;
            Sources = Array.AsReadOnly(sources.ToArray()); ExtensionJson = extensionJson;
        }
    }

    /// <summary>Projects one selected record, without executing or interpreting raw static JSON.</summary>
    public static class CatalogStructuredDetails
    {
        public static IReadOnlyList<CatalogStructuredRow> Project(StaticCatalog catalog, string recordType, string stableId)
        {
            if (catalog == null || catalog.Manifest.SchemaVersion < 4) return Array.Empty<CatalogStructuredRow>();
            var origins = catalog.FieldOrigins.Where(x => x.RecordType == recordType && x.StableId == stableId).ToLookup(x => x.Pointer);
            var rows = new List<CatalogStructuredRow>(); IReadOnlyList<StaticExtensionField> extensions = Array.Empty<StaticExtensionField>();
            void Add(string path, string label, string value, string raw = null) => rows.Add(new CatalogStructuredRow(path, label, value,
                origins[path].SelectMany(x => x.Sources).GroupBy(x => (x.SourceName, x.Url, x.Sha256, x.IsSupplemental)).Select(x => x.First()), raw));
            string Number(double? value) => value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "자료 없음";
            switch (recordType)
            {
                case "unit":
                    var unit = catalog.Units.FirstOrDefault(x => x.StableId == stableId); if (unit == null) break;
                    foreach (var p in unit.BaseStats.OrderBy(x => x.Key, StringComparer.Ordinal)) Add("/baseStats/" + FieldOrigin.Escape(p.Key), "기본 능력치 " + p.Key, Number(p.Value));
                    for (var i = 0; i < unit.AbilityVariables.Count; i++)
                    {
                        var series = unit.AbilityVariables[i]; var path = "/abilityVariables/" + i;
                        Add(path + "/name", "스킬 수치의 원본 이름", series.Name);
                        for (var j = 0; j < series.Values.Count; j++) Add(path + "/values/" + j, series.Name + " · 원본 슬롯 " + j, Number(series.Values[j]));
                    }
                    extensions = unit.Extensions; break;
                case "trait":
                    var trait = catalog.Traits.FirstOrDefault(x => x.StableId == stableId); if (trait == null) break;
                    for (var i = 0; i < trait.Breakpoints.Count; i++)
                    {
                        var b = trait.Breakpoints[i]; var path = "/breakpoints/" + i; var label = "원본 구간 " + (i + 1) + " · ";
                        Add(path + "/minUnits", label + "최소 기물 수", Number(b.MinUnits)); Add(path + "/maxUnits", label + "최대 기물 수", Number(b.MaxUnits));
                        Add(path + "/style", label + "원본 스타일 코드", Number(b.Style));
                        foreach (var p in b.Effects.OrderBy(x => x.Key, StringComparer.Ordinal)) Add(path + "/effects/" + FieldOrigin.Escape(p.Key), label + p.Key, Number(p.Value));
                    }
                    extensions = trait.Extensions; break;
                case "item":
                    var item = catalog.Items.FirstOrDefault(x => x.StableId == stableId); if (item == null) break;
                    Add("/unique", "원본 고유 속성 (unique)", item.Unique.HasValue ? item.Unique.Value ? "예" : "아니요" : "자료 없음");
                    extensions = item.Extensions; break;
                case "augment": extensions = catalog.Augments.FirstOrDefault(x => x.StableId == stableId)?.Extensions ?? Array.Empty<StaticExtensionField>(); break;
            }
            for (var i = 0; i < extensions.Count; i++)
            {
                var x = extensions[i]; var json = x.CanonicalJson;
                var length = Math.Min(120, json.Length); if (length < json.Length && length > 0 && char.IsHighSurrogate(json[length - 1])) length--;
                Add("/extensions/" + i + "/canonicalJson", x.Locale + " · 원본 " + x.Pointer,
                    json.Substring(0, length) + (length < json.Length ? "… (원문 보기)" : ""), json);
            }
            return rows.AsReadOnly();
        }
    }
}
