# 분석 최소 표본 수 설정 구현 계획

상태: 사용자 승인 범위 구현·독립검수·전체검증·QA통과, PR18 head ea890d6 검수통과 후 main3484b13 병합(2026-10-21 UTC04:16:16). 사용자 최종 결과 승인은 별도다. 기획 기준310faf6, 구현 기준0c6c1d55d1978d6e737e7a736c3c9f098664e447. 병합 결과와 검증 head의 전체 트리 일치를 확인했다.

**Goal:** 과거 집계 분석 탭에서 비율을 공개할 최소 표본 수를100이상으로 설정하고, 명시적으로 로컬 집계를 갱신했을 때만 그 기준을 적용한다.
**Architecture:** 기존 MetricPolicy의100하한·증강 분류 차단을 유지한다. 요청별 기준을 서비스에 전달하고 결과 스냅샷에 실제 적용 기준을 기록한다. 입력 변경은 이전 결과/CSV 권한을 즉시 무효화하고 진행 중 요청을 취소한다.
**Tech Stack:** Unity6000.3.8f1, C#, UI Toolkit/UXML, NUnit EditMode, 기존 Unity CLI/소유 창 검증 도구.
**Spec:** Assets/Plan/TFTDataPipeline.md §3.6, 기존 분석 서비스/VM/탭 계약. 프로젝트 워크플로에 따라 A(Antigravity)/B(GPT) 독립 검토 및 사용자 승인 후 개발한다. 구현은 root가 맡고 기존 검수/QA 담당을 재사용한다.

## 사용자 동작과 범위

- 과거 집계 분석 탭의 갱신 버튼 위에 ‘최소 표본 수’ 정수 입력(기본100), ‘100 이상, 기준 변경 후 로컬 집계 갱신 필요’ 안내, 결과가 있을 때 실제 적용 기준을 표시한다. 이 명칭/배치는 root의 제안이며 새 사용자 승인 대상이다.
- 99/0/음수는100으로 몰래 보정하지 않고 오류 안내·갱신/CSV 차단한다.100/200/int.MaxValue는 유효하며 큰 값은 대부분 ‘표본 부족’이 될 수 있다. 100 이상 값은 임의 상한으로 낮추지 않는다.
- 값이 실제로 바뀌면 유효/무효와 관계없이 이전 snapshot/rows/pairs를 비우고 요청 세대를 바꿔 진행 중 결과가 복귀하지 못하게 한다. 자동 갱신/네트워크/자동 수집/환경설정 저장은 없다. 같은 값은 멱등적이다.
- 창의 기존 RebuildUI·탭 전환·카탈로그 변경에서는 창 VM의 입력값을 유지한다. 창을 닫거나 Editor를 다시 시작한 뒤에는100부터 시작하며 영속 설정은 추가하지 않는다.
-100↔200 변경 모두 명시적 갱신이 필요하다. 서비스에 주입된 정책 하한이 더 높으면 그 값을 유지하고 실제 적용 기준을 표시한다. 증강 metadata를 잃거나 Unknown/Legend 차단을 완화하지 않는다.
- CSV 형식/행별 익명 지표는 그대로 둔다. 현재 요청에 적용된 결과만 저장할 수 있고 입력 변경/카탈로그 변경/갱신/취소·실패 때 이전 결과를 저장하지 못한다. 저장 대화상자 중 기준이 바뀌면 기존 snapshot 참조 검증으로 쓰기를 차단한다.
-100은 조합 행의 참가자 관측 수, 상대 배치 행의 비교 가능 로비 수에 각각 적용된다. 하한 완화·새 그룹 집계·범위 혼합·실제 분류 등록·실제 API 실행은 포함하지 않는다. 완전 보드 지문 때문에 n100이 어려운 기존 한계도 바뀌지 않는다.

## 변경 파일/인터페이스

- Core/Analytics/MetricPolicy.cs: 불변 public MetricPolicy WithMinimumSamples(int minimumSamples)를 추가한다. 기존 검토 metadata를 그대로 재사용하고 생성자의100하한으로 잘못된 값을 거부한다.
- Editor/UI/Analysis/AnalysisService.cs: IAnalysisService/AnalysisService.LoadAsync(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token, int? minimumSamples = null)로 요청별 입력을 받는다. 기존3인자 호출은 소스 호환되지만 interface 구현체의 시그니처 이관이 필요하다. null은 기존 주입 정책 기준을 유지한다.100미만은 저장소 접근 전에 거부한다. effective=Math.Max(_policy.MinimumSamples, requestedOrExisting). provider 경로도 effective와 검토 metadata를 함께 새 정책에 넣는다. 공용 정책을 변경하지 않는다.
- AnalysisModels.cs: AnalysisSnapshot.MinimumSamples(읽기 전용)와 생성자 끝의 int minimumSamples=100을 추가하고100미만 거부. 실제 Present는 사용한 policy.MinimumSamples를 전달한다. 기존 BuildSnapshot(policy)도 실제 기준을 보존한다.
- AnalysisViewModel.cs: MinimumSamples 기본100, SetMinimumSamples(int) 추가. 무효 입력도 보존·안내하되 서비스 호출0, CanRefresh/CanExport false. 변경 시 세대++/취소/결과 초기화. RefreshAsync는 시작 시 값 캡처해 서비스 호출; snapshot.MinimumSamples가 요청보다 낮거나100미만이면 실패로 처리한다. 더 엄격한 실제 기준은 허용·표시한다.
- AnalysisTab.cs/UXML: analysis-minimum-samples IntegerField, analysis-minimum-status 안내/적용기준 Label. 필드 이벤트 구독/해제, SetValueWithoutNotify, 기존 일회성 update/Dispose 규칙. 입력은 요청 중에도 변경 가능하며 해당 요청 취소; 필터/갱신/CSV 기존 busy 규칙 유지. CSV 대화상자 중 변경도 차단한다.
- Assets/TFTAnalyzer/Tests/EditMode/Analytics/MetricPolicyTests.cs와 Assets/TFTAnalyzer/Tests/EditMode/UI/AnalysisServiceTests.cs, AnalysisViewModelTests.cs, AnalysisTabTests.cs를 확장한다. 기존 UI/AnalysisCsvExporterTests.cs는 회귀 대상으로 포함한다. 불필요한 새 테스트 프레임워크를 만들지 않는다.
- 인터페이스 구현체 이관: AnalysisViewModelTests.Service, AnalysisTabTests.FakeService와 AgentScripts/TftAnalyzer{Analysis,Catalog,Compliance}VisualValidation.cs의 가짜 서비스. 가짜 snapshot도 전달된 기준을 기록하게 한다. 실제 네트워크/저장소 실행을 가짜 구현에 추가하지 않는다.

## Review Focus

1. provider에서 새 MetricPolicy를 만들 때 입력 기준이나 검토 metadata가 유실되지 않기.
2. 이전 비율이 노출된 상태에서99/200 입력·실행 중 변경·늦은 응답·CSV 대화상자가 겹쳐도 이전 결과를 쓰지 않기.
3. 주입된 기준200을 UI100이 낮추지 않기; 요청별200→100이 다른 요청/창 정책을 오염시키지 않기.
4. 필드 재구성/Dispose 후 옛 입력이 VM을 수정하거나 update콜백/창이 남지 않기.
5. 조합/상대 배치의 분모 차이와 정책 차단 우선순위를 유지하고 새 설정만으로 실제 비율 사용 가능이라고 알리지 않기.

## Task1: 요청별 정책과 결과 기준

- [x] metadata 보존/하한 테스트 RED: clone200의 n199 비율없음/n200 비율있음, Legend/Unknown은 n200에서도 차단. clone99 예외, 원래 policy100 불변.
- [x] 서비스 RED: 요청99는 관측/provider/cache호출0, provider성공/미등록/실패에서 요청200이 유지되고 증강 차단 유지. 주입policy250+요청100은 snapshot250, 다른 요청으로 임계값 유출0. BuildSnapshot(policy200)도 snapshot200.
- [x] 최소 구현 및 해당 Core/UI 서비스 회귀 GREEN, 기존3인자 호출/모든fake 시그니처 컴파일.
- [x] 독립 코드 검수 통과 후 체크. 원문실행/변경이유 기록.

## Task2: 입력·무효화·명시적 적용 UI

- [x] VM RED: 기본100,99·0·음수 차단/서비스0,200전달, 같은값 멱등, 입력 변경으로 snapshot/export차단, 취소를 무시한 늦은응답 폐기,카탈로그변경/rebuild/Dispose 보존·해제, 실패/취소 후 과거결과 부활0.
- [x] 실제 UXML/소유 창 RED: IntegerField 기본값/99안내/버튼차단/200갱신과실제적용표시, n199·200의조합/쌍비율, CSV미달행비율빈값, CSV대화상자 중 기준 변경시write0,재구성후옛필드무효.
- [x] 최소 구현 GREEN. CSV 헤더 변경 없이 현재 policy가 허용한 presentation만 전달되는지 확인.
- [x] 독립 코드 검수 통과 후 체크.

## Task3: 통합 증거·QA·PR

- [x] 관련 Analytics/UI 회귀 및 전체 EditMode1회, 실패/제외를 구분. 동결 소스/기존 카탈로그·관측·사용자 파일을 전후 비교하고 기존 자동 버전확인 상태 파일의 합법적 변경도 따로 보고.
- [x] 기존 분석 소유 창 검증 도구를 최소 확장해 실제940/500px 입력/오류/적용기준/표본부족을 PNG로 확인. 실행 전후 EditorWindow/native/포커스, finally 정리/예외 경로를 검증. 기존 사용자 창/Inspector 보존.
- [x] 기존 QA 담당이 승인 TODO/실행/이미지/범위 대조. 문서/원문/체크포인트 갱신, 실제 모델/추론·측정 가능한 시간/사용량만 기록.
- [x] 코드·문서 함께 PR18, 최신head ea890d6 CodeRabbit SUCCESS/미해결0/별도CI0 확인 후 main3484b13 병합. root가5분간격조회했으며 Hermes 예약/AI 반복호출은없다. API 키·실제 분류·실제 표본 검증은 별도 남은 과제로 유지.
A/B 독립검토 pass·필수보완0 후 사용자가 “이 계획대로 구현 진행 (권장)”을 명시 승인했다. 완료 상태는 실제 TODO 증거를 따른다.

Ruling(2026-10-21): 실제 UI 회귀에서 저장 창 중100→200→100 입력이벤트 지연으로write1이재현됐다. 승인된CSV일관성조건을충족하도록경로선택후일회성Editor update에서소유snapshot을재검사해저장하고Dispose/Rebuild에서취소한다. 유효저장테스트도실제update뒤쓰기완료를검증한다. CSV포맷/네트워크권한은변경하지않는다. 관련 Analytics/UI 회귀는전체suite의실제Results에서도분리집계해불필요한중복실행을피한다.
