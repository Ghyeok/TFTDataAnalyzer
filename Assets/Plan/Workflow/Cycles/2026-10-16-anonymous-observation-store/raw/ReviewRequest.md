# Task 4 fresh independent review request

Review once, read-only. Scope is approved Phase2 Task4 anonymous persistence/deduplication/index recovery. Base origin/main 678b05ada2c9fedfb974f19418f011103dc22659; new source/test files are untracked, inspect directly. Do not run Unity, read credentials/environment seeds, use network/live APIs, change files, or spawn agents. No Hermes delegation. You may run safe local read-only probes against compiled assemblies if useful; never write raw identities or payloads. Review actual defects, not generic hardening wishes.

Read these exact files:
- Assets/TFTAnalyzer/Editor/Persistence/AnalyzerStorePaths.cs
- Assets/TFTAnalyzer/Editor/Persistence/ObservationStore.cs
- Assets/TFTAnalyzer/Editor/Persistence/ProcessedMatchIndex.cs
- Assets/TFTAnalyzer/Tests/EditMode/Persistence/ObservationStoreTests.cs
- Assets/TFTAnalyzer/Core/Analytics/{AnonymousMatchObservation,AnonymousParticipant,AnonymousUnit,AnonymousTrait,MetricScope}.cs
- Assets/TFTAnalyzer/Core/Domain/{CatalogIdentity,PatchKey,CatalogSourceProfile}.cs
- Task4 only in Assets/Plan/Implementation/2026-09-17-tft-match-analytics.md; spec sections5.6/8.4/13 in Assets/Plan/TFTDataPipeline.md
- Library/TftAnalyzerValidation/phase2-task4-progress.md (decisions ledger)

Contract: NDJSON default Library/TftAnalyzerStore or explicit project-external override. Only accepted AnonymousMatchObservation enters store; credential/player/raw-response APIs absent. Hash filenames separate full catalog/profile/region/queue/tier scopes. Match hashes deduplicate globally per store (first accepted scope wins; separate stores for reprocessing). Only hash-only derived index persisted atomically. Source logs authoritative, index cannot suppress real observations or authorize duplicates. Cancellation before flush rolls back; after durable flush commit wins. Index failure preserves committed data and next access recovers without duplication. Incomplete crash tails may truncate; corrupt complete records must fail closed and survive. Per-instance async gate and transient exclusive OS lease prevent simultaneous writers; competing instances may fail safely rather than wait. Whole-store scan each operation intentionally favors correctness; O(total records) per operation, memory snapshot and no malicious same-account race resistance are documented limitations. No aggregation/collection/UI expansion.

Evidence owned by parent: initial missing types observed actual Unity compiler RED. First32 focused tests actual Unity GREEN after correcting test-only NUnit ThrowsAsync blocking the Unity context.3 extra tests added for read/write reentrancy, active OS lease, oversized record rejection; full suite pending. Parent must verify final compile/full suite and artifact privacy, not assume your source review establishes execution. The initial hang required approved restart of exact EditorPID23660, not a data loss in store. No live API/key/seed access.

Requested review model per userAGENTS sensitive-data guidance: gpt-6-astra, High. Record only observable identity/usage; don't assert actual model switching if unobservable.

Return Critical/Important/Minor actionable defects with source:line, concrete scenario and recommended correction. Separately list behavior you decline to judge. Single review; no review loop. Parent handles one focused RED/GREEN correction pass for established Critical/Important defects and full regression.
