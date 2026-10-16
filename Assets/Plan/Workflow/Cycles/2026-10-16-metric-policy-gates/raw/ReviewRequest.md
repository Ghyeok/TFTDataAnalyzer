# Task 6 single independent review request

Perform one fresh read-only review of approved Task6 minimum100/policy and safe presentation payload. Base origin/main afcaebb74af3e20fcc5cca8ce7ab01b37518fb22, branch codex/metric-policy-gates. New files untracked, inspect directly. Do not modify files, run Unity, read credentials/seeds/environment secrets, access network/live APIs, or spawn agents. No Hermes. Optional safe read-only compiled-assembly probes are allowed. Return established actionable Critical/Important/Minor defects with source:line and triggering scenario, separately behavior declined to judge. Judge approved repository requirements, not claims about current external policy compliance.

Read exact sources:
- Assets/TFTAnalyzer/Core/Analytics/MetricPolicy.cs
- Assets/TFTAnalyzer/Core/Analytics/MetricPresentation.cs
- Assets/TFTAnalyzer/Core/Analytics/{AggregateMetric,PairwiseMetric,AggregateBuilder}.cs (augment provenance and schema2 additions)
- Assets/TFTAnalyzer/Editor/Persistence/{AggregateStore,AnalyzerStorePaths}.cs (schema2 and augment IDs)
- Assets/TFTAnalyzer/Tests/EditMode/Analytics/MetricPolicyTests.cs
- Assets/TFTAnalyzer/Tests/EditMode/Persistence/AggregateStoreTests.cs (two new provenance/schema tests and version expectation)
- Supporting immutable scope/catalog/fingerprint contracts as needed.
- Task6 in Assets/Plan/Implementation/2026-09-17-tft-match-analytics.md; spec2.2,3.6,10.3; Library/TftAnalyzerValidation/phase2-task6-progress.md ledger.

Contract and necessary plan repairs:
1. Task5 fingerprints are irreversible and AugmentRecord has no policy tag. Therefore carry copied sorted augment-ID provenance from accepted observation boards into AggregateMetric and both pair sides; persist it in new aggregate schema2, preserve schema1 files. No static catalog/source/observation format change or catalog-byte rewrite. Old schema1 must rebuild before policy use; there is no aggregate reader yet.
2. MetricPolicyMetadata holds copied explicit Standard/LegendBased/Legend/Unknown classifications bound to complete CatalogIdentity plus set number. Real classification provenance/configuration is a trusted integration responsibility, not inferred from ID/description and not implemented as a production registry here. Missing/unknown/mismatched classification suppresses augmented rates. No-augment metrics can be visible without metadata. Known IDs must exist in selected StaticCatalog. Exact catalog/profile/number identity and safe region/tier/numeric queue labels checked before scope echo; invalid context suppresses scope and values.
3. Immutable minimum>=100, reject lower config; caller may raise it. Policy exclusion precedes sample exclusion. Average/sample allowed below100 with no ratio/numerator/denominator/interval payload. LegendBased blocks ratios while ordinary average remains; Legend suppresses average too. Unknown augment IDs suppress average; missing/mismatched classification keeps ordinary average but rates blocked.
4. Participant and pair overloads use the same gate; pair samples count eligible lobbies only, exclude ambiguous-lobby count. Both augment lists checked. Only fixed historical_observation / relative_placement_advantage copy keys and approved ratio keys; no arbitrary metric selector/prediction API.
5. MetricPresentation and PresentedRatio are opt-in read-only payloads. Suppressed results have empty Ratios; no raw metric/observation/augment-policy references or source identifiers are included. Scope is validated immutable catalog/filter data, fingerprints are composition hashes. Sample counts, approved averages, periods and ambiguity counts remain; independent inference from permitted averages is not a promised privacy property. ToJson uses JsonSerializer.Create with explicit settings so global JsonConvert.DefaultSettings cannot inject types/converters/contract changes. This API returns a DTO/string only; no actual UI/export-file operations introduced.

Parent evidence: actual test-first Unity RED for missing policy types, then clean compiler and focused MetricPolicyTests31/31 passed. Full regression pending (expected340=307baseline+31policy+2provenance), parent owns actual execution and artifacts. Prior real catalog bytes and user volume SHA256 must remain unchanged. Raw aggregate caches remain internal data; all future user-facing consumers must use the policy DTO. Task7 collection/UI/current external policy verification are outside scope.

Requested review model per user data integrity/privacy guidance: gpt-6-astra High. Actual runtime model/effort/usage unobservable, do not claim verified switching. Single final review, parent handles one established-defect RED/GREEN correction pass if needed.
