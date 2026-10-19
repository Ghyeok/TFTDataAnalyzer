using System;
using System.Globalization;
using System.Linq;

namespace TFTAnalyzer.Editor.Compliance
{
    public static class CompliancePolicy
    {
        public static bool CanPublish(ComplianceSettings settings, DateTimeOffset utcNow)
        {
            if (!IsWellFormed(settings) || settings.registrationStatus != "ApprovedOrAcknowledged") return false;
            if (!DateTime.TryParseExact(settings.policyReviewedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var reviewed)) return false;
            return reviewed.Date <= utcNow.UtcDateTime.Date &&
                TryAcknowledgedAt(settings.acknowledgedAt, out var acknowledged) && acknowledged <= utcNow;
        }

        internal static bool IsWellFormed(ComplianceSettings settings)
        {
            if (settings == null || (settings.registrationStatus != "Unverified" &&
                settings.registrationStatus != "ApprovedOrAcknowledged" && settings.registrationStatus != "Withdrawn")) return false;
            if (!string.IsNullOrEmpty(settings.policyReviewedOn) && !DateTime.TryParseExact(settings.policyReviewedOn,
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;
            if (settings.policyUrls == null || settings.policyUrls.Length != 3 ||
                !settings.policyUrls.OrderBy(url => url, StringComparer.Ordinal).SequenceEqual(
                    PolicyCopy.Urls.OrderBy(url => url, StringComparer.Ordinal), StringComparer.Ordinal)) return false;
            if (settings.registrationStatus != "ApprovedOrAcknowledged") return string.IsNullOrEmpty(settings.acknowledgedAt);
            return !string.IsNullOrEmpty(settings.policyReviewedOn) && TryAcknowledgedAt(settings.acknowledgedAt, out _);
        }

        private static bool TryAcknowledgedAt(string text, out DateTimeOffset value)
        {
            value = default;
            return text != null && text.EndsWith("Z", StringComparison.Ordinal) &&
                DateTimeOffset.TryParseExact(text, new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" },
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
        }
    }
}
