using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Editor.Ingestion;

namespace TFTAnalyzer.Editor.UI.Data
{
    public interface IDataManagerService
    {
        IReadOnlyList<PatchRowViewModel> LoadLocalCatalogs();
        string ResolveDestination(StaticImportRequest request);
        Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token);
        Task<StaticImportResult> ImportAsync(StaticImportRequest request, CancellationToken token);
    }
}
