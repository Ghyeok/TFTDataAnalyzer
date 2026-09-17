using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Ingestion
{
    public interface IStaticCatalogSource
    {
        Task<StaticSourceLoadResult> LoadAsync(
            StaticImportRequest request,
            CancellationToken token);
    }

    public sealed class StaticSourceLoadResult
    {
        public StaticCatalog Catalog { get; }
        public IReadOnlyList<HttpResponseData> RawResponses { get; }
        public IReadOnlyList<MergeConflict> MergeConflicts { get; }

        public StaticSourceLoadResult(
            StaticCatalog catalog,
            IEnumerable<HttpResponseData> rawResponses,
            IEnumerable<MergeConflict> mergeConflicts)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            RawResponses = Array.AsReadOnly((rawResponses ?? Enumerable.Empty<HttpResponseData>()).ToArray());
            MergeConflicts = Array.AsReadOnly((mergeConflicts ?? Enumerable.Empty<MergeConflict>()).ToArray());
        }
    }

    public sealed class NetworkStaticCatalogSource : IStaticCatalogSource
    {
        private readonly DataDragonAdapter _dataDragon;
        private readonly CommunityDragonAdapter _communityDragon;
        private readonly CatalogMerger _merger;
        private readonly IHttpTransport _transport;

        public NetworkStaticCatalogSource(
            DataDragonAdapter dataDragon,
            CommunityDragonAdapter communityDragon,
            CatalogMerger merger,
            IHttpTransport transport)
        {
            _dataDragon = dataDragon ?? throw new ArgumentNullException(nameof(dataDragon));
            _communityDragon = communityDragon ?? throw new ArgumentNullException(nameof(communityDragon));
            _merger = merger ?? throw new ArgumentNullException(nameof(merger));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<StaticSourceLoadResult> LoadAsync(
            StaticImportRequest request,
            CancellationToken token)
        {
            var officialSnapshot = await _dataDragon.FetchAsync(
                new DataDragonRequest(request.Patch),
                token);
            var official = _dataDragon.Parse(officialSnapshot, request.Patch);
            var responses = officialSnapshot.AllResponses.ToList();
            if (!request.IncludeCommunityDragon)
            {
                return new StaticSourceLoadResult(official, responses, null);
            }

            var supplementSnapshot = await _communityDragon.FetchAsync(
                new CommunityDragonRequest(request.Patch, true),
                _transport,
                token);
            responses.Add(supplementSnapshot.KoKr);
            responses.Add(supplementSnapshot.EnUs);
            var supplement = _communityDragon.Parse(
                supplementSnapshot.KoKr.Utf8Body,
                supplementSnapshot.EnUs.Utf8Body,
                request.Patch);
            var merge = _merger.Merge(official, supplement);
            return new StaticSourceLoadResult(merge.Catalog, responses, merge.Conflicts);
        }
    }

    public sealed class StaticCatalogPipeline
    {
        private readonly IStaticCatalogSource _source;
        private readonly IHttpTransport _transport;
        private readonly StaticCatalogPaths _paths;
        private readonly CatalogAssetWriter _writer;
        private readonly CatalogValidator _validator;
        private readonly ICatalogIndexWriter _indexWriter;
        private readonly Action _refreshAssets;

        public StaticCatalogPipeline(
            IStaticCatalogSource source,
            IHttpTransport transport,
            StaticCatalogPaths paths,
            CatalogAssetWriter writer,
            CatalogValidator validator,
            ICatalogIndexWriter indexWriter,
            Action refreshAssets)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _indexWriter = indexWriter ?? throw new ArgumentNullException(nameof(indexWriter));
            _refreshAssets = refreshAssets;
        }

        public async Task<StaticImportResult> RunAsync(
            StaticImportRequest request,
            CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var finalDirectory = _paths.FinalDirectory(request.Patch);
            var stagingRoot = _paths.CreateStagingDirectory();
            try
            {
                token.ThrowIfCancellationRequested();
                var loaded = await _source.LoadAsync(request, token);
                token.ThrowIfCancellationRequested();
                _writer.StoreRawSources(request.Patch, loaded.RawResponses);

                var validation = _validator.Validate(loaded.Catalog);
                if (!validation.CanPublish)
                {
                    return new StaticImportResult(
                        false,
                        false,
                        finalDirectory,
                        "",
                        loaded.Catalog,
                        validation,
                        loaded.MergeConflicts);
                }

                var withIcons = await _writer.StageIconsAsync(
                    loaded.Catalog,
                    _transport,
                    stagingRoot,
                    token);
                validation = _validator.Validate(withIcons);
                if (!validation.CanPublish)
                {
                    return new StaticImportResult(
                        false,
                        false,
                        finalDirectory,
                        "",
                        withIcons,
                        validation,
                        loaded.MergeConflicts);
                }

                var staged = _writer.WriteStagedCatalog(
                    withIcons,
                    validation,
                    stagingRoot);
                var idempotent = _writer.Publish(staged, finalDirectory);
                _indexWriter.Upsert(
                    request.Patch,
                    staged.ContentHash,
                    _paths.CatalogAssetPath(request.Patch));
                _refreshAssets?.Invoke();
                return new StaticImportResult(
                    true,
                    idempotent,
                    finalDirectory,
                    staged.ContentHash,
                    withIcons,
                    validation,
                    loaded.MergeConflicts);
            }
            finally
            {
                _writer.CleanupStaging(stagingRoot);
            }
        }
    }
}
