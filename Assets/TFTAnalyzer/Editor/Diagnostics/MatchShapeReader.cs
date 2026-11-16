using System;
using System.IO;
using System.Threading;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>Per-match shape facts held in memory until aggregated.</summary>
    internal sealed class MatchShapeSample
    {
    }

    /// <summary>Token-level reader that extracts only the diagnostic fields from one match payload.</summary>
    internal sealed class MatchShapeReader
    {
        public MatchShapeReader(MatchShapeDiagnosticInput input) { }

        public MatchShapeSample Read(Stream utf8Json, CancellationToken token) => throw new NotImplementedException();
    }
}
