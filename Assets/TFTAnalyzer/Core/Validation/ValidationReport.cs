using System;
using System.Collections.Generic;
using System.Linq;

namespace TFTAnalyzer.Validation
{
    public sealed class ValidationReport
    {
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public bool CanPublish => ErrorCount == 0;

        public ValidationReport(IEnumerable<ValidationIssue> issues)
        {
            var stable = (issues ?? Enumerable.Empty<ValidationIssue>())
                .GroupBy(issue => $"{issue.Severity}\u001f{issue.Code}\u001f{issue.RecordId}\u001f{issue.Message}")
                .Select(group => group.First())
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.RecordId, StringComparer.Ordinal)
                .ToArray();
            Issues = Array.AsReadOnly(stable);
            ErrorCount = stable.Count(issue => issue.Severity == ValidationSeverity.Error);
            WarningCount = stable.Count(issue => issue.Severity == ValidationSeverity.Warning);
        }
    }
}
