# TFTDataAnalyzer 전체 구현 계획 검수안 (게임 기획·데이터 도구 관점)

작성일: 2026-10-19  
작성자: Antigravity (Gemini 4 Argon · Medium)  
역할: 게임 기획 및 데이터 분석 도구 기획 검수  
검수 범위:
- [전체 제품·데이터 설계](../Assets/Plan/TFTDataPipeline.md)
- [구현 계획 개요](../Assets/Plan/Implementation/TFTDataAnalyzerImplementationPlan.md)
- [Phase 1 — 정적 데이터 파이프라인](../Assets/Plan/Implementation/2026-09-17-tft-static-data-pipeline.md)
- [Phase 2 — 익명 경기 통계](../Assets/Plan/Implementation/2026-09-17-tft-match-analytics.md)
- [Phase 3 — Editor UI와 정책 경계](../Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md)
- [마스터 개발 워크플로](../Assets/Plan/Workflow/README.md) 및 [Task 5 구현 계획](../Assets/Plan/Workflow/Cycles/2026-10-19-compliance-gate/refined/ImplementationPlan.md)

---

## 1. 검수 요약 및 기획적 강점

현재 프로젝트의 설계와 단계별 구현 계획은 Riot Games의 정책 경계와 데이터 무결성을 매우 높은 수준으로 통제하고 있습니다.

1. **명확한 제품 경계와 정책 방어선 (Zero-Trust Policy Boundary)**
   - 전투 시뮬레이션·미래 승률 예측·실시간 오버레이를 원천 배제하고, 과거 종료 보드 기준의 **관측 조합(Observed Composition)**과 **상대 배치 우위율(Relative Placement Advantage)**로 지표를 정의했습니다.
   - `n < 100` 최소 표본 강제 차단, 95% Wilson 신뢰구간 병기, Legend 기반 증강 비율 차단이 UI가 아닌 핵심 정책 계층(`MetricPolicy`)에서 강제되어 분석 오용 가능성을 구조적으로 막았습니다.
2. **정적 데이터의 불변성(Immutability)과 복합 식별자**
   - 단순 패치 번호(`18.2`)만 쓰지 않고 `CatalogIdentity = (setId, patchId, sourceGameVersion, sourceProfile)` 4필드 복합 키를 도입해 핫픽스 빌드나 CommunityDragon 보완 여부에 따른 데이터 오염을 방지했습니다.
3. **식별 정보의 메모리 내 즉시 소거**
   - 원본 Match API 응답을 디스크에 기록하지 않고 스트림 단계에서 즉시 익명화하며, 미등록 정적 ID 발견 시에도 원본 문자열 노출 없이 해시로 격리(`static-id-v1|...`)하는 설계가 일관됩니다.

---

## 2. 핵심 기획 리스크 및 보완 권고 사항 (4대 검수 지적)

기존 승인된 코드 범위를 임의로 확대하지 않으면서, 도구의 실질적 가치와 남은 작업(Phase 3 Task 6 및 후속 사이클)의 무결성을 확보하기 위해 반드시 점검해야 할 4가지 항목입니다.

### ① 실데이터 증강 정책 분류(`MetricPolicyMetadata`) 연결 공백
- **현상**: `MetricPolicy`는 증강 분류 메타데이터(`Standard` / `LegendBased` / `Legend` / `Unknown`)가 없거나 카탈로그와 일치하지 않으면 **안전 차단(Fail-closed)**하도록 설계·구현되어 있습니다. 그러나 실제 카탈로그(예: `TFTSet18 / 18.2 / 16.18.1`)의 증강 레코드에 대응하는 검토된 분류 데이터 공급 계획이 비어 있습니다.
- **기획 리스크**: TFT 경기에서 증강이 없는 최종 보드는 사실상 존재하지 않습니다. 실데이터 `MetricPolicyMetadata`가 공급되지 않으면 실제 경기를 100건 이상 수집하더라도 **모든 실전 조합의 Top 4·1위 비율과 상대 배치 우위율이 `PolicyBlocked`로 차단되어 도구의 핵심 지표를 확인할 수 없습니다.**
- **검수 지침 반영**:
  - 정적 카탈로그 또는 분석 파이프라인 기획 시, **정책 차단 규칙(Fail-closed)만 정의하는 것에 그치지 않고 실제 기준 카탈로그의 레코드가 정상 통과할 수 있는 검증된 정책 메타데이터(`MetricPolicyMetadata`) 공급 경로**를 별도 과업이나 데이터 검수 절차로 명시해야 합니다.

### ② 관측 조합(`CompositionFingerprint`)의 입도(Granularity) 한계와 최소 표본(100건) 충돌
- **현상**: `CompositionFingerprint`는 `[모든 기물(성급+장착 아이템 전체), 활성 시너지 전체, 증강 전체]`가 **100% 일치**해야 동일 조합으로 집계됩니다. 또한 분석 화면의 필터는 조건에 맞는 행을 나열할 뿐 서로 다른 Fingerprint를 동적으로 재집계(Roll-up)하지 않습니다.
- **기획 리스크**: TFT 최종 보드(8~9기물, 각 기물 성급 1~3성, 아이템 10여 개, 증강 3개)가 완전히 똑같은 경기가 100건 이상 관측될 확률은 실행당 1,000경기 수집 상한 내에서는 0%에 수렴합니다. 결과적으로 실데이터 수집 시 거의 모든 행이 `표본 수 1~2건 (표본 부족)`으로만 표시될 위험이 큽니다.
- **검수 지침 반영**:
  - 현재 Phase 3 범위에서는 기존 결정적 지문(`fingerprint-v1|`) 계약을 보존하되, 사용자 문서에 **"완전 일치 보드 기준이므로 소규모 표본에서는 비율 지표가 억제됨"**을 명확히 고지합니다.
  - 향후 분석 고도화 사이클 기획 시에는 **집계 키의 입도(예: 코어 시너지·주력 기물 단위 키 또는 필터 조건 기준 동적 재집계)가 최소 표본 기준(`n >= 100`)을 현실적인 수집 한도 내에서 달성할 수 있는지(표본 달성 가능성)**를 기획 단계에서 필수로 검증해야 합니다.

### ③ [Phase 3 — Task 6](../Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md) 착수 전 세부 설계 명확화
마지막 남은 Task 6(`StaticVersionCheckScheduler` 및 E2E·금지 기능 표면 검증)를 진행하기 전에 아래 3가지 모호성을 기획에서 먼저 고정해야 개발·QA 반려를 예방할 수 있습니다.
1. **"마지막 가져온 버전(Last imported version)"의 결정 기준**:
   - 로컬에 여러 카탈로그(기존 schema 1 기준 데이터 + 신규 가져온 카탈로그)가 있거나 하나도 없을 때, `CatalogIndex`의 최신 항목, `Library/TftAnalyzerStore`의 상태 파일 중 무엇을 기준으로 최신 원본 버전과 비교할지 우선순위를 명시해야 합니다.
2. **"수동적 알림(Passive notification)"의 구체적 UI/로그 형태**:
   - Editor 시작 시 모달 대화상자나 강제 창 열기는 금지됩니다. `Debug.Log` 단일 메시지 또는 이미 열린 `TftDataAnalyzerWindow` 내 비차단 상태 표시 중 허용 형태를 명확히 정의해 창 잔류 문제를 원천 차단해야 합니다.
3. **24시간 상태 파일의 경로 보안 및 손상 복구**:
   - `Library/TftAnalyzerStore` 아래 저장되는 버전 확인 상태 파일 역시 `AnalyzerStorePaths.RejectReparsePoints`를 적용해 심볼릭 링크/접합점을 거부하고, 파일이 손상되었을 때 Editor domain reload나 시작을 차단하지 않고 안전하게 실패/초기화하는 계약을 명시해야 합니다.

### ④ 상위 설계 문서와 하위 구현 계약 간의 정합성 유지
- **HTTP 429 재시도 정책**: [TFTDataPipeline.md §11](../Assets/Plan/TFTDataPipeline.md)의 "자동 재시도 시점 표시" 표현과 달리, 실제 구현 계약은 **"즉시 중단 후 수동 재시도 가능 UTC만 표시하며 자동 재개하지 않음"**으로 확정되었습니다.
- **외부 데이터 갱신 중단 범위**: [TFTDataPipeline.md §4](../Assets/Plan/TFTDataPipeline.md)의 "등록 상태가 유효하지 않으면 외부 데이터 갱신과 배포를 중단" 중, Task 5는 개인 연구용 Editor 활용을 보존하기 위해 **Player 빌드(배포) 차단만 수행하고 외부 데이터 갱신 중단은 범위 밖으로 분리**했습니다.
- **검수 지침 반영**: 하위 사이클에서 보안·사용성 이유로 상위 설계보다 보수적이거나 분리된 결정을 내린 경우, 후속 기획 검수에서 이를 결함으로 오판하지 않도록 **구현 계획과 워크플로 지침에 우선 적용 규칙을 명시**합니다.

---

## 3. 기획 검수 체크리스트 (향후 사이클 공통 적용)

모든 신규/후속 사이클의 기획 에이전트(기획 A: 범위·정책, 기획 B: 기술·검증)는 기존 워크플로 규칙에 더해 아래 4가지를 필수로 점검합니다.

1. **Fail-Closed 정책의 실데이터 통과 경로**: 차단 규칙(최소 표본, 증강 정책 분류 등)이 정상적인 기준 카탈로그·실데이터 입력까지 영구 차단하지 않도록 메타데이터 공급 경로가 정의되어 있는가?
2. **집계 입도와 최소 표본의 현실성**: 수집 상한(예: 1,000건) 대비 집계 키(Fingerprint)의 조합 공간이 지나치게 잘게 쪼개져 `n >= 100` 달성이 불가능해지지 않는가?
3. **백그라운드/시작 작업의 비차단·창 무결성·경로 보안**: `[InitializeOnLoad]`나 지연 작업이 모달 창·새 EditorWindow를 띄우지 않고, `RejectReparsePoints`와 손상 시 비차단 복구를 보장하는가?
4. **상위 설계 대비 하위 확정 계약의 일관성**: 429 수동 재시도, Player 빌드 전용 차단(개인 연구용 Editor/로컬 CSV 허용) 등 확정된 하위 계약을 침해하지 않는가?
