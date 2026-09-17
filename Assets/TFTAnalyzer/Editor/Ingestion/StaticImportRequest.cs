using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion
{
    public sealed class StaticImportRequest
    {
        public PatchKey Patch { get; }
        public bool IncludeCommunityDragon { get; }

        public StaticImportRequest(PatchKey patch, bool includeCommunityDragon)
        {
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
            IncludeCommunityDragon = includeCommunityDragon;
        }
    }
}
