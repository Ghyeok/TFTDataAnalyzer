using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>A bounded immutable diagnostic document without raw identifiers or match-level associations.</summary>
    internal sealed class CollectionQuarantineReport
    {
        internal const int MaximumEntries = 4096;
        internal string RequestContextHash { get; }
        internal DateTime CompletedUtc { get; }
        internal int QuarantinedMatches { get; }
        internal bool Truncated { get; }
        internal IReadOnlyList<UnknownStaticId> Entries { get; }
        /// <summary>Copies validated diagnostic pairs and retains only a nonidentifying requested-context digest.</summary>
        internal CollectionQuarantineReport(string context, DateTime completedUtc, int matches, bool truncated,
            IEnumerable<UnknownStaticId> entries)
        {
            if (context == null || !Regex.IsMatch(context, "\\A[0-9a-f]{64}\\z") || completedUtc.Kind != DateTimeKind.Utc ||
                completedUtc == DateTime.MinValue || matches < 1 || entries == null)
                throw new ArgumentException("quarantine_report_invalid");
            var copy = entries.ToArray();
            if (copy.Length == 0 || copy.Length > MaximumEntries || copy.Any(x => x == null))
                throw new ArgumentException("quarantine_report_invalid");
            RequestContextHash = context; CompletedUtc = completedUtc; QuarantinedMatches = matches; Truncated = truncated;
            Entries = Array.AsReadOnly(copy.GroupBy(x => x.Kind + "|" + x.IdHash, StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.First()).ToArray());
        }

        /// <summary>Hashes the selected catalog/content and requested filters; labels never enter the report.</summary>
        internal static string ContextHash(StaticCatalog catalog, MatchCollectionRequest request)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (request == null) throw new ArgumentNullException(nameof(request));
            var patch = request.ActiveCatalog.Patch;
            var context = new JArray(patch.SetId, patch.SetNumber, patch.PatchId, patch.SourceGameVersion,
                request.ActiveCatalog.ProfileId, catalog.Manifest.ContentHash, request.Region, request.QueueId, request.TierBucket);
            return ContentHasher.Sha256Hex("quarantine-context-v1|" + context.ToString(Formatting.None));
        }
    }

    /// <summary>Deduplicates only safe hashes and stops retaining diagnostics at the explicit per-run bound.</summary>
    internal sealed class CollectionQuarantineAccumulator
    {
        private readonly SortedDictionary<string, UnknownStaticId> _entries = new SortedDictionary<string, UnknownStaticId>(StringComparer.Ordinal);
        internal int QuarantinedMatches { get; private set; }
        internal bool Truncated { get; private set; }

        /// <summary>Counts the quarantined match and signals when the next request must not start.</summary>
        internal bool Add(IEnumerable<UnknownStaticId> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            QuarantinedMatches++;
            foreach (var entry in entries)
            {
                if (entry == null) throw new ArgumentException("quarantine_report_invalid");
                var key = entry.Kind + "|" + entry.IdHash;
                if (_entries.ContainsKey(key)) continue;
                if (_entries.Count >= CollectionQuarantineReport.MaximumEntries) { Truncated = true; break; }
                _entries.Add(key, entry);
            }
            return Truncated;
        }

        /// <summary>Snapshots run diagnostics after the request stream has settled.</summary>
        internal CollectionQuarantineReport Freeze(string context, DateTime completedUtc)
            => new CollectionQuarantineReport(context, completedUtc, QuarantinedMatches, Truncated, _entries.Values);
    }
}
