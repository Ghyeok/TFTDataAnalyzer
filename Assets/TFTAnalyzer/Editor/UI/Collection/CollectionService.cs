using System;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.UI.Collection
{
    public sealed class CollectionService : ICollectionService
    {
        private readonly ObservationStore _store;
        private readonly IRiotApiKeyProvider _keys;
        private readonly EnvironmentSeedPuuidProvider _seeds;
        private readonly RiotApiClient _client;

        public CollectionService(ObservationStore store, IRiotApiKeyProvider keys = null,
            EnvironmentSeedPuuidProvider seeds = null, RiotApiClient client = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _keys = keys ?? new EnvironmentRiotApiKeyProvider();
            _seeds = seeds ?? new EnvironmentSeedPuuidProvider();
            // One client retains quota state across repeated, manually approved collection runs.
            _client = client ?? new RiotApiClient(_keys);
        }

        public bool ApiKeyConfigured
        {
            get { try { return _keys.IsConfigured; } catch { return false; } }
        }

        public bool SeedConfigured
        {
            get { try { return _seeds.GetSeeds().Count > 0; } catch { return false; } }
        }

        public Task<MatchCollectionSummary> CollectAsync(StaticCatalog catalog, MatchCollectionRequest request,
            IProgress<CollectionProgress> progress, CancellationToken token) =>
            AnalyzerStoreOperations.Shared.RunAsync(() =>
                new MatchCollectionService(catalog, _store, _client, _seeds).CollectAsync(request, progress, token), token);
    }
}
