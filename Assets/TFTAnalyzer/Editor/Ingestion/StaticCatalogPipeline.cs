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
    public enum StaticImportRecoveryState
    {
        PublishedUnindexed,
        PublishedIndexedRefreshPending,
        PublishedIndexStateUncertain
    }

    public sealed class StaticImportRecoveryException : InvalidOperationException
    {
        public StaticImportRecoveryState RecoveryState { get; }
        public string FinalDirectory { get; }
        public string IndexFilePath { get; }

        public StaticImportRecoveryException(StaticImportRecoveryState recoveryState,
            string finalDirectory, string indexFilePath, Exception innerException)
            : base(SafeMessage(recoveryState, finalDirectory, indexFilePath), innerException)
        {
            RecoveryState = recoveryState;
            FinalDirectory = finalDirectory;
            IndexFilePath = indexFilePath;
        }

        private static string SafeMessage(StaticImportRecoveryState state, string directory, string indexPath)
        {
            switch (state)
            {
                case StaticImportRecoveryState.PublishedUnindexed:
                    return $"Catalog published but unindexed at {directory}; rerun import to recover the index.";
                case StaticImportRecoveryState.PublishedIndexedRefreshPending:
                    return $"Catalog published and indexed but asset refresh failed at {directory}; refresh assets to recover.";
                case StaticImportRecoveryState.PublishedIndexStateUncertain:
                    return $"Catalog published at {directory}; index state uncertain at {indexPath}; inspect or repair index before rerunning.";
                default:
                    return $"Catalog published at {directory}; recovery state unknown.";
            }
        }
    }

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

            var resolution = new LegacyCatalogResolver(_paths).Resolve(request.Identity);
            if (resolution.Status == LegacyCatalogStatus.AmbiguousLegacy)
            {
                throw new InvalidOperationException($"Existing legacy catalog cannot be classified safely: {resolution.FinalDirectory}");
            }

            var finalDirectory = resolution.FinalDirectory;
            var stagingRoot = _paths.CreateStagingDirectory();
            try
            {
                token.ThrowIfCancellationRequested();
                var loaded = await _source.LoadAsync(request, token);
                token.ThrowIfCancellationRequested();
                _writer.StoreRawSources(request.Identity, loaded.RawResponses);

                var catalog = PromoteManifest(loaded.Catalog, request.Identity);

                var validation = _validator.Validate(catalog);
                if (!validation.CanPublish)
                {
                    return new StaticImportResult(
                        false,
                        false,
                        finalDirectory,
                        "",
                        catalog,
                        validation,
                        loaded.MergeConflicts);
                }

                var withIcons = await _writer.StageIconsAsync(
                    catalog,
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

                token.ThrowIfCancellationRequested();
                var staged = _writer.WriteStagedCatalog(
                    withIcons,
                    validation,
                    stagingRoot);
                token.ThrowIfCancellationRequested();
                var isLegacy = resolution.Status == LegacyCatalogStatus.CompatibleOfficialLegacy;
                var publication = _writer.PublishVerified(staged, finalDirectory, isLegacy);
                var idempotent = publication.Idempotent;
                var publishedHash = publication.ContentHash;
                try
                {
                    _indexWriter.Upsert(request.Identity, publishedHash, resolution.CatalogAssetPath);
                }
                catch (CatalogIndexRollbackException error)
                {
                    throw new StaticImportRecoveryException(
                        StaticImportRecoveryState.PublishedIndexStateUncertain,
                        finalDirectory, error.IndexFilePath, error);
                }
                catch (Exception error)
                {
                    throw new StaticImportRecoveryException(
                        StaticImportRecoveryState.PublishedUnindexed,
                        finalDirectory, "", error);
                }
                try
                {
                    _refreshAssets?.Invoke();
                }
                catch (Exception error)
                {
                    throw new StaticImportRecoveryException(
                        StaticImportRecoveryState.PublishedIndexedRefreshPending,
                        finalDirectory, "", error);
                }
                return new StaticImportResult(
                    true,
                    idempotent,
                    finalDirectory,
                    publishedHash,
                    publication.Catalog,
                    validation,
                    loaded.MergeConflicts);
            }
            finally
            {
                _writer.CleanupStaging(stagingRoot);
            }
        }

        private static StaticCatalog PromoteManifest(StaticCatalog catalog, CatalogIdentity identity)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var source = catalog.Manifest;
            if (!new CatalogIdentity(source.Patch, identity.SourceProfile).Equals(identity))
                throw new InvalidOperationException("Loaded catalog patch differs from requested identity.");
            if (source.Identity != null && !source.Identity.Equals(identity))
                throw new InvalidOperationException("Loaded catalog identity differs from request.");
            if (!identity.IncludesCommunityDragon &&
                (!string.IsNullOrEmpty(source.CommunityDragonVersion) ||
                 source.Sources.Any(stamp => stamp.IsSupplemental)))
                throw new InvalidOperationException("Official-only import contains supplemental sources.");
            if (identity.IncludesCommunityDragon &&
                (string.IsNullOrWhiteSpace(source.CommunityDragonVersion) ||
                 !source.Sources.Any(stamp => stamp.IsSupplemental)))
                throw new InvalidOperationException("Supplemented import is missing CommunityDragon provenance.");

            var manifest = new PatchManifest(Math.Max(2, source.SchemaVersion), identity, source.SetName, source.CreatedUtc,
                source.ContentHash, source.Sources, source.PolicyReviewDate, source.PolicyUrls,
                source.ValidationErrorCount, source.ValidationWarningCount,
                source.PreviousPatchSummary, source.CommunityDragonVersion);
            return new StaticCatalog(manifest, catalog.Units, catalog.Traits, catalog.Items, catalog.Augments, catalog.FieldOrigins);
        }
    }
}
