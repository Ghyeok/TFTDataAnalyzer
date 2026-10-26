# 보완 정적 자료 형식 호환 기획안

상태: 진행 중

root가 executing-plans로 구현하고 기존 코드검수/QA 담당을 재사용한다. 신규 담당 생성·재위임을 하지 않는다.

**Goal:** 실제 보완 문서의 선택 세트와 공식 ID 범위를 확인해 기존 정적 필드로 공급한다.
**Architecture:** adapter의 명시적 세트 선택·범위 제한·관계 정규화와 catalog+진단 결과를 pipeline의 기존 검증/보고/불변 게시 경계에 연결한다.
**Tech Stack:** Unity6000.3.8f1, C#, Newtonsoft, NUnit EditMode, 기존 UI Toolkit 보고서.
**Spec:** [상위 설계](../../../../TFTDataPipeline.md) §6·7, [워크플로](../../../README.md) §3.1.6. [기획 통합](PlanningReview.md).

기획 기준54a29f35fad8d5c3624654dd2a281052bec7a340. PR21 병합 main5a67afb679bd9c2e91cf71f3bc83b2ce80622bcc와 전체 트리가 일치함을 확인한 뒤 전용 브랜치에서 구현했다. [동결 검토 자료](../../../../../../AntigravityCollab/tasks/2026-10-22-community-shape-planning/result.md).


## 목적과 사용법

사용자가 데이터 관리에서 보완 자료 사용을 직접 선택한 가져오기가 실제 CommunityDragon 문서 구조를 읽도록 한다. 현재 데이터 관리·탐색 UI의 동작과 공식 우선 병합은 유지한다. 새로 읽은 값은 기존 필드별 출처 표시에서 확인한다. 실제 다운로드 없이 창작 최소 fixture와 fake transport로 검증한다. 2026-10-22 사용자가 call_16b237d5587847698b6f91044400c6d5에 ‘이 계획대로 구현 진행’으로 명시 승인했다. PR21 병합·main 트리 일치 확인 후 이 범위의 구현을 시작한다.

## 문제와 선택

저장된 문서로 현재 Parse를 진단 실행했더니 기물·시너지·아이템·증강이 모두0이었다. 이 캐시의 URL/버전/locale은 검증하지 못했으므로 카탈로그로 게시하거나 정확한 특정 패치의 정상 자료로 간주하지 않는다. 선택 setData의 실제 레코드와 API stable ID 접두사 간 불일치, 빈 set.items가 root.items를 가리는 문제, root.items의 isAugment 구분 부재가 코드에서 확인된다.

접두사만 추가하는 방법은 다른 세트·전역 아이템 범위를 입증하지 못한다. 전역 레코드를 모두 허용하는 방법도 거절한다. 권고는 선택 세트의 명시적 소속과 같은 요청의 검증된 공식 stable ID 범위를 사용하는 방식이다. 상위 설계의 능력치·시너지 구간·아이템 제한 모델 추가는 별도 후속이며 이번에는 기존 모델 필드의 실제 공급 경로를 완성한다.

## 파싱과 경계

1. setData.number의 선택은 유지하고 같은 number의 중복·잘못된 배열 형식·중복 stable ID는 안전한 진단 오류로 거절한다. selected-set champions/traits의 명시적 소속은 세트 접두사 대신 판단 근거다. 다른 setData의 값은 유입시키지 않는다. 기존 명시적 set.items/set.augments fixture 형식도 지원한다.
2. 비용1~9가 아닌 selected-set 기물은 기존 도메인 대상 밖이므로 제외하되 개수와 정적 ID를 경고로 남긴다. 비용 필드 손상/문자열/누락은 정상 제외로 숨기지 않고 오류다. 비용0 제외가 공식 카탈로그 ID를 제거하려 하면 오류다. 보완 전용의 유효한 selected-set 기물/시너지는 기존 지원을 보존한다.
3. 기물의 trait 참조가 stable ID면 그대로 검증한다. 원본이 표시 이름이면 같은 locale의 같은 선택 세트 trait 목록에서 정확히 하나인 이름만 해당 stable ID로 정규화한다. 결과에는 stable ID만 저장한다. 부분·대소문자 무시·번역·다른 locale 추측은 하지 않는다. 중복 이름/없는 참조/손상 토큰은 오류다. primary는 기존 en 우선이며 참조와 출처는 실제 같은 문서에 연결한다.
4. 기물 설명은 기존 flat desc/description을 우선하고 비었을 때만 ability.desc를 locale별로 읽는다. 비용·역할·기존 flat numeric effects·아이콘·문자열/목록은 기존 계약을 유지한다. stats, ability.variables, trait effects 배열은 기존 모델에 억지로 평탄화하지 않는다. 미지원 구조가 존재한다는 제한과 개수 경고를 가져오기 보고서에 남긴다.
5. 명시적 set.items/set.augments가 없거나 빈 배열이고 root.items가 있으면 같은 요청의 공식 카탈로그가 전역 범위를 제한한다. NetworkStaticCatalogSource가 이미 읽은 official을 전달하고 patch 전체 일치와 official-only를 확인한다. root 레코드는 isAugment의 명시적 bool과 동일 종류 official stable ID의 정확한 교집합으로 선택한다. 다른 세트나 글로벌 보완 전용 아이템은 소속 근거가 없으므로 제외·개수 경고한다. isAugment는 정적 종류 판별일 뿐 Legend 제외나 통계 허용 근거로 쓰지 않는다.
6. root 전역 경로에 official scope가 없거나 잘못됐으면 ‘보완 자료 범위를 확인할 공식 카탈로그 필요’ 오류를 반환한다. 기존 Parse 3인수와 set-scoped fixture 호출은 호환 유지하되 실제 전역 형식이 검증 없이 빈 성공이 되지 않는다. 선택 후보의 중복ID·locale별 종류 상충·필수 bool/ID 손상은 오류다. 범위 밖 전역 레코드 전체의 임의 결함으로 정상 범위를 막지는 않는다.
7. item composition은 실제 stable ID 문자열 목록만 허용하고 참조는 기존 validator로 검증한다. 없는 등급·스탯·제한·효과값을 추정하지 않는다. ID 기반 병합의 공식 우선, 값 충돌 보고, 각 필드의 실제 source stamp는 유지한다.

## 진단과 저장

CommunityDragon 파싱 결과의 catalog와 진단을 함께 반환하는 경로를 추가하고 기존 호출은 catalog wrapper로 보존한다. StaticSourceLoadResult에 선택적 immutable 진단 목록을 추가해 CatalogValidator 결과와 합친다. 오류는 게시를 차단하고 경고는 기존 정적 가져오기 보고서에서 읽는다. 본문 전체·비밀·원본 경기 자료를 진단에 넣지 않는다. 경고는 종류별 bounded count와 필요한 정적 ID의 한정 목록만 표시한다. 문서 shape 미지원/후보0이었음을 조용히 성공으로 숨기지 않는다.

기존 schema3 모델·JSON 형식은 유지한다. schema1/2 및 기존 팩 파일·해시·전체 identity 경로는 변경하지 않는다. 새 파싱으로 값이 달라졌는데 동일 identity 팩이 이미 있으면 기존 불변 충돌로 거절하고 덮어쓰거나 마이그레이션하지 않는다. 이 제한을 사용자 문서에 명시한다.

## TODO와 객관적 완료 조건

- [x] 창작한 실제 구조 최소 fixture에 다른 namespace의 selected-set 기물/trait, 표시이름 참조, nested 설명, root.items와 bool을 포함한다. 현재 actual adapter의 값0/필드누락을 기능 RED로 확인한다. 원본 캐시 복제나 특정 locale/버전 인증을 대신하지 않는다.
- [x] selected-set·비용·stable 참조·설명 파싱을 구현한다. 정상/다른세트/중복/비정상비용/locale단독/모호한이름의 GREEN 및 공식우선 회귀를 확인한다.
- [x] official-bound root item/augment와 source profile/patch 일치·필수 bool·종류 상충·보완전용 전역 제외를 검증한다. 기존 set-scoped 보완 전용 경로는 유지한다. 후보0/미지원 shape가 보고되고 오류는 publish를 막는지 확인한다.
- [x] fake transport의 실제 NetworkStaticCatalogSource→병합→아이콘 staging→임시 게시→loader→browser 경로에서 값과 FieldOrigins를 대조한다. 정상 증강도 MetricPolicyMetadata 미공급 차단이 유지되는지 확인한다.
- [x] schema1/2 골든·schema3 기존 fixture·동일입력 해시·동일identity 불변 충돌/취소/실패복구를 회귀 검증한다. 기존 카탈로그/관측/사용자 파일은 보호 해시로 확인한다.
- [x] 관련 회귀 후 소스 동결·전체 EditMode1회. UI layout을 변경하지 않으면 동일 UI의 불필요한 창 캡처는 반복하지 않는다. 표시나 레이아웃이 실제 바뀌면 기존 소유 창 도구와 지침을 따른다. 같은 코드검수·QA 담당을 재사용하고 신규 agent/재위임0을 지킨다.
- [ ] 문서/raw/meta/모델·확인 가능한 사용량과 제한을 기록하고 PR. 최신 head CodeRabbit·미해결·CI·실행 증거를5분마다 확인해 승인 범위대로 병합. 사용자 최종 결과 승인은 별도다.

## 제외·보존

실제 정적 다운로드/Match API/키 환경 변경, 증강 정책 분류 등록, n100/집계 변경, structured stat/trait tier/item restriction 모델, 원본 캐시 게시, 기존 팩 덮어쓰기, KI-001 반복 조사, 공개 배포, Hermes 예약은 이번 범위 밖이다. Assets/DefaultVolumeProfile.asset 및 docs/riot-api-key.md는 보존하고 키 문서 본문을 읽지 않는다. Chrome·사용자 Unity 창·Inspector를 보존한다.


## 파일별 책임과 검수 초점

| 파일 | 변경 책임 |
|---|---|
| Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon/CommunityDragonAdapter.cs | 기존3인수 Parse wrapper 유지, official scope를 받는 진단 포함 파싱 경로, 선택세트·정확한 관계·locale 설명 매핑 |
| Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon/CommunityDragonParseResult.cs (신규) | StaticCatalog Catalog와 IReadOnlyList<ValidationIssue> Issues의 불변 결과. 오류를 버리는 성공 wrapper 금지 |
| Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs | NetworkStaticCatalogSource가 동일요청 official을 전달. StaticSourceLoadResult의 선택적 진단과 validator Issues를 합쳐 최종 validation으로 전달 |
| Assets/TFTAnalyzer/Tests/EditMode/Ingestion/CommunityDragonAdapterTests.cs 및 새 CommunityDragonShapeTests.cs | 실제형태 최소 fixture를 통한 기능 RED/GREEN, 세트/종류/locale/관계/손상/진단 경계 |
| Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs | fake transport 실제 기본source 경로·보고·게시 차단/기존팩 보호 회귀 |
| Assets/TFTAnalyzer/Tests/Fixtures/Static/ (새 창작 fixture 2개) | 작은 ko/en 데이터, 실제 원본 복사·인증 주장이 아닌 형식 회귀 입력 |
| README.md, 본 cycle 문서 | 사용법·새로 읽는 범위·미지원 구조·같은 identity 충돌·실행 한계 |

검수 핵심은 prefix만 교체한 가짜 성공, global 항목의 세트혼합, 이름의 모호한 관계, 빈 자료의 조용한 성공, 진단 유실, 공식 우선/원본 출처/구팩 충돌의 회귀다. 이들은 위 TODO의 운영 경계 테스트로 확인한다. 기존 UI와 모델의 무관한 재구성은 하지 않는다. 오류가 있는 파싱 결과는 직접 Parse wrapper에서도 성공 catalog로 반환하지 않는다.


실행 기준: PR21 병합 main5a67afb679bd9c2e91cf71f3bc83b2ce80622bcc와 기획54a29f3의 전체 트리 일치 확인. codex/community-shape-compatibility에서 구현한다. 사용자 파일 변경은 별도로 보존한다. 단일 연결 Unity 프로젝트와 검증 경계를 유지하기 위해 기존 작업 위치의 전용 브랜치를 사용하고 추가 worktree/Editor를 만들지 않는다.


2026-10-26 진행도 점검: 개발 TODO7개 중 앞4개의 실행 조건을 확인해 체크했다. 최신 소스7SHA는 community-shape-review-source-freeze.json과 모두 일치한다. 초기 관련97/97 후 검수수정 회귀27/27 통과, 독립 코드검수 후속pass/추가필수0이다. 97건은 검수수정 전 실행이며 같은 최종소스로 다시 실행한 전체 회귀를 뜻하지 않는다. 보호1399개는 현재도 사전SHA와 같지만 현재 Unity 창/Console 사후 상태는 미검증이다. 전체 EditMode 명령은 자동 승인 검토 사용량 한도로 실행 전에 거절되어 결과파일도 없다. 검증5~6항목·최종QA·문서완결·커밋/PR/CodeRabbit/병합은 아직 남아 있다.


2026-10-26 완료 조건 정정: 파싱 계약2의 ‘제외하되 개수와 정적 ID를 경고로 남긴다’를 현재 AddWarnings와 대조하니 비용 제외 경고에는 개수만 있고 ID가 없다. 따라서 앞선4/7 완료는3/7로 정정하고 두 번째TODO를 다시 열었다. 새 Shape_ExcludedUnitWarningRetainsItsStableId 시험을 추가했지만 승인검토한도로 **미실행**이며 기능RED라고 주장하지 않는다. 제품코드는 변경하지 않았고 기존27/27은 추가시험 전 버전의 결과다. 재개 시 먼저 새시험의 실제RED를 확인하고, bounded 정적ID 경고를 최소구현→GREEN→같은담당delta검수→최종동결과전체suite 순으로 진행한다.


2026-10-26 재개 결과: 같은 require_escalated/auto-review 경로의1회연결확인이성공해PID32384/Unityready를확인했다. 권한설정변경이나우회는0이며앞선한도차단은과거실패다. ID누락1RED와범위표본을포함한2RED를실행확인후 최소구현했다. ID예시는최대5개·원문64UTF16자와생략표시·제어문자공백으로512자보고서한도안에넣고전체건수/표본외건수를보존한다. 이는승인된bounded진단의구체화이며새범위가아니다. 현재관련104/104·신규29모두통과,코드검수delta pass/필수0. 새최종7SHA동결후전체826개824통과·실패0·호스트symlink2제외(143.15초)를실행했다. 재개전1399보호snapshot기준변경0·창9/native1/포커스동일·Console전후오류0경고0. 10월22일초기snapshot대비version-check.json1개가재개이전에이미달라진사실은분리한다. UI변경0이라추가PNG는실행하지않았다. 최종QA후속·문서/PR/최신head검수병합은별도미완료다.


최종QA후속: 기존담당pass/필수0. 최종7SHA직접대조·전체826/824P0F/호스트2제외·신규29와보호/창/Console·구schema회귀·미분류증강차단을확인했다. 새PNG미실행/실제자료인증미실행한계를유지한다. [QA보고](QAReport.md)와[실제원문](../raw/FinalQA.md). 코드/QA검수와사용자최종결과승인은별개이며현재6/7조건완료·PR통합조건대기다.
