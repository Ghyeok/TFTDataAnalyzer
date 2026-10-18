# Task 3 independent review

Date: 2026-10-16. Base: bc42f6527ae8e062bc953363692a10dfa2421303. Scope: current tracked and untracked Task 3 changes, including new Collection service/VM/binding, window wiring/UXML, new Collection tests, window test adjustment and workflow documentation. Excluded the user's DefaultVolumeProfile change. Requested reviewer model/reasoning: GPT-6 Astra / High for privacy/data integrity; actual runtime model/reasoning was not independently exposed and is not verified. No usage estimate is available.

## Declined to judge

- Automatic 429 resumption, verified participant rank filtering, analysis/aggregate rebuilding, compliance/export and scheduling: explicitly outside Task 3. Existing bounded backend behavior is reused.
- Real Riot credentials/network behavior and native confirmation dialog clicks: intentionally excluded from verification. No real environment values were read.
- Minimum-size visual QA, Unity Console inspection and protected-file hash comparison: root/QA responsibilities; not independently executed by this reviewer.

## Verdict

Changes requested for one P2 UI-state inconsistency. No additional privacy or data-integrity blocker found in the inspected changes.

### P2: Rebuilding restores accepted inputs while retaining a conflicting invalid draft

Location: `Assets/TFTAnalyzer/Editor/UI/Collection/CollectionTab.cs:34-37`; related `CollectionViewModel.cs:49-77`.

Trigger: choose a verified catalog, enter a valid maximum of 17, change it to invalid 1001, then rebuild the window with CreateGUI (or dispose/recreate the binding). The VM deliberately retains MaximumMatches=17 while `_inputMaximum=1001` and `_inputValid=false`. The new binding initializes the field from MaximumMatches, so the user sees a valid cap of 17 but Start is disabled by the hidden invalid draft. Refreshing credentials cannot resolve that mismatch; another input edit is required. Similarly, valid-looking edits made before selecting a catalog remain only in the draft and disappear from the fields after rebuilding.

This is a direct code-path finding, not a claimed Unity reproduction. Existing tests preserve invalid input during normal renders and preserve an active valid run through rebuild, but do not combine invalid/current drafts with rebuilding.

Minimal correction: make rebuilt visible fields and the validation state agree. Preserve the current safe draft for binding reconstruction, or deliberately reset/revalidate the draft to the restored visible values as one operation. Do not expose arbitrary rejected strings merely to restore a draft. Keep busy-run reconstruction from mutating captured conditions.

Required regression: real UI test valid cap -> invalid cap -> rebuild, checking visible value and CanStart remain consistent; correct it and submit, asserting the visible cap reaches the request. Also cover input entered without a selected catalog -> rebuild -> catalog selection so the chosen draft policy is explicit.

## Evidence reviewed

- `collection-green-compile.json`: compilation completed, failed=false, compilationFailed=false, errors empty.
- `collection-focused.json`: 43 total / 43 passed / 0 failed / 0 skipped.
- `collection-full.json`: 453 total / 453 passed / 0 failed / 0 skipped.
- These are root-produced execution artifacts inspected by this reviewer; this reviewer ran no Unity commands/tests.
- Checked code paths for full identity snapshot, immutable confirmation/request, configured-only secret exposure and controlled exception text, invalid input gating, confirmation rejection, reentry during busy states, cancellation until actual settle, authoritative final summary, partial saved counters, 401/403/429 guidance, ten-page scan limit distinction, stale progress rejection, captured context dispatch, binding disposal/window close, and one retained client across manual retries.
- Offline adapter tests exercise existing anonymization/storage and persisted fabricated identifier absence, duplicate handling, retained quota state, bounded server retries. UI tests use NavigationSubmitEvent and actual delayCall boundary.
- No product source, generated data, git state or credentials were mutated by this reviewer. Only this report was written.
