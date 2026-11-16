using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>
    /// The only Persistence boundary of the diagnostic: reads already verified local catalogs through the read-only
    /// published-catalog loader. It never imports, upserts, refreshes assets or opens analyzer stores.
    /// </summary>
    internal sealed class PublishedCatalogMatchShapeSource : IMatchShapeCatalogSource
    {
        private static readonly Regex ReleasePrefix = new Regex(@"\A([0-9]{1,5})\.([0-9]{1,5})(?:\.|\z)", RegexOptions.CultureInvariant);
        private readonly StaticCatalogPaths _paths;
        private readonly Func<CatalogIndex> _index;

        public PublishedCatalogMatchShapeSource(StaticCatalogPaths paths, Func<CatalogIndex> index)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        /// <summary>Selects the most recently imported verified catalog; ambiguity or any mismatch stops before the network.</summary>
        public MatchShapeCatalogSelection Load(out string failureCode)
        {
            failureCode = null;
            try
            {
                var rows = new PublishedCatalogLoader(_paths).Load(_index()).Where(row => row != null && row.IsSelectable).ToArray();
                if (rows.Length == 0) return Fail("catalog_unavailable", out failureCode);
                var row = Select(rows);
                if (row == null) return Fail("catalog_ambiguous", out failureCode);
                var catalog = row.Catalog;
                var patch = catalog.Manifest.Patch;
                var identity = row.Identity.Patch;
                if (patch.SetNumber != identity.SetNumber || patch.SetNumber < 1 ||
                    !string.Equals(patch.SetId, identity.SetId, StringComparison.Ordinal) ||
                    !string.Equals(patch.PatchId, identity.PatchId, StringComparison.Ordinal) ||
                    !string.Equals(patch.SourceGameVersion, identity.SourceGameVersion, StringComparison.Ordinal))
                    return Fail("catalog_set_mismatch", out failureCode);
                var release = ReleasePrefix.Match(identity.SourceGameVersion ?? "");
                if (!release.Success) return Fail("catalog_release_unavailable", out failureCode);
                return new MatchShapeCatalogSelection(identity.SetNumber, release.Groups[1].Value + "." + release.Groups[2].Value,
                    Copy(catalog.Units.Select(value => value.StableId)), Copy(catalog.Items.Select(value => value.StableId)),
                    Copy(catalog.Traits.Select(value => value.StableId)), Copy(catalog.Augments.Select(value => value.StableId)));
            }
            catch { return Fail("catalog_unavailable", out failureCode); }
        }

        /// <summary>Uses the latest UTC import completion time; without one, only a single verified row is unambiguous.</summary>
        private static PatchRowViewModel Select(IReadOnlyList<PatchRowViewModel> rows)
        {
            var timed = rows.Select(row => new { Row = row, Time = Completed(row.Entry.ImportCompletedUtc) })
                .Where(value => value.Time.HasValue).OrderByDescending(value => value.Time.Value).ToArray();
            if (timed.Length > 0)
                return timed.Length > 1 && timed[0].Time == timed[1].Time ? null : timed[0].Row;
            return rows.Count == 1 ? rows[0] : null;
        }

        private static DateTimeOffset? Completed(string value) =>
            !string.IsNullOrEmpty(value) && (value.EndsWith("Z", StringComparison.Ordinal) || value.EndsWith("+00:00", StringComparison.Ordinal)) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) && parsed.Offset == TimeSpan.Zero
                ? parsed : (DateTimeOffset?)null;

        private static IReadOnlyCollection<string> Copy(IEnumerable<string> values)
            => new HashSet<string>((values ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);

        private static MatchShapeCatalogSelection Fail(string code, out string failureCode)
        {
            failureCode = code;
            return null;
        }
    }
}
