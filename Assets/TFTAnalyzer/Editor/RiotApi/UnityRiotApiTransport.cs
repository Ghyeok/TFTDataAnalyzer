using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Memory-only authenticated Unity transport with redirect forwarding disabled.</summary>
    public sealed class UnityRiotApiTransport : IRiotApiTransport
    {
        /// <summary>Sends only approved HTTPS TFT requests; never logs URLs, bodies, headers or native error text.</summary>
        public async Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers,
            CancellationToken token)
        {
            RiotRouting.ValidateTarget(uri);
            if (headers == null || headers.Count != 1 || !headers.TryGetValue("X-Riot-Token", out var key) ||
                !EnvironmentRiotApiKeyProvider.IsSafeKey(key))
                throw new ArgumentException("One valid Riot authentication header is required.", nameof(headers));
            token.ThrowIfCancellationRequested();
            try
            {
                using (var request = UnityWebRequest.Get(uri.AbsoluteUri))
                {
                    request.redirectLimit = 0;
                    request.timeout = 30;
                    request.SetRequestHeader("X-Riot-Token", key);
                    var operation = request.SendWebRequest();
                    var completion = new TaskCompletionSource<bool>();
                    operation.completed += _ => completion.TrySetResult(true);
                    if (operation.isDone) completion.TrySetResult(true);
                    using (token.Register(() => completion.TrySetCanceled()))
                    {
                        await WaitForResponseAsync(completion.Task, () => request.responseCode > 0,
                            request.Abort, token);
                    }
                    var status = request.responseCode;
                    if (status <= 0 || (!token.IsCancellationRequested && status < 300 &&
                        (request.result == UnityWebRequest.Result.ConnectionError ||
                         request.result == UnityWebRequest.Result.DataProcessingError)))
                        throw new RiotApiException(RiotApiFailure.TransportError);
                    var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var name in new[] { "Retry-After", "X-App-Rate-Limit", "X-App-Rate-Limit-Count",
                                 "X-Method-Rate-Limit", "X-Method-Rate-Limit-Count" })
                    {
                        var value = request.GetResponseHeader(name);
                        if (value != null) responseHeaders[name] = value;
                    }
                    // Preserve quota headers before aborting an interrupted body; the client observes
                    // them before propagating cancellation. Error payloads are never consumed.
                    if (token.IsCancellationRequested && !operation.isDone) request.Abort();
                    var bytes = !token.IsCancellationRequested && status >= 200 && status < 300
                        ? request.downloadHandler?.data ?? Array.Empty<byte>() : Array.Empty<byte>();
                    return new RiotApiResponse(status, new MemoryStream(bytes, false), responseHeaders);
                }
            }
            catch (OperationCanceledException) { throw new OperationCanceledException(token); }
            catch (RiotApiException) { throw; }
            catch { throw new RiotApiException(RiotApiFailure.TransportError); }
        }

        /// <summary>Settles native completion and cancellation without exposing native error details.</summary>
        internal static async Task WaitForResponseAsync(Task completion, Func<bool> hasResponse,
            Action abort, CancellationToken token)
        {
            try { await completion; }
            catch (OperationCanceledException)
            {
                if (hasResponse()) return;
                abort();
                throw new OperationCanceledException(token);
            }
            if (token.IsCancellationRequested && !hasResponse())
            {
                abort();
                throw new OperationCanceledException(token);
            }
        }
    }
}
