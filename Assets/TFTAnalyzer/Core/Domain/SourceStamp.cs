using System;

namespace TFTAnalyzer.Domain
{
    public sealed class SourceStamp
    {
        public string SourceName { get; }
        public string Url { get; }
        public DateTime RetrievedUtc { get; }
        public string ETag { get; }
        public string Sha256 { get; }
        public bool IsSupplemental { get; }

        public SourceStamp(
            string sourceName,
            string url,
            DateTime retrievedUtc,
            string etag,
            string sha256,
            bool isSupplemental = false)
        {
            SourceName = DomainValues.RequireStable(sourceName, nameof(sourceName));
            Url = DomainValues.RequireStable(url, nameof(url));
            RetrievedUtc = retrievedUtc.Kind == DateTimeKind.Utc
                ? retrievedUtc
                : retrievedUtc.ToUniversalTime();
            ETag = etag ?? "";
            Sha256 = sha256 ?? "";
            IsSupplemental = isSupplemental;
        }
    }
}
