# Final scoped collection/window review

Date: 2026-10-16. Scope: final scheduling/ownership correction already inspected, plus the new legacy catalog identity admission/capture/display contract and its associated regressions. Earlier feature/draft reviews are not repeated. Requested reviewer: Astra / High; actual runtime model/reasoning independently unavailable and not verified.

## Declined to judge

- Live credentials/network, future analysis/export/scheduler features, and unrelated product changes are outside this scoped review.
- This reviewer executed no Unity commands/tests/manual captures. Evidence below was produced by root.
- Final full-suite and normal/both-failure manual artifacts are pending at this report snapshot; no approval of absent evidence is implied.

## Code verdict

APPROVED at the code-review level: no unresolved concrete finding in this delta. Final execution gate remains pending the artifacts noted below.

The verified loader row now passes its StaticCatalog and CatalogIdentity together into the collection VM. Schema 1 cannot start from a null manifest identity or an inferred UI profile: it requires an explicit matching full official-only identity. Schema 2 retains its manifest identity by default and rejects incompatible explicit metadata. Both validate SetNumber independently because CatalogIdentity.Equals intentionally does not compare it. Patch/source version/profile mismatches cannot authorize collection.

Catalog and identity are snapshotted together before approval. Request construction, confirmation and the active label use the captured full identity during a run, while later selection is held for the next run and applied after settlement. The backend MatchAnonymizer constructor already accepts the matching explicit legacy identity and separately checks SetNumber; no backend expansion is introduced.

Regressions cover verified schema-1 window selection/label/approval/request flow, missing and mismatched identities including profile and set number for both schemas, running snapshot versus next selection, and the real offline adapter/anonymizer/store path parameterized across schema 1 and 2.

The previously reviewed scheduling delta uses one-shot EditorApplication.update callbacks in both bindings: coalesced registration, unsubscribe before render/reset, and unsubscribe on disposal. Its test barrier uses the same actual event and a five-second deadline, removes itself in callback/finally, and opens no Inspector. Owned shown windows use Close; unshown initialization failures and exact surviving objects use destruction fallback, with cleanup failures observable. Manual scripts match exact ID plus random per-run name and report both EditorWindow and native ContainerWindow preservation.

## Execution evidence inspected so far

- collection-render-final-compile.json: successful compilation.
- collection-render-final-ui.json: 81/81 passed after scheduling/cleanup correction (before legacy additions).
- collection-legacy-final-compile.json: completed, failed=false, compilationFailed=false, errors empty.
- collection-legacy-final-ui.json: 90/90 passed, 0 failed/skipped/inconclusive.
- collection-legacy-ui-before.json and collection-legacy-ui-after.json: same nine EditorWindow IDs (50490 through 50506 in increments of two), same sole ContainerWindow 50466, focus 0 before/after. No added or missing managed/native window appears in these snapshots.

Pending: latest complete EditMode result and current tracked normal/post-Show-failure/pre-Show-failure visual cleanup reports, including their command/fallback success and owned-window/native-container residual checks. Historical failed attempts do not substitute for these final artifacts.

Only this report was written. No source, git state, generated data, Unity state or credentials were modified by this reviewer.
