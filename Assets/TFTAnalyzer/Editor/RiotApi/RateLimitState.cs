using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Thread-safe, in-memory app/method quota reservations and route-wide cooldowns.</summary>
    public sealed class RateLimitState
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, Region> _regions = new Dictionary<string, Region>(StringComparer.Ordinal);

        /// <summary>Atomically checks every known quota and reserves one physical request.</summary>
        internal void Acquire(string route, string method, DateTimeOffset now)
        {
            lock (_gate)
            {
                var region = GetRegion(route);
                var windows = Windows(region, method).ToArray();
                var retry = BlockedUntil(region, windows, now);
                if (retry.HasValue) throw new RiotApiException(RiotApiFailure.RateLimited, 429, retry);
                foreach (var window in windows) window.Count++;
            }
        }

        /// <summary>Updates only quota metadata; no credentials, identifiers or response bodies are retained.</summary>
        internal void Observe(string route, string method, long status, IReadOnlyDictionary<string, string> headers,
            DateTimeOffset now)
        {
            lock (_gate)
            {
                var region = GetRegion(route);
                Update(region.App, headers, "X-App", now);
                Update(GetMethod(region, method), headers, "X-Method", now);
                if (status == 429)
                {
                    headers.TryGetValue("Retry-After", out var value);
                    var retry = ParseRetryAfter(value, now);
                    if (retry > region.BlockedUntil) region.BlockedUntil = retry;
                }
            }
        }

        /// <summary>Reports the latest outstanding route or exhausted-window deadline without reserving a call.</summary>
        internal DateTimeOffset? RetryAt(string route, string method, DateTimeOffset now)
        {
            lock (_gate)
            {
                var region = GetRegion(route);
                return BlockedUntil(region, Windows(region, method), now);
            }
        }

        /// <summary>Expires elapsed buckets and combines all constraints conservatively.</summary>
        private static DateTimeOffset? BlockedUntil(Region region, IEnumerable<Window> windows, DateTimeOffset now)
        {
            DateTimeOffset? retry = region.BlockedUntil > now ? region.BlockedUntil : (DateTimeOffset?)null;
            foreach (var window in windows)
            {
                window.Refresh(now);
                if (window.Count >= window.Limit && (!retry.HasValue || window.ResetUtc > retry.Value))
                    retry = window.ResetUtc;
            }
            return retry;
        }

        /// <summary>Updates limits without letting stale server counts undo local reservations.</summary>
        private static void Update(Dictionary<int, Window> windows, IReadOnlyDictionary<string, string> headers,
            string prefix, DateTimeOffset now)
        {
            headers.TryGetValue(prefix + "-Rate-Limit", out var limitText);
            headers.TryGetValue(prefix + "-Rate-Limit-Count", out var countText);
            var limits = ParseWindows(limitText, false);
            var counts = ParseWindows(countText, true);
            foreach (var pair in limits)
            {
                if (!windows.TryGetValue(pair.Key, out var window))
                {
                    // A new quota with no usable count must not create an unlimited burst.
                    var count = counts.TryGetValue(pair.Key, out var observed) ? Math.Max(1, observed) : pair.Value;
                    windows[pair.Key] = new Window(pair.Value, count, pair.Key, now);
                }
                else
                {
                    window.Refresh(now);
                    window.Limit = pair.Value;
                }
            }
            foreach (var pair in counts)
            {
                if (!windows.TryGetValue(pair.Key, out var window)) continue;
                window.Refresh(now);
                window.Count = Math.Max(window.Count, pair.Value);
            }
        }

        /// <summary>Reads numeric count/window pairs and ignores malformed header fragments without logging them.</summary>
        private static Dictionary<int, int> ParseWindows(string text, bool allowZero)
        {
            var result = new Dictionary<int, int>();
            foreach (var entry in (text ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) &&
                    int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
                    seconds > 0 && amount >= (allowZero ? 0 : 1))
                    result[seconds] = amount;
            }
            return result;
        }

        /// <summary>Supports delta seconds and HTTP dates, defaulting an unusable 429 header to 60 seconds.</summary>
        private static DateTimeOffset ParseRetryAfter(string text, DateTimeOffset now)
        {
            if (double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) &&
                !double.IsNaN(seconds) && !double.IsInfinity(seconds) && seconds >= 0)
                return AddSeconds(now, seconds);
            if (DateTimeOffset.TryParseExact(text, "r", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date))
                return date > now ? date.ToUniversalTime() : now;
            return AddSeconds(now, 60);
        }

        /// <summary>Bounds arithmetic for untrusted large header windows.</summary>
        private static DateTimeOffset AddSeconds(DateTimeOffset now, double seconds)
            => seconds >= (DateTimeOffset.MaxValue - now).TotalSeconds
                ? DateTimeOffset.MaxValue : now.AddSeconds(seconds);

        /// <summary>Obtains the credential-free state for one approved regional route.</summary>
        private Region GetRegion(string route)
        {
            if (!_regions.TryGetValue(route, out var region)) _regions[route] = region = new Region();
            return region;
        }

        /// <summary>Obtains the quota buckets for one endpoint method.</summary>
        private static Dictionary<int, Window> GetMethod(Region region, string method)
        {
            if (!region.Methods.TryGetValue(method, out var windows))
                region.Methods[method] = windows = new Dictionary<int, Window>();
            return windows;
        }

        /// <summary>Enumerates application and endpoint constraints for one request.</summary>
        private static IEnumerable<Window> Windows(Region region, string method)
            => region.App.Values.Concat(GetMethod(region, method).Values);

        private sealed class Region
        {
            public DateTimeOffset BlockedUntil;
            public readonly Dictionary<int, Window> App = new Dictionary<int, Window>();
            public readonly Dictionary<string, Dictionary<int, Window>> Methods =
                new Dictionary<string, Dictionary<int, Window>>(StringComparer.Ordinal);
        }

        private sealed class Window
        {
            public int Limit;
            public int Count;
            public readonly int Seconds;
            public DateTimeOffset ResetUtc;

            /// <summary>Starts a conservative server-reported window from its observation time.</summary>
            public Window(int limit, int count, int seconds, DateTimeOffset now)
            {
                Limit = limit; Count = count; Seconds = seconds; ResetUtc = AddSeconds(now, seconds);
            }

            /// <summary>Reopens a known quota after its complete window has elapsed.</summary>
            public void Refresh(DateTimeOffset now)
            {
                if (now < ResetUtc) return;
                Count = 0;
                ResetUtc = AddSeconds(now, Seconds);
            }
        }
    }
}
