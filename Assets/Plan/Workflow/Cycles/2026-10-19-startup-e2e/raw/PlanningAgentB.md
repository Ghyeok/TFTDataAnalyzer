# Task6 기술 기획 검토

판정: **수정 필요**. 초안과 Phase3 Task6 및 아래 실제 소스를 확인했다. 제품 수정·Unity 실행·네트워크·Git 쓰기는 수행하지 않았다. 요청 모델은 GPT-6.1 Sol/High이나 런타임 모델·사용량은 독립 확인하지 못했다. 실행 통과 보고가 아니다.

## 확정할 결함과 결정

1. **24시간 보장은 영속 예약 성공이 선행해야 한다.** `CheckAsync(token)`에 `Func<DateTimeOffset> utcNow`, 상태 저장소 `Load/TrySaveAtomic`, 버전 조회 delegate를 주입한다. 두 JSON 필드는 유지하되 `lastCheckedUtc`는 성공 시각이 아닌 시도 예약 시각으로 정의한다. 단일 실행 gate 안에서 취소 확인→상태 판정→현재 UTC와 이전 `lastSeenVersion`의 원자 저장→요청 순서다. 예약 전 취소는 차감하지 않고 예약 후 실패/취소는 24시간 차감한다. 성공만 lastSeen을 갱신하며 세대 번호·token을 확인해 재로드 뒤 늦은 결과를 폐기한다. 성공 결과 저장 실패도 이미 저장한 예약은 남는다. 읽기/예약 쓰기 실패에는 HTTP를 보내지 않는다. 손상·미래 상태는 삭제하지 않고 현재 시각/빈 버전으로 교체 성공 후 24시간 보류한다. 저장소가 외부 삭제·변조되는 상황까지 절대 상한을 보장한다고 주장하지 않는다.

2. **유휴 대기와 테스트 전역 초기화가 불명확하다.** Unity 연결은 scheduler와 분리하고 배치/재생 진입·컴파일/업데이트 중에는 요청하지 않는다. 초기 delayCall이 바쁘면 무한 재등록하지 말고 해당 상태 종료 이벤트에서 단 한 번 다시 예약한다. 재로드/종료에서 소유 callback·event를 해제하고 CTS를 취소한다. `CheckAsync`는 같은 Task를 공유한다. 테스트는 초기화 전부터 적용되는 주입 가능한 실행 환경 판정으로 테스트 실행을 제외하고, 별도의 bootstrap 계약 테스트는 동일 연결에 가짜 시계/전송/상태 루트를 주입한다. 일반 대화형 Editor에서는 실제 버전 조회가 유지되어야 한다. 가짜 결과를 실제 Library/version-check.json에 기록하지 않는다.

3. **가져온 순서를 현재 메타데이터로는 알 수 없다.** `PublishedCatalogLoader`의 `RetrievedUtc`는 manifest.createdUtc이고 이는 원본 응답 시각이다. 이를 실제 마지막 가져오기 시각이라고 부르면 틀린다. 최소 변경은 `CatalogIndexEntry`에 선택적 ImportCompletedUtc를 추가하고 실제 게시·인덱스 성공 때 주입 시계로 기록하는 것이다. 기존 항목은 순서 미상으로 두고 새 필드가 있는 `IsSelectable` 행 중 최댓값, 동률 identity ordinal로 선택한다. 전체가 미상이면 안내 비교를 보류한다. 같은 내용 재가져오기의 완료도 순서를 갱신할지 명시한다. 기존 창의 데이터 탭에 수동 가져오기 안내 Label을 연결하고 창 생성·ShowNotification·포커스 변경은 하지 않는다. 창 열기는 메모리 결과만 읽고 재조회하지 않는다.

4. **임시 ProjectRoot만으로 인덱스가 격리되지 않는다.** `CatalogIndexWriter.Upsert`는 AssetDatabase의 고정 CatalogIndexAssetPath를 사용한다. `StaticCatalogPaths`에 기본값을 유지한 검증된 Generated 상대 루트를 주입하고 canonical/legacy/icon/index 경로 및 writer의 정적 상수 사용을 함께 전환한다. E2E는 실제 프로젝트 Assets의 고유 테스트 하위 폴더를 사용해 실제 CatalogAssetWriter.Publish와 CatalogIndexWriter/AssetDatabase 저장·재로드를 실행한다. 사용자 Generated는 사전/사후 해시로 보존 확인한다. 익명 저장소·scheduler 상태는 고유 외부 임시 루트다. 삭제는 정확한 소유 경로만 finally에서 수행한다.

5. **실제 창은 서비스 주입점이 없다.** `TftDataAnalyzerWindow`는 서비스를 직접 생성한다. GUI 구성 전에 데이터/수집/분석 VM과 확인 delegate를 제공하는 작은 내부 주입점을 추가한다. `DataManagerService`→가짜 IHttpTransport→실제 NetworkStaticCatalogSource/파이프라인/게시/인덱스→PublishedCatalogLoader→CollectionService(가짜 IRiotApiKeyProvider, EnvironmentSeedPuuidProvider(() => fabricatedSeed), RiotApiClient의 가짜 IRiotApiTransport)→실제 ObservationStore→AnalysisService/AggregateStore→기존 AnalysisTab을 연결한다. 기본 생성자를 먼저 실행한 뒤 교체하면 키/seed 조회를 피했다는 증거가 되지 않는다.

## 객관적 종료 조건

가짜 시계 23:59:59/24:00:00, 재구성·동시 호출, 예약 전후 취소, 미래/손상·읽기/교체 실패별 요청 횟수와 파일 바이트를 검증한다. bootstrap 테스트는 배치/재생/유휴 전환·재로드 뒤 callback 0 및 늦은 UI 변경 0을 확인한다. E2E는 실제 저장 인덱스 재로드, 승인 취소 시 게시/수집 0, 두 경기 익명 관측 및 중복 수, 표본 억제·상대 배치 라벨, 모든 저장 파일의 식별자/가짜 비밀값 부재, 실제 전송 0과 보호 파일 해시·소유 창 정리를 확인한다. 이 검증들은 아직 실행하지 않았다.

## 좁은 보충 확인

① IndexEntry만 갱신하는 안은 기존 CatalogIndexTests의 항목 교체·정렬·롤백, pipeline의 동일내용/조회시각 변경 idempotence와 충돌하지 않는다. 기존 인자 유지+선택 인자를 뒤에 추가하고, Unity 직렬화 필드는 nullable DateTime 대신 선택적 UTC ISO 문자열로 둔다. writer의 주입 clock 값을 SaveWithRollback 안에서 새 entry에 반영하고 저장 실패 때 기존 entry·바이트를 복원한다. 재가져오기도 index 시각만 갱신하며 catalog/manifest는 보존한다. IsSelectable 행의 원본 버전이 한 종류면 미상 시각이어도 비교 가능하고, 여러 미상 버전이면 보류하는 fallback은 타당하다. 시각 누락/불량과 한 버전/여러 버전·재가져오기·롤백을 추가 검증한다.

② 설치 패키지1.6.0 소스에서 `UnityEditor.TestTools.TestRunner.Api.ICallbacks` 및 `TestRunnerApi.RegisterTestCallback/UnregisterTestCallback`은 public이다. static API가 있어 SO 생성 없이 작은 어댑터로 등록 가능하다. Editor asmdef에 `UnityEditor.TestRunner` 참조1개가 필요하다. RunStarted(ITestAdaptor)/RunFinished(ITestResultAdaptor), TestStarted/TestFinished를 구현하고 IErrorCallbacks.OnError도 종료 처리한다. RunStarted에서 자동 예약/진행 HTTP를 취소하며 실행 중 재예약하지 않는다. 단, 이미 발송한 시작 HTTP를 소급 제거할 수 없고 callbacks는 재로드 때 사라지므로 활성 플래그는 SessionState에 보존하고 초기화 시 재등록해야 한다. 종료 누락 시 해당 Editor 세션은 보류하는 정책이 필요하다. 배치는 Application.isBatchMode로 먼저 제외하고 일반 시작 확인은 유지한다. 이 callback보다 단순한 공개 active 조회는 확인하지 못했다. 실제 실행 검증은 하지 않았다.
