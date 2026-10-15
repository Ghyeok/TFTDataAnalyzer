using System;
using System.Linq;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Reads RIOT_API_KEY on demand; neither caches nor persists the key.</summary>
    public sealed class EnvironmentRiotApiKeyProvider : IRiotApiKeyProvider
    {
        private readonly Func<string> _read;
        public bool IsConfigured => Read() != null;

        /// <summary>Allows an isolated reader for offline tests; production uses only the environment.</summary>
        public EnvironmentRiotApiKeyProvider(Func<string> read = null)
        {
            _read = read ?? (() => Environment.GetEnvironmentVariable("RIOT_API_KEY"));
        }

        /// <summary>Rejects missing or control-bearing credentials without echoing their values.</summary>
        public string GetRequiredKey()
            => Read() ?? throw new RiotApiException(RiotApiFailure.ApiKeyUnavailable);

        /// <summary>Reads defensively and discards untrusted reader error details.</summary>
        private string Read()
        {
            try
            {
                var value = _read();
                if (!IsSafeKey(value)) return null;
                return value.Trim();
            }
            catch { return null; }
        }

        /// <summary>Checks the header-value boundary without including the value in an error.</summary>
        internal static bool IsSafeKey(string value)
            => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    }
}
