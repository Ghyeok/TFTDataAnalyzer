using System;
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

    /// <summary>Accumulates per-match shapes into aggregate counters only.</summary>
    internal sealed class MatchShapeAccumulator
    {
        private readonly MatchShapeDiagnosticInput _input;
        private int _unreadable;

        public MatchShapeAccumulator(MatchShapeDiagnosticInput input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public void Add(MatchShapeSample sample) => throw new NotImplementedException();

        public void AddUnreadable() => _unreadable++;

        public JObject BuildReport(MatchShapeRunSummary run) => new JObject
        {
            ["status"] = run.Status,
            ["partial"] = run.Partial,
            ["stop"] = new JObject { ["stage"] = run.StopStage, ["reason"] = run.StopReason },
            ["runConsumed"] = run.RunConsumed,
            ["requests"] = new JObject
            {
                ["list"] = run.ListSends, ["detail"] = run.DetailSends,
                ["total"] = run.ListSends + run.DetailSends, ["limit"] = run.Limit
            },
            ["unreadableDetails"] = _unreadable,
            ["classification"] = new JObject { ["result"] = "undecidable" }
        };
    }
}
