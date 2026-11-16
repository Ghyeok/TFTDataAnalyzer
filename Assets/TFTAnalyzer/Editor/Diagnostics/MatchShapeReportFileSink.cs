using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>
    /// Publishes each run's aggregate report to a new, uniquely named file under
    /// Library/TftAnalyzerDiagnostics/match-shape/, outside Assets and every analyzer store root. It never overwrites an
    /// existing report, rejects reparse points on the path, and leaves no partial or temporary file on failure.
    /// </summary>
    internal sealed class MatchShapeReportFileSink : IMatchShapeReportSink
    {
        public const string FilePrefix = "match-shape-report-";
        private static readonly Regex Suffix = new Regex(@"\A[0-9a-f]{8}\z", RegexOptions.CultureInvariant);
        private readonly string _projectRoot;
        private readonly string[] _storeRoots;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly Func<string> _suffix;

        public string ReportDirectory { get; }
        /// <summary>The file written by the last successful publication of this instance; null until then.</summary>
        public string LastReportPath { get; private set; }

        /// <summary>Test hook invoked with the flushed temporary path immediately before the atomic move.</summary>
        internal Action<string> BeforeCommit { get; set; }

        /// <summary>Captures the location only; validation happens in Preflight and again in Publish, without writing here.</summary>
        public MatchShapeReportFileSink(string projectRoot, IEnumerable<string> storeRoots)
            : this(projectRoot, storeRoots, null, null) { }

        /// <summary>Injects the file-name clock and random suffix for deterministic collision tests.</summary>
        internal MatchShapeReportFileSink(string projectRoot, IEnumerable<string> storeRoots, Func<DateTimeOffset> utcNow,
            Func<string> suffix)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("A project root is required.", nameof(projectRoot));
            if (storeRoots == null) throw new ArgumentException("Store roots are required.", nameof(storeRoots));
            _projectRoot = Normalize(projectRoot);
            _storeRoots = storeRoots.Select(Normalize).ToArray();
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            _suffix = suffix ?? (() => Guid.NewGuid().ToString("N").Substring(0, 8));
            ReportDirectory = Path.Combine(_projectRoot, "Library", "TftAnalyzerDiagnostics", "match-shape");
        }

        /// <summary>Checks the location and proves it is creatable and writable with a probe file that is removed again.</summary>
        public void Preflight()
        {
            string probe = null;
            try
            {
                ValidateLocation();
                Directory.CreateDirectory(ReportDirectory);
                ValidateLocation();
                probe = Path.Combine(ReportDirectory, ".match-shape-preflight-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.Flush(true);
            }
            catch { throw new IOException("match_shape_report_path_unavailable"); }
            finally { TryDelete(probe); }
        }

        /// <summary>Writes a flushed temporary file and moves it to a new name; the move fails rather than overwrite.</summary>
        public void Publish(byte[] utf8Json, CancellationToken token)
        {
            if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
            token.ThrowIfCancellationRequested();
            string temp = null;
            try
            {
                ValidateLocation();
                Directory.CreateDirectory(ReportDirectory);
                ValidateLocation();
                var suffix = _suffix();
                if (suffix == null || !Suffix.IsMatch(suffix)) throw new IOException("match_shape_report_name_invalid");
                var target = Path.Combine(ReportDirectory, FilePrefix +
                    _utcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + "-" + suffix + ".json");
                temp = Path.Combine(ReportDirectory, ".match-shape-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(utf8Json, 0, utf8Json.Length);
                    stream.Flush(true);
                }
                BeforeCommit?.Invoke(temp);
                token.ThrowIfCancellationRequested();
                RejectReparsePoints(target);
                // File.Move refuses an existing destination, so an earlier report can never be replaced.
                File.Move(temp, target);
                temp = null;
                LastReportPath = target;
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

        /// <summary>Requires the directory to stay outside Assets and every store root, with no link on the path.</summary>
        private void ValidateLocation()
        {
            if (IsWithin(ReportDirectory, Path.Combine(_projectRoot, "Assets")))
                throw new IOException("match_shape_report_inside_assets");
            foreach (var store in _storeRoots)
                if (IsWithin(ReportDirectory, store) || IsWithin(store, ReportDirectory))
                    throw new IOException("match_shape_report_overlaps_store");
            RejectReparsePoints(ReportDirectory);
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
