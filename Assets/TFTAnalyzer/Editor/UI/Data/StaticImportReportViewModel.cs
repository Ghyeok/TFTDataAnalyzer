using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;

namespace TFTAnalyzer.Editor.UI.Data
{
    /// <summary>Bounded, session-only display snapshot of a static import result.</summary>
    public sealed class StaticImportReportViewModel
    {
        public CatalogIdentity RequestedIdentity { get; }
        public bool IncludeSupplement { get; }
        public DateTimeOffset StartedUtc { get; }
        public bool ResultAvailable { get; }
        public bool? Published { get; }
        public bool? Idempotent { get; }
        public bool HasValidation { get; }
        public int? ErrorCount { get; }
        public int? WarningCount { get; }
        public int? ValidationTotal { get; }
        public int? MergeTotal { get; }
        public int OfficialWinsCount { get; }
        public int SupplementFillCount { get; }
        public IReadOnlyList<string> ValidationRows { get; }
        public IReadOnlyList<string> MergeRows { get; }
        public bool TextTruncated { get; }

        /// <summary>Copies only approved static display fields; null means no returned report.</summary>
        public static StaticImportReportViewModel FromResult(CatalogIdentity requested, bool includeSupplement,
            DateTimeOffset startedUtc, StaticImportResult result) =>
            new StaticImportReportViewModel(requested, includeSupplement, startedUtc, result);

        private StaticImportReportViewModel(CatalogIdentity requested, bool includeSupplement,
            DateTimeOffset startedUtc, StaticImportResult result)
        {
            RequestedIdentity = requested ?? throw new ArgumentNullException(nameof(requested));
            IncludeSupplement = includeSupplement;
            StartedUtc = startedUtc.ToUniversalTime();
            ResultAvailable = result != null;
            Published = result?.Published;
            Idempotent = result?.Idempotent;
            HasValidation = result?.Validation != null;
            ErrorCount = result?.Validation?.ErrorCount;
            WarningCount = result?.Validation?.WarningCount;
            ValidationTotal = result?.Validation?.Issues.Count;
            MergeTotal = result?.MergeConflicts.Count;
            var clipped = false;
            var validation = new List<string>();
            foreach (var issue in result?.Validation?.Issues.Take(200) ?? Enumerable.Empty<TFTAnalyzer.Validation.ValidationIssue>())
                validation.Add($"{(issue.Severity == TFTAnalyzer.Validation.ValidationSeverity.Error ? "오류" : "경고")} · {Bound(issue.Code, ref clipped)} · {Bound(issue.RecordId, ref clipped)}\n{Bound(issue.Message, ref clipped)}");
            var merges = new List<string>();
            if (result != null)
            {
                OfficialWinsCount = result.MergeConflicts.Count(x => x.Resolution == MergeResolution.OfficialWins);
                SupplementFillCount = result.MergeConflicts.Count(x => x.Resolution == MergeResolution.SupplementFill);
                foreach (var decision in result.MergeConflicts.Take(200))
                    merges.Add($"{Bound(decision.RecordType, ref clipped)} · {Bound(decision.StableId, ref clipped)} · {Bound(decision.FieldName, ref clipped)}\n" +
                        $"공식: {Bound(decision.OfficialValue, ref clipped)}\n보완: {Bound(decision.SupplementValue, ref clipped)}\n" +
                        (decision.Resolution == MergeResolution.OfficialWins ? "결정: 공식 값 유지" : "결정: 빈 필드 보완"));
            }
            ValidationRows = validation.AsReadOnly();
            MergeRows = merges.AsReadOnly();
            TextTruncated = clipped;
        }

        /// <summary>Limits each external static field without splitting a surrogate pair.</summary>
        private static string Bound(string value, ref bool clipped)
        {
            value = value ?? "";
            if (value.Length <= 512) return value;
            clipped = true;
            const string marker = "… (일부 생략)";
            var length = 512 - marker.Length;
            if (char.IsHighSurrogate(value[length - 1])) length--;
            return value.Substring(0, length) + marker;
        }
    }
}
