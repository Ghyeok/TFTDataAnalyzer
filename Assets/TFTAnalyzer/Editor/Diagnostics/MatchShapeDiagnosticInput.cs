using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>Immutable, non-identifying inputs of one shape diagnostic run, copied from a verified local catalog.</summary>
    public sealed class MatchShapeDiagnosticInput
    {
        private static readonly Regex ReleasePattern = new Regex(@"\A[0-9]{1,5}\.[0-9]{1,5}\z", RegexOptions.CultureInvariant);

        public string Region { get; }
        public int TargetQueueId { get; }
        public int SetNumber { get; }
        public string CatalogRelease { get; }
        internal IReadOnlyCollection<string> UnitIds { get; }
        internal IReadOnlyCollection<string> ItemIds { get; }
        internal IReadOnlyCollection<string> TraitIds { get; }
        internal IReadOnlyCollection<string> AugmentIds { get; }

        /// <summary>Validates and copies every value so later catalog changes cannot affect a running diagnostic.</summary>
        public MatchShapeDiagnosticInput(string region, int targetQueueId, int setNumber, string catalogRelease,
            IEnumerable<string> unitIds, IEnumerable<string> itemIds, IEnumerable<string> traitIds, IEnumerable<string> augmentIds)
        {
            Region = RiotRouting.NormalizeRegion(region);
            if (targetQueueId < 1 || targetQueueId > 999999) throw new ArgumentOutOfRangeException(nameof(targetQueueId));
            if (setNumber < 1) throw new ArgumentOutOfRangeException(nameof(setNumber));
            if (catalogRelease == null || !ReleasePattern.IsMatch(catalogRelease))
                throw new ArgumentException("A catalog release of two numeric groups is required.", nameof(catalogRelease));
            TargetQueueId = targetQueueId;
            SetNumber = setNumber;
            CatalogRelease = catalogRelease;
            UnitIds = Copy(unitIds, nameof(unitIds));
            ItemIds = Copy(itemIds, nameof(itemIds));
            TraitIds = Copy(traitIds, nameof(traitIds));
            AugmentIds = Copy(augmentIds, nameof(augmentIds));
        }

        private static IReadOnlyCollection<string> Copy(IEnumerable<string> values, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            return new HashSet<string>(values.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }
    }
}
