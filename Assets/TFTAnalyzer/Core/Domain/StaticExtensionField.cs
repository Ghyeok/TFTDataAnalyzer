using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TFTAnalyzer.Domain
{
    /// <summary>Inert source JSON. It is never a rule, relationship, URL to fetch, or policy grant.</summary>
    public sealed class StaticExtensionField
    {
        public string SourceName { get; }
        public string Locale { get; }
        public string Pointer { get; }
        public string CanonicalJson { get; }
        public StaticExtensionField(string sourceName, string locale, string pointer, string canonicalJson)
        {
            SourceName = DomainValues.RequireStable(sourceName, nameof(sourceName));
            Locale = DomainValues.RequireStable(locale, nameof(locale));
            if (string.IsNullOrEmpty(pointer) || !pointer.StartsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("Extension source pointer is required.");
            Pointer = pointer;
            if (canonicalJson == null || System.Text.Encoding.UTF8.GetByteCount(canonicalJson) > 65536)
                throw new ArgumentException("Extension exceeds the source budget.");
            using (var reader = new JsonTextReader(new StringReader(canonicalJson)) { MaxDepth = 16, DateParseHandling = DateParseHandling.None })
            {
                var value = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonSerializationException("Unexpected extension content.");
                CanonicalJson = Stable(value).ToString(Formatting.None);
            }
        }
        private static JToken Stable(JToken value)
        {
            if (value is JObject obj) return new JObject(obj.Properties().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => new JProperty(x.Name, Stable(x.Value))));
            if (value is JArray array) return new JArray(array.Select(Stable));
            if (value.Type == JTokenType.Float) DomainValues.RequireFinite(value.Value<double>());
            if (value.Type != JTokenType.String && value.Type != JTokenType.Integer && value.Type != JTokenType.Float &&
                value.Type != JTokenType.Null && value.Type != JTokenType.Boolean) throw new JsonSerializationException("Unsupported extension token.");
            return value.DeepClone();
        }
    }
}
