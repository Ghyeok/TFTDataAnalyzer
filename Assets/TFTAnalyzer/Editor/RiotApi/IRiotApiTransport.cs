using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Authenticated in-memory transport, isolated from verbose static-data download errors.</summary>
    public interface IRiotApiTransport
    {
        /// <summary>Sends a fixed endpoint request without persisting or logging sensitive inputs.</summary>
        Task<RiotApiResponse> SendAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token);
    }

    /// <summary>Owns an in-memory response until success transfers its body to the caller.</summary>
    public sealed class RiotApiResponse : IDisposable
    {
        private Stream _body;
        public long StatusCode { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>Copies headers case-insensitively and takes ownership of the provided body.</summary>
        public RiotApiResponse(long statusCode, Stream body, IReadOnlyDictionary<string, string> headers = null)
        {
            StatusCode = statusCode;
            _body = body ?? Stream.Null;
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
                foreach (var pair in headers) copy[pair.Key] = pair.Value ?? "";
            Headers = new ReadOnlyDictionary<string, string>(copy);
        }

        /// <summary>Transfers the response body exactly once.</summary>
        internal Stream TakeBody()
        {
            var body = _body ?? throw new InvalidOperationException("Response body is unavailable.");
            _body = null;
            return body;
        }

        /// <summary>Releases an untransferred in-memory body without leaking cleanup error details.</summary>
        public void Dispose()
        {
            var body = _body;
            _body = null;
            try { body?.Dispose(); }
            catch { /* Memory-only cleanup must not replace safe status errors with payload details. */ }
        }
    }
}
