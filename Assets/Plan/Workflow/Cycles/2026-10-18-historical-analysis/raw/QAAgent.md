# Task 4 independent final QA

Reviewed 2026-10-19 against the current working tree, supplied base `9d87f5d3d0c9d181ceab587f915d496e79fd3d0e`. Requested worker configuration: Sol / High. Actual runtime model, inference setting, token usage and billing were not independently observable; none are invented here.

This worker performed read-only source/document/evidence inspection and directly opened the six final PNGs. It did not run Unity, access the network, inspect environment secrets, mutate Git, edit product code or other documentation, or spawn agents. Only this report is owned by this worker. Saved Unity execution output is evidence from the coordinator's executions, not execution performed by this QA worker.

## Current assessment

**PASS — independent final QA gate.** Bounded product, test, saved execution, image and final-document review found no open actionable defect. The coordinator's latest protection, Console and current-helper failure-probe evidence was read directly and agrees with the final documents. This passes the QA gate for the current working tree; it is not a commit, PR, CodeRabbit, merge or user-acceptance completion claim.

## Requirements checked

| Requirement | Assessment from implementation and evidence |
|---|---|
| Explicit local refresh; no API or filter-triggered IO | `AnalysisService` reads/rebuilds/saves only through Refresh. VM catalog/filter changes remain in memory. Fake service call-count tests cover opening, filtering, rebinding and explicit refresh. |
| Coherent selection and snapshot | Full catalog identity plus set number is checked; schema 1 requires separately supplied verified official-only identity. Selection change clears old rows/export and cancels the old generation. Late completion cannot publish. |
| Scope and composition filtering | Region/queue/tier narrow scoped rows without pooling. Unit/active trait/item/augment intersect on one composition; a pair requires one complete endpoint to satisfy all board filters. |
| Policy and copy | Existing MetricPolicy produces every presented number. 99 suppresses ratios; 100 displays allowed counts/rates/95% intervals. Unknown/Legend average and ratio suppression stays intact. No classification is inferred. Pair direction and eligible-lobby count are retained and explicitly explained as relative final placement. |
| Time and empty state | Dates are observed match FirstPlayedAt/LastPlayedAt, with explicit empty-state and current identity/filter information; no retrieval time is invented. |
| CSV | Accepts MetricPresentation only; all pages of the current filter are exported. Suppressed metric/numerator/denominator/estimate/lower/upper are blank. UTF-8, quoting, formula escaping and invariant fractions are covered. Export is local/personal-research and independent of key/registration. Device namespaces are rejected early; canonical Windows directory checks reject project aliases. |
| UI state/lifetime | Busy state, cancellation, rebind, close/dispose and one-shot render cleanup are implemented/tested. Save-dialog event pumping rechecks snapshot, identity/set, row/pair references and all seven visible filter values. Rejection copy survives the queued render. |
| Scope boundary | No combat simulation/prediction, new aggregate schema, real-data augment classification, Task 5 build/compliance feature or Task 6 scheduler/E2E implementation was introduced. |

Sources inspected: AGENTS.md and agent-guide index; original Task 4 in `Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md`; refined PlanningReview; all six Analysis production classes; window C#/UXML/USS integration; four new Analysis test classes; underlying MetricPolicy and composition fingerprint contract; current manual validation wrapper/C# helper; independent code-review report including resolved follow-ups.

## Saved execution evidence read directly

- `analysis-final-compile.json`: inner status completed, failed=false, compilationFailed=false, errors=[]; outer tool success and no errors/warnings.
- `analysis-full-final.json`: 495 total / 495 passed / 0 failed / 0 skipped / 0 inconclusive, EditMode assembly TFTAnalyzer.Tests.EditMode, duration 54.24 seconds, inner and outer success.
- `analysis-ui-final.json`: AnalysisTabTests 7/7, no skipped/inconclusive, including save-dialog filter-change rejection through render.
- `analysis-csv-pathfix-green.json`: AnalysisCsvExporterTests 5/5, no skipped/inconclusive. Native Windows namespace and 8.3 alias tests actually passed rather than being ignored.
- `analysis-full-windows.json`: identical nine pre/post EditorWindow IDs and one native ContainerWindow ID; focus baseline 0 remains 0. WindowsSame, ContainersSame and FocusSame are true.
- Initial `analysis-red-tests.json` is 12/12 failed (intended initial RED). `analysis-focused-final.json` is an earlier 27/28 run with a modal failure, despite its name; it is superseded by final UI7/7 and full495/495 and must not be cited as GREEN.
- `analysis-path-attribute-trace-bounded.txt` directly demonstrates the repeated extended drive-root parent value. Earlier two hung test attempts and user restarts are not passing timeout/cancellation evidence.

The tests cover real temporary observation-store build/filter/export integration, invalid/corrupt input without publication, multiple independent scopes, set identity, pair endpoint semantics/99-100/ambiguous exclusion/direction, policy restrictions, safe failure/cancel/dispose, actual window selection/rebuild/close, pagination and modal changes. The save failure-preservation test injects invalid serialization input before staging; it does not inject an OS staging/replace failure. That is a coverage limit, not an independently established defect.

## Images inspected directly

Opened the current `analysis-window-{940,500}-{top,rows,pairs}.png` files using the image viewer. All six show the Analyzer itself. The 940px top exposes the identity, explicit refresh, observed period and filters. Its rows show 100-sample allowed percentages/95% intervals and a 99-sample row with no percentage. Its pairs show distinct A/B compositions, direction explanation, eligible-lobby and ambiguous counts, 100-sample ratios and a 99-sample suppressed pair. The 500px top/rows/pairs wrap text and provide scrolling; the historical disclaimer remains visible outside the scroll viewport. The 500px images are shorter viewports and do not independently show every row. Synthetic visual classifications do not establish that unclassified real-data augments display ratios.

The earlier pair-heading-only capture and wrong-application capture attempts are not passing visual evidence. Geometry/label JSON and PNG content are treated separately.

## Validation helper inspection

The helper records baseline window/container/focus state, immediately records the newly created exact owner with a unique name, and uses Close first for shown windows. Its finally path separately attempts each VM disposal and exact owner release, then restores a surviving original focus. Fallback cleanup resolves exact ID plus unique name; it never bulk closes a title/type. Native visibility uses current PID plus a difference of exactly one HWND from the per-run baseline and fails closed on ambiguity. Only that new frame receives SetWindowPos. Pre/post actual EditorWindow and native ContainerWindow sets are checked, not merely counts. AlignTop ensures row/pair sections are at the scroll viewport top before captures.

Final normal `analysis-visual-final.json` records Passed=true, six captures, no failure/cleanup failure, no added/missing window or container IDs, owned remaining 0, existing preserved and original focus restored; LoadCalls=1, CollectionCalls=0. The final pre-show and post-show command results were also read directly: fresh dynamically compiled helper assemblies, Passed=true, ExpectedProbeFailure, no cleanup failure, no added/missing window or native container IDs, owned remaining 0, existing preserved and focus restored. Their saved outputs are newer than the final helper source (source 00:36:05, pre-show result 00:37:06, post-show 00:37:11 on 2026-10-19 KST). Final shell fallback has outer/inner success, Verified=true, OwnedRemaining=0 and identical baseline sets. Transport disconnects and every possible timeout path have not been independently exercised and must not be claimed verified.

## Final documentation, preservation and Console

Read the new cycle Checkpoint, DevelopmentReport, FinalDocumentation, ValidationEvidence, preserved Reviewer report and ImplementationHandoff; reread current README and original Task 4 checklist. User guidance matches implementation, including active-catalog patch selection, board-level item intersection rather than an item-to-selected-unit relationship, observed match periods, unclassified real-data augment suppression, all-filtered-pages CSV and key-independent local export. Task 4 Steps 1–4 are checked, Step 5 correctly remains pending the commit. Task 5/6 remain unchecked. Independent QA is correctly described as pending before this report is integrated; root must update the current checkpoint when integrating this PASS. The raw review's historical pending-execution remarks are superseded by explicit final UI/full-suite evidence rather than rewritten as executions by that reviewer.

Checked 39 local Markdown link targets across README, the original implementation plan and all new-cycle Markdown files: zero broken targets. Current documents distinguish passing execution, initial intended RED, modal regression, path hangs/user restarts and bad capture iterations; they do not claim timeout cancellation, real API use, native dialog clicks or every disconnect path was verified. Requested model settings and unavailable actual runtime/usage are separated. Hermes monitor remains paused, and no new polling/re-enable is requested.

`analysis-protected-final.json` records Files=1386, Changed=0, Paths=[] for the coordinator's protected baseline. This report inspected that result, without recomputing those hashes or staging the Volume/generated files. `analysis-console-final.json` has outer success and empty tool errors/warnings, same session `09a2ffa10ea742cea187375c43f3fb1b`, since=32, cursor=52, entries=[], returned=0, dropped=false, reset=false. Existing error count 2 and warn count 0 are retained. Thus the new warning/error delta is empty; total historic errors are not claimed zero. No material documentation/evidence mismatch found.

## Read-only verification

`git diff --check` exited 0, with existing line-ending conversion warnings only. No reported execution result was inferred from an AI review or from a filename containing "final".
