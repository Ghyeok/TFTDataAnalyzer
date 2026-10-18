# Final Task 3 QA request — 2026-10-18

Read AGENTS.md, docs/unity-editor-validation.md, Assets/Plan/Workflow/README.md, Phase3 Task3 in Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md, current cycle PlanningReview/PlanReReview, product and test changes including untracked files against base bc42f6527ae8e062bc953363692a10dfa2421303.

Perform independent read-only QA of Task3 requirements, exceptions/regressions, evidence and scope. Existing code review reports are collection-review-report.md, collection-rereview-report.md and collection-window-final-review.md under Library/TftAnalyzerValidation. No product/test source has changed since the last scoped code approval/UI90; new docs are included in this commit. Do not repeat architecture planning.

No Unity commands, real env/credentials/network, git mutations, Assets/docs edits or subagents. Write only Library/TftAnalyzerValidation/collection-final-qa-report.md. Inspect actual artifacts and code rather than treating the following expected values as facts.

Root is running final full suite sequentially now. Final evidence when available:
- collection-legacy-final-compile.json: status completed OR up_to_date, failed=false, compilationFailed=false, errors empty. up_to_date is a legitimate package terminal status; source RecompileCommand.cs explicitly supports it. It means no new compilation was needed, not a newly executed compile.
- collection-legacy-final-full.json: current complete default EditMode result, expected467. Inner success, Total>0, Failed0 required. Do not approve while incomplete. Earlier458 result predates the legacy correction.
- collection-legacy-full-before.json, collection-legacy-full-after.json, collection-legacy-full-windows-check.json: exact EditorWindow/native ContainerWindow IDs and focus preservation.
- collection-legacy-final-ui.json: historical latest focused UI90/90 (2026-10-16); current full suite also includes these tests.
- collection-visual-final.json, collection-visual-command.json, collection-visual-fallback-cleanup.json: current normal bounded tracked wrapper, real shipped verified schema1 catalog, injected unavailable fake credentials, no network. Root visually inspected current940 and500 PNGs on2026-10-18: both show target collection UI, full identity and persistent footer; small viewport scrolls. Prior500 capture showed another screen; root is not claiming that intermittent pixel capture issue root cause has been fixed. Every capture requires human/agent image inspection separately from JSON geometry success.
- collection-visual-inspection.json: root records current image inspection and SHA256, separate from wrapper Passed.
- collection-visual-failure-cleanup.json and collection-visual-initialization-failure-cleanup.json: refreshed root controlled postShow/preShow failure checks. Both preserve prior IDs/frame/focus and leave owned0. Do not infer OS crash/disconnection/real timeout validation.
- collection-protected-check.json: compare all1386 baseline hashes, including user Volume and Generated. No protected files should be staged.
- collection-console-final.json: native current groundTruth counts, distinguish historical cumulative intentionalRED/errors. No clearing unrelated console/user state.

Match requirements to actual tests/code: cap1..1000 and current draft/rebuild, fullidentity confirmation and immutable running pair including explicit verified legacyidentity, numeric progress, busy/cancel/close/stale callbacks, controlled401/403/429/manual retry, preserved client quota, real fake-transport anonymizer/store path, no secret values or raw identifiers. Prior intended RED histories are not current failed tests.

Only Task3 plus user-authorized plan re-review/window cleanup/docs. Analysis/CSV/compliance/startup scheduler and Hermes daily automation are future work. Confirm remaining limitations: real network/real credentials/native confirmation dialog clicks not executed. Root will update checkpoint and final reports after QA; identify any stale documentation that must be corrected, but do not edit it.

Return concrete blockers vs nonblocking observations, TODO coverage and execution/limitations verdict. Requested model GPT-6.1 Sol/High per AGENTS complexity policy; actual runtime identity/reasoning/usage not independently observable.
