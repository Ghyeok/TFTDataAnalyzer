using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Editor.UI.Collection;

namespace TFTAnalyzer.Tests.UI
{
    internal sealed class CollectionFakeService : ICollectionService
    {
        internal bool KeyAvailable = true;
        internal bool SeedsAvailable = true;
        internal Exception ConfigurationError;
        internal readonly List<MatchCollectionRequest> Requests = new List<MatchCollectionRequest>();
        internal readonly List<StaticCatalog> Catalogs = new List<StaticCatalog>();
        internal CancellationToken LastToken;
        internal IProgress<CollectionProgress> LastProgress;
        internal Func<CancellationToken, Task<MatchCollectionSummary>> Run =
            token => Task.FromResult(Summary());

        public bool ApiKeyConfigured => ConfigurationError == null ? KeyAvailable : throw ConfigurationError;
        public bool SeedConfigured => ConfigurationError == null ? SeedsAvailable : throw ConfigurationError;

        public Task<MatchCollectionSummary> CollectAsync(StaticCatalog catalog, MatchCollectionRequest request,
            IProgress<CollectionProgress> progress, CancellationToken token)
        {
            Catalogs.Add(catalog);
            Requests.Add(request);
            LastToken = token;
            LastProgress = progress;
            return Run(token);
        }

        internal static MatchCollectionSummary Summary(int requested = 0, int fetched = 0, int stored = 0,
            int duplicate = 0, int filtered = 0, int failed = 0, bool cancelled = false, int idPages = 0,
            bool scanLimitReached = false, int statusCode = 0, DateTimeOffset? retryAfterUtc = null) =>
            new MatchCollectionSummary(requested, fetched, stored, duplicate, filtered, failed, cancelled,
                idPages, scanLimitReached, statusCode, retryAfterUtc);
    }
}
