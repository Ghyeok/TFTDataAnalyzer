using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>
    /// Publishes the aggregate report atomically under Library/TftAnalyzerDiagnostics/match-shape/, outside Assets and
    /// every analyzer store root. Reparse points on the path are rejected; failures leave no partial or temporary file.
    /// </summary>
    public sealed class MatchShapeReportFileSink : IMatchShapeReportSink
    {
        public const string FileName = "match-shape-report.json";
        public string ReportDirectory { get; }
        public string ReportPath => Path.Combine(ReportDirectory, FileName);

        /// <summary>Test hook invoked with the flushed temporary path immediately before the atomic move.</summary>
        internal Action<string> BeforeCommit { get; set; }

        /// <summary>Validates the location only; construction performs no filesystem writes.</summary>
        public MatchShapeReportFileSink(string projectRoot, IEnumerable<string> storeRoots)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("A project root is required.", nameof(projectRoot));
            if (storeRoots == null) throw new ArgumentException("Store roots are required.", nameof(storeRoots));
            var root = Normalize(projectRoot);
            ReportDirectory = Path.Combine(root, "Library", "TftAnalyzerDiagnostics", "match-shape");
            if (IsWithin(ReportDirectory, Path.Combine(root, "Assets")))
                throw new ArgumentException("The diagnostic report must stay outside Assets.");
            foreach (var store in storeRoots.Select(Normalize))
                if (IsWithin(ReportDirectory, store) || IsWithin(store, ReportDirectory))
                    throw new ArgumentException("The diagnostic report must stay outside analyzer store roots.");
        }

        /// <summary>Writes a flushed temporary file in the target directory and moves it into place in one step.</summary>
        public void Publish(byte[] utf8Json, CancellationToken token)
        {
            if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
            token.ThrowIfCancellationRequested();
            string temp = null;
            try
            {
                RejectReparsePoints(ReportPath);
                Directory.CreateDirectory(ReportDirectory);
                RejectReparsePoints(ReportPath);
                temp = Path.Combine(ReportDirectory, ".match-shape-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(utf8Json, 0, utf8Json.Length);
                    stream.Flush(true);
                }
                BeforeCommit?.Invoke(temp);
                token.ThrowIfCancellationRequested();
                RejectReparsePoints(ReportPath);
                if (File.Exists(ReportPath)) File.Replace(temp, ReportPath, null);
                else File.Move(temp, ReportPath);
                temp = null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                TryDelete(temp);
                throw;
            }
            catch
            {
                TryDelete(temp);
                // Native IO messages can contain local paths; only a fixed code leaves this method.
                throw new IOException("match_shape_report_publish_failed");
            }
        }

        private static void TryDelete(string path)
        {
            if (path == null) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* Cleanup failure is reported by the caller's fixed failure code. */ }
        }

        /// <summary>Rejects a junction or symbolic link at the target or any existing ancestor.</summary>
        private static void RejectReparsePoints(string path)
        {
            for (var current = Normalize(path); current != null; current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("match_shape_report_link_rejected");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.");
            var full = Path.GetFullPath(path);
            return full.Length == Path.GetPathRoot(full).Length ? full
                : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsWithin(string path, string parent)
            => string.Equals(path, parent, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }
}
