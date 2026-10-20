using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Validation;
using UnityEditor;

namespace TFTAnalyzer.Editor.UI.Data
{
    public sealed class DataManagerService : IDataManagerService
    {
        private readonly StaticCatalogPaths _paths;
        private readonly DataDragonAdapter _dataDragon;
        private readonly IHttpTransport _transport;
        private readonly Func<CatalogIndex> _indexProvider;
        private readonly Func<DateTimeOffset> _utcNow;

        public DataManagerService(StaticCatalogPaths paths, IHttpTransport transport, Func<CatalogIndex> indexProvider)
            : this(paths, transport, indexProvider, () => DateTimeOffset.UtcNow) { }

        public DataManagerService(StaticCatalogPaths paths, IHttpTransport transport, Func<CatalogIndex> indexProvider,
            Func<DateTimeOffset> utcNow)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _indexProvider = indexProvider ?? throw new ArgumentNullException(nameof(indexProvider));
            _dataDragon = new DataDragonAdapter(transport);
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public IReadOnlyList<PatchRowViewModel> LoadLocalCatalogs() =>
            new PublishedCatalogLoader(_paths).Load(_indexProvider());

        public string ResolveDestination(StaticImportRequest request)
        {
            var resolution = new LegacyCatalogResolver(_paths).Resolve(request.Identity);
            if (resolution.Status == LegacyCatalogStatus.AmbiguousLegacy) throw new InvalidOperationException();
            return resolution.FinalDirectory;
        }

        public Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token) =>
            _dataDragon.FetchVersionsAsync(token);

        public Task<StaticImportResult> ImportAsync(StaticImportRequest request, CancellationToken token)
        {
            var source = new NetworkStaticCatalogSource(_dataDragon, new CommunityDragonAdapter(),
                new CatalogMerger(), _transport);
            return new StaticCatalogPipeline(source, _transport, _paths, new CatalogAssetWriter(_paths),
                new CatalogValidator(_paths.GeneratedRelativeDirectory), new CatalogIndexWriter(_paths, _utcNow), AssetDatabase.Refresh).RunAsync(request, token);
        }
    }
}
