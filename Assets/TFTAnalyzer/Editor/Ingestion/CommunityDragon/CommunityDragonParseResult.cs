using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Editor.Ingestion.CommunityDragon
{
    public sealed class CommunityDragonParseResult
    {
        public StaticCatalog Catalog { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }

        public CommunityDragonParseResult(StaticCatalog catalog, IEnumerable<ValidationIssue> issues)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Issues = Array.AsReadOnly((issues ?? Enumerable.Empty<ValidationIssue>()).ToArray());
        }
    }
}
