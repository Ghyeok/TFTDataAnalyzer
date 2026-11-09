using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion
{
    public sealed class StaticImportRequest
    {
        public CatalogIdentity Identity { get; }
        public PatchKey Patch => Identity.Patch;
        public bool IncludeCommunityDragon => Identity.IncludesCommunityDragon;
        public CatalogIdentity BaselineIdentity { get; }
        public string BaselineContentHash { get; }

        public StaticImportRequest(CatalogIdentity identity)
            : this(identity, null, null) { }

        public StaticImportRequest(CatalogIdentity identity, CatalogIdentity baselineIdentity, string baselineContentHash)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if ((baselineIdentity == null) != string.IsNullOrEmpty(baselineContentHash))
                throw new ArgumentException("Baseline identity and content hash must be supplied together.");
            BaselineIdentity = baselineIdentity;
            BaselineContentHash = baselineContentHash ?? "";
        }
    }
}
