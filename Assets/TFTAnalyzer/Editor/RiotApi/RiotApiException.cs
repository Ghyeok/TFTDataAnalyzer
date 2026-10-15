using System;
using System.Globalization;

namespace TFTAnalyzer.Editor.RiotApi
{
    public enum RiotApiFailure
    {
        ApiKeyUnavailable, HttpError, RateLimited, TransportError, InvalidResponse
    }

    /// <summary>A safe failure containing only controlled codes, status and a retry timestamp.</summary>
    public sealed class RiotApiException : Exception
    {
        public string Code { get; }
        public long StatusCode { get; }
        public DateTimeOffset? RetryAfterUtc { get; }

        /// <summary>Never retains URLs, response bodies, credentials or an untrusted inner exception.</summary>
        public RiotApiException(RiotApiFailure failure, long statusCode = 0, DateTimeOffset? retryAfterUtc = null)
            : base("Riot API request failed: " + SafeCode(failure) + " (HTTP " +
                   statusCode.ToString(CultureInfo.InvariantCulture) + ").")
        {
            Code = SafeCode(failure);
            StatusCode = statusCode;
            RetryAfterUtc = retryAfterUtc?.ToUniversalTime();
        }

        /// <summary>Maps only known failure kinds to public diagnostic codes.</summary>
        private static string SafeCode(RiotApiFailure failure)
        {
            switch (failure)
            {
                case RiotApiFailure.ApiKeyUnavailable: return "api_key_unavailable";
                case RiotApiFailure.HttpError: return "http_error";
                case RiotApiFailure.RateLimited: return "rate_limited";
                case RiotApiFailure.TransportError: return "transport_error";
                case RiotApiFailure.InvalidResponse: return "invalid_response";
                default: return "request_failed";
            }
        }
    }
}
