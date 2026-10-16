using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.UI
{
    internal static class DataManagerTestSupport
    {
        internal static readonly DateTime RetrievedUtc = new DateTime(2026, 10, 15, 9, 23, 0, DateTimeKind.Utc);

        internal static CatalogIdentity Identity(string version = "16.18.1",
            CatalogSourceProfile profile = CatalogSourceProfile.OfficialOnly) =>
            new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", version), profile);

        internal static StaticCatalog Catalog(CatalogIdentity identity, string unitName = "Before",
            DateTime? retrieved = null, bool legacy = false)
        {
            var time = retrieved ?? RetrievedUtc;
            var sources = new List<SourceStamp>
            {
                new SourceStamp("Riot Data Dragon", "https://ddragon.leagueoflegends.com/test.json",
                    time, "test-etag", "test-sha")
            };
            if (identity.IncludesCommunityDragon)
                sources.Add(new SourceStamp("CommunityDragon", "https://raw.communitydragon.org/test.json",
                    time, "", "supplement-sha", true));
            var manifest = legacy
                ? new PatchManifest(1, identity.Patch, "Test Set", time, "", sources, time, null, 0, 0, "", "")
                : new PatchManifest(2, identity, "Test Set", time, "", sources, time, null, 0, 0, "",
                    identity.IncludesCommunityDragon ? "16.18" : "");
            // Missing icon deliberately produces one real validator warning in standalone metadata.
            return new StaticCatalog(manifest,
                new[] { new UnitRecord("TEST18_Unit", new LocalizedText("동일", unitName), 1, null, "",
                    sources.Where(source => !source.IsSupplemental)) },
                null, null, null);
        }

        internal static PatchRowViewModel Row(CatalogIdentity identity, string name = "Before",
            bool valid = true, DateTime? retrieved = null)
        {
            var catalog = Catalog(identity, name, retrieved);
            return new PatchRowViewModel(new CatalogIndexEntry(identity, new string('a', 64), "test/catalog.json"),
                identity, "test/directory", valid ? catalog : null, retrieved ?? RetrievedUtc,
                valid ? 0 : 1, 1, valid ? "" : "LocalIntegrityFailed");
        }

        internal static StaticImportResult ImportResult(bool published = true) =>
            new StaticImportResult(published, false, "test/directory", new string('a', 64),
                Catalog(Identity()), new ValidationReport(null), null);
    }

    internal sealed class DataManagerFakeService : IDataManagerService
    {
        internal IReadOnlyList<PatchRowViewModel> Rows = Array.Empty<PatchRowViewModel>();
        internal readonly List<StaticImportRequest> Imports = new List<StaticImportRequest>();
        internal int VersionCalls;
        internal Exception LocalLoadError;
        internal Func<StaticImportRequest, string> Destination;
        internal CancellationToken LastToken;
        internal Func<CancellationToken, Task<IReadOnlyList<string>>> Versions =
            token => Task.FromResult<IReadOnlyList<string>>(new[] { "16.18.2", "16.18.1" });
        internal Func<CancellationToken, Task<StaticImportResult>> Run =
            token => Task.FromResult(DataManagerTestSupport.ImportResult());

        public IReadOnlyList<PatchRowViewModel> LoadLocalCatalogs()
        {
            if (LocalLoadError != null) throw LocalLoadError;
            return Rows;
        }

        public string ResolveDestination(StaticImportRequest request)
        {
            if (Destination != null) return Destination(request);
            return new LegacyCatalogResolver(new StaticCatalogPaths(Path.Combine(Path.GetTempPath(), "TftDataManagerFakeDestination"))).Resolve(request.Identity)
                .FinalDirectory;
        }

        public Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token)
        {
            VersionCalls++;
            LastToken = token;
            return Versions(token);
        }

        public Task<StaticImportResult> ImportAsync(StaticImportRequest request, CancellationToken token)
        {
            Imports.Add(request);
            LastToken = token;
            return Run(token);
        }
    }
}
