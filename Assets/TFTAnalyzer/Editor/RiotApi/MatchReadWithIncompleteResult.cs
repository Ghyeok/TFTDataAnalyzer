using Newtonsoft.Json;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Preserves the original parser outcome and optionally carries a separately validated candidate.</summary>
    public sealed class MatchReadWithIncompleteResult
    {
        public AnonymizationResult NormalResult { get; }
        public MatchPayloadException Failure { get; }
        public IncompleteMatchCandidate IncompleteCandidate { get; }
        /// <summary>Local-store duplicate key; never include it in UI, diagnostics or collaboration artifacts.</summary>
        [JsonIgnore] public string IncompleteMatchHash { get; }
        // Provenance is bound by the extractor and checked against published data by the store.
        [JsonIgnore] internal CatalogIdentity ReferenceCatalogIdentity { get; }
        [JsonIgnore] internal string ReferenceCatalogHash { get; }

        internal MatchReadWithIncompleteResult(AnonymizationResult normalResult, MatchPayloadException failure,
            IncompleteMatchCandidate candidate = null, string incompleteMatchHash = null,
            CatalogIdentity referenceCatalogIdentity = null, string referenceCatalogHash = null)
        {
            NormalResult = normalResult; Failure = failure;
            IncompleteCandidate = candidate; IncompleteMatchHash = incompleteMatchHash;
            ReferenceCatalogIdentity = referenceCatalogIdentity; ReferenceCatalogHash = referenceCatalogHash;
        }
    }
}
