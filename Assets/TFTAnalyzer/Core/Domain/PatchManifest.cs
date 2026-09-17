using System;
using System.Collections.Generic;

namespace TFTAnalyzer.Domain
{
    public sealed class PatchManifest
    {
        public int SchemaVersion { get; }
        public PatchKey Patch { get; }
        public string SetName { get; }
        public DateTime CreatedUtc { get; }
        public string ContentHash { get; }
        public IReadOnlyList<SourceStamp> Sources { get; }
        public DateTime PolicyReviewDate { get; }
        public IReadOnlyList<string> PolicyUrls { get; }
        public int ValidationErrorCount { get; }
        public int ValidationWarningCount { get; }
        public string PreviousPatchSummary { get; }
        public string CommunityDragonVersion { get; }

        public PatchManifest(
            int schemaVersion,
            PatchKey patch,
            string setName,
            DateTime createdUtc,
            string contentHash,
            IEnumerable<SourceStamp> sources,
            DateTime policyReviewDate,
            IEnumerable<string> policyUrls,
            int validationErrorCount,
            int validationWarningCount,
            string previousPatchSummary,
            string communityDragonVersion)
        {
            if (schemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            if (validationErrorCount < 0 || validationWarningCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(validationErrorCount));
            }

            SchemaVersion = schemaVersion;
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
            SetName = setName ?? "";
            CreatedUtc = DomainValues.AsUtc(createdUtc);
            ContentHash = contentHash ?? "";
            Sources = DomainValues.Copy(sources);
            PolicyReviewDate = DomainValues.AsUtc(policyReviewDate);
            PolicyUrls = DomainValues.Copy(policyUrls);
            ValidationErrorCount = validationErrorCount;
            ValidationWarningCount = validationWarningCount;
            PreviousPatchSummary = previousPatchSummary ?? "";
            CommunityDragonVersion = communityDragonVersion ?? "";
        }
    }
}
