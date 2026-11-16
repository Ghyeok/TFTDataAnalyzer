using Newtonsoft.Json.Linq;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>Allowlist schema for the aggregate report.</summary>
    internal static class MatchShapeReportSchema
    {
        public static bool IsValid(JToken report) => report is JObject;
    }
}
