using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TFTAnalyzer.Diff;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.UI.Data
{
    internal sealed class CatalogDiffFieldDisplay
    {
        internal const string Unavailable = "비교 당시 원본 자료를 확인할 수 없습니다.";
        internal string Before { get; }
        internal string After { get; }

        /// <summary>Builds directional display values from the original records without changing the public field strings.</summary>
        internal CatalogDiffFieldDisplay(FieldChange field, object before, object after)
        {
            Before = Format(field.FieldName, before, field.Before);
            After = Format(field.FieldName, after, field.After);
        }

        /// <summary>Quotes sorted relationship, effect, and source structures, or reports missing originals without guessing their contents.</summary>
        private static string Format(string field, object record, string ordinary)
        {
            if (field != "traitIds" && field != "componentIds" && field != "relatedIds" &&
                field != "effects" && field != "sources") return ordinary;
            if (record == null) return Unavailable;
            IEnumerable<string> relations = null;
            IReadOnlyDictionary<string, double> effects;
            IReadOnlyList<SourceStamp> sources;
            switch (record)
            {
                case UnitRecord unit:
                    if (field == "traitIds") relations = unit.TraitIds;
                    effects = unit.Effects; sources = unit.Sources; break;
                case TraitRecord trait:
                    effects = trait.Effects; sources = trait.Sources; break;
                case ItemRecord item:
                    if (field == "componentIds") relations = item.ComponentIds;
                    effects = item.Effects; sources = item.Sources; break;
                case AugmentRecord augment:
                    if (field == "relatedIds") relations = augment.RelatedIds;
                    effects = augment.Effects; sources = augment.Sources; break;
                default: return Unavailable;
            }
            if (relations != null)
                return "[" + string.Join(",", relations.OrderBy(x => x, StringComparer.Ordinal).Select(Quote)) + "]";
            if (field == "effects")
                return "{" + string.Join(",", effects.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => Quote(x.Key) + ":" + x.Value.ToString("R", CultureInfo.InvariantCulture))) + "}";
            if (field == "sources")
                return "[" + string.Join(",", sources.OrderBy(x => x.SourceName, StringComparer.Ordinal)
                    .ThenBy(x => x.Url, StringComparer.Ordinal).Select(x =>
                        "{\"SourceName\":" + Quote(x.SourceName) + ",\"Url\":" + Quote(x.Url) +
                        ",\"ETag\":" + Quote(x.ETag) + ",\"Sha256\":" + Quote(x.Sha256) +
                        ",\"IsSupplemental\":" + (x.IsSupplemental ? "true" : "false") + "}")) + "]";
            return Unavailable;
        }

        /// <summary>Quotes JSON strings with distinct control and backslash escapes while preserving valid surrogate pairs.</summary>
        private static string Quote(string value)
        {
            if (value == null) return "null";
            var result = new StringBuilder("\"");
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    case '\b': result.Append("\\b"); break;
                    case '\f': result.Append("\\f"); break;
                    default:
                        if (char.IsHighSurrogate(character) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                        { result.Append(character).Append(value[++index]); }
                        else if (char.IsControl(character) || char.IsSurrogate(character) || character == '\u2028' || character == '\u2029')
                            result.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else result.Append(character);
                        break;
                }
            }
            return result.Append('"').ToString();
        }
    }
}
