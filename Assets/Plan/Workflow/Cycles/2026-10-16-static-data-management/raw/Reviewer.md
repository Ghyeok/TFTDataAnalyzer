# Phase 3 Task 2 independent review

Verdict: **Changes required — 2 Important findings; full-suite verification remains failing.** No Critical finding identified. No additional Minor finding is raised.

Requested review model/reasoning: `gpt-6.1-sol / high` as the replacement after the preceding Astra turn reached its usage limit. The actual runtime model/reasoning is not independently verifiable here. This is not a claim that the Astra review completed. No token, cost, or elapsed-time estimate is made.

## Review basis and boundaries

Reviewed the frozen `Library/TftAnalyzerValidation/data-manager-review.diff` against base `cd1b67f`, the review/task briefs, and the implementation report through Stage 3. Inspected the existing pipeline/resolver/writer only for the named risk that the confirmed destination could change before execution. Subsequent implementation edits are outside this frozen review.

No Unity execution, test rerun, network access, code/index/Git mutation, subagent, or Hermes work was performed. This report is the only written artifact.

## Important findings

1. **A failed post-publication local reload is overwritten by success.** `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerViewModel.cs:102–103`, with failure handling at `:167–174` in the frozen diff. If publication succeeds but `LoadLocalCatalogs()` throws, `ReloadRows()` empties the list and sets `Failed` / `LocalCatalogsUnavailable`; `ImportAsync` immediately replaces both with `Succeeded` and a message directing the user to select from that empty list. This conceals a real local/index read problem after publication and gives incorrect recovery guidance. Minimum correction: return an explicit reload outcome (or propagate a sanitized failure) and only publish the success status when the reload succeeds, preserving the already-published files. Add the planned regression covering successful import followed by a throwing local reload, including opt-in reset and no secret exception text.

2. **The approved destination is display-only and is not revalidated after the confirmation callback.** `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerViewModel.cs:80–95`; production boundary `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerService.cs:32–36,42–48`. The dialog captures `ResolveDestination(request)`, but execution only passes `request` to the service. The existing pipeline resolves the destination again on entry. For an official-only identity, a compatible schema-1 legacy directory appearing while approval is pending changes the resolver result from the canonical directory shown in the dialog to that legacy directory; disappearance changes the reverse direction. The user therefore approves one storage path while the actual operation uses another. This is a concrete consent-boundary gap, even though the full identity itself remains captured and immutable. Minimum correction within Task 2: immediately after approval and cancellation/disposal checks, resolve the same request again and decline import with a fixed, safe retry/approval-required status if the destination differs. Test a destination change inside the injected confirmation callback and assert zero import calls, opt-in false, and fresh approval on retry. No new pipeline-wide filesystem transaction design is requested.

## Actual evidence read

- `data-manager-final-compile.json`: completed, `failed=false`, zero compilation errors.
- `data-manager-focused-final.json`: 21 total / 21 passed.
- `data-manager-loader-focused.json`: 11 total / 11 passed.
- `data-manager-full.json`: 408 total / 405 passed / 3 failed. All three failures occur in `DataManagerTabTests` SetUp while the test-local `WaitForEditorRender` delay-call barrier exceeds five seconds (`DataManagerTabTests.cs:207`). They are `BusyControlsAndCancelWaitForTheExistingOperationToSettle`, `DisposedBindingCannotChangeInputOrReceiveScheduledUpdatesAndRebuildKeepsActiveIdentity`, and `FewerThanTwoHealthyCatalogsShowsAnEmptyDiffStateAndDisablesComparison`.
- `data-manager-open.json`: actual menu/window smoke confirms controls, default supplement false, footer, no missing-asset view, and one local row.
- `data-manager-lifetime.json`: actual window rebuild retains the VM and active full identity; closing disposes the VM. The existing official row is selectable with actual standalone UTC `2026-09-17T10:18:56.3864991Z` and hash prefix `b306b524fad3`.

The three full-suite failures block completion. Their recorded failure site is the fixture's scheduling barrier before each affected test body, so these results alone do not establish three product failures. The focused pass does not waive the full-suite failure. Root/implementer are already investigating the harness ordering/scheduling cause; this review does not repeat that diagnosis or recommend increasing the timeout, weakening assertions, or adding a production test hook.

## Next review requirements

Review the two small fixes and their regression tests as a fresh diff, then read root-owned actual compile, focused loader/VM/UI, and full EditMode results. Require the full suite to pass after the scheduling cause is corrected. Retain the existing window lifetime/menu/protected-byte evidence, refreshing it if the relevant product paths change. Native dialog clicks, real network traffic, physical pointer interaction, and a full domain reload have not been verified by this reviewer and are not asserted as completed or as product defects.
