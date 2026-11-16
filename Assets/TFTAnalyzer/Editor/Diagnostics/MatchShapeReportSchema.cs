using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>
    /// Allowlist schema for the aggregate report: exact key sets, enumerations, bounded integers and the signature
    /// alphabet. Anything else, including any free-form string, fails validation and blocks publication.
    /// </summary>
    internal static class MatchShapeReportSchema
    {
        private const int MaxCount = 100000;
        private static readonly string[] Reasons = { "unauthorized", "forbidden", "not_found", "rate_limited",
            "server_error", "http_error", "transport_error", "invalid_response", "budget_exhausted", "clock_stalled",
            "internal_error" };
        private static readonly HashSet<string> StopReasons = new HashSet<string>(
            new[] { "none", "cancelled" }.Concat(new[] { "list", "detail" }.SelectMany(stage => Reasons.Select(reason => stage + "_" + reason))),
            StringComparer.Ordinal);
        private static readonly Regex Queue = new Regex(
            @"\A(?:[1-9][0-9]{0,5}|out_of_range|type:(?:string|float|boolean|null|array|object|missing))\z", RegexOptions.CultureInvariant);
        private static readonly Regex FieldType = new Regex(
            @"\A(?:none|missing|array:(?:[0-9]|1[0-6]|17\+)(?:\+nonobject)?|(?:array|boolean|float|integer|missing|null|object|string)(?:\+(?:array|boolean|float|integer|missing|null|object|string))*)\z",
            RegexOptions.CultureInvariant);
        private static readonly Regex Release = new Regex(@"\A[0-9]{1,5}\.[0-9]{1,5}\z", RegexOptions.CultureInvariant);

        public static bool IsValid(JToken report)
        {
            try
            {
                Root(report);
                return true;
            }
            catch (SchemaViolation) { return false; }
        }

        private static void Root(JToken token)
        {
            var root = Object(token, "schema", "schemaVersion", "status", "partial", "stop", "runConsumed", "requests",
                "input", "unreadableDetails", "queues", "compositionDiversity", "classification");
            Constant(root["schema"], MatchShapeAccumulator.Schema);
            Integer(root["schemaVersion"], MatchShapeAccumulator.SchemaVersion, MatchShapeAccumulator.SchemaVersion);
            Enumeration(root["status"], "completed", "cancelled", "aborted");
            Boolean(root["partial"]);
            var stop = Object(root["stop"], "stage", "reason");
            Enumeration(stop["stage"], "none", "list", "detail");
            if (!StopReasons.Contains(String(stop["reason"]))) throw new SchemaViolation();
            Boolean(root["runConsumed"]);
            var requests = Object(root["requests"], "list", "detail", "total", "limit");
            var list = Integer(requests["list"], 0, DiagnosticRequestBudget.ListLimit);
            var detail = Integer(requests["detail"], 0, MatchShapeDiagnostic.DetailLimit);
            Integer(requests["total"], list + detail, list + detail);
            Integer(requests["limit"], DiagnosticRequestBudget.ListLimit + MatchShapeDiagnostic.DetailLimit,
                DiagnosticRequestBudget.ListLimit + MatchShapeDiagnostic.DetailLimit);
            var input = Object(root["input"], "targetQueue", "setNumber", "catalogRelease", "detailLimit");
            Integer(input["targetQueue"], 1, 999999);
            Integer(input["setNumber"], 1, 1000);
            Matches(input["catalogRelease"], Release);
            Integer(input["detailLimit"], MatchShapeDiagnostic.DetailLimit, MatchShapeDiagnostic.DetailLimit);
            Integer(root["unreadableDetails"], 0, MatchShapeDiagnostic.DetailLimit);
            var queues = Array(root["queues"], MatchShapeDiagnostic.DetailLimit);
            foreach (var queue in queues) QueueEntry(queue);
            Diversity(root["compositionDiversity"]);
            Classification(root["classification"]);
        }

        private static void QueueEntry(JToken token)
        {
            var queue = Object(token, "queue", "isTarget", "matches", "gameVersion", "dataVersion", "gameVersionRegex",
                "release", "setNumber", "augmentState", "unknownStaticIds", "requiredFieldRows");
            Matches(queue["queue"], Queue);
            Boolean(queue["isTarget"]);
            Integer(queue["matches"], 1, MatchShapeDiagnostic.DetailLimit);
            Version(queue["gameVersion"]);
            Version(queue["dataVersion"]);
            Counts(queue["gameVersionRegex"], "match", "noMatch");
            Counts(queue["release"], "match", "mismatch", "unextractable");
            Counts(queue["setNumber"], "match", "mismatch", "unavailable");
            Counts(queue["augmentState"], "allProvided", "noneProvided", "mixed", "noParticipants");
            var unknown = Object(queue["unknownStaticIds"], MatchShapeSample.StaticKinds);
            foreach (var kind in MatchShapeSample.StaticKinds) Counts(unknown[kind], "occurrences", "matches");
            foreach (var row in Array(queue["requiredFieldRows"], MatchShapeDiagnostic.DetailLimit))
            {
                var entry = Object(row, "fields", "count");
                var fields = Object(entry["fields"], MatchShapeSample.FieldNames);
                foreach (var name in MatchShapeSample.FieldNames) Matches(fields[name], FieldType);
                Integer(entry["count"], 1, MatchShapeDiagnostic.DetailLimit);
            }
        }

        private static void Version(JToken token)
        {
            var version = Object(token, "types", "signatures", "versionPrefix", "leadingGroups");
            Counts(version["types"], "string", "integer", "null", "other", "missing");
            foreach (var item in Array(version["signatures"], MatchShapeDiagnostic.DetailLimit))
            {
                var signature = Object(item, "type", "signature", "truncated", "count");
                Enumeration(signature["type"], "string", "integer", "null", "other", "missing");
                if (!VersionShapeSignature.IsValidSignature(String(signature["signature"]))) throw new SchemaViolation();
                Boolean(signature["truncated"]);
                Integer(signature["count"], 1, MatchShapeDiagnostic.DetailLimit);
            }
            Counts(version["versionPrefix"], "present", "absent");
            foreach (var item in Array(version["leadingGroups"], MatchShapeDiagnostic.DetailLimit))
            {
                var group = Object(item, "value", "count");
                if (!VersionShapeSignature.IsValidLeadingGroups(String(group["value"]))) throw new SchemaViolation();
                Integer(group["count"], 1, MatchShapeDiagnostic.DetailLimit);
            }
        }

        private static void Diversity(JToken token)
        {
            var diversity = Object(token, "scope", "basis", "interpretation", "totalBoards", "excludedBoards",
                "uniqueCompositions", "singletonCompositions", "top5Frequencies", "frequencyOfFrequencies");
            Constant(diversity["scope"], "target_queue");
            Constant(diversity["basis"], "fingerprint_v2_raw_ids");
            Constant(diversity["interpretation"], "lower_bound_reference_single_seed_correlated_sample");
            foreach (var name in new[] { "totalBoards", "excludedBoards", "uniqueCompositions", "singletonCompositions" })
                Integer(diversity[name], 0, MaxCount);
            foreach (var value in Array(diversity["top5Frequencies"], 5)) Integer(value, 1, MaxCount);
            foreach (var item in Array(diversity["frequencyOfFrequencies"], MaxCount))
            {
                var entry = Object(item, "frequency", "compositions");
                Integer(entry["frequency"], 1, MaxCount);
                Integer(entry["compositions"], 1, MaxCount);
            }
        }

        private static void Classification(JToken token)
        {
            var classification = Object(token, "result", "reason", "targetSampleCount", "threshold",
                "gameVersionSignatureCount", "dataVersionSignatureCount", "unobservedShapeUpper95");
            Enumeration(classification["result"], "undecidable", "c_or_d", "shape_conditions_met_pending_external_evidence");
            Enumeration(classification["reason"], "partial_run", "insufficient_sample", "unreadable_details",
                "multiple_game_version_signatures",
                "release_unextractable", "release_mismatch", "none");
            Integer(classification["targetSampleCount"], 0, MatchShapeDiagnostic.DetailLimit);
            Integer(classification["threshold"], MatchShapeAccumulator.DecisionThreshold, MatchShapeAccumulator.DecisionThreshold);
            Integer(classification["gameVersionSignatureCount"], 0, MatchShapeDiagnostic.DetailLimit);
            Integer(classification["dataVersionSignatureCount"], 0, MatchShapeDiagnostic.DetailLimit);
            var upper = classification["unobservedShapeUpper95"];
            if (upper.Type == JTokenType.Null) return;
            if (upper.Type != JTokenType.Float && upper.Type != JTokenType.Integer) throw new SchemaViolation();
            var value = upper.Value<double>();
            if (double.IsNaN(value) || value <= 0 || value > 1) throw new SchemaViolation();
        }

        private static JObject Object(JToken token, params string[] keys)
        {
            if (!(token is JObject value)) throw new SchemaViolation();
            var names = value.Properties().Select(property => property.Name).ToArray();
            if (names.Length != keys.Length || !keys.All(key => names.Contains(key, StringComparer.Ordinal)))
                throw new SchemaViolation();
            return value;
        }

        private static JArray Array(JToken token, int maximum)
        {
            if (!(token is JArray value) || value.Count > maximum) throw new SchemaViolation();
            return value;
        }

        private static void Counts(JToken token, params string[] keys)
        {
            var value = Object(token, keys);
            foreach (var key in keys) Integer(value[key], 0, MaxCount);
        }

        private static int Integer(JToken token, int minimum, int maximum)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new SchemaViolation();
            var value = token.Value<long>();
            if (value < minimum || value > maximum) throw new SchemaViolation();
            return (int)value;
        }

        private static void Boolean(JToken token)
        {
            if (token == null || token.Type != JTokenType.Boolean) throw new SchemaViolation();
        }

        private static string String(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new SchemaViolation();
            return (string)token;
        }

        private static void Constant(JToken token, string expected)
        {
            if (!string.Equals(String(token), expected, StringComparison.Ordinal)) throw new SchemaViolation();
        }

        private static void Enumeration(JToken token, params string[] allowed)
        {
            if (!allowed.Contains(String(token), StringComparer.Ordinal)) throw new SchemaViolation();
        }

        private static void Matches(JToken token, Regex pattern)
        {
            if (!pattern.IsMatch(String(token))) throw new SchemaViolation();
        }

        private sealed class SchemaViolation : Exception { }
    }
}
