using System;
using System.Collections.Generic;

namespace TFTAnalyzer.Domain
{
    public sealed class PatchManifest
    {
        public int SchemaVersion { get; }
        public PatchKey Patch { get; }
        public CatalogIdentity Identity { get; }
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
            : this(schemaVersion, patch, null, setName, createdUtc, contentHash, sources,
                policyReviewDate, policyUrls, validationErrorCount, validationWarningCount,
                previousPatchSummary, communityDragonVersion)
        {
        }

        public PatchManifest(
            int schemaVersion,
            CatalogIdentity identity,
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
            : this(schemaVersion, identity?.Patch, identity, setName, createdUtc, contentHash,
                sources, policyReviewDate, policyUrls, validationErrorCount,
                validationWarningCount, previousPatchSummary, communityDragonVersion)
        {
        }

        private PatchManifest(
            int schemaVersion,
            PatchKey patch,
            CatalogIdentity identity,
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

            if ((schemaVersion == 1 && identity != null) ||
                (schemaVersion >= 2 && identity == null))
            {
                throw new ArgumentException("Schema 1 has no profile; schema 2 requires an identity.");
            }

            SchemaVersion = schemaVersion;
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
            Identity = identity;
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
