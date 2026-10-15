using System;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>A normalized composition SHA-256 value with ordinal equality.</summary>
    public sealed class CompositionFingerprint : IEquatable<CompositionFingerprint>
    {
        public string Value { get; }

        /// <summary>Creates a normalized SHA-256 composition value, rejecting invalid hexadecimal input.</summary>
        public CompositionFingerprint(string value)
        {
            Value = AnalyticsHashes.RequireSha256(value, nameof(value));
        }

        /// <summary>Compares normalized SHA-256 composition values using ordinal equality.</summary>
        public bool Equals(CompositionFingerprint other)
            => other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);

        /// <summary>Compares normalized SHA-256 composition values using ordinal equality.</summary>
        public override bool Equals(object obj) => Equals(obj as CompositionFingerprint);
        /// <summary>Returns a collection hash consistent with equality; this is not a persisted identifier.</summary>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    }

    internal static class AnalyticsHashes
    {
        /// <summary>Validates and normalizes SHA-256 hex without including rejected input values in errors.</summary>
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
