using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Editor.Persistence;

namespace TFTAnalyzer.Editor.UI.Analysis
{
    public sealed class AnalysisCsvExporter
    {
        private readonly string _projectRoot;
        public AnalysisCsvExporter(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("analysis_export_project_required");
            _projectRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        public static string Serialize(IEnumerable<MetricPresentation> presentations)
        {
            if (presentations == null) throw new ArgumentNullException(nameof(presentations));
            var csv = new StringBuilder("purpose,kind,set_id,set_number,patch,source_game_version,source_profile,region,queue,tier_label,first_composition,second_composition,sample_count,average_placement,first_played_at_utc,last_played_at_utc,ambiguous_lobby_count,state,policy_note,metric,numerator,denominator,estimate_fraction,lower_fraction,upper_fraction\r\n");
            foreach (var value in presentations)
            {
                if (value == null || value.Scope == null) throw new ArgumentException("analysis_export_invalid_presentation");
                var patch = value.Scope.ActiveCatalog.Patch;
                var prefix = new[] { Text("personal_research_local"), Text(value.SecondFingerprint == null ? "composition" : "pairwise"),
                    Text(patch.SetId), Number(patch.SetNumber), Text(patch.PatchId), Text(patch.SourceGameVersion), Text(value.Scope.ActiveCatalog.ProfileId),
                    Text(value.Scope.Region), Text(value.Scope.QueueId), Text(value.Scope.TierBucket), Text(value.FirstFingerprint), Text(value.SecondFingerprint),
                    Number(value.SampleCount), value.AveragePlacement.HasValue ? Number(value.AveragePlacement.Value) : "",
                    Time(value.FirstPlayedAt), Time(value.LastPlayedAt), Number(value.AmbiguousLobbyCount), Text(value.State.ToString()), Text(value.ReasonCode) };
                if (value.Ratios.Count == 0) csv.Append(string.Join(",", prefix)).Append(",,,,,,\r\n");
                else foreach (var ratio in value.Ratios)
                    csv.Append(string.Join(",", prefix)).Append(',').Append(string.Join(",", new[] { Text(ratio.Key), Number(ratio.Numerator), Number(ratio.Denominator),
                        Number(ratio.Rate), Number(ratio.Interval.Lower), Number(ratio.Interval.Upper) })).Append("\r\n");
            }
            return csv.ToString();
        }
        public void Save(string path, IEnumerable<MetricPresentation> presentations)
        {
            // Mono's parent traversal can stay at the same device root forever. Reject before any IO.
            if (string.IsNullOrWhiteSpace(path) || IsDevicePath(path) || !Path.IsPathRooted(path))
                throw new ArgumentException("analysis_export_path_invalid");
            var target = Path.GetFullPath(path);
            if (IsDevicePath(target) || !string.Equals(Path.GetExtension(target), ".csv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, _projectRoot, StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith(_projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("analysis_export_external_csv_required");
            AnalyzerStorePaths.RejectReparsePoints(target);
            var directory = Path.GetDirectoryName(target);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("analysis_export_directory_missing");
            EnsureExternalDirectory(directory);
            var content = Serialize(presentations); var staging = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false, true).GetBytes(content);
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                AnalyzerStorePaths.RejectReparsePoints(target);
                EnsureExternalDirectory(directory);
                if (File.Exists(target)) File.Replace(staging, target, null); else File.Move(staging, target);
            }
            finally { if (File.Exists(staging)) File.Delete(staging); }
        }
        private static bool IsDevicePath(string path)
        {
            var windows = path.Replace('/', '\\');
            return windows.StartsWith(@"\\?\", StringComparison.Ordinal) || windows.StartsWith(@"\\.\", StringComparison.Ordinal);
        }
        private void EnsureExternalDirectory(string directory)
        {
            if (Path.DirectorySeparatorChar != '\\' || !Directory.Exists(_projectRoot)) return;
            // File handles resolve short names and substituted drives to the same physical namespace.
            var project = FinalDirectory(_projectRoot).TrimEnd('\\');
            var destination = FinalDirectory(directory).TrimEnd('\\');
            if (string.Equals(project, destination, StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith(project + "\\", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("analysis_export_external_csv_required");
        }
        private static string FinalDirectory(string path)
        {
            // Read attributes only; backup-semantics permits opening a directory, not writing it.
            using (var handle = OpenDirectory(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new IOException("analysis_export_path_unresolved");
                var buffer = new StringBuilder(512);
                var length = GetFinalDirectory(handle, buffer, (uint)buffer.Capacity, 0);
                if (length >= buffer.Capacity && length < 32768)
                {
                    buffer = new StringBuilder((int)length + 1);
                    length = GetFinalDirectory(handle, buffer, (uint)buffer.Capacity, 0);
                }
                if (length == 0 || length >= buffer.Capacity) throw new IOException("analysis_export_path_unresolved");
                return buffer.ToString();
            }
        }
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle OpenDirectory(string path, uint access, uint share, IntPtr security,
            uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalDirectory(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Time(DateTimeOffset? value) => value.HasValue ? value.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture) : "";
        private static string Text(string value)
        {
            value = value ?? "";
            var meaningful = value.TrimStart();
            if (meaningful.Length != 0 && ("=+-@".IndexOf(meaningful[0]) >= 0 || value[0] == '\t' || value[0] == '\r' || value[0] == '\n')) value = "'" + value;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
