using System;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    public sealed class CompositionFingerprint : IEquatable<CompositionFingerprint>
    {
        public string Value { get; }

        public CompositionFingerprint(string value)
        {
            Value = AnalyticsHashes.RequireSha256(value, nameof(value));
        }

        public bool Equals(CompositionFingerprint other)
            => other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as CompositionFingerprint);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    }

    internal static class AnalyticsHashes
    {
        public static string RequireSha256(string value, string parameterName)
        {
            var normalized = DomainValues.RequireStable(value, parameterName).ToLowerInvariant();
            if (normalized.Length != 64 || normalized.Any(c =>
                    !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')))
                throw new ArgumentException("A SHA-256 hexadecimal hash is required.", parameterName);
            return normalized;
        }
    }
}
