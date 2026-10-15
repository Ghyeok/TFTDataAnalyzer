using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Key-safe Match API client with in-memory responses, regional quotas and bounded server retries.</summary>
    public sealed class RiotApiClient
    {
        private readonly IRiotApiKeyProvider _keys;
        private readonly IRiotApiTransport _transport;
        private readonly RateLimitState _limits;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);

        /// <summary>Injects transport and time for offline tests; construction never reads a credential.</summary>
        public RiotApiClient(IRiotApiKeyProvider keys = null, IRiotApiTransport transport = null,
            RateLimitState limits = null, Func<DateTimeOffset> utcNow = null,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _keys = keys ?? new EnvironmentRiotApiKeyProvider();
            _transport = transport ?? new UnityRiotApiTransport();
            _limits = limits ?? new RateLimitState();
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        }

        /// <summary>Returns a caller-owned memory stream; the caller must anonymize and dispose it.</summary>
        public Task<Stream> GetMatchStreamAsync(string regionalRoute, string matchId, CancellationToken token)
            => RequestAsync(RiotRouting.Match(regionalRoute, matchId), "match_detail", token);

        /// <summary>Reads only a bounded ID list and disposes its response without logging payload diagnostics.</summary>
        public async Task<IReadOnlyList<string>> GetMatchIdsAsync(string regionalRoute, string seedPuuid,
            int start, int count, CancellationToken token)
        {
            var uri = RiotRouting.MatchIds(regionalRoute, seedPuuid, start, count);
            try
            {
                using (var stream = await RequestAsync(uri, "match_ids", token))
                using (var text = new StreamReader(stream, new UTF8Encoding(false, true), true, 1024, false))
                using (var reader = new JsonTextReader(text)
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = 8,
                    Culture = CultureInfo.InvariantCulture
                })
                {
                    token.ThrowIfCancellationRequested();
                    var array = JArray.Load(reader);
                    if (array.Count > count || array.Any(value => value.Type != JTokenType.String ||
                            string.IsNullOrWhiteSpace((string)value)) || reader.Read())
                        throw new RiotApiException(RiotApiFailure.InvalidResponse);
                    token.ThrowIfCancellationRequested();
                    return Array.AsReadOnly(array.Select(value => (string)value).ToArray());
                }
            }
            catch (OperationCanceledException) { throw new OperationCanceledException(token); }
            catch (RiotApiException) { throw; }
            catch { throw new RiotApiException(RiotApiFailure.InvalidResponse); }
        }

        /// <summary>Serializes each physical send, reserves quotas and releases failed bodies before backoff.</summary>
        private async Task<Stream> RequestAsync(Uri uri, string method, CancellationToken token)
        {
            RiotRouting.ValidateTarget(uri);
            var region = uri.Host.Substring(0, uri.Host.IndexOf('.'));
            try
            {
                for (var attempt = 0; attempt < 4; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    await _sendGate.WaitAsync(token);
                    RiotApiResponse response = null;
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _limits.Acquire(region, method, _utcNow());
                        var headers = new ReadOnlyDictionary<string, string>(
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            { ["X-Riot-Token"] = ReadKey() });
                        response = await _transport.SendAsync(uri, headers, token);
                        var now = _utcNow();
                        // A completed 429 still governs future calls even if this consumer just cancelled.
                        if (response != null)
                            _limits.Observe(region, method, response.StatusCode, response.Headers, now);
                        token.ThrowIfCancellationRequested();
                        if (response == null) throw new RiotApiException(RiotApiFailure.InvalidResponse);
                        if (response.StatusCode >= 200 && response.StatusCode < 300)
                            return response.TakeBody();
                        if (response.StatusCode == 429)
                            throw new RiotApiException(RiotApiFailure.RateLimited, 429,
                                _limits.RetryAt(region, method, now) ?? now);
                        if (!Retryable(response.StatusCode) || attempt == 3)
                            throw new RiotApiException(RiotApiFailure.HttpError, response.StatusCode);
                    }
                    finally
                    {
                        response?.Dispose();
                        _sendGate.Release();
                    }
                    await _delay(TimeSpan.FromSeconds(1 << attempt), token);
                }
                throw new RiotApiException(RiotApiFailure.TransportError);
            }
            catch (OperationCanceledException) { throw new OperationCanceledException(token); }
            catch (RiotApiException) { throw; }
            catch { throw new RiotApiException(RiotApiFailure.TransportError); }
        }

        /// <summary>Sanitizes arbitrary provider failures and rejects header-injection values.</summary>
        private string ReadKey()
        {
            try
            {
                var key = _keys.GetRequiredKey();
                if (!EnvironmentRiotApiKeyProvider.IsSafeKey(key))
                    throw new RiotApiException(RiotApiFailure.ApiKeyUnavailable);
                return key.Trim();
            }
            catch { throw new RiotApiException(RiotApiFailure.ApiKeyUnavailable); }
        }

        /// <summary>Allows only the four planned transient HTTP statuses to retry.</summary>
        private static bool Retryable(long status) => status == 500 || status == 502 || status == 503 || status == 504;
    }
}
