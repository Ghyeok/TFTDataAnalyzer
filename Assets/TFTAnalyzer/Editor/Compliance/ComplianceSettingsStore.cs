using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Editor.Persistence;

namespace TFTAnalyzer.Editor.Compliance
{
    public enum ComplianceReadState { Missing, Valid, Invalid, Unavailable }
    public sealed class ComplianceSettingsReadResult
    {
        public ComplianceReadState State { get; }
        public ComplianceSettings Settings { get; }
        public string ErrorCode { get; }
        public ComplianceSettingsReadResult(ComplianceReadState state, ComplianceSettings settings = null, string errorCode = null)
        { State = state; Settings = settings; ErrorCode = errorCode; }
    }
    public sealed class ComplianceSettingsStore
    {
        private readonly string _projectRoot;
        private readonly Action<string, string> _replace;
        public ComplianceSettingsStore(string projectRoot) : this(projectRoot, (staging, target) => File.Replace(staging, target, null)) { }
        internal ComplianceSettingsStore(string projectRoot, Action<string, string> replace)
        { _projectRoot = projectRoot; _replace = replace ?? throw new ArgumentNullException(nameof(replace)); }

        public ComplianceSettingsReadResult Load()
        {
            try
            {
                var target = TargetPath();
                AnalyzerStorePaths.RejectReparsePoints(target);
                if (Directory.Exists(target)) throw new IOException();
                // Open directly: File.Exists hides access errors and would misreport them as missing.
                string json;
                try { json = File.ReadAllText(target, new UTF8Encoding(false, true)); }
                catch (FileNotFoundException) { return new ComplianceSettingsReadResult(ComplianceReadState.Missing); }
                catch (DirectoryNotFoundException) { return new ComplianceSettingsReadResult(ComplianceReadState.Missing); }
                using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                {
                    var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new JsonException();
                    var names = new[] { "policyReviewedOn", "policyUrls", "registrationStatus", "acknowledgedAt" };
                    if (!document.Properties().Select(p => p.Name).OrderBy(n => n).SequenceEqual(names.OrderBy(n => n))) throw new JsonException();
                    foreach (var name in new[] { "policyReviewedOn", "registrationStatus", "acknowledgedAt" })
                        if (document[name].Type != JTokenType.String && document[name].Type != JTokenType.Null) throw new JsonException();
                    if (!(document["policyUrls"] is JArray urls) || urls.Any(url => url.Type != JTokenType.String)) throw new JsonException();
                    var settings = document.ToObject<ComplianceSettings>();
                    if (!CompliancePolicy.IsWellFormed(settings)) throw new JsonException();
                    return new ComplianceSettingsReadResult(ComplianceReadState.Valid, settings);
                }
            }
            catch (JsonException) { return new ComplianceSettingsReadResult(ComplianceReadState.Invalid, errorCode: "compliance_invalid"); }
            catch (DecoderFallbackException) { return new ComplianceSettingsReadResult(ComplianceReadState.Invalid, errorCode: "compliance_invalid"); }
            catch (Exception error) when (IsAccessFailure(error))
            { return new ComplianceSettingsReadResult(ComplianceReadState.Unavailable, errorCode: "compliance_unavailable"); }
        }

        public void Save(ComplianceSettings settings)
        {
            if (!CompliancePolicy.IsWellFormed(settings)) throw new ArgumentException("compliance_invalid");
            string staging = null;
            try
            {
                var target = TargetPath();
                AnalyzerStorePaths.RejectReparsePoints(target);
                var directory = Path.GetDirectoryName(target);
                Directory.CreateDirectory(directory);
                AnalyzerStorePaths.RejectReparsePoints(target);
                staging = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                AnalyzerStorePaths.RejectReparsePoints(staging);
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false, true).GetBytes(JsonConvert.SerializeObject(settings, Formatting.Indented));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                AnalyzerStorePaths.RejectReparsePoints(staging);
                AnalyzerStorePaths.RejectReparsePoints(target);
                if (File.Exists(target)) _replace(staging, target); else File.Move(staging, target);
            }
            catch (Exception error) when (IsAccessFailure(error) || error is JsonException)
            { throw new IOException("compliance_save_failed"); }
            finally
            {
                if (staging != null)
                {
                    // Never follow an unexpected parent link during cleanup.
                    try { AnalyzerStorePaths.RejectReparsePoints(staging); if (File.Exists(staging)) File.Delete(staging); }
                    catch (Exception error) when (IsAccessFailure(error)) { }
                }
            }
        }
        private string TargetPath()
        {
            var windows = _projectRoot?.Replace('/', '\\');
            if (windows != null && (windows.StartsWith(@"\\?\", StringComparison.Ordinal) || windows.StartsWith(@"\\.\", StringComparison.Ordinal)))
                throw new ArgumentException("compliance_path_invalid");
            var paths = new AnalyzerStorePaths(_projectRoot);
            return Path.Combine(paths.RootDirectory, "compliance.json");
        }
        private static bool IsAccessFailure(Exception error) => error is IOException || error is UnauthorizedAccessException ||
            error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException;
    }
}
