using System;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion.CommunityDragon
{
    public sealed class CommunityDragonRequest
    {
        public PatchKey Patch { get; }
        public bool IncludeCommunityDragon { get; }

        public CommunityDragonRequest(PatchKey patch, bool includeCommunityDragon)
        {
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
            IncludeCommunityDragon = includeCommunityDragon;
        }
    }

    public sealed class CommunityDragonSnapshot
    {
        public HttpResponseData KoKr { get; }
        public HttpResponseData EnUs { get; }

        public CommunityDragonSnapshot(HttpResponseData koKr, HttpResponseData enUs)
        {
            KoKr = koKr ?? throw new ArgumentNullException(nameof(koKr));
            EnUs = enUs ?? throw new ArgumentNullException(nameof(enUs));
        }
    }
}
