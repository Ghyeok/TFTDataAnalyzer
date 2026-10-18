using System;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.UI.Collection
{
    public interface ICollectionService
    {
        bool ApiKeyConfigured { get; }
        bool SeedConfigured { get; }
        Task<MatchCollectionSummary> CollectAsync(StaticCatalog catalog, MatchCollectionRequest request,
            IProgress<CollectionProgress> progress, CancellationToken token);
    }
}
