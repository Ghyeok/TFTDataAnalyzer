using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Editor.UI.Data;

namespace TFTAnalyzer.Editor.Persistence
{
    internal static class LastImportedSourceVersion
    {
        internal static string Select(IEnumerable<PatchRowViewModel> rows)
        {
            var valid = (rows ?? Array.Empty<PatchRowViewModel>()).Where(row => row != null && row.IsSelectable).ToArray();
            var timed = valid.Select(row => new { Row = row, Time = Completed(row.Entry.ImportCompletedUtc) })
                .Where(value => value.Time.HasValue)
                .OrderByDescending(value => value.Time.Value)
                .ThenByDescending(value => IdentityKey(value.Row), StringComparer.Ordinal)
                .FirstOrDefault();
            if (timed != null) return timed.Row.Identity.Patch.SourceGameVersion;
            var versions = valid.Select(row => row.Identity.Patch.SourceGameVersion).Distinct(StringComparer.Ordinal).ToArray();
            return versions.Length == 1 ? versions[0] : null;
        }

        private static DateTimeOffset? Completed(string value) =>
            !string.IsNullOrEmpty(value) && (value.EndsWith("Z", StringComparison.Ordinal) || value.EndsWith("+00:00", StringComparison.Ordinal)) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) && parsed.Offset == TimeSpan.Zero
                ? parsed : (DateTimeOffset?)null;

        private static string IdentityKey(PatchRowViewModel row) => row.Identity.Patch.SetId + "/" + row.Identity.Patch.PatchId + "/" +
            row.Identity.Patch.SourceGameVersion + "/" + row.Identity.ProfileId;
    }
}
