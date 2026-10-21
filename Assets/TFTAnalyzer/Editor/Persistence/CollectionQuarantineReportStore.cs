using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.Persistence
{
    /// <summary>Atomically publishes only the safe quarantine schema in the observation store's validated root.</summary>
    internal sealed class CollectionQuarantineReportStore
    {
        private readonly AnalyzerStorePaths _paths;
        private readonly Action<string, string> _replace;

        /// <summary>Shares validated store paths; the replace delegate permits controlled publication-failure tests.</summary>
        internal CollectionQuarantineReportStore(AnalyzerStorePaths paths, Action<string, string> replace = null)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _replace = replace ?? ((source, target) => File.Replace(source, target, null));
        }

        /// <summary>Preserves the previous report on publication failure and cleans only this call's staging file.</summary>
        internal void Save(CollectionQuarantineReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string staging = null;
            try
            {
                _paths.EnsureSafe();
                var target = _paths.QuarantineReportPath(report.RequestContextHash);
                AnalyzerStorePaths.RejectReparsePoints(target);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                AnalyzerStorePaths.RejectReparsePoints(target);
                var document = new JObject
                {
                    ["schemaVersion"] = 1, ["requestContextHash"] = report.RequestContextHash,
                    ["completedUtc"] = report.CompletedUtc.ToString("o", CultureInfo.InvariantCulture),
                    ["quarantinedMatches"] = report.QuarantinedMatches, ["truncated"] = report.Truncated,
                    ["entries"] = new JArray(report.Entries.Select(x => new JObject { ["kind"] = x.Kind, ["idHash"] = x.IdHash }))
                };
                var bytes = new UTF8Encoding(false, true).GetBytes(document.ToString(Formatting.Indented));
                staging = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                AnalyzerStorePaths.RejectReparsePoints(staging);
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                AnalyzerStorePaths.RejectReparsePoints(staging);
                AnalyzerStorePaths.RejectReparsePoints(target);
                if (File.Exists(target)) _replace(staging, target); else File.Move(staging, target);
            }
            catch (Exception error) when (IsAccessFailure(error))
            { throw new IOException("quarantine_report_save_failed"); }
            finally
            {
                if (staging != null)
                {
                    try { AnalyzerStorePaths.RejectReparsePoints(staging); if (File.Exists(staging)) File.Delete(staging); }
                    catch (Exception error) when (IsAccessFailure(error)) { }
                }
            }
        }

        /// <summary>Recognizes filesystem failures without preserving attacker-controlled messages or inner exceptions.</summary>
        private static bool IsAccessFailure(Exception error) => error is IOException || error is UnauthorizedAccessException ||
            error is ArgumentException || error is NotSupportedException || error is SecurityException;
    }
}
