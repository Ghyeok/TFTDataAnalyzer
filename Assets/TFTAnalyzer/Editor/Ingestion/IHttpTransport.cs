using System.Threading;
using System.Threading.Tasks;

namespace TFTAnalyzer.Editor.Ingestion
{
    public interface IHttpTransport
    {
        Task<HttpResponseData> GetAsync(string url, CancellationToken token);
    }
}
