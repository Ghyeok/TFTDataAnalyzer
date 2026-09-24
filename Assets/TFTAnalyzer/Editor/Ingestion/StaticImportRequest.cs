using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion
{
    public sealed class StaticImportRequest
    {
        public CatalogIdentity Identity { get; }
        public PatchKey Patch => Identity.Patch;
        public bool IncludeCommunityDragon => Identity.IncludesCommunityDragon;

        public StaticImportRequest(CatalogIdentity identity)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        }
    }
}
