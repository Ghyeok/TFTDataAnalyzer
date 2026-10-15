using System;
using System.Globalization;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Builds only the approved TFT regional Match API endpoints.</summary>
    public static class RiotRouting
    {
        /// <summary>Maps approved platform IDs, including legacy SEA aliases, to a TFT region.</summary>
        public static string ForPlatform(string platform)
        {
            switch ((platform ?? "").Trim().ToUpperInvariant())
            {
                case "KR": case "JP1": case "PH2": case "SG2": case "TH2": case "TW2": case "VN2":
                    return "asia";
                case "NA1": case "BR1": case "LA1": case "LA2": case "OC1":
                    return "americas";
                case "EUN1": case "EUW1": case "TR1": case "RU":
                    return "europe";
                default: throw new ArgumentException("Unsupported TFT platform.", nameof(platform));
            }
        }

        /// <summary>Rejects user-supplied hosts and routes outside the TFT regional allowlist.</summary>
        public static string NormalizeRegion(string regionalRoute)
        {
            var region = (regionalRoute ?? "").Trim().ToLowerInvariant();
            if (region != "asia" && region != "americas" && region != "europe")
                throw new ArgumentException("Unsupported TFT region.", nameof(regionalRoute));
            return region;
        }

        /// <summary>Encodes one identifying path segment and bounds ID-list pagination.</summary>
        internal static Uri MatchIds(string region, string puuid, int start, int count)
        {
            region = NormalizeRegion(region);
            if (string.IsNullOrWhiteSpace(puuid))
                throw new ArgumentException("A seed identifier is required.", nameof(puuid));
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (count < 1 || count > 100) throw new ArgumentOutOfRangeException(nameof(count));
            return new Uri("https://" + region + ".api.riotgames.com/tft/match/v1/matches/by-puuid/" +
                Escape(puuid) + "/ids?start=" + start.ToString(CultureInfo.InvariantCulture) +
                "&count=" + count.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Builds a match-detail endpoint without interpreting its identifier as a URL.</summary>
        internal static Uri Match(string region, string matchId)
        {
            region = NormalizeRegion(region);
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("A match identifier is required.", nameof(matchId));
            return new Uri("https://" + region + ".api.riotgames.com/tft/match/v1/matches/" + Escape(matchId));
        }

        /// <summary>Restricts authenticated transport targets to HTTPS, default port and TFT Match paths.</summary>
        internal static void ValidateTarget(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
                (uri.Host != "asia.api.riotgames.com" && uri.Host != "americas.api.riotgames.com" &&
                 uri.Host != "europe.api.riotgames.com") ||
                !uri.AbsolutePath.StartsWith("/tft/match/v1/matches/", StringComparison.Ordinal))
                throw new ArgumentException("Unsupported authenticated TFT endpoint.", nameof(uri));
        }

        /// <summary>Discards URI-encoding diagnostics that could contain identifying input.</summary>
        private static string Escape(string value)
        {
            try { return Uri.EscapeDataString(value); }
            catch { throw new ArgumentException("Invalid identifier encoding."); }
        }
    }
}
