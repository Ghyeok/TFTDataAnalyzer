using System;

namespace TFTAnalyzer.Domain
{
    public enum CatalogSourceProfile
    {
        OfficialOnly,
        OfficialPlusCommunityDragon
    }

    public static class CatalogSourceProfileIds
    {
        public static string ToStableId(CatalogSourceProfile profile)
        {
            switch (profile)
            {
                case CatalogSourceProfile.OfficialOnly: return "official-only";
                case CatalogSourceProfile.OfficialPlusCommunityDragon: return "official-plus-communitydragon";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        public static CatalogSourceProfile Parse(string stableId)
        {
            switch (stableId)
            {
                case "official-only": return CatalogSourceProfile.OfficialOnly;
                case "official-plus-communitydragon": return CatalogSourceProfile.OfficialPlusCommunityDragon;
                default: throw new ArgumentException("Unknown catalog source profile.", nameof(stableId));
            }
        }
    }
}
