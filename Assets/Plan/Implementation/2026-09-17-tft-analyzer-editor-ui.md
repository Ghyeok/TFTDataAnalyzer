# TFT Data Analyzer Editor UI and Compliance Implementation Plan

> 공통 모델·단계·기록 규칙은 [구현 계획 개요](TFTDataAnalyzerImplementationPlan.md)와 [워크플로](../Workflow/README.md)를 따른다. 아래 체크 항목은 이 단계의 구현·검증 상태를 관리한다.

**Goal:** Provide a complete Unity UI Toolkit Editor workflow for static imports, bounded match collection, historical metric exploration, policy disclosure, and deployment blocking until compliance is acknowledged.

**Architecture:** A single EditorWindow hosts four lazily bound tabs backed by small view-models. View-models depend on Phase 1/2 interfaces and contain no direct HTTP or AssetDatabase logic; compliance rules are shared by UI, export, and build preprocessing.

**Tech Stack:** Unity 6000.3.8f1, UI Toolkit/UXML/USS, Unity Editor APIs, Unity Test Framework 1.6.0.

**Spec:** `Assets/Plan/TFTDataPipeline.md`

## Global Constraints

- Execute only after Phase 1 and Phase 2 completion gates pass.
- UI copy says historical observation and relative placement advantage, never combat win rate or prediction.
- Never render or log API key, PUUID, Riot ID, or raw Match ID.
- CommunityDragon remains an unchecked opt-in on every static import.
- Catalog rows and the active selection carry the complete `(setId, patchId, sourceGameVersion, sourceProfile)` identity. A schema-1 legacy row is selectable only after the official-only resolver validates it.
- Player builds are blocked until a local compliance setting confirms current Riot registration/review.
- The Editor tool remains usable while player builds are blocked.

## 2026-10-16 Plan Review: Verification and State Contracts

사용자의 계획 재검토·검증 창 잔류 지적을 반영했다. 의도한 RED, 확인된 제품 결함, 도구 실행 오류와 수정 후 GREEN을 기록에서 구분한다. 이후 기능의 범위는 늘리지 않는다.

- 각 Task 착수 전에 입력 초안, 검증된 요청, 실행 중 snapshot과 종료 결과의 책임을 정한다. UI 재생성·닫기는 정상 실행과 분리된 마지막 검증으로 미루지 않는다.
- MCP/CLI·자동 테스트 모두 기존 창 ID·포커스를 기록하고, 새 창은 Show 전에 정확한 소유 객체를 추적한다. 성공·오류·취소·시간 초과에서 finally 정리 후 기존 창 보존·소유 창 잔류 0개를 증거로 남긴다. 기존 사용자 창의 위치·크기를 변경하거나 종류별로 일괄 종료하지 않는다.
- 수동 캡처는 생성→렌더 대기→크기별 캡처→정리를 한 bounded 작업에서 끝낸다. 독립적인 열기/캡처 호출 사이에 창을 남겨두지 않는다. Inspector 등 보조 창도 같은 규칙을 적용한다.
- 각 Task에서 기존 모듈과 연결한 짧은 fake 통합 검증을 끝낸다. 전체 fabricated E2E는 Task 6에 유지한다. 수정 중에는 관련 테스트를 실행하고, 최종 코드가 고정되면 전체 suite를 한 번 실행한다. 새 변경·실패·미해결 근거 없이 같은 전체 검증을 반복하지 않는다.
- 체크포인트는 현재 단계·마지막 실제 결과·다음 한 행동을 갱신한다. 최초 실패와 이미 해결한 문제를 현재 장애로 남기지 않는다.

## Locked File Structure

```text
Assets/TFTAnalyzer/Editor/UI/
  TftDataAnalyzerWindow.cs
  TftDataAnalyzerWindow.uxml
  TftDataAnalyzerWindow.uss
  Data/{DataManagerViewModel,PatchRowViewModel}.cs
  Collection/{CollectionViewModel,CollectionProgressViewModel}.cs
  Analysis/{AnalysisViewModel,MetricRowViewModel}.cs
  About/AboutViewModel.cs
Assets/TFTAnalyzer/Editor/Compliance/
  {ComplianceSettings,ComplianceSettingsProvider,ComplianceBuildGuard,PolicyCopy}.cs
Assets/TFTAnalyzer/Tests/EditMode/UI/
Assets/TFTAnalyzer/Tests/EditMode/Compliance/
```

---

### Task 1: Build the Editor Window Shell

2026-10-16 실행 범위: [창 체크포인트](../Workflow/Cycles/2026-10-16-editor-window-shell/Checkpoint.md). 사용자 재개 요청에 따라 이 Task의 창·탭 전환만 수행한다. Task 2 이후 기능은 이번 구현에 포함하지 않는다.

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Create: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/TftDataAnalyzerWindowTests.cs`

**Interfaces:**
- Produces menu item `TFT Analyzer/Open Data Analyzer`.
- Produces named tab roots `data-tab`, `collection-tab`, `analysis-tab`, and `about-tab`.

- [x] **Step 1: Write a failing UI structure test**

```csharp
[Test]
public void CreateGUI_ContainsAllPolicyBoundedTabs()
{
    var window = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
    window.CreateGUI();
    Assert.That(window.rootVisualElement.Q("data-tab"), Is.Not.Null);
    Assert.That(window.rootVisualElement.Q("collection-tab"), Is.Not.Null);
    Assert.That(window.rootVisualElement.Q("analysis-tab"), Is.Not.Null);
    Assert.That(window.rootVisualElement.Q("about-tab"), Is.Not.Null);
}
```

- [x] **Step 2: Confirm failure and implement UXML shell**

Use a toolbar for tab buttons, one content root per tab, and a persistent footer label named `historical-data-disclaimer`. No continuous `Update()` polling; coalesce main-thread view-model events through a one-shot `EditorApplication.update` callback. Remove the callback before rendering and on binding disposal. UI control updates do not depend on Inspector processing; geometry/repaint completion for screenshots is verified separately. Task 6's idle startup scheduler retains its separate `delayCall` contract.

The initial selection shows only `data-tab`; clicking each toolbar button shows exactly its corresponding content. Preserve selection across UI reconstruction without duplicating roots, callbacks, or stylesheet references. Keep the historical disclaimer outside all tab roots and present it on the missing-asset screen too. Test real UI events, each missing asset independently, recovery after failure, and serialized selection restoration without moving existing assets. Reconstructing the UI alone is not evidence of a complete domain reload.

- [x] **Step 3: Add resilient asset loading**

Load UXML/USS through fixed asset paths and show a plain IMGUI-style error label if either asset is missing, so a broken stylesheet does not make the tool unusable.

- [x] **Step 4: Run focused and full suites, then commit**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI
git commit -m "feat: add TFT analyzer editor window shell"
```

---

### Task 2: Implement the Static Data Management Tab

2026-10-16 재개: [정적 데이터 관리 체크포인트](../Workflow/Cycles/2026-10-16-static-data-management/Checkpoint.md). 기존 계획의 기능 연결에 필요한 작은 서비스 어댑터·disposable tab binding과 `Editor/Persistence/PublishedCatalogLoader.cs`를 허용한다. VM은 HTTP/AssetDatabase를 직접 사용하지 않으며 기존 pipeline/serializer/hash 검증/differ를 재사용한다. 활성선택은 열린 창의 메모리에만 유지하고, 재생성은 binding 교체·VM 유지를 따른다. 표시용 실제 수집UTC/validation은 정규화된 catalog가 아닌 standalone manifest에서 읽는다. 취소는 실행 종료까지 busy를 유지하고 publish 후 성공을 늦은 취소로 번복하지 않는다.

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/Data/PatchRowViewModel.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerViewModelTests.cs`

**Interfaces:**
- Consumes: `StaticCatalogPipeline`, `CatalogIndex`, `CatalogDiffer`.
- Produces: `CheckForUpdatesAsync`, `ImportAsync`, `CancelImport`, `SelectActiveCatalog(CatalogIdentity)`, and read-only catalog rows.

- [x] **Step 1: Write failing explicit-consent tests**

Assert checking versions performs no catalog or image download. Assert `IncludeCommunityDragon` defaults false and is reset false after every completed/cancelled import.

- [x] **Step 2: Implement the view-model state machine**

States are `Idle`, `Checking`, `AwaitingApproval`, `Importing`, `Succeeded`, `Failed`, and `Cancelled`. Commands reject invalid transitions and expose user-facing error codes without response bodies.

- [x] **Step 3: Bind controls and patch table**

Show Set, TFT patch, source game version, stable source-profile ID, source badges, retrieved UTC, validation status, content hash prefix, resolved target path, and active catalog selection. Require a modal confirmation with the full identity and target before `ImportAsync`.

- [x] **Step 4: Add patch-diff presentation**

Show added/removed/changed counts per entity and an expandable field-level list. Retrieval timestamps never appear as content changes.

- [x] **Step 5: Run tests and commit**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerViewModelTests.cs
git commit -m "feat: add static data management workflow"
```

---

### Task 3: Implement the Bounded Collection Tab

2026-10-16 실행·재검토: [경기 수집 탭 체크포인트](../Workflow/Cycles/2026-10-16-collection-tab/Checkpoint.md). 수집 UI와 검증 도구의 소유 창 정리까지 이번 범위다.

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/Collection/CollectionViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/Collection/CollectionProgressViewModel.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/CollectionViewModelTests.cs`

**Interfaces:**
- Consumes: `IRiotApiKeyProvider`, `EnvironmentSeedPuuidProvider`, `MatchCollectionService`.
- Receives the verified selected `(StaticCatalog, CatalogIdentity)` pair from the Data Manager row. Schema 1 intentionally has a null `Manifest.Identity`; never recover the selection by reading that nullable field or infer an unverified source profile. Reject a missing or mismatched full identity before confirmation. Preserve the verified identity separately in the running snapshot.
- Produces: platform, expected patch, queue, tier bucket, capped maximum matches, `StartAsync`, and `Cancel`.

**State contract and rebuild cases:**

| 상태 | 입력·실행 대상 | 재생성·닫기 검증 |
|---|---|---|
| 카탈로그 없음 / 입력 무효 | 현재 초안을 유지하고 시작 차단, 거부된 임의 문자열 비노출 | 재생성 후 보이는 값과 검증 상태 일치, 카탈로그 선택·입력 수정 후 정상 시작 |
| 유효 입력 / 대기 | 전체 카탈로그와 유효 요청을 구분 | 재생성 후 입력 보존, 열기·탭 전환은 수집하지 않음 |
| 확인 대기 / 실행 | 승인 대상 snapshot 고정, 입력·중복 시작 차단 | 재바인딩은 실행 변경·재시작 없음, 다른 카탈로그 선택은 다음 실행에만 적용 |
| 취소 요청 | 실제 종료까지 busy, 저장 개수 보존 | 닫기는 취소·구독 해제, 늦은 진행 알림 무시 |
| 성공 / 취소 / 오류 / 401·403·429 / 탐색 상한 | 최종 요약이 권위, 상태별 안전한 안내와 입력 해제 | 최종 상태 재생성, 소유 창·콜백 잔류 없음 |

Task 종료 시 검증된 카탈로그 선택→보이는 조건→확인→fake 수집·익명 저장→취소/실패→창 정리의 짧은 통합 경로를 검증한다. 기존 429는 중단 후 수동 재시도 가능 UTC를 표시하며 자동 재개하지 않는다. 티어는 실제 등급을 검증한 필터가 아닌 기록용 라벨이다.

- [x] **Step 1: Write failing secret-visibility and cap tests**

Assert the model exposes only `ApiKeyConfigured`/`SeedConfigured` booleans, never values. Assert match limit accepts 1–1,000 and rejects 0 or 1,001.

Cover both schema 2 and verified official-only schema 1 selection through the window, confirmation and actual fake-transport collection. Schema 1 without a supplied verified identity, or a mismatched patch/source/version/set number, must not start. Include a final read-only smoke check against the shipped verified legacy catalog; never migrate or mutate it just to satisfy the UI.

- [x] **Step 2: Implement validated input and confirmation**

Before collection, show route, patch, queue, maximum matches, and the statement that only user-supplied seeds are queried. Do not show the seeds themselves.

- [x] **Step 3: Bind progress and cancellation**

Display requested, fetched, stored, duplicate, filtered, failed, rate-limit wait, and cancellation state. Disable all editable inputs during a run and re-enable them on every terminal state.

- [x] **Step 4: Test cancellation, 401, 429, and retry copy**

Use fake services and assert no exception or UI text contains fabricated secrets. Run full suite.

2026-10-18: 최신 코드 전체 EditMode 467/467(그중 UI90), 컴파일 상태 up_to_date/실패 false/오류0. 실제 포함 schema1 카탈로그의 전체 식별자와 940px·500px 화면을 확인했다. 정상·표시 전/후 실패 작업 및 전체 테스트 뒤 기존 창/프레임 보존·포커스 복원·소유 창 잔류0을 확인했다. 실제 API·네이티브 확인창 클릭은 미실행이다. Console에는 검증 전 Pipeline 통신 오류2건이 남으며 최종 cursor 이후 새 경고·오류0이다. 상세 증거와 제한은 [개발 보고서](../Workflow/Cycles/2026-10-16-collection-tab/refined/DevelopmentReport.md)를 따른다.

- [x] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI/CollectionViewModelTests.cs
git commit -m "feat: add bounded match collection workflow"
```

2026-10-18: 검증된 Task3 구현과 사용자 요청의 계획·검증 지침·문서를 한 커밋으로 기록했다. 실제 제목은 `feat: add bounded collection UI and scoped Editor validation`이며, 커밋 식별자는 현재 브랜치 Git 로그에서 확인한다. PR/CodeRabbit 검수·병합과 사용자 최종 승인은 별도다.

---

### Task 4: Implement Historical Analysis Views

2026-10-18 착수: 사용자 “이어서 진행해줘”에 따라 기존 Task4 범위를 수행한다. [독립 기획 검토](../Workflow/Cycles/2026-10-18-historical-analysis/refined/PlanningReview.md)의 연결 계약을 적용한다. `AggregateStore`는 현재 저장 전용이고 조합 해시에 필터용 구성 정보가 없으므로, 명시적인 로컬 새로고침에서 익명 관측을 읽고 같은 snapshot으로 집계와 구성 설명을 만든다. 관측 읽기는 인덱스·미완성 꼬리 복구를 수행할 수 있으며 창 열기·필터 변경으로 실행하지 않는다. 전체 선택은 scope별 행을 나열하고 합산하지 않으며 쌍 필터는 한 endpoint 전체가 교집합을 만족해야 한다. 새 집계 스키마·실데이터 증강 분류·Task5/6 기능은 추가하지 않는다.

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/Analysis/AnalysisViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/Analysis/MetricRowViewModel.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/AnalysisViewModelTests.cs`

**Interfaces:**
- Consumes: `AggregateStore`, `MetricPolicy`, and the verified active `(StaticCatalog, CatalogIdentity)` selection. Keep the full identity independent of a nullable legacy `Manifest.Identity`, matching Task 3's selection contract.
- Produces filters for patch, region, queue, tier bucket, unit, trait, item, and augment.
- Produces metric rows containing sample count, average placement, top-four, first-place, Wilson interval, and policy state.

- [x] **Step 1: Write failing copy and suppression tests**

```csharp
[Test]
public void Rows_DoNotUsePredictionLanguage()
{
    var allCopy = string.Join(" ", _viewModel.Load(Fixture.Aggregates).Select(x => x.DisplayText));
    Assert.That(_viewModel.PredictionClaims(Fixture.Aggregates), Is.Empty);
}
```

The scan should reject positive prediction or combat-win-rate claims while allowing the explanatory negation `같은 로비의 최종 순위 비교이며 실제 전투 승률이 아닙니다.` Assert samples 99 show `표본 부족` and no percentage; 100 show rate plus confidence interval.

- [x] **Step 2: Implement filter intersection**

Every selection narrows the immutable snapshot in memory; no filter triggers an API request. Display an explicit empty state with scope and the observed match period (`FirstPlayedAt` / `LastPlayedAt`); do not call it a retrieval period or invent retrieval timestamps.

- [x] **Step 3: Implement aggregate and pairwise tables**

Label pairwise data `상대 배치 우위율` and include the tooltip `같은 로비의 최종 순위 비교이며 실제 전투 승률이 아닙니다.` Show numerator/denominator and 95% interval.

- [x] **Step 4: Add CSV export of approved presentation DTOs only**

Export a local personal-research UTF-8 CSV from `MetricPresentation`; refuse raw observations and raw aggregate counters. Include scope, sample count, observed match period, metric name, numerator, denominator, estimate, lower, upper, and policy note. For suppressed metrics, keep the allowed sample/average/state fields and leave all ratio fields (including numerator/denominator/interval) blank, matching the empty `Ratios` contract. Do not reconstruct suppressed values. Task 4 adds no public publishing/export surface; Task 5 must preserve this local-CSV allowance independently of API-key availability or registration state.

- [x] **Step 5: Run tests and commit**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI/AnalysisViewModelTests.cs
git commit -m "feat: present policy-safe historical TFT metrics"
```

Before the checkpoint, verify fake aggregate→policy presentation→filter→local CSV, suppressed fields remain blank, no API request, and owned validation windows close. This small gate does not replace Task 6's complete E2E.

---

### Task 5: Add About, Policy Metadata, and Compliance Build Gate

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/About/AboutViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/PolicyCopy.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/ComplianceSettings.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/ComplianceSettingsProvider.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/ComplianceBuildGuard.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Compliance/ComplianceBuildGuardTests.cs`

**Interfaces:**
- Produces Project Settings page `Project/TFT Data Analyzer/Compliance`.
- Produces local settings under `Library/TftAnalyzerStore/compliance.json` with review date, policy URLs, product registration status, and acknowledgement timestamp.
- Implements `IPreprocessBuildWithReport` and blocks Player builds when registration status is not `ApprovedOrAcknowledged`.

- [ ] **Step 1: Write a failing unregistered-build test**

```csharp
[Test]
public void OnPreprocessBuild_BlocksWhenRegistrationIsNotAcknowledged()
{
    var guard = ComplianceFixture.UnregisteredGuard();
    Assert.Throws<BuildFailedException>(() => guard.ValidateForBuild());
}
```

- [ ] **Step 2: Implement local compliance settings**

Settings are intentionally outside `Assets` and version control. Required fields are `policyReviewedOn`, the three Riot policy URLs from the spec, `registrationStatus`, and `acknowledgedAt`. Never infer approval from API-key presence.

- [ ] **Step 3: Implement build and export gates**

Block Player build and public-style export while unregistered. Do not block Editor data exploration or local aggregate CSV export labeled for personal research.

Task 5's gate must not reclassify Task 4's local personal-research CSV as a public export. Confirm these paths with fake registration states and verify the settings/About validation windows are cleaned up. A future public publishing surface requires its own explicit scope; no new one is added by this plan clarification.

- [ ] **Step 4: Bind the About tab**

Show data sources, policy-review date, registration state, required unofficial-project disclaimer source link, privacy summary, metric definitions, and excluded features.

- [ ] **Step 5: Run tests and commit**

```powershell
git add Assets/TFTAnalyzer/Editor/Compliance Assets/TFTAnalyzer/Editor/UI/About Assets/TFTAnalyzer/Tests/EditMode/Compliance
git commit -m "feat: add policy disclosure and build compliance gate"
```

---

### Task 6: Add Startup Version Check and End-to-End Editor Tests

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/StaticVersionCheckScheduler.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/TftDataAnalyzerEndToEndTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Compliance/ForbiddenFeatureSurfaceTests.cs`

**Interfaces:**
- Produces a once-per-24-hours non-blocking Data Dragon version check.
- Persists only last-check UTC and last-seen version under `Library/TftAnalyzerStore`.

- [ ] **Step 1: Write failing scheduler tests**

Assert two Editor loads within 24 hours perform one version request, the check never downloads catalogs or icons, and network failure does not block domain reload.

- [ ] **Step 2: Implement `[InitializeOnLoad]` scheduling**

Schedule after the Editor is idle using `EditorApplication.delayCall`; skip batch mode and play mode; use a cancellation token tied to assembly reload. Display a passive notification only when the latest source version differs from the last imported version.

- [ ] **Step 3: Add an end-to-end fabricated workflow test**

With fake transports: check version → approve import → validate/publish a fabricated Set 18 catalog → collect two fabricated matches → persist anonymous observations → aggregate → show analysis rows. Assert no network, no secrets on disk, and the relative-placement label is correct.

- [ ] **Step 4: Add forbidden-surface reflection and text scans**

Fail if project assemblies expose types or menu items containing `Spectator`, `LiveGame`, `Overlay`, `CombatSimulator`, or `WinPrediction`; scan UI text for prohibited Korean/English prediction wording while allowing the documented negation disclaimer.

- [ ] **Step 5: Run full verification**

Recompile, run the entire EditMode assembly, and inspect Unity Console errors. Expected: zero compile errors, zero failed tests, explicit network tests skipped by default.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/Ingestion/StaticVersionCheckScheduler.cs Assets/TFTAnalyzer/Tests
git commit -m "test: verify TFT analyzer workflow and policy boundary"
```

## Phase 3 Completion Gate

Run the full EditMode suite, manually open `TFT Analyzer/Open Data Analyzer`, and execute the fabricated end-to-end workflow. Confirm the tool never displays identities or secrets, all ratio metrics show sample count and interval, CommunityDragon remains opt-in, startup checks do not mutate data, and Player builds are blocked until compliance status is acknowledged.
