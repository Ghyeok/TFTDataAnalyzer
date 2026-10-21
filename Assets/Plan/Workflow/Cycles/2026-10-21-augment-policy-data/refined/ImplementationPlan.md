# 실제 증강 분류 자료 첫 등록 구현 계획

상태: 반려됨. **A/B 반려, 추가 공식 근거 필요, 미승인/미구현**. 아래는 검토한 제안이며 확정 분류가 아니다. 현재 등록0을 유지한다. 기준 main3484b13fa30a09ba4c6319fa071c17448d46e8b8(PR18 병합). 새 분석 역할이나 새 에이전트 생성은 없다.

**Goal:** 근거를 개별 검토한 실제 증강 ID만 기존 해시 고정 공급 경로에 등록하고, 미확인 증강의 비율 차단을 유지한다.
**Architecture:** 기존 schema1 정책 JSON과 ReviewedAugmentPolicyProvider.Approved 등록만 사용한다. 카탈로그 및 정책 문서 해시를 고정하고, 기존 MetricPolicy의 100 하한·Unknown/Legend/LegendBased 차단을 그대로 적용한다.
**Tech Stack:** Unity6000.3.8f1, C#, 기존 strict JSON reader, NUnit EditMode, 로컬 카탈로그.
**Spec:** Assets/Plan/TFTDataPipeline.md, 기존 PR16 분류 공급 계약, 서비스 잔여 점검. executing-plans 방식으로 root가 구현하며 기존 코드검수/QA 담당을 재사용한다.

## 범위와 데이터 판단

- 현재 카탈로그는 TFTSet18/18.2/16.18.1/official-only, contentHash b306b524fad37119ea88fe28f2f7241423ee997f06bddc563890adb35f0c27f9, 증강485개다. 기존 catalog/manifest/icon/관측/사용자 파일은 변경하지 않는다.
- 첫 제안은 아래 정확한4개 ID의 Standard 분류다. 기획 검토에서 근거가 부족한 항목은 등록 대상에서 제외한다. 검토된 확정 ID 목록과 근거를 사용자 승인안에 명시하며, 승인 이후 별도 ID를 추가하지 않는다.

| ID | 이름 | 개별 검토 근거와 한계 |
|---|---|---|
| DA_18_CovenTraitAugment | Coven Acolyte | 공식18.2의 일반 Augments 절과 고유 Coven 정기 수집/회복 효과를 로컬 설명에 대조. 일반 특성 증강이라는 분류는 문맥 기반 검토 판단이며 공식 enum이 아니다. |
| DA_18_CovenTraitAugment_LootToAP | Dark Ritual | 같은 절의 Coven cashout AP 효과를 로컬 설명과 대조. 두 Coven 증강은 해당 패치에서 서로 배타적이므로 실제 조합 예시로 둘을 동시에 선택하지 않는다. |
| DA_18_FloraFatalisAugment | Consuming Flora | 공식18.2의 고유 Flora 상징 효과를 로컬 설명과 대조. 패치 노트의 수치와 캐시 설명의 차이는 정책 분류 근거와 데이터 최신성 검증을 구분한다. |
| DA_18_RivalsAugment | Unrivaled | 공식18.2의 Rengar/Kha'Zix 상호 효과를 로컬 설명과 대조. 이름/prefix만으로 판단하지 않는다. |

- 위4개가 Legend 기반이 아닌 Set18 고유 일반 특성 증강이라는 판단을 A/B가 별도 검토한다. 검토 결과는 인간 또는 Riot의 공식 승인으로 표현하지 않는다. 실제 JSON 등록은 사용자 범위 승인과 이후 독립 코드/자료 검수 통과가 필요하다.
- 향후 승인 확정 목록을 S, 그 개수를 N으로 두며 미등록 485-N개는 Unknown을 유지한다. 현재는 N=0이다. 명시적 Unknown 규칙485개를 만들어 공급 완료처럼 표시하지 않는다. Big Grab Bag은 역사적 Legend 표와 현행 ID의 효과가 달라 보류한다. 중복 이름119개와 Baron's Lair/Nesting Dolls의 복수 ID 때문에 이름 일괄 매칭·접두사 추론·자동 분류는 금지한다.
- 후보 중 일부 공급은 전체 지원 완료가 아니다. 모든 관측 증강이 허용된 경우에만 비율이 열리고 미등록 하나라도 섞이면 계속 차단된다. N=0이면 빈 승인 등록을 하지 않고 해당 자료 과제를 미완료로 보고한다.
- 실제 API 요청·PUUID 조회·환경 변수 변경·자동 수집·n100 완화·새 그룹 집계·공개 배포는 이번 범위 밖이다. 현재 Chrome 창/탭·기존 Unity 사용자 창을 보존한다.

## 변경 파일과 인터페이스

- 생성: Assets/TFTAnalyzer/Data/Policy/<documentHash>.json 및 Unity meta. schema1, catalog 전체 식별자, contentHash, rules의 augmentId/kind만 넣고 기존 strict reader를 변경하지 않는다. 정확한 UTF-8 바이트 SHA256을 파일명/등록값에 사용한다.
- 수정: Assets/TFTAnalyzer/Editor/Compliance/ReviewedAugmentPolicyProvider.cs의 Approved 정적 배열에 단1개 등록. CatalogIdentity/내용·문서 해시/reviewedUtc/HTTPS sources를 지정한다. 임의 파일 발견, 사용자 입력 승인, 네트워크 조회는 추가하지 않는다.
- 생성 테스트 제안: Assets/TFTAnalyzer/Tests/EditMode/Compliance/ApprovedAugmentPolicyDataTests.cs. 양성 검증은 기본 provider와 실제 저장소의 변경하지 않은 기준 카탈로그·운영 정책 JSON을 직접 읽는다. 치환 fixture는 해시/identity 변조 음성 시험에만 사용한다. 현재 계획 반려 상태이므로 테스트는 작성하지 않는다.
- 근거/문서: 이 사이클 PlanningReview.md, Checkpoint.md와 공개 AI 기획 요청/응답은 실제로 보존한다. 실제 API 원문을 뜻하지 않는다. 개발/QA/최종 문서는 해당 단계 실행 후 생성한다. README의 공급 상태는 승인 등록 전0으로 유지한다.

## Review Focus

1. 실제 ID별 근거가 일반 증강 분류를 뒷받침하는가; 이름 재사용·Legend 역사·고유 효과의 불일치가 있는 항목은 등록하지 않는다.
2. 실제 기본 provider 경로를 통해 확정 목록 S의 N개만 공급되는가; 테스트 전용 주입 성공으로 운영 등록 성공을 대체하지 않는다.
3. 현재 카탈로그/문서 해시가 다르면 차단되며 미등록 485-N개는 허용되지 않는가.
4. synthetic n100 결과와 실제 관측 자료 확보·실제 사용 가능성을 혼동하지 않는가.
5. 원본 카탈로그·관측·사용자 파일과 Chrome/Unity 창을 보존하는가.

## Task1: 근거 확정과 실제 등록

- [ ] A/B가 같은 기준/입력 snapshot의 네 항목 근거와 범위·완료조건을 독립 검토. 필수 보완 반영 후 확정 ID별 판단을 포함한 사용자 명시 승인 대기.
- [ ] 사용자 승인으로 S와 N>0이 확정된 경우에만 실제 기본 provider에 기준 catalog/identity를 전달해 Loaded 및 정확히 S의 N개 Standard 분류를 기대하는 RED. 현재 Approved0 때문에 Missing으로 실패해야 하며 파일 없음/설정 오류를 RED로 세지 않는다. N=0이면 등록·양성 테스트 구현을 시작하지 않는다.
- [ ] 기본 provider의 단1개 승인 등록과 해시 고정 JSON만 추가해 GREEN. 정책 reader와 분류 차단 로직은 변경하지 않는다.
- [ ] 데이터 근거표는 각 ID·공식URL·확인일·로컬 효과 대조·분류 판단/미확정 한계·등록 해시를 연결. 기존 코드검수 담당이 자료와 diff를 함께 독립 검토.

## Task2: 실제 공급의 분석 통과·차단과 QA

- [ ] 새 실제 등록 metadata + 기준 identity + 합성 Aggregate로 n99 차단/n100 허용을 확인. 양성 조합은 확정 목록 S 안에서 실제로 가능한 조합으로 정하고, 구성할 수 없으면 해당 양성 검증은 미완료로 기록한다. S 밖의 미등록 증강 혼합은 차단, 잘못된 identity/content/document는 기존 차단을 유지한다. 기존 Legend/LegendBased 회귀도 실행.
- [ ] 테스트는 운영 JSON/Approved를 읽고 실제 API/관측 쓰기0. 정확히 S만 dictionary에 있고 미등록 485-N개는 없는지 검증하며, 승인 수/표본을 과장하는 보고를 막는다.
- [ ] 관련 Compliance/Analytics/UI 서비스 회귀와 변경에 적합한 전체 EditMode1회. 현재 입력 필드/창은 변경하지 않으므로 새 시각 캡처를 반복하지 않는다. 카탈로그·관측·사용자 파일 전후 해시 보존.
- [ ] 기존 QA 담당이 TODO/실행/등록 바이트/자료 한계/원본 보존을 대조. 문서 링크·meta·비밀값 비포함 확인 후 코드/자료/문서 함께 PR.
- [ ] 최신 head CodeRabbit/미해결/CI를5분마다 root가 확인하고 조건 충족 시 기존 승인대로 병합. Hermes 예약은 삭제 유지. 사용자 최종 결과 승인과 실제 API/n100 검증은 별도 남은 과제다.
