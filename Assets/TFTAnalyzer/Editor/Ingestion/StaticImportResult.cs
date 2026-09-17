using System;
using System.Collections.Generic;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Ingestion
{
    public sealed class StaticImportResult
    {
        public bool Published { get; }
        public bool Idempotent { get; }
        public string FinalDirectory { get; }
        public string ContentHash { get; }
        public StaticCatalog Catalog { get; }
        public ValidationReport Validation { get; }
        public IReadOnlyList<MergeConflict> MergeConflicts { get; }

        public StaticImportResult(
            bool published,
            bool idempotent,
            string finalDirectory,
            string contentHash,
            StaticCatalog catalog,
            ValidationReport validation,
            IReadOnlyList<MergeConflict> mergeConflicts)
        {
            Published = published;
            Idempotent = idempotent;
            FinalDirectory = finalDirectory ?? "";
            ContentHash = contentHash ?? "";
            Catalog = catalog;
            Validation = validation;
            MergeConflicts = mergeConflicts ?? Array.Empty<MergeConflict>();
        }
    }
}
