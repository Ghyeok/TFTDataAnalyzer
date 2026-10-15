# Static Catalog Composite Identity Implementation Plan

갱신일: 2026-10-16
상태: 구현·코드 검수·오프라인 Unity QA 통과, 최종 사용자 완료 승인 대기

이 문서는 실행 순서와 개발 TODO·완료·검수·QA 조건의 단일 관리 문서다. 기존 `DevelopmentTodo.md`의 조건을 통합했다. Tasks 1–5는 원래 구현 절차이며 구현 이력은 [2026-09-24 개발 기록](DevelopmentReport.md), 새 검증은 [2026-10-16 QA 보고서](QAReport.md)에 근거한다. 현재 위치와 Git 상태는 [Checkpoint](../Checkpoint.md)를 따른다.

모델 선택은 [AGENTS.md](../../../../../../AGENTS.md), 제품 코드의 TDD·검수 절차는 [워크플로](../../../README.md)를 따른다. 완료된 구현과 코드 검수를 다시 배정하지 않는다.

**Goal:** Make static catalog identity include Set, TFT patch, Data Dragon source version, and source profile while preserving the committed legacy official-only catalog byte-for-byte.

**Architecture:** Introduce one immutable `CatalogIdentity` shared by import requests, manifests, paths, indexes, and UI status. New outputs use a composite directory. A narrowly scoped legacy resolver recognizes safe schema-1 official-only outputs without moving or rewriting them. Publishing verifies actual catalog bytes before reporting idempotency.

**Tech Stack:** Unity 6000.3.8f1, C#, Newtonsoft Json 3.2.2, Unity Test Framework 1.6.0, Unity Editor APIs.

**Spec:** `Assets/Plan/Workflow/Cycles/2026-09-23-source-profile-identity/refined/PlanningReview.md`

## 개발·검수·QA 조건

| ID / 실행 항목 | 객관적 완료 조건 | 개발 검수 조건 | QA 필수 증거 |
|---|---|---|---|
| D1 / Task 1 | 두 프로필이 요청·manifest 직렬화에서 왕복하고 알 수 없는 값은 거부됨 | 프로필과 CommunityDragon 사용 여부가 단일 계약에서 파생됨 | 집중 테스트 결과와 직렬화 예시 |
| D2 / Task 2 | 같은 set/patch의 두 프로필·두 원본 버전이 별도 경로·인덱스로 공존 | `setId+patchId`만으로 항목을 선택·제거하지 않음 | 네 identity 공존 테스트와 실제 경로 목록 |
| D3 / Task 3 | 기존 18.2 catalog/manifest/.meta 바이트가 변하지 않고 official-only로 검색·재검증 가능 | 판별 불가능한 legacy를 추측하지 않음 | 작업 전후 SHA-256과 legacy fixture, 실제 Unity 기존 index 역직렬화 |
| D4 / Task 3 | 변조를 감지하고 같은 identity의 다른 콘텐츠가 기존 파일·인덱스를 바꾸지 않음 | manifest 해시 문자열만 신뢰하지 않음 | 변조·충돌·취소·실패 테스트와 실제 AssetDatabase 확인 |
| D5 / Task 4 | 확인·결과에 version/profile/대상 경로 표시, 확인 전 취소 시 네트워크 0건 | 보완 소스를 매 실행 명시적으로 선택하고 기본 false | fake transport 테스트와 실제 Editor 창 수동 확인 |
| D6 / Task 5 | 컴파일 성공, 기본 EditMode 전체 통과, 기본 테스트는 네트워크 없음, 계획·코드 계약 일치 | 범위 밖 Phase 2 변경과 사용자 파일 변경 제외 | 명령·종료 상태·테스트 수·git diff 요약 |

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
- Canonical serialization remains deterministic for the same identity and content; retain schema-1 deserialization regression coverage.

- [x] **Step 1: Write failing identity tests**

Assert two identities differing only by source version are unequal, two differing only by profile are unequal, stable IDs round-trip, and an unknown stable ID throws a typed argument/serialization error.

- [x] **Step 2: Run focused tests and verify RED**

Run the `CatalogIdentityTests` filter. Expected: compile failure for the missing identity types.

- [x] **Step 3: Implement the minimum domain contract**

Use constructor-validated immutable values. Do not allow callers to pass a profile plus a contradictory CommunityDragon boolean.

- [x] **Step 4: Add explicit manifest identity and legacy default handling**

New manifests use schema version 2 and serialize the stable profile ID. Schema-1 deserialization may map to official-only only through the legacy resolver in Task 3, not through a universal silent default.

- [x] **Step 5: Run focused and full default EditMode tests**

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
- Raw cache paths distinguish input identities, or safely share content-addressed originals under a tested contract.

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

- [x] **Step 6: Run focused and full default suites**

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
- A legacy index entry keeps its existing asset path; an identical official-only rerun must not create a duplicate output directory.

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

- [x] **Step 6: Recompute protected file hashes and run suites**

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
- Error status includes identity and safe recovery information without response bodies or secret values.

- [x] **Step 1: Write failing presentation and consent tests**

Assert default official-only, exact summary fields, zero transport calls when confirmation is cancelled, zero CommunityDragon-host calls for official-only, and reset after every terminal state.

- [x] **Step 2: Verify RED**

Use a fake command/state boundary rather than asserting Unity framework internals.

- [x] **Step 3: Implement the minimum testable state boundary and UI binding**

Keep `EditorWindow` thin. Do not introduce the planned Phase-3 UI Toolkit window.

- [x] **Step 4: Run focused and full default suites**

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

- [x] **Step 3: Run final verification**

Run Unity recompile, full default EditMode tests, protected-file SHA-256 comparison, index/path/manifest consistency scan, and `git diff --check`.

- [x] **Step 4: Save development and raw reports**

Record commands, exit states, exact test counts, skipped explicit tests, review findings, fixes, and remaining risks. Do not claim unexecuted tests passed.

## Development Completion Gate

- [x] D1–D5 implementation, self-review and independent code-review approval recorded.
- [x] Two profiles and two source versions coexist; the index resolves valid test catalogs.
- [x] Protected legacy bytes are unchanged; compatible legacy lookup is covered by fixtures.
- [x] Tampering, collision, cancellation and failure-preservation regression evidence recorded.
- [x] CommunityDragon remains explicit opt-in for each run.
- [x] D6 plan alignment and historical development/raw evidence recorded.
- [x] Implementation committed as `3715893`; unrelated user changes excluded.
- [x] Actual Unity compilation is clean and the full default EditMode suite has zero failures (82/82 baseline, then 114/114 including the new contracts).
- [x] Actual Unity legacy-index loading, native index persistence and Import window defaults inspected; publication/recovery and consent behavior covered by offline regression tests (see QA scope).
- [x] D1–D6 offline QA evidence accepted by an independent reviewer and the 2026-10-16 report recorded.
- [ ] Final user completion approval recorded. This does not block the separately requested next implementation.

2026-09-24의 보완 결과는 이력으로 보존한다. 2026-10-16의 실제 Unity 결과와 QA 범위는 [QAReport](QAReport.md)를 따른다. 실제 네트워크 import, 네이티브 확인창 클릭, production `CatalogIndexWriter.Upsert` 전체 발행의 네이티브 실행은 미실행이며 해당 범위까지 검증했다고 주장하지 않는다.
