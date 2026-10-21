# Minimum samples Task 2 static review

Date: 2026-10-21. Scope: approved minimum-samples Task 2 VM, binding, UXML, and related new tests. Existing reviewer reused; actual runtime model/reasoning cannot be independently confirmed. No Unity/network execution, delegation, or product modification.

## Required finding

**P2 — The export callback can write before queued minimum-input changes invalidate the snapshot.** `Assets/TFTAnalyzer/Editor/UI/Analysis/AnalysisTab.cs:91` through `_writeExport` at line 104 validates the VM snapshot/row references and seven dropdown values synchronously after `_chooseExportPath`. Minimum changes reach the VM only through `OnMinimumChanged` at line 57. Nested UI Toolkit change events can be delivered after the enclosing button callback returns; this dispatch behavior was already reproduced for the existing filter-modal regression during the earlier analysis implementation. Therefore a dialog callback setting the IntegerField from 100 to 200 and back to 100 can still pass all current guards while the original snapshot remains published, and write before the queued changes clear it. A comparison of only the final visible minimum would also miss that round trip.

The new `MinimumChangeDuringDialogRejectsExportEvenIfTheInputReturnsToItsOldValue` test covers the relevant reproduction but its post-implementation GREEN run has not happened. This is a static finding based on the existing dispatch mechanism, not a claim of a newly executed failure. Revalidate/write only after nested change events have drained, with any scheduled callback owned and removed on disposal, or use an equally reliable mutation-version mechanism that detects the round trip before writing. Keep the existing zero-write and changed-target notice assertions.

## Other inspected behavior

- The VM preserves invalid input, rejects refresh/export, clears all old results on any actual threshold change, advances the request generation, cancels the active token, and ignores late completion. Same-value calls are idempotent.
- Requests capture the minimum; stricter returned snapshot thresholds are accepted and displayed, lower thresholds rejected. The constructor's floor plus a valid request ensure that accepted snapshots cannot be below 100.
- The IntegerField is outside the disabled filters container and remains editable while busy. Rebinding uses SetValueWithoutNotify; disposal removes its callback. Existing VM ownership preserves input through catalog/rebuild changes.
- Korean copy distinguishes the minimum, explicitly requested refresh, actual applied threshold, participant observations, and comparable lobbies.
- No further required static change identified in the bounded scope. Task 1 findings/results are unchanged.

## Evidence and limits

Directly read saved JSON: VM RED 22 total / 13 passed / 9 failed, then GREEN 22/22; UI RED 12 total / 7 passed / 5 failed. `minimum-samples-task2-ui-green-status.json` reports `compilationFailed=true`, despite the outer request success and inner `status=completed`. It is not UI GREEN or successful compilation evidence. Coordinator reports bee_backend native failure and has stopped execution pending an Editor restart; this reviewer did not rerun it or count the subsequent rejected request as another crash.

Assessment: one required export-ordering correction remains. No Task 2 execution-pass or overall QA-pass claim. Screenshot-helper additions and actual PNG/window-cleanup execution are outside this bounded review and remain unverified.
