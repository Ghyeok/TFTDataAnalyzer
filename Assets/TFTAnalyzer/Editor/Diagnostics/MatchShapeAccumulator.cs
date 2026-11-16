using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>Run-level facts that the report records next to the aggregates.</summary>
    internal sealed class MatchShapeRunSummary
    {
        public string Status { get; }
        public bool Partial { get; }
        public string StopStage { get; }
        public string StopReason { get; }
        public int ListSends { get; }
        public int DetailSends { get; }
        public int Limit { get; }
        public bool RunConsumed { get; }

        public MatchShapeRunSummary(string status, bool partial, string stopStage, string stopReason,
            int listSends, int detailSends, int limit, bool runConsumed)
        {
            Status = status; Partial = partial; StopStage = stopStage; StopReason = stopReason;
            ListSends = listSends; DetailSends = detailSends; Limit = limit; RunConsumed = runConsumed;
        }
    }

    /// <summary>
    /// Accumulates per-match shapes into aggregate counters only: no per-match rows, order or time.
    /// Composition digests stay in memory and only their frequency statistics reach the report.
    /// </summary>
    internal sealed class MatchShapeAccumulator
    {
        public const string Schema = "tft-analyzer.match-shape-diagnostic";
        public const int SchemaVersion = 1;
        public const int DecisionThreshold = 10;
        private readonly MatchShapeDiagnosticInput _input;
        private readonly SortedDictionary<string, QueueCounts> _queues = new SortedDictionary<string, QueueCounts>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _compositions = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _unreadable, _boards, _excludedBoards;

        public MatchShapeAccumulator(MatchShapeDiagnosticInput input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        private string TargetQueue => _input.TargetQueueId.ToString(CultureInfo.InvariantCulture);

        /// <summary>Adds one match; diversity is accumulated for the target queue only.</summary>
        public void Add(MatchShapeSample sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));
            if (!_queues.TryGetValue(sample.QueueKey, out var queue)) _queues[sample.QueueKey] = queue = new QueueCounts();
            queue.Add(sample);
            if (sample.QueueKey != TargetQueue) return;
            _boards += sample.Boards;
            _excludedBoards += sample.ExcludedBoards;
            foreach (var key in sample.CompositionKeys)
                _compositions[key] = _compositions.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        public void AddUnreadable() => _unreadable++;

        /// <summary>Builds the fixed-key aggregate report and its pre-registered classification.</summary>
        public JObject BuildReport(MatchShapeRunSummary run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            var queues = new JArray(_queues.Select(pair => pair.Value.ToJson(pair.Key, pair.Key == TargetQueue)));
            return new JObject
            {
                ["schema"] = Schema,
                ["schemaVersion"] = SchemaVersion,
                ["status"] = run.Status,
                ["partial"] = run.Partial,
                ["stop"] = new JObject { ["stage"] = run.StopStage, ["reason"] = run.StopReason },
                ["runConsumed"] = run.RunConsumed,
                ["requests"] = new JObject
                {
                    ["list"] = run.ListSends, ["detail"] = run.DetailSends,
                    ["total"] = run.ListSends + run.DetailSends, ["limit"] = run.Limit
                },
                ["input"] = new JObject
                {
                    ["targetQueue"] = _input.TargetQueueId, ["setNumber"] = _input.SetNumber,
                    ["catalogRelease"] = _input.CatalogRelease, ["detailLimit"] = MatchShapeDiagnostic.DetailLimit
                },
                ["unreadableDetails"] = _unreadable,
                ["queues"] = queues,
                ["compositionDiversity"] = Diversity(),
                ["classification"] = Classify(run)
            };
        }

        private JObject Diversity()
        {
            var frequencies = _compositions.Values.OrderByDescending(value => value).ToArray();
            return new JObject
            {
                ["scope"] = "target_queue",
                ["basis"] = "fingerprint_v2_raw_ids",
                ["interpretation"] = "lower_bound_reference_single_seed_correlated_sample",
                ["totalBoards"] = _boards,
                ["excludedBoards"] = _excludedBoards,
                ["uniqueCompositions"] = frequencies.Length,
                ["singletonCompositions"] = frequencies.Count(value => value == 1),
                ["top5Frequencies"] = new JArray(frequencies.Take(5)),
                ["frequencyOfFrequencies"] = new JArray(frequencies.GroupBy(value => value).OrderBy(group => group.Key)
                    .Select(group => new JObject { ["frequency"] = group.Key, ["compositions"] = group.Count() }))
            };
        }

        /// <summary>Applies the approved ordered rules: partial/n&lt;k, multiple game_version signatures, release, otherwise shape met.</summary>
        private JObject Classify(MatchShapeRunSummary run)
        {
            _queues.TryGetValue(TargetQueue, out var target);
            var n = target?.Matches ?? 0;
            var gameSignatures = target?.GameVersion.SignatureCount ?? 0;
            var dataSignatures = target?.DataVersion.SignatureCount ?? 0;
            string result, reason;
            if (run.Partial) { result = "undecidable"; reason = "partial_run"; }
            else if (n < DecisionThreshold) { result = "undecidable"; reason = "insufficient_sample"; }
            else if (gameSignatures >= 2) { result = "undecidable"; reason = "multiple_game_version_signatures"; }
            else if (target.ReleaseUnextractable > 0) { result = "c_or_d"; reason = "release_unextractable"; }
            else if (target.ReleaseMismatch > 0) { result = "c_or_d"; reason = "release_mismatch"; }
            else { result = "shape_conditions_met_pending_external_evidence"; reason = "none"; }
            return new JObject
            {
                ["result"] = result,
                ["reason"] = reason,
                ["targetSampleCount"] = n,
                ["threshold"] = DecisionThreshold,
                ["gameVersionSignatureCount"] = gameSignatures,
                ["dataVersionSignatureCount"] = dataSignatures,
                ["unobservedShapeUpper95"] = n > 0 ? new JValue(Math.Round(Math.Min(1.0, 3.0 / n), 4)) : JValue.CreateNull()
            };
        }

        private sealed class QueueCounts
        {
            public int Matches;
            public readonly VersionCounts GameVersion = new VersionCounts();
            public readonly VersionCounts DataVersion = new VersionCounts();
            public int RegexMatch, RegexNoMatch;
            public int ReleaseMatch, ReleaseMismatch, ReleaseUnextractable;
            public int SetMatch, SetMismatch, SetUnavailable;
            public int AllProvided, NoneProvided, Mixed, NoParticipants;
            public readonly int[] UnknownOccurrences = new int[MatchShapeSample.StaticKinds.Length];
            public readonly int[] UnknownMatches = new int[MatchShapeSample.StaticKinds.Length];
            private readonly Dictionary<string, int> _rows = new Dictionary<string, int>(StringComparer.Ordinal);

            public void Add(MatchShapeSample sample)
            {
                Matches++;
                GameVersion.Add(sample.GameVersion);
                DataVersion.Add(sample.DataVersion);
                if (sample.GameVersionRegexMatch) RegexMatch++; else RegexNoMatch++;
                switch (sample.Release)
                {
                    case "match": ReleaseMatch++; break;
                    case "mismatch": ReleaseMismatch++; break;
                    default: ReleaseUnextractable++; break;
                }
                switch (sample.SetNumber)
                {
                    case "match": SetMatch++; break;
                    case "mismatch": SetMismatch++; break;
                    default: SetUnavailable++; break;
                }
                switch (sample.AugmentState)
                {
                    case "allProvided": AllProvided++; break;
                    case "noneProvided": NoneProvided++; break;
                    case "mixed": Mixed++; break;
                    default: NoParticipants++; break;
                }
                for (var i = 0; i < UnknownOccurrences.Length; i++)
                {
                    UnknownOccurrences[i] += sample.UnknownOccurrences[i];
                    if (sample.UnknownOccurrences[i] > 0) UnknownMatches[i]++;
                }
                var row = string.Join("\n", sample.FieldTypes);
                _rows[row] = _rows.TryGetValue(row, out var count) ? count + 1 : 1;
            }

            public JObject ToJson(string queue, bool isTarget)
            {
                var unknown = new JObject();
                for (var i = 0; i < MatchShapeSample.StaticKinds.Length; i++)
                    unknown[MatchShapeSample.StaticKinds[i]] = new JObject
                        { ["occurrences"] = UnknownOccurrences[i], ["matches"] = UnknownMatches[i] };
                return new JObject
                {
                    ["queue"] = queue,
                    ["isTarget"] = isTarget,
                    ["matches"] = Matches,
                    ["gameVersion"] = GameVersion.ToJson(),
                    ["dataVersion"] = DataVersion.ToJson(),
                    ["gameVersionRegex"] = new JObject { ["match"] = RegexMatch, ["noMatch"] = RegexNoMatch },
                    ["release"] = new JObject
                        { ["match"] = ReleaseMatch, ["mismatch"] = ReleaseMismatch, ["unextractable"] = ReleaseUnextractable },
                    ["setNumber"] = new JObject
                        { ["match"] = SetMatch, ["mismatch"] = SetMismatch, ["unavailable"] = SetUnavailable },
                    ["augmentState"] = new JObject
                    {
                        ["allProvided"] = AllProvided, ["noneProvided"] = NoneProvided,
                        ["mixed"] = Mixed, ["noParticipants"] = NoParticipants
                    },
                    ["unknownStaticIds"] = unknown,
                    ["requiredFieldRows"] = new JArray(_rows.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
                    {
                        var types = pair.Key.Split('\n');
                        var fields = new JObject();
                        for (var i = 0; i < MatchShapeSample.FieldCount; i++) fields[MatchShapeSample.FieldNames[i]] = types[i];
                        return new JObject { ["fields"] = fields, ["count"] = pair.Value };
                    }))
                };
            }
        }

        private sealed class VersionCounts
        {
            private readonly SortedDictionary<string, int> _types = new SortedDictionary<string, int>(StringComparer.Ordinal);
            private readonly SortedDictionary<string, int> _signatures = new SortedDictionary<string, int>(StringComparer.Ordinal);
            private readonly SortedDictionary<string, int> _groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
            private int _prefix, _noPrefix;

            /// <summary>Distinct type-and-signature shapes, independent of the truncation flag.</summary>
            public int SignatureCount => _signatures.Keys.Select(key => key.Substring(0, key.LastIndexOf('\n'))).Distinct().Count();

            public void Add(VersionShape shape)
            {
                Increment(_types, shape.Type);
                Increment(_signatures, shape.Type + "\n" + shape.Signature + "\n" + (shape.Truncated ? "1" : "0"));
                Increment(_groups, shape.LeadingGroups);
                if (shape.HasVersionPrefix) _prefix++; else _noPrefix++;
            }

            public JObject ToJson()
            {
                var types = new JObject();
                foreach (var type in new[] { "string", "integer", "null", "other", "missing" })
                    types[type] = _types.TryGetValue(type, out var count) ? count : 0;
                return new JObject
                {
                    ["types"] = types,
                    ["signatures"] = new JArray(_signatures.Select(pair =>
                    {
                        var parts = pair.Key.Split('\n');
                        return new JObject
                        {
                            ["type"] = parts[0], ["signature"] = parts[1], ["truncated"] = parts[2] == "1", ["count"] = pair.Value
                        };
                    })),
                    ["versionPrefix"] = new JObject { ["present"] = _prefix, ["absent"] = _noPrefix },
                    ["leadingGroups"] = new JArray(_groups.Select(pair => new JObject { ["value"] = pair.Key, ["count"] = pair.Value }))
                };
            }

            private static void Increment(IDictionary<string, int> counts, string key)
                => counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        }
    }
}
