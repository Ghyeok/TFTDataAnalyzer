# Task6 independent code review

2026-10-19. Base `ab63a3c`; read-only review of the supplied GREEN_READY working tree, canonical `refined/ImplementationPlan.md`, current Antigravity task manifest/result/runtime, and new CLI entry point. Requested model/effort: GPT-6 Astra / High; actual execution metadata is not independently exposed to this reviewer and must be recorded by root. No Unity, network, Git write, Hermes, or additional agent invocation. Only this report was written. Runtime test completion and PNG correctness are not claimed.

## Verdict: revise

### P2 — Notify open windows when restoring a cached version on recovery/failure paths

- Location: `Assets/TFTAnalyzer/Editor/Ingestion/StaticVersionCheckScheduler.cs:72-85` (especially lines 75-76 and 80-84).
- Trigger: an existing window subscribes before startup checking. State has a valid differing `lastSeenVersion` plus a corrupt/future timestamp; alternatively state is at least 24 hours old and the new request fails, times out, or returns no valid version.
- Impact: `LatestVersion` is restored but `Changed` is never emitted. The window stays blank despite a valid cached update notice; no further state transition is guaranteed for the next 24 hours. This violates restoration of the stored notice for an already-open window. The existing cache restoration test covers only a valid, unexpired timestamp.
- Minimal fix: publish an event when the effective cached version changes after successful state restoration, independently of HTTP success. Keep cancellation/generation safeguards. Add recovery and expired-state/failed-request tests that assert both the notice and a subscribed event, ideally through the real window binding.
- Evidence: static control flow. No Unity execution by reviewer.

### P2 — Validate the complete Antigravity response schema before accepting success

- Location: `AgentScripts/Invoke-AntigravityPlanningReview.py:124-129`.
- Trigger: SUCCESS/exit0 response with matching task/digest and valid verdict, but missing `findings`/`unverified`, `findings` as an empty string, or `findings: [{}]`.
- Impact: all three malformed responses satisfy the local checks and are persisted with `protocolSucceeded=true`. Passing `--json-schema` to a remote/CLI producer is not local validation of its fallback `response` JSON. Missing severity, impact, action, and inScope can be mistaken for a complete review.
- Minimal fix: validate payload against the full already-declared schema locally, including object/array/item types, required fields, enum values, maximum items, and additional properties, before writing review.json or setting success. Reject malformed results and record the failure.
- Evidence: an offline, filesystem-free Python evaluation of the exact acceptance predicate returned `[True, True, True]` for the three malformed examples. No CLI or paid request was made.

### P2 — Apply the task-path boundary to control inputs and outputs as well as copied inputs

- Location: `AgentScripts/Invoke-AntigravityPlanningReview.py:25-27`, with writes at lines 128 and 146.
- Trigger: a task inside the approved collaboration directory contains a `request.md` symlink/reparse point to a text file outside that task, or `outputs` points to an unrelated directory. Copied manifest inputs can all remain valid.
- Impact: request.md is read into the transmitted prompt without the containment check applied to manifest files. Output writes likewise follow the unchecked directory; runtime.json can overwrite an unrelated existing file. This defeats the entry point's supplied-task file boundary and preservation contract. `accessRestrictionVerified=false` accurately describes the remote process limitation but does not protect these local reads/writes.
- Minimal fix: resolve/validate request, manifest, outputs and output-file paths before I/O; reject linked control files/directories or enforce their expected in-task roots, including existing parents. Keep the explicit statement that remote tool/OS isolation remains unverified. Use local benign fixtures only for traversal/link tests.
- Evidence: static path flow; no external/private file was read, no link created, and no transmission performed.

## Scope and evidence limits

- Reviewed reservation-before-HTTP, exact 24-hour gate, in-flight Task sharing, cancellation deadline and late-result generation checks, atomic two-field persistence, public TestRunner callback lifecycle, and single scheduled idle callback ownership.
- Reviewed injected generated/cache/index/canonical/legacy/icon/raw/staging/recovery paths, targeted `SaveAssetIfDirty`, optional completion metadata and deterministic latest selection, immutable reimport assertions, fake providers configured before GUI creation, actual E2E bindings, anonymous storage assertions, suppression below sample threshold, and rendered dynamic UI claim scanning.
- Reviewed the visual tool's exact window ID/name, PID-scoped HWND baseline, deadline/finally cleanup, and wrapper fallback. Root must independently execute and inspect actual PNGs and final window/frame comparisons. Failure-probe success would not establish every timeout/disconnection path.
- Root reported a separate current E2E `ValidationRejected` issue caused by default icon-root validation while injected paths are used. It was already assigned for repair during this review; actively edited validator/test files were excluded from re-review pending a new GREEN_READY signal. This is an outstanding known blocker, not an independently verified fix.
- No final `task6-*.json` execution file was designated to this reviewer, so no suite totals or current GREEN claim are inferred from older reports. Current Antigravity runtime records its actual prior A execution; the new generalized entry point's dry run must remain distinct from that paid execution.

Root should record actual model/effort and available usage/time metadata separately. Review findings require fixes and suitable focused evidence before approval.

## Follow-up review — findings closed, full QA still pending

2026-10-19, after root's targeted-fix review request. Scope was limited to the three findings above and the already-known injected icon-root failure; no additional hypotheses or scope expansion. Product files remained unchanged by this reviewer.

1. **Cached notice finding: closed.** Both repaired-state and valid cached-state branches now emit `Changed` immediately when `LatestVersion` changes. This no longer depends on HTTP success or the unexpired gate. The added `RepairedOrExpiredStateNotifiesAnOpenWindowEvenWithoutHttpSuccess` cases check the event, retained notice, and expected HTTP count for a future timestamp and an expired timestamp with a failed request. Root reports a failing RED assertion followed by scheduler 20/20; the source and test changes were directly reviewed, while that execution count is root-provided evidence.
2. **Response schema finding: closed.** `validate_review` locally enforces the complete subset used by the declared schema: exact required object keys, arrays/item validation, maximum findings, string/boolean types, and enum values. Validation precedes success; review publication additionally waits for exit0. The actual-entry fake-process test covers valid results and the four malformed forms. Root reports all three offline test methods passing; this reviewer did not rerun the tests or invoke agy.
3. **Local file boundary finding: closed.** `local_path` now checks normalized/resolved containment, rejects path aliases through links and multiple hard links for existing files, and covers task/request/manifest/copied inputs/output directory/review/runtime. Publication rechecks review/runtime paths. The new control-file tests exercise four external hard-link aliases. Remote process/tool isolation remains explicitly unverified, as required.
4. **Known injected icon-root blocker: closed for this focused review.** `CatalogValidator` now receives the generated directory, limits local icons to its exact `Icons/` child plus a single PNG filename, and rejects default/other roots, traversal, nested paths and query suffixes. `DataManagerService` and `PublishedCatalogLoader` both pass the same injected directory. Direct inspection of root-designated artifacts found outer/inner success, zero failed/skipped/inconclusive, and totals: `task6-green3-e2e.json` 4/4, `task6-green3-icons.json` 1/1, `task6-green3-validator.json` 11/11.

**Follow-up conclusion:** no unresolved finding remains within this limited code-review scope. Root may proceed to the planned full suite and manual validation. This conclusion does not assert full-suite success, PNG correctness, final window/native-frame preservation, or final project completion; those were still pending at the follow-up request.

Root subsequently designated the remaining artifacts, which were read directly without executing tests: `Library/TftAnalyzerValidation/task6-final-scheduler.json` has outer/inner success and Total20/Passed20/Failed0/Skipped0/Inconclusive0; `Library/TftAnalyzerValidation/task6-antigravity-unit-tests.txt` records 3 tests, OK, 0.198 seconds. These replace the root-report-only qualification for the focused GREEN scheduler and offline CLI counts above. The prior RED evidence was not separately re-read.
