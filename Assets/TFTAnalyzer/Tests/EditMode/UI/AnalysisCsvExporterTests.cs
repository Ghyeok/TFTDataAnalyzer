using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Analysis;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class AnalysisCsvExporterTests
    {
        [Test]
        public void SuppressedCellsStayEmptyAndVisibleCellsIncludeApprovedCounts()
        {
            var hidden = AnalysisFixture.Snapshot(99).AllRows.First().Presentation;
            var csv = AnalysisCsvExporter.Serialize(new[] { hidden });
            var lines = csv.Trim().Split('\n');
            var header = lines[0].TrimEnd('\r').Split(',');
            var cells = lines[1].TrimEnd('\r').Split(',');
            foreach (var name in new[] { "metric", "numerator", "denominator", "estimate_fraction", "lower_fraction", "upper_fraction" })
                Assert.That(cells[Array.IndexOf(header, name)], Is.Empty, name);
            Assert.That(cells[Array.IndexOf(header, "sample_count")], Is.EqualTo("99"));
            var visible = AnalysisCsvExporter.Serialize(new[] { AnalysisFixture.Snapshot(100).AllRows.First().Presentation });
            Assert.That(visible, Does.Contain("top_four_rate")); Assert.That(visible, Does.Contain("first_place_rate"));
            Assert.That(visible, Does.Not.Contain("MatchHash"));
            Assert.That(visible, Does.Contain("1970-01-01T00:00:00"));
        }

        [Test]
        public void ExportCannotOverwriteProjectOrNonCsvFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "analysis-project-" + Guid.NewGuid().ToString("N"));
            var exporter = new AnalysisCsvExporter(root);
            var presentations = AnalysisFixture.Snapshot(1).AllRows.Select(x => x.Presentation);
            Assert.Throws<ArgumentException>(() => exporter.Save(Path.Combine(root, "Assets", "catalog.csv"), presentations));
            Assert.Throws<ArgumentException>(() => exporter.Save(Path.Combine(Path.GetTempPath(), "observations.ndjson"), presentations));
        }

        [Test]
        public void WindowsDeviceNamespaceCannotBypassProjectContainment()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows namespace behavior only.");
            var folder = Path.Combine(Path.GetTempPath(), "TftCsvNamespace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var target = Path.Combine(folder, "protected.csv");
                File.WriteAllText(target, "preserve original");
                var exporter = new AnalysisCsvExporter(folder);
                Assert.Throws<ArgumentException>(() => exporter.Save(@"\\?\" + target,
                    AnalysisFixture.Snapshot(1).AllRows.Select(row => row.Presentation)));
                Assert.Throws<ArgumentException>(() => exporter.Save((@"\\?\" + target).Replace('\\', '/'),
                    AnalysisFixture.Snapshot(1).AllRows.Select(row => row.Presentation)));
                Assert.That(File.ReadAllText(target), Is.EqualTo("preserve original"));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void WindowsShortNameAliasCannotOverwriteProjectCsv()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows alias behavior only.");
            var folder = Path.Combine(Path.GetTempPath(), "HistoricalExportProject-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var shortPath = new StringBuilder(32768);
                var length = GetShortPathName(folder, shortPath, shortPath.Capacity);
                if (length == 0 || length >= shortPath.Capacity || string.Equals(folder, shortPath.ToString(), StringComparison.OrdinalIgnoreCase))
                    Assert.Ignore("Host does not provide an 8.3 alias for the temporary directory.");
                var target = Path.Combine(folder, "protected.csv");
                File.WriteAllText(target, "preserve original");
                Assert.Throws<ArgumentException>(() => new AnalysisCsvExporter(folder).Save(
                    Path.Combine(shortPath.ToString(), "protected.csv"),
                    AnalysisFixture.Snapshot(1).AllRows.Select(row => row.Presentation)));
                Assert.That(File.ReadAllText(target), Is.EqualTo("preserve original"));
            }
            finally { Directory.Delete(folder, true); }
        }
        [DllImport("kernel32.dll", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathName(string path, StringBuilder shortPath, int capacity);

        [Test]
        public void CsvEscapesFormulaTextAndUsesUtf8InvariantFractionsAndAtomicReplace()
        {
            var identity = new CatalogIdentity(new PatchKey("=한글,\"연구\"\n설명", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var catalog = new StaticCatalog(new PatchManifest(2, identity, "", DateTime.UnixEpoch, "", null,
                DateTime.UnixEpoch, null, 0, 0, "", ""), null, null, null, null);
            var source = AnalysisFixture.Observations(100).Select(x => new AnonymousMatchObservation(x.MatchHash,
                new MetricScope(identity, "asia", "1100", "ALL"), x.PlayedAt, x.Participants));
            var snapshot = AnalysisService.BuildSnapshot(catalog, identity, source);
            var folder = Path.Combine(Path.GetTempPath(), "TftCsvTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "Project"));
                var path = Path.Combine(folder, "개인.csv"); File.WriteAllText(path, "old");
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                try
                {
                    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                    new AnalysisCsvExporter(Path.Combine(folder, "Project")).Save(path, snapshot.AllRows.Take(1).Select(x => x.Presentation));
                }
                finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
                var csv = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
                Assert.That(csv, Does.Contain("\"'=한글,\"\"연구\"\"\n설명\""));
                Assert.That(csv, Does.Contain("0.")); Assert.That(csv, Does.Not.Contain("old"));
                Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
                var before = File.ReadAllBytes(path);
                Assert.Throws<ArgumentException>(() => new AnalysisCsvExporter(Path.Combine(folder, "Project")).Save(path,
                    new MetricPresentation[] { null }));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
