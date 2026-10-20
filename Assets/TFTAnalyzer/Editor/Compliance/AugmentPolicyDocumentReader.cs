using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Compliance
{
    /// <summary>Validates the complete bounded JSON document before constructing immutable classifications.</summary>
    internal static class AugmentPolicyDocumentReader
    {
        internal const int MaximumBytes = 256 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static MetricPolicyMetadata Read(byte[] bytes, StaticCatalog catalog, CatalogIdentity identity, CancellationToken token)
        {
            if (bytes.Length > MaximumBytes) throw Invalid();
            var text = Utf8.GetString(bytes);
            new JsonGrammar(text).Validate();
            JObject document;
            using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 })
                document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Fields(document, "schemaVersion", "catalog", "contentHash", "rules");
            if (Integer(document["schemaVersion"]) != 1) throw Invalid();
            var context = document["catalog"] as JObject;
            Fields(context, "setId", "setNumber", "patchId", "sourceGameVersion", "sourceProfile");
            var patch = identity.Patch;
            if (Text(context["setId"]) != patch.SetId || Integer(context["setNumber"]) != patch.SetNumber ||
                Text(context["patchId"]) != patch.PatchId || Text(context["sourceGameVersion"]) != patch.SourceGameVersion ||
                Text(context["sourceProfile"]) != identity.ProfileId) throw Invalid();
            var contentHash = Text(document["contentHash"]);
            if (!ReviewedAugmentPolicyRegistration.IsHash(contentHash) || contentHash != catalog.Manifest.ContentHash) throw Invalid();
            var rules = document["rules"] as JArray;
            if (rules == null || rules.Count > 4096) throw Invalid();
            var known = new HashSet<string>(catalog.Augments.Select(x => x.StableId), StringComparer.Ordinal);
            var classifications = new Dictionary<string, AugmentPolicyKind>(StringComparer.Ordinal);
            foreach (var value in rules)
            {
                token.ThrowIfCancellationRequested();
                var rule = value as JObject; Fields(rule, "augmentId", "kind");
                var id = Text(rule["augmentId"]).Trim();
                var name = Text(rule["kind"]);
                if (!known.Contains(id) || classifications.ContainsKey(id) || !Enum.TryParse(name, out AugmentPolicyKind kind) ||
                    !Enum.IsDefined(typeof(AugmentPolicyKind), kind) || kind.ToString() != name) throw Invalid();
                classifications.Add(id, kind);
            }
            return new MetricPolicyMetadata(identity, classifications);
        }

        private static void Fields(JObject value, params string[] names)
        { if (value == null || value.Count != names.Length || names.Any(name => value.Property(name, StringComparison.Ordinal) == null)) throw Invalid(); }
        private static string Text(JToken value)
        { if (value?.Type != JTokenType.String) throw Invalid(); return value.Value<string>(); }
        private static int Integer(JToken value)
        {
            if (value?.Type != JTokenType.Integer) throw Invalid();
            if (!int.TryParse(value.ToString(Formatting.None), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1) throw Invalid();
            return number;
        }
        private static InvalidDataException Invalid() => new InvalidDataException("augment_policy_document_invalid");

        // Newtonsoft intentionally accepts JavaScript syntax. This bounded grammar pass excludes it before schema parsing.
        private sealed class JsonGrammar
        {
            private static readonly Regex Number = new Regex(@"\G-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);
            private readonly string _text;
            private int _position;
            internal JsonGrammar(string text) { _text = text; }
            internal void Validate() { Value(0); White(); if (_position != _text.Length) throw Invalid(); }
            private void Value(int depth)
            {
                White(); if (_position >= _text.Length) throw Invalid();
                var next = _text[_position];
                if (next == '{' || next == '[')
                {
                    if (depth >= 8) throw Invalid();
                    _position++; var end = next == '{' ? '}' : ']';
                    if (Take(end)) return;
                    do
                    {
                        if (next == '{') { White(); String(); if (!Take(':')) throw Invalid(); }
                        Value(depth + 1);
                        if (Take(end)) return;
                        if (!Take(',')) throw Invalid();
                    } while (true);
                }
                if (next == '"') { String(); return; }
                foreach (var literal in new[] { "true", "false", "null" })
                    if (_text.Length - _position >= literal.Length && string.CompareOrdinal(_text, _position, literal, 0, literal.Length) == 0)
                    { _position += literal.Length; return; }
                var number = Number.Match(_text, _position);
                if (!number.Success) throw Invalid(); _position += number.Length;
            }
            private void String()
            {
                if (_position >= _text.Length || _text[_position++] != '"') throw Invalid();
                while (_position < _text.Length)
                {
                    var ch = _text[_position++];
                    if (ch == '"') return;
                    if (ch < 32) throw Invalid();
                    if (ch != '\\') continue;
                    if (_position >= _text.Length) throw Invalid();
                    ch = _text[_position++];
                    if (ch == 'u')
                    {
                        for (var i = 0; i < 4; i++)
                            if (_position >= _text.Length || !Uri.IsHexDigit(_text[_position++])) throw Invalid();
                    }
                    else if ("\"\\/bfnrt".IndexOf(ch) < 0) throw Invalid();
                }
                throw Invalid();
            }
            private bool Take(char ch) { White(); if (_position >= _text.Length || _text[_position] != ch) return false; _position++; return true; }
            private void White() { while (_position < _text.Length && " \t\r\n".IndexOf(_text[_position]) >= 0) _position++; }
        }
    }
}
