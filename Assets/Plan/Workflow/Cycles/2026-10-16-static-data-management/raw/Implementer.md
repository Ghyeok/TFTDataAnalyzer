# Phase 3 Task 2 implementer report

## Stage 1 — tests only

Requested model/reasoning: `gpt-6.1-sol / high`. Actual runtime model/reasoning cannot be independently verified from this agent. No usage or elapsed-time estimate is recorded.

Branch inspected: `codex/static-data-management-tab`; assigned base `cd1b67f`.

Read task brief, root AGENTS.md, relevant TDD (including writing-good-tests), UI Toolkit and Karpathy guidance. Existing writer, serializer, validator, identity, legacy resolver, pipeline and differ are retained as the semantic authorities.

Added test files:

- `Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerViewModelTests.cs`: explicit version checks, captured consent request/destination, opt-in reset, invalid input, validation refusal, sanitized errors and three recovery states, busy guards, cancellation settlement, late published success, disposal, immutable list snapshot, full-identity selection, directional diff and timestamp-only diff.
- `Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerTestSupport.cs`: shared fixture and narrowly scoped service fake. Unit source provenance stays official while the supplemented manifest records both sources, so the field-diff expectation is independently one name change.
- `Assets/TFTAnalyzer/Tests/EditMode/Persistence/PublishedCatalogLoaderTests.cs`: real writer-created canonical and schema-1 legacy snapshots; four version/profile identities; actual standalone retrieval UTC and warning count versus Epoch/zero catalog metadata; corrupt bytes/index hash/path/provenance/catalog validation/metadata; healthy-row survival; verified legacy without migration; unprofiled canonical refusal; file snapshot preservation.
- `Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerServiceTests.cs`: real service plus fake HTTP transport asserts exactly the versions endpoint, no implicit calls and no files created.
- `Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerTabTests.cs`: real UXML and panel events cover confirmation rejection/approval, visible opt-in reset, busy/cancel controls, selection, diff field rendering, disposed event/scheduled update removal and rebinding the same VM, and fewer-than-two-catalog empty state.

No production stub or implementation was written. No Unity operation, network request, stage, commit, push, Hermes interaction, workflow-document edit or generated-catalog write was performed by this agent. Temp loader fixtures use `CatalogAssetWriter` and in-memory `CatalogIndex`, never `CatalogIndexWriter`.

## Minimal proposed implementation contracts

Namespace `TFTAnalyzer.Editor.UI.Data`:

```csharp
public interface IDataManagerService
{
    IReadOnlyList<PatchRowViewModel> LoadLocalCatalogs();
    string ResolveDestination(StaticImportRequest request);
    Task<IReadOnlyList<string>> FetchVersionsAsync(CancellationToken token);
    Task<StaticImportResult> ImportAsync(StaticImportRequest request, CancellationToken token);
}
```

Reuse existing `StaticImportRequest`, `StaticImportResult`, `CatalogIdentity`, `PatchKey`, `StaticCatalog`, and `CatalogDiff`; no new import or catalog mapping DTO.

`PatchRowViewModel` immutable constructor: `(CatalogIndexEntry entry, CatalogIdentity identity, string directory, StaticCatalog catalog, DateTime retrievedUtc, int validationErrorCount, int validationWarningCount, string errorCode)`. Existing index entry is preserved for display even if identity resolution fails. Exposes `Entry`, `Identity`, `Directory`, `Catalog`, `RetrievedUtc`, validation counts, `ErrorCode`, derived `IsSelectable`, `SourceBadge`, `ContentHashPrefix`. Failed rows have no usable catalog; null identity is permitted for unresolvable entries.

`DataManagerViewModel(IDataManagerService service)` implements `IDisposable`, exposes `Changed`, immutable `Rows`/`Versions`, `InputPatch`, `IncludeCommunityDragon`, `State`, `IsBusy`, `ErrorCode`, `StatusText`, `ActiveIdentity`, `DiffBeforeIdentity`, `DiffAfterIdentity`, and existing `CatalogDiff Diff`. Commands: `bool TrySetInput(PatchKey, bool)`, `Task CheckForUpdatesAsync()`, `Task ImportAsync(Func<string,bool>)`, `void CancelImport()`, `bool SelectActiveCatalog(CatalogIdentity)`, `bool Compare(CatalogIdentity before, CatalogIdentity after)`. Busy/disposed commands decline without acquiring a new operation; import approval captures existing immutable input. Invalid-input checking is repeated at import boundary even if `TrySetInput` accepts an immutable patch containing unsafe path segments.

State enum: Idle, Checking, AwaitingApproval, Importing, Succeeded, Failed, Cancelled. Error codes are fixed: `InvalidInput`, `ValidationRejected`, `ImportFailed`, and recovery enum-name codes; version/local safe codes can follow the same pattern without exception text.

`DataManagerService(StaticCatalogPaths paths, IHttpTransport transport, Func<CatalogIndex> indexProvider)` constructs existing adapters/pipeline for production; optional existing pipeline injection may be added only if needed, not a new pipeline abstraction. Version checks call existing `DataDragonAdapter.FetchVersionsAsync`.

Namespace `TFTAnalyzer.Editor.Persistence`: `PublishedCatalogLoader(StaticCatalogPaths paths).Load(CatalogIndex index)` returns immutable rows; existing serializer maps all catalog content; metadata parser is restricted to standalone UTC/count display fields. All identity, directory, content hash, provenance and validator checks precede selectable state.

`DataManagerTab(VisualElement tab, DataManagerViewModel vm, Func<string,bool> confirm)` implements `IDisposable`; window owns VM lifetime and disposes binding before removing its tree. UI names: `set-id`/`set-number`/`tft-patch`/`source-version`/`communitydragon-toggle`; `check-updates-button`/`import-button`/`cancel-button`; `catalog-list` (ListView), `active-catalog`/`data-status` (Label), `diff-before`/`diff-after` (DropdownField), `compare-button`, `catalog-diff`, `diff-empty-state` (Label). Explanatory labels remain Korean. The modal is replaced only at the injected confirmation delegate boundary in tests; no native dialog interaction occurs.

## Verification and next step

Root explicitly approved missing-type compilation RED, matching the prior Task 1 sequence. The new tests intentionally reference absent production types. This agent has not executed Unity, so no actual RED/GREEN result is claimed. Root must refresh/compile and record the actual missing-type RED, then send an implementation follow-up. Static `git diff --check` reported no whitespace errors (existing CRLF conversion notices only); test files were reviewed against current domain constructor and endpoint signatures.

Remaining runtime checks owned by root: actual RED, implementation follow-up, focused/full EditMode GREEN, actual window/menu/rebuild/close checks and screenshot/layout at 500×300, console safety, and original Volume/generated byte hashes. Binding tests exercise rebinding the same VM; the actual `TftDataAnalyzerWindow.CreateGUI` retention/OnDisable integration also requires root's window verification.

## Stage 2 — implementation after root's actual RED

Root reported actual Unity compilation RED in `Library/TftAnalyzerValidation/data-manager-red-compile.json`: completed, failed=true, eleven CS0234/CS0246 errors for the deliberately absent Data namespace and types. This agent read that artifact before returning implementation. No Unity command has been executed by this agent.

Added production files:

- `Assets/TFTAnalyzer/Editor/Persistence/PublishedCatalogLoader.cs`: read-only row-isolated loading; existing identity/legacy resolver, published hash/provenance checker, serializer and validator; small standalone timestamp/count parsing uses case-insensitive metadata keys. No catalog DTO duplication, migration or rewrite.
- `Assets/TFTAnalyzer/Editor/UI/Data/PatchRowViewModel.cs`: immutable data row and derived source/validation/hash/identity display.
- `Assets/TFTAnalyzer/Editor/UI/Data/IDataManagerService.cs`: the four proposed small boundary methods.
- `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerService.cs`: existing adapter/pipeline assembly; no HTTP during construction/local read. Existing `DataDragonAdapter.FetchVersionsAsync` alone handles explicit version checks.
- `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerViewModel.cs`: memory-only selection, operation-owned CTS, approval of captured immutable request and resolved directory, busy/dispose guards, safe fixed error codes/guidance, reset opt-in, existing directional differ. Late cancellation cannot convert a published result into cancellation. Checking completion also resets supplement opt-in, consistent with the reset-on-terminal-operation requirement.
- `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerTab.cs`: disposable named-control binding, real list selection/diff, coalesced EditorApplication.delayCall rendering, callback removal before tree disposal. Import inputs remain explicit and version responses never infer a TFT patch or set.

Changed existing product files only to connect Task 2: window `.cs`, complete `.uxml` and `.uss`. Window lazily creates one VM per open lifetime, retains it through repeated CreateGUI and missing-asset rebuilds, disposes old binding before root tree removal, and clears/disposes VM on OnDisable so later activation creates a fresh one. Four tabs, menu, notices and failure view remain. The data tab now has its own vertical ScrollView, bounded catalog ListView and wrapping row metadata/path while navigation/footer remain outside the scroll area.

One test assertion was strengthened during self-review: rejected confirmation must finish Cancelled with no error code, ensuring a confirmation assertion swallowed by safe exception handling cannot masquerade as a successful rejection test. No existing shell test was changed; if Unity's builtin control labels fail the original blanket label-wrapping assertion, root should distinguish user-facing wrapping text from builtin editor input labels using actual runtime evidence.

Self-review covered cancellation settlement, disposal suppressing late notifications, approved request capture and profile reset, full identity/profile disambiguation, loader path validation beyond ResolveIdentity, no sensitive exception message/log emission, and no new catalog mappings or importer-window edits. Read-only inspection of the existing legacy manifest confirmed mixed camelCase/PascalCase compatibility; its timestamps/counts are read from standalone metadata.

Static checks: complete UXML parsed as XML; tracked and newly created production C# files passed whitespace checks (CRLF conversion notices only). These are not Unity parse/compile/test results. Actual focused/full EditMode and window/layout verification remain owned by root. No additional agent, Hermes, network, Git mutation, workflow doc, Volume or generated-data edit was performed by this agent.

## Stage 3 — first focused-run failure investigation

Root reported actual compilation GREEN (zero errors), then `data-manager-focused.json`: 21 DataManager tests, 17 passed and 4 failed, all in DataManagerTabTests. This agent read the actual messages/stack traces before changing anything and applied systematic-debugging guidance.

Two distinct causes were identified:

1. The test fixture did not reset `_approve` or `_confirmations` in SetUp. The alphabetically earlier Busy test set approval true and incremented confirmation once; later RealFields expected a fresh zero count/default refusal but observed two confirmations. This is test isolation failure, not evidence of duplicate product callbacks. SetUp now resets both fields.
2. The tests assumed `yield return null` (or two frames) guaranteed `EditorApplication.delayCall` had run. Product rendering intentionally coalesces on delayCall, a different editor event boundary. Actual evidence: ListSelection asserted the changed VM active identity successfully before seeing an unchanged active label; DisposedBinding observed the completed task/Idle VM status while the new label still said Checking. Busy likewise inspected enabled controls before the queued render. These tests must await the actual boundary, not an arbitrary number of frames.

Only `DataManagerTabTests.cs` was changed: a test-local delayCall barrier is registered after the binding's queued Flush and awaited with a bounded timeout; cancellation/completion first observes `IsBusy == false` through VM Changed, then waits for the render barrier. Both helpers unregister their callbacks in finally. Existing assertions and real field/button/list event paths remain, with one immediate Importing-state assertion separating model and render failures. No production flush hook, rendering change, weakened expectation or arbitrary extra frame count was added.

No Unity rerun was performed by this agent. Root must compile and rerun the focused tests to confirm these hypotheses. At handoff, the test file is frozen again; production files remain unchanged from Stage 2.

## Stage 4 — full-run scheduling diagnosis and post-publication reload RED test

Read the specified actual full-run artifact `data-manager-full.json`: 408 total, 405 passed, three failures. All three failed in UnitySetUp's delayCall barrier, before any test body; the final two Tab tests passed. This distinguishes the failure from a VM state/selection/control assertion. The binding constructor calls Render synchronously and queues no initial model change, so waiting for delayCall in SetUp (and after the empty-state synchronous Rebuild) imposed an unrelated idle-editor event requirement.

Local package source inspection confirmed EditMode TestJobRunner advances test iterators from EditorApplication.update; EnumeratorHelper advances nested test coroutines but does not promise an inspector/delayCall tick. The test host did not explicitly request a repaint after model mutations. Exact native timing/host focus cannot be proven from the JSON alone; no timeout increase or repeated speculative rerun was used.

Minimal test-only scheduling changes: SetUp and the synchronous empty-state rebuild now assert after constructor rendering with no delayCall wait. After actual model changes, the existing barrier still proves delayCall ran, and now requests one meaningful host Repaint plus QueuePlayerLoopUpdate so the editor has pending UI work. Five-second bound and finally-unsubscribe remain unchanged. This agent has not executed the rerun; root must verify real behavior and stop/reassess if the event barrier is still not serviced.

Separately, source inspection confirms a production defect: ImportAsync calls ReloadRows, which may catch a list-loading failure and set Failed/LocalCatalogsUnavailable, then unconditionally overwrites it with Succeeded and an empty error code. Added `PublishedCatalogWithUnreadableLocalListKeepsRecoveryGuidanceInsteadOfNormalSuccess` before changing production: initial loading succeeds, fake service throws only on the post-publication reload, and the test expects preserved Failed/LocalCatalogsUnavailable plus safe text acknowledging saved data and the unavailable list. The fake gained a test-only LocalLoadError at the existing local-load boundary; no new production seam was added. Opt-in reset, settled busy state and one import are also asserted.

Only test files changed in this stage. Product behavior is deliberately unchanged awaiting root's actual RED for the new regression test. Proposed subsequent minimal fix: make ReloadRows report success and allow ImportAsync's ordinary success only on a successful reload; otherwise retain fixed local-list error code with publication-aware recovery guidance. No retry or deletion of published files is proposed.

## Stage 5 — post-publication reload failure fix after actual RED

Read root's actual `data-manager-reload-red.json`: new regression test failed 1/1, Expected Failed but was Succeeded; root also reported compilation errors zero. Applied only the authorized minimal product fix in DataManagerViewModel: ReloadRows now returns a bool success result after its existing row/selection/diff cleanup. A published import enters ordinary Succeeded only when that reload succeeds. Otherwise it finishes Failed/LocalCatalogsUnavailable with fixed Korean guidance explicitly saying the catalog was saved, the local list could not be read, and to inspect assets/reopen the window. No exception text, automatic retry, new abstraction or published-file mutation was added. Initial constructor loading retains existing failure handling.

The existing regression test and all other test files are unchanged in this stage. Whitespace check passed (CRLF conversion notice only); this agent did not run Unity and makes no GREEN claim. Root owns focused/full verification. Code/test writing is stopped again after this fix; separate consent-destination review has not been changed here.

## Stage 6 — destination consent regression test only

Root conveyed an independent confirmed consent issue and requested a RED test before the guard: while the confirmation delegate is open, legacy directory resolution may change, so the identity alone does not ensure the pipeline will write to the approved destination. Added `DestinationChangedDuringApprovalRejectsDownloadAndRequestsFreshConfirmation`: the existing fake service resolves a captured string; confirmation changes it and approves the original summary. The test expects zero imports/download boundary calls, Failed/DestinationChanged, guidance to confirm again, false opt-in and a settled operation. The fake support gained only a nullable destination delegate; its normal resolver path and existing successful-consent tests remain unchanged.

No destination guard, DTO/interface change or pipeline filesystem change was written. Root must confirm actual RED for this test before the small production guard. Proposed fix remains re-resolving the same immutable request immediately after approval and refusing an Ordinal directory mismatch. Code/test writing is stopped at this handoff. Stage 5's local-list fix is the only product change since the preceding root validation.

## Stage 7 — destination revalidation guard after actual RED

Read `data-manager-consent-red.json`: VM suite 17 total, 16 passed, one failed; the destination-change regression observed one import when zero was required. The local-list regression passed. Root also reported consent compilation completed with zero errors.

Applied only the small authorized guard in DataManagerViewModel.ImportAsync. After approval/cancellation checks and before starting import, it reuses the exact captured immutable request to resolve the destination again and compares it with the approved directory using StringComparison.Ordinal. A mismatch returns Failed with the fixed existing regression-test code DestinationChanged and Korean guidance to confirm the target/location again. Existing finally resets opt-in and releases the operation, and the pipeline is never invoked on mismatch. No DTO, service interface, pipeline, filesystem publication or test behavior was changed in this stage. This closes the reviewed confirmation-interval mismatch; it does not introduce a new atomic filesystem approval protocol outside Task 2's scope.

Whitespace check passed (CRLF conversion notice only). This agent did not execute Unity or claim final GREEN. Code/test writing is stopped again for root's final focused/full verification and independent re-review.

## Stage 8 — actual native Inspector phase scheduling evidence and test-only correction

The earlier test-ordering/frame assumptions were insufficient to explain the remaining scheduling failures. Root performed native probes outside the test runner and provided concrete environmental evidence. This agent read `data-manager-editor-scheduling-state.json`: non-batch editor, application unfocused, not updating/compiling, 35 queued delayCall delegates. Root reported a separately scheduled marker did not run for tens of seconds; focusing the Editor drained the queue, while repainting all views alone did not service a fresh marker. This agent also read `data-manager-inspector-focus-probe.json`: the actual `EditorApplication.ExecuteMenuItem("Window/General/Inspector")` call succeeded and the Inspector activation allowed the marker to run.

The primary Unity 6000.3 API documentation explicitly places delayCall after Inspector updates: [EditorApplication.delayCall](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorApplication-delayCall.html). Root's source inspection additionally established that native Internal_CallDelayFunctions separates/clears the delegate list before invocation. The evidence identifies a background native Inspector-update phase condition; it is not a claim that arbitrary extra frames, test ordering, or host repaint solved the problem.

Only the test-local WaitForEditorRender helper changed: after registering its barrier, it opens/focuses Inspector through the real menu, asserts that menu execution succeeded, and awaits the existing bounded delayCall callback. It stores the previously focused EditorWindow and restores focus in finally while unregistering its own barrier. The previous insufficient host Repaint/QueuePlayerLoopUpdate requests were removed. Timeout remains five seconds. No global delegate invocation, direct product Flush call, production seam, weakened assertion or product behavior change was introduced.

This agent did not execute Unity; root owns the native-phase UI scheduling confirmation and final focused/full runs. Per root's report both independent Important review items are addressed; only scheduling verification remains. Code/test writing is stopped at this handoff.
