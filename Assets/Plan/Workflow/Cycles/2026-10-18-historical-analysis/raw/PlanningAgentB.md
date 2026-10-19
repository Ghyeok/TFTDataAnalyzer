# Independent technical planning review B — Phase 3 Task 4

Date: 2026-10-18. Requested role/model: planning reviewer B, Sol / High. Runtime model and reasoning metadata are not exposed to this reviewer; no switch is claimed. Usage and wall-clock totals are unavailable. Review was read-only except this report. No Unity, network, environment, Git mutations, or subagents were used. Product implementation and tests were not run.

## Recommendation

Proceed with the existing Task 4 historical-analysis scope, using a small local rebuild bridge. The accepted plan already authorizes verified active-catalog selection, immutable filter intersection, aggregate/pairwise presentation, policy suppression, and personal-research local CSV. Their routine connection does not require a new permission round merely because the existing implementation lacks an adapter.

There are two genuine current gaps: `AggregateStore` only saves, and saved aggregate records contain composition hashes and augment IDs but no unit/trait/item membership. There is also no production provider of reviewed augment classification metadata. Do not invent a pre-existing aggregate-reader/filter or classification API. Do not expand Task 4 into aggregate-schema migration, automatic policy classification, network policy retrieval, public publishing, registration settings, or the Task 6 scheduler.

## Verified existing interfaces

- `Core/Analytics/AggregateBuilder.cs`: `AggregateBuilder.Build(IEnumerable<AnonymousMatchObservation>)` returns immutable `AggregateSnapshot.Scopes`. A scope contains `Scope`, `MatchCount`, `FirstPlayedAt`, `LastPlayedAt`, data/game versions, `Metrics`, and `Pairs`. All accepted lobbies must retain all eight distinct placements. Equivalent repeated hashes deduplicate; conflicting duplicates fail.
- `Core/Analytics/AggregateMetric.cs`: metric has complete scope, fingerprint, sorted augment IDs, raw count/counters, average, and observed period. It does not have unit/trait/item IDs.
- `Core/Analytics/PairwiseMetric.cs`: canonical unordered fingerprint pair; eligible-lobby `Count`, `FirstHigherCount`, `AmbiguousLobbyCount`, endpoint augment IDs, period. Duplicate endpoint compositions in a lobby exclude that lobby from its denominator. No participant/cartesian-product reconstruction is needed or appropriate.
- `Core/Analytics/CompositionFingerprintBuilder.cs`: fingerprint includes unit IDs, unit tiers, equipped item IDs, active trait IDs, augment IDs. Placement and inactive traits are excluded. Hashes cannot be decoded into composition membership.
- `Editor/Persistence/AggregateStore.cs`: only public operation is `SaveAsync(ScopeAggregate, CancellationToken)`. Schema 2 persists the above raw counters and augment provenance. There is no public load/read/rebuild API. `AnalyzerStorePaths.AggregatePath(scope, schemaVersion)` supplies complete-scope partitioning.
- `Editor/Persistence/ObservationStore.cs`: `ReadAllAsync(CancellationToken)` supplies validated anonymous snapshots. This is a recovery read: it acquires a lease, rebuilds/saves the processed index, and truncates unterminated crash tails; malformed complete records fail. It reads all stored scopes. Do not describe it as a byte-preserving filesystem read.
- `Core/Analytics/MetricPolicy.cs`: `Evaluate(AggregateMetric, StaticCatalog)` and `Evaluate(PairwiseMetric, StaticCatalog)` produce `MetricPresentation`. Minimum cannot drop below 100. Policy classification is optional constructor-supplied `MetricPolicyMetadata` bound to exact catalog identity; no production loader currently constructs it.
- `Core/Analytics/MetricPresentation.cs`: allowlisted `State`, `ReasonCode`, `CopyKey`, scope, sample, nullable average, endpoint hashes, ambiguous count, observed period and `Ratios`. Every suppressed decision has an empty ratio list. Ratios alone contain approved numerator, denominator, rate and Wilson interval. UI/export must not retain raw aggregates for fallback ratios.
- `Core/Domain/AugmentRecord.cs`: no reviewed policy classification field; tier/name/description do not authorize classification.
- `Editor/UI/Data/PatchRowViewModel.cs`: carries verified `Catalog` and `Identity` separately, and `IsSelectable` gates admission.
- `Editor/UI/TftDataAnalyzerWindow.cs`: DataManager VM survives UI reconstruction; bindings are disposed/recreated. `SyncCollectionCatalog` selects the verified row and passes both values to collection. `OnDisable` disposes bindings/models and unsubscribes. Analysis currently has placeholder text only.
- `Editor/UI/Collection/CollectionViewModel.cs`: `SetCatalog`/`MatchesCatalog` is a useful exact selection precedent. Schema 1 requires separately supplied official-only identity. `CatalogIdentity.Equals` omits `SetNumber`; admission explicitly checks it. New analysis code must also check it for scope matching, because `MetricScope.Equals` inherits that omission.
- `Editor/UI/Collection/CollectionTab.cs`: view events use a coalesced one-shot `EditorApplication.update`, removed before render and on disposal; no polling or Inspector dependency.

## Minimal bridge and data ownership

Create an analysis service next to the view-model. Proposed names below are new code, not existing APIs:

1. An explicit local load/rebuild command snapshots the verified `(StaticCatalog, CatalogIdentity)` and reads `ObservationStore.ReadAllAsync` once. No view creation, filter change, tab switch, or CSV export invokes Riot/HTTP. A bounded local load on first explicit user action makes disk side effects clear and avoids unnecessary work on reconstruction.
2. Use `AggregateBuilder.Build` on complete observations, never on participant-filtered/incomplete lobbies. Retain separate complete-scope records; do not merge counts across regions, queues, tier labels, patch, source version/profile or set number to get above the sample floor.
3. Derive an immutable in-memory `fingerprint -> composition membership` index using the same `CompositionFingerprintBuilder.Build(participant)` from those observations. Copy only unit IDs, active trait IDs, item IDs and augment IDs, with scope identity if the index is keyed per scope. Do not retain match hashes, placement, last round, or participant references in UI rows. Grouping membership may be deduplicated; do not inflate counts while deriving the index.
4. Save each complete rebuilt scope via existing `AggregateStore.SaveAsync` if consuming the planned aggregate cache. It remains a derived cache. No schema bump or reader is needed to deliver Task 4 because each explicit refresh can rebuild from the anonymous source of truth. Cache-save failure must not be silently reported as full success; surface a fixed safe error or a distinct loaded-but-not-saved warning if that is the documented outcome.
5. Publish one result snapshot atomically to the VM only after the requested operation has completed and the operation token/catalog generation still matches. Dispose/cancel prevents late completions from changing rows or status. Work should run without Unity APIs off the main thread; the existing store may otherwise perform synchronous recovery work before asynchronous enumeration yields.

This bridge is ordinary integration needed to fulfill the approved filters and local persistence plan. A durable composition-rich aggregate format, standalone aggregate-only reader/import feature, or migration of existing schema files is avoidable and belongs to a separate scope if desired later.

Concrete existing API wiring (inside the new local adapter; `projectRoot` and `token` are injected):

```csharp
var paths = new AnalyzerStorePaths(projectRoot);
var observationStore = new ObservationStore(paths);
var aggregateStore = new AggregateStore(paths);
var observations = new List<AnonymousMatchObservation>();
await foreach (var observation in observationStore.ReadAllAsync(token))
    observations.Add(observation);
var snapshot = AggregateBuilder.Build(observations);
foreach (var scope in snapshot.Scopes)
    await aggregateStore.SaveAsync(scope, token);
foreach (var participant in observations.SelectMany(value => value.Participants))
{
    CompositionFingerprint fingerprint = CompositionFingerprintBuilder.Build(participant);
    string key = fingerprint.Value;
    // Copy membership under key; no attempt to decode key or retain participant in UI.
}
var policy = new MetricPolicy(); // Existing fail-closed classification behavior.
// After exact scope admission using selectedIdentity plus SetNumber:
MetricPresentation presentation = policy.Evaluate(snapshot.Scopes[0].Metrics[0], selectedCatalog);
```

The final indexing expression is illustrative only and must be guarded for empty snapshots/metrics; production should enumerate matching scopes/rows. The supplied verified `selectedIdentity` is retained independently of `selectedCatalog.Manifest.Identity`, especially for schema 1. `new CompositionFingerprint(string)` validates an already-known 64-character SHA-256 value; it is not a parser for composition data.

## Filter and state contract

- The active catalog bounds all results by full identity plus set number. Patch filter is consistent with that selected catalog; it cannot silently select a second patch without that patch's verified catalog. Showing a single available patch is preferable to pretending cross-patch policy evaluation is supported.
- Region/queue/tier choices come from the loaded valid scope values; an All choice displays multiple explicitly scoped rows, not a pooled metric. Tier remains the existing caller-supplied recording label, never a verified player rank.
- Unit, active trait, item and augment selections are AND conditions on one composition's membership. A unit/item selected separately means the composition contains each; the existing plan does not authorize claiming the item is equipped specifically on that unit unless that relation is expressly retained and labeled.
- Pair filtering needs an explicit consistent rule. Recommended: retain a pair when at least one endpoint satisfies the full composition intersection; the other endpoint is the comparison composition. Label this behavior. Never accept a pair because the selected unit exists only on A and the selected item only on B. An alternative both-endpoints rule is a routine local design choice if clearly labeled/tested; do not silently choose mixed semantics between controls.
- Each filter derives from the original immutable loaded snapshot, so clearing a filter restores rows without I/O or loss of samples. Aggregate values never change merely because unrelated rows were hidden.
- Existing raw-filter strings are not an output surface. Choices should be trusted static IDs from the verified catalog and/or safely validated loaded scope values. Unknown/rejected values must not appear verbatim in errors, dropdowns or exports.
- Empty state distinguishes no selection, not yet loaded, no stored observations, no rows satisfying current filters, local failure and cancellation. Show selected scope, plus the actual observed `FirstPlayedAt`/`LastPlayedAt` where available; use an explicit unavailable period when there are no observations. For a composition-filtered empty set, a labeled loaded-scope period remains valid context. Never invent retrieval timestamps.
- Catalog selection changes invalidate displayed old-catalog results and export eligibility immediately, cancel any pending load, and preserve/normalize compatible filter drafts. An old load may not replace new selection status.
- Rebuild UI retains VM/filter/terminal state, swaps binding subscriptions, and does not load/restart/export. Close disposes and cancels; detached visual elements do not receive late updates. Event scheduling follows the existing one-shot update contract.

## Policy and presentation

Production should use the existing fail-closed `MetricPolicy`; absent reviewed augment metadata, augmented composition rates stay blocked with an understandable policy note. Explicit metadata injection is appropriate in fabricated tests. Do not mark all catalog augments Standard, infer from IDs/descriptions, or silently strip augment IDs to make rates visible.

`MetricRowViewModel` should hold only `MetricPresentation` and approved display data. Preserve the exact canonical pair orientation when showing numerator/denominator; the numerator is First's higher-placement count. If sorting endpoints for display, reorientation must not manufacture a ratio from raw counters. Easiest is keeping canonical endpoint ordering.

- Sample 99: approved sample/average/state and period may appear; ratios, percentages, numerator/denominator and interval are absent.
- Sample 100: approved ratio, numerator/denominator and 95% Wilson interval appear.
- Policy-blocked average is nullable by policy; `Legend` and unknown augment cases must not recover it from raw counters. Some other policy blocks allow average; honor the DTO rather than applying a blanket replacement rule.
- Pair label: `상대 배치 우위율`; tooltip: `같은 로비의 최종 순위 비교이며 실제 전투 승률이 아닙니다.`
- Explain eligible lobbies and ambiguous exclusions; do not call pair Count participant count or count exclusions toward the sample minimum.
- Copy scan tests reject positive predictive/combat-rate claims while allowing the specific documented explanatory negation and existing historical disclaimer.

## Local CSV boundary

Use a writer that accepts `IEnumerable<MetricPresentation>` (or only the rows' presentation DTOs), not observations or raw aggregate metrics. Export a snapshot of current visible approved presentations. The save-panel interaction stays in the binding/window and the VM/writer has no `AssetDatabase` or HTTP dependency.

Include full scope including source identity, sample count, observed period, metric name, nullable average, numerator, denominator, estimate, lower, upper, state/policy note, and personal-research purpose. For empty Ratios, output a suppression row with every ratio field blank. Do not reconstruct counters or export a hidden ratio through formatting. Use invariant numeric formatting, UTC timestamps, UTF-8, CSV escaping for comma/quote/newline, and protect text fields from spreadsheet formula interpretation where values originate in stored/catalog text. Saving cancellation does not change analysis state; writing failure uses a safe fixed diagnostic. Task 4 local CSV must remain available without API credentials or registration status. Public export is excluded.

## Exact proposed implementation boundaries

Required original files:

- Add `Assets/TFTAnalyzer/Editor/UI/Analysis/AnalysisViewModel.cs`: selection admission, immutable loaded snapshot/filter state, local operation lifecycle, safe error/status, rows/export eligibility, disposal.
- Add `Assets/TFTAnalyzer/Editor/UI/Analysis/MetricRowViewModel.cs`: presentation-only formatting, policy copy and pair label/tooltips.
- Modify `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`: create/preserve/dispose analysis VM and replaceable binding; forward the same verified row pair as collection. Avoid broad refactor of working data/collection flows.
- Modify `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml` and `.uss`: usable narrow-width analysis controls, scope/period/status, aggregate and pairwise lists, local load/cancel/export controls.
- Add `Assets/TFTAnalyzer/Tests/EditMode/UI/AnalysisViewModelTests.cs` for meaningful state/filter/policy tests.

Small necessary adapters beside these files:

- Add `Assets/TFTAnalyzer/Editor/UI/Analysis/IAnalysisService.cs`: injectable local rebuild boundary only.
- Add `Assets/TFTAnalyzer/Editor/UI/Analysis/AnalysisService.cs`: existing observation-reader, builder, aggregate saver bridge. A small immutable snapshot/composition membership type may live in this file initially; avoid a general query framework.
- Add `Assets/TFTAnalyzer/Editor/UI/Analysis/AnalysisTab.cs`: disposable UI binding and save-path prompt; same one-shot render event pattern as CollectionTab.
- Add `Assets/TFTAnalyzer/Editor/UI/Analysis/MetricCsvExporter.cs`: presentation-only CSV encoding/writing; keep filesystem out of VM formatting.
- Add focused test files `AnalysisServiceTests.cs`, `MetricCsvExporterTests.cs`, `AnalysisWindowTests.cs` under the existing EditMode UI directory if clarity warrants them instead of growing one test class. Add Unity meta files for new Assets content.

No product changes are needed in Core analytics, current AggregateStore schema, ObservationStore, static generated catalogs, compliance, collection network code, `Volume`, or generated data. Existing owned-window visual helper may receive a narrowly scoped analysis scenario if root chooses to capture Task 4; don't reuse old split open/capture scripts.

## Meaningful RED and integration evidence

1. Before implementation, add tests that fail for missing analysis behavior: full verified schema-2 and official-only schema-1 pairs admitted; missing schema-1 identity and mismatches in every identity field/set number rejected. Result source scopes with matching `CatalogIdentity.Equals` but wrong set number are excluded.
2. Composition fixture contains deliberately crossed memberships: A has selected unit + item but wrong trait, B has selected trait + augment but wrong unit, C has all four, D includes only inactive selected trait. Only C survives all four AND filters. Clearing each filter restores the expected source rows; the fake service load count stays one. Scope dimensions and active identity remain separate.
3. Pair fixture A matches only unit, B only item: pair must not pass combined filters. A complete matching endpoint versus an unmatching opponent follows documented endpoint rule. Numerator orientation, eligible lobby denominator and ambiguity count match independently specified expected values, with no filter-induced reaggregation.
4. View rows/CSV at sample 99 and 100, raised floor, policy-blocked Standard-unclassified/Legend/LegendBased/unknown/mismatched metadata cases; both pair endpoints can cause suppression. Assert no percentage/count ratio fields survive suppressed presentation or CSV. Assert nullable average remains absent where policy denies it.
5. Local bridge integration uses a temporary isolated store: append fabricated anonymous complete matches through actual ObservationStore, invoke actual builder and aggregate saver through service, select/filter rows, generate/read CSV. Assert exact scope/period/count, expected derived-cache file, no observation mutation on the clean-input path, no secret/sentinel identifier export, no network transport dependency/calls. Malformed complete observation fails safely; no partial UI snapshot is published.
6. Async tests control TaskCompletionSource completions: catalog A load pending → select B → complete A; A cannot publish rows/export. Close/dispose/cancel then late success/error cannot alter UI status or stale elements. Rebinding while one load is pending does not create a second load. Start-after-cancel only when previous operation is actually settled or explicitly superseded with a generation guard.
7. Actual UI event test: verified catalog selection through window → explicit local load → visible filters/rows → CSV callback → repeated CreateGUI → close; no hidden collection/API request, no duplicate callbacks. Missing assets and recovery preserve retained VM state while disposing old bindings.
8. Root-run window evidence must use owned windows, finally cleanup and before/after EditorWindow + native ContainerWindow comparison as required by `docs/unity-editor-validation.md`. Test no selection, empty/suppressed/visible data, narrow/wide rendering, loading cancellation/failure. AI review is not screenshot or runtime evidence.
9. After relevant focused tests pass and final code is fixed, run one full EditMode suite and inspect compilation/Console as the established plan requires. Do not reuse prior 467-test results as this change's validation.

## Approval interpretation and exclusions

User continuation plus existing Task 4 scope authorizes these routine local adapters, explicit refresh, filter endpoint semantics with clear UI/test contract, and the DTO-only CSV connection. Master should record the refined boundaries and evidence checklist without claiming new external authority. If the active cycle has genuinely unapproved new product requirements, the workflow's planning approval gate applies; it must be tied to those concrete changes, not every missing implementation detail.

Explicit new approval would be needed for public publishing/export, new network collection or policy classification source, blanket policy exceptions, cross-patch pooling semantics, legacy/static catalog modification, future prediction/combat surfaces, Task 5/6 feature expansion, or Hermes work outside its collaboration folder. Preserve current uncommitted Hermes monitoring documents and `Volume`/generated assets.

Conclusion: existing analytics are sufficient for Task 4 values and policy enforcement; a local anonymous-observation rebuild plus membership index is the smallest honest composition-filter bridge. Fail-closed augment suppression is expected product behavior until reviewed metadata actually exists, not a reason to fabricate authorization or leave all historical views unimplemented.
