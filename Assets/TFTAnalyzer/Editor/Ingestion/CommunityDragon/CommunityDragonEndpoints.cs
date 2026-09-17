using System;
using System.Linq;

namespace TFTAnalyzer.Editor.Ingestion.CommunityDragon
{
    public static class CommunityDragonEndpoints
    {
        public static string VersionFromDataDragon(string sourceGameVersion)
        {
            if (string.IsNullOrWhiteSpace(sourceGameVersion))
            {
                throw new ArgumentException("Source game version cannot be blank.", nameof(sourceGameVersion));
            }

            var parts = sourceGameVersion.Split('.');
            if (parts.Length < 2 ||
                !parts.Take(2).All(part => int.TryParse(part, out _)))
            {
                throw new FormatException($"Unsupported Data Dragon version: {sourceGameVersion}");
            }

            return $"{parts[0]}.{parts[1]}";
        }

        public static string Document(
            string version,
            string locale,
            bool includeCommunityDragon)
        {
            if (!includeCommunityDragon)
            {
                throw new InvalidOperationException(
                    "CommunityDragon access requires explicit opt-in for this import.");
            }

            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(locale))
            {
                throw new ArgumentException("Version and locale are required.");
            }

            if (string.Equals(version, "latest", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Immutable imports cannot use CommunityDragon /latest/.");
            }

            return $"https://raw.communitydragon.org/{Uri.EscapeDataString(version)}/cdragon/tft/" +
                   $"{Uri.EscapeDataString(locale)}.json";
        }
    }
}
