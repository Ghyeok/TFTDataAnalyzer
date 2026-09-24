# Static Catalog Composite Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:test-driven-development`. The Development Lead must create exactly one implementation agent and one read-only review agent for this cycle. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Make static catalog identity include Set, TFT patch, Data Dragon source version, and source profile while preserving the committed legacy official-only catalog byte-for-byte.

**Architecture:** Introduce one immutable `CatalogIdentity` shared by import requests, manifests, paths, indexes, and UI status. New outputs use a composite directory. A narrowly scoped legacy resolver recognizes safe schema-1 official-only outputs without moving or rewriting them. Publishing verifies actual catalog bytes before reporting idempotency.

**Tech Stack:** Unity 6000.3.8f1, C#, Newtonsoft Json 3.2.2, Unity Test Framework 1.6.0, Unity Editor APIs.

**Spec:** `Assets/Plan/Workflow/Cycles/2026-09-23-source-profile-identity/refined/PlanningReview.md`

## Global Constraints

- Do not modify, move, or delete the committed `Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2` catalog, manifest, or `.meta` files.
- Do not modify the unrelated `Assets/DefaultVolumeProfile.asset` user change.
- New identity is exactly `(setId, patchId, sourceGameVersion, sourceProfile)`.
- Allowed stable profile IDs are exactly `official-only` and `official-plus-communitydragon`.
- CommunityDragon remains explicit opt-in and defaults off for every import run.
- Existing schema-1 output is inferred as legacy official-only only when its manifest has no CommunityDragon version or supplemental source.
- Default tests use fakes and never require network.
- No Match API, analytics, full UI Toolkit, auto-update, external publishing, or data deletion work is in scope.

## Review Focus

- A schema-1 directory whose sources include supplemental data must not be silently classified as official-only.
- Different Data Dragon versions containing dots must generate safe, distinct paths.
- An index update must never remove a sibling profile or sibling source version.
- Manifest/catalog tampering must not return `Idempotent=true`.
- Cancellation or publication failure must leave existing outputs and the existing index unchanged.

---

### Task 1: Define Composite Catalog Identity

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Domain/CatalogSourceProfile.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/CatalogIdentity.cs`
- Modify: `Assets/TFTAnalyzer/Core/Domain/PatchManifest.cs`
- Modify: `Assets/TFTAnalyzer/Editor/Ingestion/StaticImportRequest.cs`
- Modify: adapters, fixtures, and constructors that create `PatchManifest`
- Test: `Assets/TFTAnalyzer/Tests/EditMode/Domain/CatalogIdentityTests.cs`
- Test: existing serialization and ingestion tests

**Interfaces:**
- Produces: `CatalogIdentity(PatchKey patch, CatalogSourceProfile sourceProfile)`.
- Produces: stable profile IDs `official-only` and `official-plus-communitydragon`.
- Produces: `bool IncludesCommunityDragon` derived only from `SourceProfile`.
- `StaticImportRequest` consumes a `CatalogIdentity`; its `Patch` and `IncludeCommunityDragon` values are derived views.

- [x] **Step 1: Write failing identity tests**

Assert two identities differing only by source version are unequal, two differing only by profile are unequal, stable IDs round-trip, and an unknown stable ID throws a typed argument/serialization error.

- [x] **Step 2: Run focused tests and verify RED**

Run the `CatalogIdentityTests` filter. Expected: compile failure for the missing identity types.

- [x] **Step 3: Implement the minimum domain contract**

Use constructor-validated immutable values. Do not allow callers to pass a profile plus a contradictory CommunityDragon boolean.

- [x] **Step 4: Add explicit manifest identity and legacy default handling**

New manifests use schema version 2 and serialize the stable profile ID. Schema-1 deserialization may map to official-only only through the legacy resolver in Task 3, not through a universal silent default.

- [ ] **Step 5: Run focused and full default EditMode tests**

Expected: all tests pass, no network.

---

### Task 2: Key Paths and Indexes by Composite Identity

**Files:**
- Modify: `Assets/TFTAnalyzer/Editor/Persistence/StaticCatalogPaths.cs`
- Modify: `Assets/TFTAnalyzer/Editor/Persistence/CatalogIndex.cs`
- Modify: `Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs`
- Test: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Persistence/CatalogIndexTests.cs`

**Interfaces:**
- `StaticCatalogPaths.CanonicalFinalDirectory(CatalogIdentity identity)` returns `{generated}/{setId}/{patchId}/{sourceGameVersion}/{profileId}`.
- `StaticCatalogPaths.CatalogAssetPath(CatalogIdentity identity)` uses the same segments.
- `ICatalogIndexWriter.Upsert` consumes `CatalogIdentity`.
- `CatalogIndexEntry` exposes the four identity fields.

- [x] **Step 1: Write failing path coexistence tests**

Use literal expected paths for two profiles and two source versions. Add traversal/separator rejection for every segment.

- [x] **Step 2: Write failing index coexistence tests**

Insert four combinations for one set/patch and assert all four remain. Reinsert one exact identity and assert only that entry is replaced.

- [x] **Step 3: Run focused tests and verify RED**

Expected failures must name the missing composite key behavior.

- [x] **Step 4: Implement paths and index keying**

Keep deterministic ordering by set number, patch ID, source version, then profile stable ID.

- [x] **Step 5: Update pipeline call sites and fakes**

No code may select or remove an index entry using only set ID and patch ID.

- [ ] **Step 6: Run focused and full default suites**

Expected: all pass, no network.

---

### Task 3: Preserve Legacy Output and Strengthen Publication Integrity

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Persistence/LegacyCatalogResolver.cs`
- Modify: `Assets/TFTAnalyzer/Editor/Persistence/CatalogAssetWriter.cs`
- Modify: `Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs`
- Test: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Persistence/LegacyCatalogResolverTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/Fixtures/Static/legacy-official-manifest.json`

**Interfaces:**
- Produces: a resolution result `NotFound`, `CompatibleOfficialLegacy`, or `AmbiguousLegacy` plus the resolved path.
- A compatible legacy result requires matching set, patch, source version, no CommunityDragon version, and no supplemental source stamps.
- `Publish` verifies the actual existing `catalog.json` SHA-256 against its manifest before comparing with staged content.

- [x] **Step 1: Capture pre-change SHA-256 values**

Record hashes for the committed legacy catalog, manifest, and their `.meta` files in the development report before implementation touches publishing code.

- [x] **Step 2: Write failing legacy classification tests**

Cover safe official legacy, supplemental legacy, missing manifest, mismatched source version, and malformed manifest.

- [x] **Step 3: Write failing tamper and failure-preservation tests**

Cover changed catalog bytes with unchanged manifest, same identity/different content, cancellation, invalid icon, and index-writer exception. Assert no existing output or sibling index entry changes and no false idempotency.

- [x] **Step 4: Verify RED for the intended reasons**

Do not accept parse errors caused by a malformed fixture when testing identity behavior.

- [x] **Step 5: Implement resolver and actual-byte integrity checks**

Do not migrate or rewrite legacy files. Any partial-publish condition must return/throw a recoverable error describing paths without response bodies.

- [ ] **Step 6: Recompute protected file hashes and run suites**

Expected: hashes are identical to Step 1 and all focused/full tests pass.

---

### Task 4: Expose Identity in the Import Flow

**Files:**
- Modify: `Assets/TFTAnalyzer/Editor/Menu/StaticDataImportMenu.cs`
- Create or modify testable presentation/state helper under `Assets/TFTAnalyzer/Editor/Menu/`
- Test: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticImportPresentationTests.cs`

**Interfaces:**
- Produces a confirmation summary containing set ID, patch ID, source version, profile stable ID, and resolved target path.
- CommunityDragon selection resets to false after success, cancellation, and failure.

- [x] **Step 1: Write failing presentation and consent tests**

Assert default official-only, exact summary fields, zero transport calls when confirmation is cancelled, zero CommunityDragon-host calls for official-only, and reset after every terminal state.

- [x] **Step 2: Verify RED**

Use a fake command/state boundary rather than asserting Unity framework internals.

- [x] **Step 3: Implement the minimum testable state boundary and UI binding**

Keep `EditorWindow` thin. Do not introduce the planned Phase-3 UI Toolkit window.

- [ ] **Step 4: Run focused and full default suites**

Expected: all pass, no network.

---

### Task 5: Align Plans and Produce Development Evidence

**Files:**
- Modify: `Assets/Plan/TFTDataPipeline.md`
- Modify: `Assets/Plan/Implementation/TFTDataAnalyzerImplementationPlan.md`
- Modify: `Assets/Plan/Implementation/2026-09-17-tft-static-data-pipeline.md`
- Modify: `Assets/Plan/Implementation/2026-09-17-tft-match-analytics.md`
- Modify: `Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md`
- Create: cycle refined/raw development records

**Interfaces:**
- Plans use the same composite identity and legacy rules as code.
- Phase 2 explicitly depends on an active catalog identity that includes set, source version, and profile.

- [x] **Step 1: Correct stale plan facts**

Replace the obsolete no-Git statement, record Phase 1 baseline, and replace the old `{setId}/{patchId}` uniqueness contract.

- [x] **Step 2: Correct downstream selection contracts**

Phase 2 must not identify observations by patch ID alone. Phase 3 must select a full catalog identity. Text scans must allow the documented negative disclaimer while forbidding prediction claims.

- [ ] **Step 3: Run final verification**

Run Unity recompile, full default EditMode tests, protected-file SHA-256 comparison, index/path/manifest consistency scan, and `git diff --check`.

- [x] **Step 4: Save development and raw reports**

Record commands, exit states, exact test counts, skipped explicit tests, review findings, fixes, and remaining risks. Do not claim unexecuted tests passed.

## Development Completion Gate

- Both profiles coexist for one set/patch/version.
- Two source versions coexist for one set/patch/profile.
- Existing legacy assets remain byte-identical and selectable.
- Tampering prevents idempotent success.
- Index entries resolve to existing valid catalogs.
- CommunityDragon remains explicit opt-in.
- Unity compilation is clean and the full default EditMode suite has zero failures.
- Implementation agent self-review and read-only review agent approval are recorded.
- No out-of-scope code or unrelated user file is included.

> 2026-09-24 개발 인계: 구현 및 같은 독립 리뷰어의 수정 검수 승인 완료. 보완 .NET 회귀 80/0, Unity 참조 컴파일 0 warnings/errors. 실제 Unity 재컴파일/EditMode/AssetDatabase/UI 검증은 Pipeline 연결 불가로 미완료이며 관련 게이트는 체크하지 않았다. 전체 근거와 Git 커밋 이관은 `DevelopmentReport.md`, raw 기록 참조. QA는 시작하지 않았다.
