using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>Static facts copied from one verified local catalog.</summary>
    public sealed class MatchShapeCatalogSelection
    {
        public int SetNumber { get; }
        public string Release { get; }
        public IReadOnlyCollection<string> UnitIds { get; }
        public IReadOnlyCollection<string> ItemIds { get; }
        public IReadOnlyCollection<string> TraitIds { get; }
        public IReadOnlyCollection<string> AugmentIds { get; }

        public MatchShapeCatalogSelection(int setNumber, string release, IReadOnlyCollection<string> unitIds,
            IReadOnlyCollection<string> itemIds, IReadOnlyCollection<string> traitIds, IReadOnlyCollection<string> augmentIds)
        {
            SetNumber = setNumber; Release = release; UnitIds = unitIds; ItemIds = itemIds; TraitIds = traitIds; AugmentIds = augmentIds;
        }
    }

    /// <summary>Loads a verified local catalog read-only; returns null and a fixed code when none is usable.</summary>
    public interface IMatchShapeCatalogSource
    {
        MatchShapeCatalogSelection Load(out string failureCode);
    }

    /// <summary>
    /// Testable body of the Editor menu item: builds the immutable inputs from a verified catalog, stopping before any
    /// confirmation or network access when none is usable, then runs one diagnostic.
    /// </summary>
    public sealed class MatchShapeDiagnosticEntryPoint
    {
        public const string Platform = "KR";
        public const int TargetQueueId = 1100;
        private readonly IMatchShapeCatalogSource _catalogs;
        private readonly Func<MatchShapeDiagnostic> _diagnostic;

        public MatchShapeDiagnosticEntryPoint(IMatchShapeCatalogSource catalogs, Func<MatchShapeDiagnostic> diagnostic)
        {
            _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            _diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
        }

        public async Task<MatchShapeRunResult> RunAsync(CancellationToken token)
        {
            MatchShapeDiagnosticInput input;
            try
            {
                var selection = _catalogs.Load(out var code);
                if (selection == null) return MatchShapeRunResult.PreconditionFailed(code ?? "catalog_unavailable");
                input = new MatchShapeDiagnosticInput(RiotRouting.ForPlatform(Platform), TargetQueueId, selection.SetNumber,
                    selection.Release, selection.UnitIds, selection.ItemIds, selection.TraitIds, selection.AugmentIds);
            }
            catch { return MatchShapeRunResult.PreconditionFailed("catalog_unavailable"); }
            return await _diagnostic().RunAsync(input, token);
        }
    }
}
