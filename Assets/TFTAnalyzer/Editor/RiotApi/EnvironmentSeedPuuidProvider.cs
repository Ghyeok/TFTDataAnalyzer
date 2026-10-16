using System;
using System.Collections.Generic;
using System.Linq;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Reads only explicitly configured seeds into transient memory, without caching or diagnostic values.</summary>
    public sealed class EnvironmentSeedPuuidProvider
    {
        private readonly Func<string> _read;

        /// <summary>Allows an isolated reader for tests; production reads the current process environment on demand.</summary>
        public EnvironmentSeedPuuidProvider(Func<string> read = null)
        {
            _read = read ?? (() => Environment.GetEnvironmentVariable("RIOT_TFT_SEED_PUUIDS"));
        }

        /// <summary>Accepts at most 100 opaque comma-separated seeds and removes duplicates without logging inputs.</summary>
        public IReadOnlyList<string> GetSeeds()
        {
            try
            {
                var value = _read();
                if (string.IsNullOrWhiteSpace(value) || value.Length > 65536 || value.Any(char.IsControl)) throw Invalid();
                var parts = value.Split(',');
                if (parts.Length > 100) throw Invalid();
                var seeds = parts.Select(part => part.Trim()).ToArray();
                if (seeds.Any(seed => seed.Length == 0 || seed.Length > 512 || seed.Any(char.IsWhiteSpace))) throw Invalid();
                return Array.AsReadOnly(seeds.Distinct(StringComparer.Ordinal).ToArray());
            }
            catch { throw Invalid(); }
        }

        /// <summary>Never retains environment contents or arbitrary reader exceptions.</summary>
        private static InvalidOperationException Invalid() => new InvalidOperationException("collection_seeds_unavailable");
    }
}
