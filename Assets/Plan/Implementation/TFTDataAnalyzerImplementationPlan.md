# TFT Data Analyzer Implementation Plan Index

> 모델 선택은 [AGENTS.md](../../../AGENTS.md), 단계·역할·기록은 [개발 워크플로](../Workflow/README.md)를 따른다. 단계별 계획은 구현 절차이며 현재 진행 상태와 과거 실행 근거를 구분한다.

**Goal:** Build a policy-bounded Unity Editor tool that imports patch-versioned TFT static data, ingests Riot Match API responses without persisting identity-bearing payloads, and displays anonymous historical aggregate statistics.

**Architecture:** The implementation is split into three independently testable plans. Phase 1 produces a usable static-data catalog and patch-diff tool; Phase 2 adds memory-only match ingestion, anonymized local observations, and deterministic aggregates; Phase 3 adds the UI Toolkit Editor experience and compliance gates.

**Tech Stack:** Unity 6000.3.8f1, C#, Unity Test Framework 1.6.0, Newtonsoft Json 3.2.2, UnityWebRequest, UI Toolkit, Unity CLI/Pipeline 0.7.0-exp.1.

**Spec:** `Assets/Plan/TFTDataPipeline.md`

## 2026-10-20 구현 검토와 다음 작업

- Phase1~3의 계획된 주요 기능과 Task6 커밋은 구현됐고 PR14가 main `fa0df4d`에 병합됐다. PR14 병합 기준 제품 트리는 당시 전체620/619통과/0실패/호스트 링크1제외의 실행 버전과 같았다. 이후 아래 준비 안내 수정 후 전체631개 중630통과/0실패/호스트 링크1제외를 실제 실행했다.
- [KI-001](../../../docs/known-issues.md)은 미해결 기록 후 개발 계속이다. 실제 Riot API 사용 검증·증강 신뢰 분류 공급·최소 표본 달성은 구현된 기본 경로와 분리해 다룬다.
- 수집 전 준비 상태 안내는 기획 A/B 검토·사용자 승인 후 구현·코드 검수·QA를 통과했다. 제품1/테스트2파일의 변경이며 실제 API·새 자동 수집은 추가하지 않았다. [승인 계획](../Workflow/Cycles/2026-10-20-collection-readiness/refined/ImplementationPlan.md)과 [결과·한계](../Workflow/Cycles/2026-10-20-collection-readiness/refined/FinalDocumentation.md)를 따르며 PR15 최신 head 검수·main 병합·사용자 최종 승인을 완료했다.

[검토된 증강 분류 공급 경로](../Workflow/Cycles/2026-10-20-augment-policy-provider/Checkpoint.md)는 기획 A/B 검토·사용자 승인 후 구현·코드검수·QA를 통과했다. 초기 head bfb5231의 전체680개/679P/0F/1호스트제약 제외를 확인했고 [PR #16](https://github.com/Ghyeok/TFTDataAnalyzer/pull/16)의 검수 보완을 반영했다. 제품 코드 변경 없이 강화한 서비스 테스트7/7을 실행했다. 사용자 재개 후 최신 cc62282의 CodeRabbit SUCCESS·inline thread0·별도 CI0을 확인했고 새 outside-diff 의견1건은 현재 저장·취소 계약에 대조해 미반영으로 판단했다. 최초 자동 승인 검토의 명시적 병합 승인 부족은 사용자 “병합 승인 거절 근거를 찾고 해결한 뒤 다시 병합해줘” 요청으로 해소했다. PR16은 main57891e0에 squash 병합됐고 원격 MERGED·커밋을 확인했다. 사용자 최종 결과 승인은 별도이며 실제 분류 자료 등록 전 비율 차단은 유지된다. 다음 범위는 자료 확보·검토·등록의 기획이다.

## Global Constraints

2026-10-26 최신: PR22는 main f997472에 병합했고 최신 검수 head와 전체 트리가 같다. 다음 [구조화 정적 데이터 계획](../Workflow/Cycles/2026-10-26-structured-static-data/Checkpoint.md)은 schema4의 기물 수치·시너지 구간·아이템 제한·비실행 확장 자료와 구 schema1/2/3 불변 보존을 기획한다. 아직 제품 구현 미승인이며 아래 이전 ‘PR통합단계’는 당시 기록이다. 실제API·분류등록0·실제n100/입도·KI-001·사용자 최종 확인의 잔여를 유지한다.

2026-10-26 현재: 필드별 출처 기록은PR21/main5a67afb에병합됐다. [보완 정적 자료 형식 호환](../Workflow/Cycles/2026-10-22-community-shape-compatibility/Checkpoint.md)은새기획A/B·사용자승인후구현·독립코드검수·전체826/824P0F/호스트2제외와최종QA통과를확인했고PR통합단계다. 같은요청official catalog로global범위를검증하며제외ID경고도bounded표본으로보존한다. 새능력치/시너지구간/아이템제한구조모델과실제API·분류등록·n100은여전히잔여다. 아래이전날짜의현재표시는각시점이력이다.

2026-10-22 현재 사이클: 정적 가져오기 보고서는 PR20/mainf005879에 병합했다. [필드별 출처 기록](../Workflow/Cycles/2026-10-22-field-provenance/Checkpoint.md)은 A/B 검토·사용자 명시 승인 후 구현·코드검수를 마쳤으며 전체796/794P0F/호스트2제외와4PNG·소유창0·보호1399불변을 확인했다. 최종QA·PR 최신head 검수/병합은 별도 진행한다. 구schema1/2를 변경하지 않으며 실제 API 수집은 계속 미룬다.

2026-10-21 현재 다음 사이클: 상위 설계§12.1의 검증/병합 결과 화면 누락을 [정적 가져오기 보고서 계획](../Workflow/Cycles/2026-10-21-static-import-report/refined/ImplementationPlan.md)으로 보완한다. A/B pass·필수0 및 사용자 명시 승인 후 구현·동결10소스 전체765개763통과/실패0/호스트2제외, UI211/211과4PNG·소유창0을 확인했고 최종QA도필수0으로통과해PR단계다. 실제네트워크·기존카탈로그변경·새분류허용은없다.

2026-10-21 잔여 구현 추적: 실제 증강4개 분류 후보는 A/B에서 Legend 배제 근거가 부족해 반려했고 등록0을 유지한다. 같은 자료의 AI 재검토는 반복하지 않는다. 상위 설계§10.2의 실제 수집 격리 보고서 누락을 별도로 찾아 [승인 계획](../Workflow/Cycles/2026-10-21-collection-quarantine/refined/ImplementationPlan.md)에 따라 구현·코드검수·QA를 통과했다. 전체750개748통과/실패0/호스트symlink2제외,6PNG·소유창0·보호1,399개 보존이며 Console 경고1은 문서 자산 등록에 관한 기록으로 남겼다. 이후 PR19 최신head d902286검수통과후main1a90059로병합했고전체트리일치를확인했다. 실제 API·실제 분류 자료·실제 n100달성은 별도 미완료다.

현재 활성 목표의 [잔여 점검](../Workflow/Cycles/2026-10-20-service-completion/refined/ServiceCompletionAudit.md)에서 빠진 정적 레코드 탐색을 확인했다. [기획 A/B](../Workflow/Cycles/2026-10-20-service-completion/refined/PlanningReview.md) 보완과 사용자 [계획 승인](../Workflow/Cycles/2026-10-20-service-completion/refined/ImplementationPlan.md) 후 구현·독립 검수·QA를 통과했다. 관련UI169/169, 동결 전체701개 중700통과·실패0·호스트 링크1제외, 실제4PNG·창/프레임/보호파일 보존을 확인했다. PR17은최신head검수통과후main0c6c1d5로병합했다. 최소표본설정은전체726/725P0F1환경제외·QA·최신검수통과후PR18/main3484b13으로병합했다. 실제분류자료기획은별도반려상태로등록0을유지한다. 실제API설정/수집은사용자요청으로미뤘다. Hermes예약은재등록하지않는다. 실제분류자료·API·표본검토 등 전체목표의 남은일은 계속 진행한다.

- This project never simulates TFT combat and never reports future win probability.
- Match API payloads containing PUUIDs or raw Match IDs never touch disk or logs.
- Static-data source priority is Riot Data Dragon first and CommunityDragon only for missing fields.
- CommunityDragon ingestion is opt-in for every import run.
- Statistics are segmented by patch, region, queue, and tier bucket.
- Ratio metrics are hidden below 100 observations and use a 95% Wilson confidence interval.
- Legends and Legend-based augment win-rate metrics are blocked at the policy boundary.
- API keys come only from `RIOT_API_KEY` or an OS credential provider; never from project files, `EditorPrefs`, URLs, or command-line arguments.
- No live-game, spectator, overlay, screen-capture, opponent-tracking, or recommendation feature is introduced.
- This workspace has a Git repository. The Phase 1 official-only Set 18 / 18.2 baseline was committed at `ec9d296`; preserve its generated catalog and manifest bytes.
- A static catalog is selected by `CatalogIdentity = (setId, patchId, sourceGameVersion, sourceProfile)`. Profiles are `official-only` or `official-plus-communitydragon`; schema-1 official-only output is read through the guarded legacy resolver.

---

## Plan Order

1. [Phase 1 — Static Data Pipeline](2026-09-17-tft-static-data-pipeline.md)
   - 기준 카탈로그는 `ec9d296`, source-profile identity 강화는 `3715893`에 반영됐다. 기능 구현과 실제 Unity 검증 완료를 구분하며, 남은 검증·QA는 [현재 체크포인트](../Workflow/Cycles/2026-09-23-source-profile-identity/Checkpoint.md)를 따른다.
2. [Phase 2 — Anonymous Match Analytics](2026-09-17-tft-match-analytics.md)
   - Starts only after the four-field active catalog identity is stable. Bounded Match API collection produces only anonymous observations and aggregate metrics in `Library/TftAnalyzerStore`.
3. [Phase 3 — Editor UI and Compliance](2026-09-17-tft-analyzer-editor-ui.md)
   - Deliverable: the complete `TFT Data Analyzer` window exposes import, collection, and analysis workflows with policy and build gates.

Each phase must pass its own EditMode suite before the next phase begins. Network tests remain `[Explicit]`; the default suite uses fabricated fixtures and never requires a Riot API key.

## Standard Verification Commands

After adding or changing scripts:

```powershell
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command recompile --project-path 'C:\Unity\TFTDataAnalyzer' --format json
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command recompile_status --project-path 'C:\Unity\TFTDataAnalyzer' --format json
```

Run the full EditMode suite:

```powershell
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command run_tests --project-path 'C:\Unity\TFTDataAnalyzer' --mode editor --filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout 300 --format json
```

Success requires `success: true`, completed test status, zero failed tests, and no compiler errors in the fresh command output.


2026-10-27 현재: 구조화 정적 데이터는 사용자 승인 후 schema4 모델·실제 adapter·공식우선 병합·불변 게시/loader·차이 비교·20행 탐색/4096자 원문까지 구현했다. 관련112/UI226·실제8PNG·정상과예외창정리를 확인했고 최종 코드검수·회귀·QA 진행 중이다. 구schema1/2/3은 자동변환하지 않는다. 실제API·분류등록0·실제n100사용성·성급/장착의 미공급 의미·KI-001·사용자 최종 확인은 계속 잔여다.
