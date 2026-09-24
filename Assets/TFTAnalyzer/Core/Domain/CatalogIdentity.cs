using System;

namespace TFTAnalyzer.Domain
{
    public sealed class CatalogIdentity : IEquatable<CatalogIdentity>
    {
        public PatchKey Patch { get; }
        public CatalogSourceProfile SourceProfile { get; }
        public string ProfileId => CatalogSourceProfileIds.ToStableId(SourceProfile);
        public bool IncludesCommunityDragon => SourceProfile == CatalogSourceProfile.OfficialPlusCommunityDragon;

        public CatalogIdentity(PatchKey patch, CatalogSourceProfile sourceProfile)
        {
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
            CatalogSourceProfileIds.ToStableId(sourceProfile);
            SourceProfile = sourceProfile;
        }

        public bool Equals(CatalogIdentity other)
        {
            return other != null &&
                   string.Equals(Patch.SetId, other.Patch.SetId, StringComparison.Ordinal) &&
                   string.Equals(Patch.PatchId, other.Patch.PatchId, StringComparison.Ordinal) &&
                   string.Equals(Patch.SourceGameVersion, other.Patch.SourceGameVersion, StringComparison.Ordinal) &&
                   SourceProfile == other.SourceProfile;
        }

        public override bool Equals(object obj) => Equals(obj as CatalogIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(Patch.SetId);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Patch.PatchId);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Patch.SourceGameVersion);
                return hash * 31 + (int)SourceProfile;
            }
        }
    }
}
