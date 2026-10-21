# 코드 조사로 고정한 계약
기준310faf6. 제품 변경/실제 네트워크 테스트를 수행하지 않은 기획 입력이다.
상위 TFTDataPipeline.md §3.6: 기본 최소 표본100, 비율은 Wilson95%,100미만은 숨김. 설정은100이상으로만 높일 수 있다.
현재 IAnalysisService.LoadAsync(catalog,identity,token), AnalysisService 생성자 policy/provider 동시주입 금지. _policy는 기본100. provider.Load 성공 시 new MetricPolicy(augmentMetadata:read.Metadata)여서 per-request 기준 경로가 없다. 서비스는 explicitRefresh 뒤 store를 읽고 aggregate 캐시를 저장한다. BuildSnapshot은 주입 policy 사용.
MetricPolicy.MinimumSamples 읽기전용, 생성자 minimumSamples=100/augmentMetadata=null;100미만 예외. metadata private immutable. Decide는 catalog/scope→Unknown/Legend차단→sample비교. 조합Count는 참가자관측,PairCount는비교가능로비.
AnalysisSnapshot은Identity/AllRows/AllPairs/기간, 아직 최소표본필드 없음. 생성자는internal.
AnalysisViewModel은 IAnalysisService를 주입받고 Catalog/ActiveIdentity/Snapshot/Rows/Pairs/Filters/IsBusy/CanRefresh/CanExport/StatusText를 유지. SetCatalog 변경은 generation++/Cancel/snapshot비움. RefreshAsync는 generation/token을 검사해 stale결과폐기, 시작 때 이전snapshot비움. SetFilters는 기존행만선택하고 재집계하지 않음. Dispose취소/이벤트해제.
AnalysisTab은실제 UXML과 VM결합, 일회성EditorApplication.update,Dispose해제. Export는 대화상자 전에 snapshot/identity/rows/pairs/visibleFilters를고정하고 이후CanExport/참조/identity/setnumber/필터변경을검사해달라졌으면쓰기차단. _writeExport의signature는 Action<string,IReadOnlyList<MetricPresentation>>. CSV는presentation에이미차단된비율만직렬화하고threshold컬럼은없음.
실제 서비스VM은 창 RebuildUI에서 유지되고binding만재생성된다. 최소표본은새창기본100이며환경설정저장은현재범위밖.
IAnalysisService구현체는제품 AnalysisService, 테스트 AnalysisViewModelTests.Service/AnalysisTabTests.FakeService, AgentScripts/TftAnalyzer{Analysis,Catalog,Compliance}VisualValidation.cs 내 가짜3개다. 후자는실제네트워크/기록에접근하면안됨.
기존 실제증강 승인등록0,키/seed미설정. 사용자가로컬설정을준비하겠다고답했으나완료증거없음. 이번계획으로비율영구차단원인이나완전보드지문n100부족이해결됐다고주장하지않음.
사용자목표는미구현계획구현·최신PR검수5분조회/통과시병합. 새기획은A/B독립검토후명시승인필요. Hermes예약은삭제상태. 기존소유창도구/사용자데이터보존규칙을유지.
