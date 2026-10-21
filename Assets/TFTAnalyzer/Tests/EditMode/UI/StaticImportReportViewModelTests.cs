using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class StaticImportReportViewModelTests
    {
        private static readonly DateTimeOffset Time = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(9));

        [Test]
        public void RejectedResultKeepsBothValuesAndSeparatesDecisionCounts()
        {
            var report = Read(Result(false, new ValidationReport(new[] {
                new ValidationIssue(ValidationSeverity.Error, "bad_trait", "TEST18_Unit", "Missing trait"),
                new ValidationIssue(ValidationSeverity.Warning, "no_icon", "TEST18_Unit", "Missing icon") }),
                new[] { Decision(MergeResolution.OfficialWins), Decision(MergeResolution.SupplementFill) }));
            Assert.That(report.Published, Is.False);
            Assert.That(report.ErrorCount, Is.EqualTo(1)); Assert.That(report.WarningCount, Is.EqualTo(1));
            Assert.That(report.OfficialWinsCount, Is.EqualTo(1)); Assert.That(report.SupplementFillCount, Is.EqualTo(1));
            Assert.That(string.Join("\n", report.MergeRows), Does.Contain("Official").And.Contain("Supplement").And.Contain("공식 값 유지").And.Contain("빈 필드 보완"));
            Assert.That(report.StartedUtc.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void AbsentResultAndAbsentValidationNeverImplySuccessfulEmptyValidation()
        {
            var absent = Read(null);
            Assert.That(absent.ResultAvailable, Is.False); Assert.That(absent.Published, Is.Null);
            Assert.That(absent.ValidationTotal, Is.Null); Assert.That(absent.MergeTotal, Is.Null);
            var noValidation = Read(Result(true, null, null));
            Assert.That(noValidation.ResultAvailable, Is.True); Assert.That(noValidation.HasValidation, Is.False);
            Assert.That(noValidation.ErrorCount, Is.Null); Assert.That(noValidation.WarningCount, Is.Null);
            Assert.That(noValidation.MergeTotal, Is.Zero);
        }

        [Test]
        public void ReturnedZeroIssueValidationIsDistinctFromUnavailableInformation()
        {
            var report = Read(Result(true, new ValidationReport(null), null));
            Assert.That(report.HasValidation, Is.True); Assert.That(report.ErrorCount, Is.Zero);
            Assert.That(report.ValidationTotal, Is.Zero); Assert.That(report.ValidationRows, Is.Empty);
        }

        [Test]
        public void SnapshotRetainsTotalsButCopiesOnlyTwoHundredRowsAndBoundedFields()
        {
            var longValue = new string('x', 4096);
            var issues = Enumerable.Range(0, 215).Select(i => new ValidationIssue(ValidationSeverity.Error,
                "bad", "ID"+i, longValue));
            var merges = Enumerable.Range(0, 217).Select(i => new MergeConflict("unit", "ID"+i,
                "description", longValue, "tail", MergeResolution.OfficialWins)).ToArray();
            var report = Read(Result(false, new ValidationReport(issues), merges));
            Assert.That(report.ValidationTotal, Is.EqualTo(215)); Assert.That(report.MergeTotal, Is.EqualTo(217));
            Assert.That(report.ValidationRows, Has.Count.EqualTo(200)); Assert.That(report.MergeRows, Has.Count.EqualTo(200));
            Assert.That(report.OfficialWinsCount, Is.EqualTo(217)); Assert.That(report.TextTruncated, Is.True);
            Assert.That(report.ValidationRows[0], Does.Not.Contain(new string('x', 513)).And.Contain("생략"));
        }

        [Test]
        public void MutableInputCannotChangeCopiedRowsAndMarkupRemainsLiteral()
        {
            var list = new List<MergeConflict> { new MergeConflict("unit", "ID", "name", "<b>Official</b>", "Supplement", MergeResolution.OfficialWins) };
            var report = Read(Result(true, new ValidationReport(null), list));
            list.Clear();
            Assert.That(report.MergeRows, Has.Count.EqualTo(1));
            Assert.That(report.MergeRows[0], Does.Contain("<b>Official</b>"));
            Assert.That(((IList<string>)report.MergeRows).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)report.MergeRows).Clear());
        }

        [Test]
        public void SnapshotDoesNotKeepCatalogOrFullResultOrExceptionObjects()
        {
            var report = Read(Result(true, new ValidationReport(null), null));
            Assert.That(report.RequestedIdentity, Is.EqualTo(DataManagerTestSupport.Identity()));
            Assert.That(typeof(StaticImportReportViewModel).GetProperties().Any(p =>
                p.PropertyType == typeof(StaticImportResult) || p.PropertyType == typeof(TFTAnalyzer.Domain.StaticCatalog) ||
                typeof(Exception).IsAssignableFrom(p.PropertyType)), Is.False);
        }

        internal static MergeConflict Decision(MergeResolution resolution) =>
            new MergeConflict("unit", "TEST18_Unit", "name.enUs", "Official", "Supplement", resolution);
        internal static StaticImportResult Result(bool published, ValidationReport validation, IReadOnlyList<MergeConflict> merges) =>
            new StaticImportResult(published, true, "private/directory", "hash", DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()), validation, merges);
        private static StaticImportReportViewModel Read(StaticImportResult result) =>
            StaticImportReportViewModel.FromResult(DataManagerTestSupport.Identity(), false, Time, result);
    }
}
