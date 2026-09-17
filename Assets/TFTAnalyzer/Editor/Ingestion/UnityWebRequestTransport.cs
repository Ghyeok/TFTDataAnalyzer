using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace TFTAnalyzer.Editor.Ingestion
{
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        public async Task<HttpResponseData> GetAsync(string url, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be blank.", nameof(url));
            }

            using (var request = UnityWebRequest.Get(url))
            using (token.Register(request.Abort))
            {
                var operation = request.SendWebRequest();
                var completion = new TaskCompletionSource<bool>();
                operation.completed += _ => completion.TrySetResult(true);
                await completion.Task;
                token.ThrowIfCancellationRequested();

                var response = new HttpResponseData(
                    url,
                    request.responseCode,
                    request.downloadHandler?.data,
                    request.GetResponseHeader("ETag"),
                    request.GetResponseHeader("Content-Type"),
                    DateTime.UtcNow);

                if (request.result != UnityWebRequest.Result.Success ||
                    response.StatusCode < 200 || response.StatusCode >= 300)
                {
                    throw new HttpTransportException(url, response.StatusCode, request.error);
                }

                return response;
            }
        }
    }
}
