using System;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;

namespace TFTAnalyzer.Editor.Menu
{
    public sealed class StaticImportPresentation
    {
        private readonly LegacyCatalogResolver _resolver;

        public bool IncludeCommunityDragon { get; set; }

        public StaticImportPresentation(StaticCatalogPaths paths)
        {
            _resolver = new LegacyCatalogResolver(paths);
        }

        public StaticImportRequest CreateRequest(PatchKey patch)
        {
            return new StaticImportRequest(new CatalogIdentity(patch,
                IncludeCommunityDragon
                    ? CatalogSourceProfile.OfficialPlusCommunityDragon
                    : CatalogSourceProfile.OfficialOnly));
        }

        public string BuildSummary(CatalogIdentity identity)
        {
            var targetDirectory = TargetDirectory(identity);
            return $"Set: {identity.Patch.SetId}\nTFT patch: {identity.Patch.PatchId}\n" +
                   $"Data Dragon version: {identity.Patch.SourceGameVersion}\n" +
                   $"Source profile: {identity.ProfileId}\nTarget: {targetDirectory}";
        }

        public string TargetDirectory(CatalogIdentity identity)
        {
            var target = _resolver.Resolve(identity);
            if (target.Status == LegacyCatalogStatus.AmbiguousLegacy)
            {
                throw new InvalidOperationException($"Existing legacy catalog cannot be classified safely: {target.FinalDirectory}");
            }

            return target.FinalDirectory;
        }

        public string FailureStatus(CatalogIdentity identity, string targetDirectory, Exception error)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (error == null) throw new ArgumentNullException(nameof(error));
            var prefix = $"Import failed for {identity.Patch.SetId} / {identity.Patch.PatchId} / " +
                         $"{identity.Patch.SourceGameVersion} / {identity.ProfileId} at {targetDirectory}: ";
            if (error is StaticImportRecoveryException recovery)
            {
                switch (recovery.RecoveryState)
                {
                    case StaticImportRecoveryState.PublishedUnindexed:
                        return prefix + "Catalog published but unindexed; rerun import to recover the index. CommunityDragon selection reset.";
                    case StaticImportRecoveryState.PublishedIndexedRefreshPending:
                        return prefix + "Catalog published and indexed; refresh assets to finish. CommunityDragon selection reset.";
                    case StaticImportRecoveryState.PublishedIndexStateUncertain:
                        return prefix + $"Catalog published; index state uncertain; inspect or repair index at {recovery.IndexFilePath} before rerunning. CommunityDragon selection reset.";
                }
            }

            return prefix + $"{error.GetType().Name}. CommunityDragon selection reset. See Console.";
        }

        public string CancellationStatus(CatalogIdentity identity, string targetDirectory)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            return $"Import cancelled for {identity.Patch.SetId} / {identity.Patch.PatchId} / " +
                   $"{identity.Patch.SourceGameVersion} / {identity.ProfileId} at {targetDirectory}. " +
                   "Staging was cleaned; CommunityDragon selection reset.";
        }

        public async Task<T> ExecuteAsync<T>(
            PatchKey patch,
            Func<string, bool> confirm,
            Func<StaticImportRequest, CancellationToken, Task<T>> run,
            CancellationToken token) where T : class
        {
            if (confirm == null) throw new ArgumentNullException(nameof(confirm));
            if (run == null) throw new ArgumentNullException(nameof(run));
            try
            {
                var request = CreateRequest(patch);
                if (!confirm(BuildSummary(request.Identity))) return null;
                token.ThrowIfCancellationRequested();
                return await run(request, token);
            }
            finally
            {
                IncludeCommunityDragon = false;
            }
        }
    }
}
