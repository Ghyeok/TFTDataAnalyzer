using System;
using System.Text;

namespace TFTAnalyzer.Editor.Ingestion
{
    public sealed class HttpResponseData
    {
        public string Url { get; }
        public long StatusCode { get; }
        public byte[] Body { get; }
        public string ETag { get; }
        public string ContentType { get; }
        public DateTime RetrievedUtc { get; }
        public string Utf8Body => Encoding.UTF8.GetString(Body);

        public HttpResponseData(
            string url,
            long statusCode,
            byte[] body,
            string etag,
            string contentType,
            DateTime retrievedUtc)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be blank.", nameof(url));
            }

            Url = url;
            StatusCode = statusCode;
            Body = body == null ? Array.Empty<byte>() : (byte[])body.Clone();
            ETag = etag ?? "";
            ContentType = contentType ?? "";
            RetrievedUtc = retrievedUtc.Kind == DateTimeKind.Utc
                ? retrievedUtc
                : retrievedUtc.ToUniversalTime();
        }
    }

    public sealed class HttpTransportException : Exception
    {
        public string RequestUrl { get; }
        public long StatusCode { get; }

        public HttpTransportException(string requestUrl, long statusCode, string statusDescription)
            : base($"HTTP {statusCode} while requesting {requestUrl}: {statusDescription ?? "request failed"}")
        {
            RequestUrl = requestUrl ?? "";
            StatusCode = statusCode;
        }
    }
}
