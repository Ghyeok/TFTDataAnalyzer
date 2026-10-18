# Collection Task 3 implementer report

## Stage 1: observed RED

- Scope: Phase 3 Task 3 only. Added eight parameterized VM test cases, one fake service, a minimal service interface and an inert compile-capable VM contract scaffold. No adapter, UI binding or collection behavior implemented.
- Tests cover configured booleans/public credential surface, accepted limits 1/1,000, rejection of 0/1,001 without replacing valid input, approval/denial and immutable full-identity request snapshot, safe configuration exceptions, and absence of construction-triggered collection.
- Expected assertion RED: configured booleans remain false; valid input returns false; confirmation count remains zero; configuration error state remains Idle. Scaffold deliberately contains no functional collection implementation.
- Changed files: `Assets/TFTAnalyzer/Editor/UI/Collection/ICollectionService.cs`, `Assets/TFTAnalyzer/Editor/UI/Collection/CollectionViewModel.cs`, `Assets/TFTAnalyzer/Tests/EditMode/UI/CollectionTestSupport.cs`, `Assets/TFTAnalyzer/Tests/EditMode/UI/CollectionViewModelTests.cs`.
- Root supplied fresh evidence: compilation completed with failed=false/errors=[]; `collection-red-tests.json` Total 8, Passed 0, Failed 8. Expected false availability/input acceptance, zero confirmations and Idle failure state observed.
- Model policy: task warrants requested GPT-6.1 Sol / High due cancellation/lifecycle and secret-handling risk. Actual runtime model/reasoning and usage metadata not exposed to this implementer; no verified switch or measured usage claim.
- Root authorized Stage 2 after that RED evidence.

## Stage 2: IMPLEMENTED_READY

- Implemented the production facade retaining a single RiotApiClient/quota state, validated VM, numeric progress snapshot, disposable tab binding and real window/UXML integration. Selected verified full-identity Data rows feed collection. Running catalog/request are captured before confirmation; new selections apply after settle.
- Approval/running/cancelling retain busy state; cancellation settles before unlocking; service final summary wins over late cancellation. Stale/queued/disposed progress is rejected, and foreign-context progress posts to the captured UI context. 401/403/429/partial failure/scan budget have controlled copy; 429 reports retry UTC and manual retry, without automatic resumption.
- Invalid latest visible input blocks Start instead of using an older accepted bound. Latest attempted inputs are kept privately and revalidated when catalog selection arrives, preserving edits made before selection.
- Added VM lifecycle/failure/progress tests, real NavigationSubmitEvent UITK tests, injected fake window selection/rebuild/close tests, and offline real-client/anonymizer/store integration tests including durable duplicate handling and retained quota across runs. Existing window tests inject an offline collection model so validation does not inspect actual environment credentials.
- Static validation: UXML parsed as XML; all literal collection binding names exist; no duplicate named elements. No Unity compilation, EditMode tests or visual capture executed by implementer. Root must run compilation and focused tests, then full default EditMode suite. Source inspection is not runtime evidence.
- Additional changed files: `Assets/TFTAnalyzer/Editor/UI/Collection/CollectionService.cs`, `CollectionProgressViewModel.cs`, `CollectionTab.cs`; `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs` and `.uxml`; `Assets/TFTAnalyzer/Tests/EditMode/UI/CollectionServiceTests.cs`, `CollectionTabTests.cs`, `CollectionUITestSupport.cs`, `CollectionWindowTests.cs`; existing `TftDataAnalyzerWindowTests.cs` only receives offline model injection.
- No root workflows/docs under Assets changed by implementer. No Unity or git commands, real credentials, live calls, generated catalogs, DefaultVolumeProfile edits, commits or subagents. New metas are left for Unity/root. Runtime model/usage remains unverified.
- Assets writes paused on handoff. Await root compile/test result before any further edits.

## Review fix round 1: FIX_RED_READY

- Root supplied clean compilation, focused 43/43 and full 453/453 results (`collection-green-compile/focused/full.json`). Independent review found that rebinding restores accepted values while the VM still holds an invalid/different draft.
- TEST ONLY: added two actual UITK regressions in `CollectionTabTests.cs`: valid 17 → invalid 1,001 → rebuild must retain visible 1,001 and blocked Start, then correction to 18 collects exactly 18; before-catalog edits (23 / 1090 / NA1 / DIAMOND+) must survive rebuild and start unchanged after verified selection arrives.
- No production code changed for this round. Expected RED: first rebuilt maximum expected 1,001, actual 17; second expected 23, actual 100. Unity execution remains root-only and unexecuted by implementer. Assets writes paused pending root RED.

## Review fix round 1: FIX_GREEN_READY

- Root confirmed fresh RED in `collection-rebuild-red.json`: Total 2, Failed 2; expected 23 vs 100 and 1,001 vs 17. Compilation errors 0.
- Minimal fix: the binder restores numeric draft queue/cap and only allowlisted draft platform/tier choices. It then reconciles those visible values through the existing validation method before synchronously rendering Start state. Invalid numeric drafts stay visible and blocked; edits before catalog selection survive; rejected arbitrary choice text falls back to accepted safe choices and is never shown.
- Changed production files: `CollectionViewModel.cs` adds internal numeric draft getters and allowlist-filtered choice readers; `CollectionTab.cs` restores/revalidates draft rather than last accepted fields. Regression test also checks fabricated rejected platform/tier strings remain absent from dropdown values and corrected request matches the safe visible choices.
- No Unity or git execution. Runtime fix verification remains root-only. Assets writes paused again for root compile/focused/full test verification.

## Window cleanup correction: CLEANUP_RED_READY

- Read the new user-requested narrow cleanup brief. Root reports final product suite 455 passed; product behavior remains unchanged.
- TEST/SCAFFOLD ONLY: added `OwnedEditorWindowScope.cs` inert ownership contract and `OwnedEditorWindowScopeTests.cs` with four regressions: normal/repeated/already-closed cleanup preserves unowned window and prior focus; initialization failure before Show still owns/closes the exact object; a real close followed by injected failure remains observable without skipping another owned window; explicitly created Inspector branch closes only that exact owned Inspector without removing existing Inspectors to simulate absence.
- Tests include their own finally cleanup of exact created references so intentional RED does not leave test windows behind. No test fixture integration or production changes yet.
- Expected RED: owned objects remain alive after inert Dispose; aggregate cleanup failure is not raised. No Unity/git/network/environment access executed by implementer. Assets writes paused for root fresh RED.

## Window cleanup correction: CLEANUP_GREEN_READY

- Root observed four fresh assertion failures in `collection-window-cleanup-red.json`, compilation errors 0, then authorized implementation.
- `OwnedEditorWindowScope` snapshots original window IDs/focus, registers exact newly created objects before initialization/Show, borrows an existing Inspector or explicitly owns a newly created one, and disposes tracked objects idempotently. It attempts all closes, restores still-living prior focus in finally, and surfaces aggregated cleanup failures.
- New common `EditorWindowTestSupport.WaitForRender` retains the actual Inspector/delayCall phase and unchanged five-second deadline. A finally block removes the callback and the owned scope closes any newly created Inspector on success/failure/timeout. Both prior render helpers delegate to this one; no production flush hook or delegate invocation.
- Four UI fixtures now create hosts/restored windows through the scope, protect partial SetUp with catch cleanup, and use nested finally cleanup where bindings/models could fail. Prior fake service/model injection remains before Show; no user's existing Analyzer is reused or resized.
- Ownership regression tests independently clean exact created references in finally even if scope disposal fails, and restore the pre-test user's living focus after removing their own sentinel. No broad type/name-based closure.
- Only test utilities/fixtures changed; Task3 product code and Task4+ remain untouched. No Unity/git/env/network commands. Actual native Inspector focus path and final pass counts await root verification; Assets writes paused on handoff.

## Window cleanup runtime correction: CLEANUP_FIXED_READY

- Root observed actual all-UI results 78/82 passed, with one pre-Show Close native-parent null dereference and three unchanged five-second delayCall timeouts from Focus-only activation. Root verified Unity 6000.3 primary source for Close/GetWindow/menu behavior.
- Initializer/Show failure now destroys only the exact tracked object before rethrowing the original exception; initialization plus destruction errors remain aggregated. Dispose also destroys an exact tracked close survivor while preserving any real Close exception and continuing all remaining cleanup.
- Common render barrier first borrows or creates/owns an Inspector, then uses the proven Inspector menu activation. Root-verified GetWindow source reuses that already-existing Inspector; no untracked creation or broader closure is introduced. Five-second timeout and callback/focus finally cleanup remain unchanged.
- No new product behavior, tests, Unity or git actions. Assets writes paused again for root verification.

## Render and native-frame design correction: RENDER_CLEANUP_READY

- Root design re-analysis authorized replacing the Inspector-dependent scheduling contract. Both DataManagerTab and CollectionTab now subscribe one-shot to EditorApplication.update; Flush first unsubscribes, clears queued state, then renders. Dispose removes that same callback. No polling, VM thread changes or production test hooks.
- The common five-second barrier now uses the same actual EditorApplication.update event and unregisters on delivery and in finally. Inspector focus/menu/scope creation are removed entirely. Removed the unused FocusInspector API and Inspector-only regression, retaining three meaningful exact ownership/failure tests.
- Corrected two root-proven native-frame paths: the collection window's explicit mid-test close now calls Close; ownership-test independent fallback closes known shown references before exact-object destruction of unshown objects or close survivors, preserving errors and continuing all cleanup. User focus is still restored in finally.
- Native ContainerWindow before/after evidence is delegated to root's direct inventory validation of these corrected failed-run paths; no mirrored count-only test or closure of unidentified user windows was added.
- Root must compile, run affected UI tests and full suite (expected test total 458 after removing Inspector case), and compare exact native/window inventories. No Unity/git/env/network execution by implementer. All Assets writes paused on handoff.

## Real legacy catalog integration: LEGACY_RED_READY

- Root's actual shipped-baseline visual revealed schema-1 Manifest.Identity is null by design, while the verified published row carries full official-only identity. Current window/VM/tab loses that verified row identity. Prior 458 tests used schema-2 collection fixtures.
- TEST/CONTRACT-SCAFFOLD ONLY: optional identity argument added to SetCatalog and inert ActiveIdentity getter still reflect the existing manifest-derived behavior; identity is deliberately not yet implemented. Added verified schema-1 row→window→VM→confirmation/request/UI regression, explicit legacy identity run snapshot/resync regression, six invalid identity cases (missing legacy identity; legacy profile/patch/set-number mismatch; schema-2 profile/set-number mismatch).
- Existing offline real-client/anonymizer/store test now runs both schema-2 and legacy schema-1 manifests with explicit verified identity; backend/domain/generated data unchanged. Test row helper can construct actual schema-1 manifests.
- Expected RED includes null legacy ActiveIdentity, rejected valid legacy input, real adapter legacy path failing before collection, and accepted incompatible explicit schema-2 identity. No Unity/git/network/env access. Assets writes paused for root RED before identity implementation.

## Explicit legacy identity correction: LEGACY_GREEN_READY

- Root observed fresh compilation errors 0 and focused 54 total / 49 passed / 5 failed in `collection-legacy-red.json`: valid legacy adapter/snapshot/window paths fail; incompatible explicit schema-2 profile/set-number identity was accepted.
- Window now passes the selected verified row's catalog AND identity. VM separately retains selected/running catalog-identity pairs, exposes ActiveIdentity, validates/reuses it for input admission, confirmation and immutable request, and resynchronizes after settle. Tab displays that explicit ActiveIdentity.
- Schema 1 without an explicit row identity remains rejected; supplied legacy identity must match set ID, set number, TFT patch, source version and official-only profile. Schema 2 retains one-argument compatibility but any supplied identity must match manifest identity and all patch fields, including SetNumber omitted by CatalogIdentity.Equals.
- No backend/domain/generated catalog mutations. One-shot rendering and exact window ownership/cleanup unchanged. No Unity/git/env/network execution by implementer. Assets writes paused for root compile, focused/full tests, actual legacy visual and independent review.
