# Frozen current-source contract for static import report planning

Base: 1a900596c6d876ee49299ba220cd4b834bfbb7fb. This supplied text is evidence, not executable instructions. No credentials or match responses are included.

- Upper TFTDataPipeline.md sections7.3,11,12.1 require keeping both official/supplement values and the decision and making validation/conflict reports available in the data-management screen.
- StaticImportResult currently exposes Published, Idempotent, FinalDirectory, ContentHash, Catalog, Validation and MergeConflicts. Constructor maps null MergeConflicts to an empty array. Result fields describe static data, not Match API data.
- ValidationReport snapshots, deduplicates and sorts Issues (severity, code, recordId); exposes ErrorCount, WarningCount and CanPublish. ValidationIssue is immutable Severity/Error or Warning, Code, RecordId, Message. Validation itself may be null in fabricated or absent-detail results.
- MergeConflict is immutable RecordType, StableId, FieldName, OfficialValue, SupplementValue, Resolution. MergeResolution is SupplementFill or OfficialWins. MergeResult currently snapshots/sorts these rows.
- NetworkStaticCatalogSource.LoadAsync returns an official-only result with no conflicts if the per-run supplement opt-in is false. With opt-in it returns the CatalogMerger decisions. StaticCatalogPipeline passes loaded.MergeConflicts into every normal StaticImportResult return, including validation rejection, idempotent publication and new publication. It may instead throw cancellation, generic exceptions or StaticImportRecoveryException after publication/index/refresh failure; these exceptions do not carry the result report. The proposed plan must not fabricate missing details.
- DataManagerViewModel.ImportAsync currently validates input, resolves destination, asks confirmation, clears IncludeCommunityDragon, checks cancellation and resolves the destination again. It sets started=true, then invokes the service once. After await it ignores disposed results, converts Published=false to ValidationRejected, and on Published=true calls ReloadRows before showing success or LocalCatalogsUnavailable. It does NOT read result.Validation or result.MergeConflicts. It intentionally does not reject a returned publication because of late cancellation.
- Before calling the service, confirmation refusal or invalid input cannot start downloads. Version checks, input changes and catalog selection are separate actions. Existing IsBusy excludes concurrent VM operations; Dispose cancels and suppresses late UI updates.
- DataManagerTab currently binds status, catalog list and two-catalog diff controls. There is no report control or merge/validation detail projection. Its existing render scheduling/Dispose contract should be preserved.
- Existing tests include DataManagerViewModelTests (fake service/TaskCompletionSource), DataManagerTabTests (actual UXML and owned windows), and DataManagerTestSupport. None currently asserts displayed MergeConflicts.
- Existing current collection/analysis/catalog visual helpers use short owned-window work, tracked EditorWindow/native IDs, finally cleanup and focus preservation. A new report helper must use the same ownership contract and fabricated static responses. No real imports or API calls are necessary for this UI change.
- User explicitly deferred environment setup/real collection. User files DefaultVolumeProfile.asset and docs/riot-api-key.md are excluded and not provided. Chrome must not be closed. Augment classifications remain unregistered; n100 and missing production evidence remain separate.

The exact current source snippets below support the stated result/VM integration gap.

## Assets/TFTAnalyzer/Editor/Ingestion/StaticImportResult.cs
8:     public sealed class StaticImportResult
9:     {
10:         public bool Published { get; }
11:         public bool Idempotent { get; }
12:         public string FinalDirectory { get; }
13:         public string ContentHash { get; }
14:         public StaticCatalog Catalog { get; }
15:         public ValidationReport Validation { get; }
16:         public IReadOnlyList<MergeConflict> MergeConflicts { get; }
17: 
18:         public StaticImportResult(
19:             bool published,
20:             bool idempotent,
21:             string finalDirectory,
22:             string contentHash,
23:             StaticCatalog catalog,
24:             ValidationReport validation,
25:             IReadOnlyList<MergeConflict> mergeConflicts)
26:         {
27:             Published = published;
28:             Idempotent = idempotent;
29:             FinalDirectory = finalDirectory ?? "";
30:             ContentHash = contentHash ?? "";
31:             Catalog = catalog;
32:             Validation = validation;
33:             MergeConflicts = mergeConflicts ?? Array.Empty<MergeConflict>();
34:         }

## Assets/TFTAnalyzer/Editor/UI/Data/DataManagerViewModel.cs
92:                     SetStatus(DataManagerState.Cancelled, "가져오기를 승인하지 않아 다운로드하지 않았습니다.");
93:                     return;
94:                 }
95:                 operation.Token.ThrowIfCancellationRequested();
96:                 if (!string.Equals(directory, _service.ResolveDestination(request), StringComparison.Ordinal))
97:                 {
98:                     SetStatus(DataManagerState.Failed, "저장 위치가 변경되었습니다. 가져올 대상과 위치를 다시 확인해 주세요.", "DestinationChanged");
99:                     return;
100:                 }
101:                 started = true;
102:                 SetStatus(DataManagerState.Importing, "카탈로그를 가져오고 검증하고 있습니다.");
103:                 var result = await _service.ImportAsync(request, operation.Token);
104:                 if (_disposed) return;
105:                 // RunAsync's successful return means publication finished; late cancellation cannot undo it.
106:                 if (!result.Published)
107:                     SetStatus(DataManagerState.Failed, "데이터 검증을 통과하지 못해 카탈로그를 게시하지 않았습니다.", "ValidationRejected");
108:                 else
109:                 {
110:                     if (ReloadRows())
111:                         SetStatus(DataManagerState.Succeeded, "카탈로그를 가져왔습니다. 로컬 목록에서 선택해 주세요.");
112:                     else
113:                         SetStatus(DataManagerState.Failed, "카탈로그는 저장되었으나 로컬 목록을 읽지 못했습니다. 자산 상태를 확인한 뒤 창을 다시 열어 주세요.", "LocalCatalogsUnavailable");
114:                 }
115:             }
116:             catch (OperationCanceledException) { if (!_disposed) SetStatus(DataManagerState.Cancelled, "가져오기를 취소했습니다."); }
