# Task 6 시작 버전 확인·전체 흐름 검증 실행 계획

2026-10-19. 기존 [Phase 3 Task 6](../../../../Implementation/2026-09-17-tft-analyzer-editor-ui.md)의 실행 계약을 독립 기획 2건에 따라 구체화했다. PR13 최신6281238의 검수 통과·병합(ab63a3c)을 확인했으며 구현을 시작한다. 사용자 전체 작업 계획 완료 요청 범위에 속하며 Task6 밖의 기능은 추가하지 않는다.

## 목적과 범위

- Editor 시작 후 유휴 시점에 Data Dragon 원본 버전 목록을 24시간에 한 번 비차단 조회하고, 마지막으로 가져온 검증된 카탈로그의 원본 버전과 다르면 수동 가져오기를 안내한다.
- 가짜 전송을 사용한 실제 파이프라인 통합, 금지 표면 검사, 전체 EditMode 및 수동 검증으로 Phase3 완료 조건을 확인한다.
- 기본 카탈로그·사용자 Volume·실제 키/seed·개인 식별 원본 응답을 보존한다. 원본 버전을 TFT 패치 번호로 추정하지 않는다. 자동 카탈로그 가져오기·이미지 다운로드·Hermes 예약은 추가하지 않는다.

## 1. 버전 확인과 24시간 상태

Files: `Editor/Ingestion/StaticVersionCheckScheduler.cs`, 필요 시 상태 저장과 Unity 연결을 위한 작은 동반 파일, 대응 EditMode 테스트. 경로 접두사는 `Assets/TFTAnalyzer/`다.

- 핵심 scheduler는 전송·시계·상태 위치·마지막 검증된 원본 버전 조회를 주입받고 `CheckAsync(CancellationToken)`로 검사한다. 실제 버전 요청은 기존 `DataDragonAdapter.FetchVersionsAsync` 경로를 재사용한다.
- 저장 위치는 `Library/TftAnalyzerStore/version-check.json`, 영속 필드는 `lastCheckedUtc`, `lastSeenVersion` 두 개뿐이다. 계정·API 키·카탈로그 본문·동의 상태를 저장하지 않는다.
- 24시간 미만이면 요청 없이 종료, 정확히 24시간 경계는 실행한다. 중복 실행을 합치고 도메인 재로드 전후 반복 요청을 막는다. 시각은 UTC 시계로 테스트한다.
- 호출 실패도 재로드 때마다 다시 요청하지 않도록 요청 시도 시작 시간을 기록하고 기존 성공 버전을 보존한다. 취소/재로드 뒤 늦은 응답이 상태·UI를 덮어쓰지 않게 한다.
- 상태가 없으면 예약 저장 후 조회한다. 손상·미래 시각은 삭제하지 않고 현재 UTC/검증 가능한 기존 버전으로 원자 복구한 뒤 24시간 보류한다. 읽기/예약 저장 실패는 요청 없이 종료한다. 메타데이터 실패나 HTTP 오류가 Editor 초기화를 중단하지 않는다. 오류는 고정 문구만 사용한다. 카탈로그·아이콘은 이 경로로 요청하지 않는다. 호출 자체는 취소 가능한 유한 기한을 둔다.
- 같은 폴더의 고유 임시 파일과 교체를 사용하고 기존 경로 보호를 재사용한다. 상태의 기존 파일을 무작정 삭제하지 않는다.

완료 증거: 첫 실행/같은 날 재로드/24시간 경계, 동시에 두 요청, 네트워크 실패·취소·재로드, 손상/미래 상태·IO 실패, 버전 목록 URL만 요청하는 테스트. 테스트는 고유 임시 루트와 가짜 시계/전송을 사용한다.

## 2. Unity 시작 연결과 수동 안내

- `[InitializeOnLoad]` 연결은 `EditorApplication.delayCall`에서 유휴 상태를 확인한 뒤 실행한다. 배치/재생 중에는 요청하지 않는다. 재로드 때 예약 콜백을 해제하고 진행 중 요청을 취소한다.
- 마지막 가져온 버전은 현재 활성 선택과 구분한다. 인덱스에만 선택적 UTC 문자열 `ImportCompletedUtc`를 추가해 성공한 가져오기를 주입 시계로 기록하고 유효 행 중 최댓값/identity ordinal로 선택한다. manifest의 원본 응답 시각을 완료 시각으로 재해석하지 않는다. 카탈로그가 없으면 비교 기준을 꾸며내지 않는다.
- 안내는 원본 버전이 다를 때 기존 데이터 탭의 작은 Label에 표시하며 창을 새로 열거나 사용자 창의 위치·크기·포커스를 바꾸지 않는다. 창이 없으면 결과를 보관하고 다음 열기에서 표시한다. 화면 열기·탭 전환은 추가 버전 요청을 시작하지 않는다.
- UI/버전 상태의 연결은 예약 콜백·구독을 명시적으로 소유하고 재생성·닫기·재로드에서 해제한다. 시작 검사로 Generated·아이콘·관측·집계 파일을 수정하지 않는다.

완료 증거: 기본 연결의 유휴/배치/재생/재로드 계약, 버전 일치/차이/가져온 카탈로그 없음의 안내, 기본 연결과 주입형 핵심이 같은 경로를 사용하는 테스트. 테스트 중 실제 전송이 실행되는 시점과 가짜 전송 증거는 구분한다.

## 3. 가짜 전송으로 전체 흐름 연결

File: `Tests/EditMode/UI/TftDataAnalyzerEndToEndTests.cs` 및 최소 공용 fixture.

- 실제 경로로 버전 확인 → 명시적 가져오기 승인 → 가짜 Set18 카탈로그 검증·게시 → 제공한 seed의 가짜 경기 두 개 수집 → 익명 관측 저장 → 집계 → 실제 분석 탭 표시까지 연결한다. 기본 Generated·캐시·저장소를 바꾸지 않는 격리 경로/쓰기 경계를 사용한다. 수집에도 별도 명시적 동의를 적용한다.
- 카탈로그 소스와 키/seed·원본 경기 식별자는 테스트에서 생성한다. 전송 기록의 요청 목적/횟수와 저장 파일 전체를 검사해 원본 식별자·가짜 비밀값이 저장되지 않았음을 확인한다. 관측 수·중복·범위·상대 배치 우위율 라벨과 고지를 확인한다.
- 두 경기만으로 표본100을 충족했다고 가장하지 않는다. 해당 행은 비율을 억제한다. 99/100과 표시 비율·95% 구간은 기존 실제 분석 테스트와 별도 수동 합성 snapshot 검증도 연결해 Phase3 전체 조건을 확인한다.
- 승인 취소 시 게시/수집이 없고 CommunityDragon opt-in이 실행마다 해제되는 경계도 확인한다. 기존 로컬 CSV와 Task5 빌드 차단을 회귀 범위로 유지한다.

## 4. 금지 표면 검사와 최종 검증

File: `Tests/EditMode/Compliance/ForbiddenFeatureSurfaceTests.cs`.

- 제품 어셈블리의 노출 타입·메뉴에 `Spectator`, `LiveGame`, `Overlay`, `CombatSimulator`, `WinPrediction` 기능이 생기면 실패한다. 검사 코드와 테스트 fixture 자체를 제품 표면으로 오인하지 않는다.
- 실제 UI 텍스트의 한국어/영어 예측·전투 승률 주장을 검사한다. 승인된 부정 고지와 제외 기능 설명을 허용하되 문자열 전체를 무조건 면제하는 방식으로 긍정 주장을 통과시키지 않는다. 검사기의 금지/허용 반례도 테스트한다.
- root만 Unity를 실행한다. 실패 테스트 → 최소 구현 → 집중 통과 → 최종 제품 고정 후 전체 EditMode 순서로 검증한다. 수동 가짜 E2E·기존 분석 표시·빌드 콜백을 확인하고 Console·보호 파일 해시·기존 창/프레임 ID·포커스를 대조한다. 소유 창은 단일 짧은 작업에서 finally로 정리하고 실제 PNG를 직접 확인한다.
- 독립 코드 검수와 별도 QA 이후 코드·문서를 함께 커밋·PR 요청하고 push 뒤 반복 조회하지 않는다. Phase1/2 완료 조건은 기존 결과를 그대로 복사하지 않고 현재 구현과 연결된 실행 증거를 최종 감사한다. 실데이터/API 키/원본 응답을 요구하는 선택적 검증은 기본 가짜 통합과 구분한다.

## 통합한 연결 계약

1. 24시간 gate는 상태 읽기→취소 확인→시도 예약의 원자 저장→HTTP 순서다. 예약 전 취소는 소비하지 않고 이후 실패/취소는 한 번을 소비한다. 같은 실행은 Task를 공유한다. 성공만 lastSeen을 갱신하며 늦은 결과는 세대/token으로 폐기한다. 성공 결과 저장 실패도 이미 저장한 예약을 제거하지 않는다. 파일 외부 삭제/변조까지 절대 상한을 보장한다고 주장하지 않는다.
2. 기존 인덱스 항목의 완료 시각은 미상으로 남긴다. 유효 행의 원본 버전이 모두 하나면 순서를 몰라도 비교 가능하다. 완료 시각이 기록된 행은 그 중 최신을 사용하며, 모두 미상이고 버전이 여러 개면 비교를 보류한다. 동일 데이터 재가져오기도 성공 기록은 갱신하되 게시된 catalog/manifest/icon 바이트·전체 식별자·해시는 유지한다. 인덱스 실패는 시각을 포함해 기존 상태로 롤백한다. 기존 자산을 읽는 것만으로 시각을 채우거나 저장하지 않는다.
3. `CatalogIndex.cs`의 실제 writer는 고정 AssetDatabase 경로를 사용한다. `StaticCatalogPaths`에 기본값을 유지한 Generated 상대 루트·캐시 상대 루트 주입을 추가하고 index/canonical/legacy/icon/raw/staging 및 pipeline 복구 경로가 같은 인스턴스를 따르게 한다. 상대 경로는 허용 프로젝트 영역에 정규화하고 링크·탈출을 거부한다. E2E는 실제 프로젝트의 `Assets/TftAnalyzerE2E_<guid>/Generated`와 별도 `Library/TftAnalyzerValidation/e2e_<guid>/cache`를 쓴다. 익명 관측·버전 상태·compliance fixture는 그 실행만의 임시 프로젝트형 루트다. 실제 CatalogAssetWriter/IndexWriter/AssetDatabase 저장·재로드를 사용하고 디렉터리·결과 자체를 stub으로 바꾸지 않는다.
4. 인덱스 writer의 전역 `AssetDatabase.SaveAssets`는 대상만 저장하는 `SaveAssetIfDirty(index)`로 좁혀 사용자 dirty 자산을 저장하지 않게 한다. 기존 기본 경로·저장 알고리즘은 유지한다. 생성한 고유 자산 폴더·캐시·임시 프로젝트만 소유 목록과 정규화된 허용 경계 확인 후 finally에서 정리한다. 테스트 중 root의 외부 Assets 편집은 중단한다.
5. 실제 창에는 GUI 생성 전 데이터/수집/분석 VM과 확인 delegate를 주입할 작은 내부 경계를 제공한다. 기본 생성 뒤 교체해 실제 키/seed 조회를 감추지 않는다. 정적 가져오기와 수집 각각 승인/취소 이벤트를 실제 binding으로 통과시킨다. 전송만 가짜이며 pipeline/publisher/index/관측/집계/분석은 실제 코드다.
6. 기본 테스트의 자동 HTTP를 막기 위해 이미 설치된 `UnityEditor.TestRunner`의 공개 `TestRunnerApi.RegisterTestCallback/UnregisterTestCallback`, `ICallbacks` 및 `IErrorCallbacks`를 작은 어댑터로 연결한다. Editor asmdef에 해당 참조만 추가한다. RunStarted에서 진행 요청을 취소하고 테스트 중 보류, RunFinished/OnError에서 상태를 해제한다. 재로드 간 실행 상태는 SessionState로 보존하며 종료/재로드에서 소유 구독·콜백·CTS를 해제한다. 내부 IsRunActive 반사나 제품 영구 비활성화·실제 캐시에 가짜 확인 시각 저장은 하지 않는다. 테스트 이전 일반 Editor 시작의 실제 버전 요청과 가짜 fixture 검증은 별도 기록한다.
7. busy 상태의 delayCall을 무한 반복하지 않는다. 유휴/재생 종료 상태 변경 때 소유한 단일 예약만 다시 연결한다. bootstrap 테스트는 같은 어댑터 경로에 가짜 환경/전송/시계/상태 위치를 주입하고 실제 전역 테스트 실행 상태를 중간에 바꾸지 않는다.

## 파일 책임과 완료 체크

- [ ] `Editor/Ingestion/StaticVersionCheckScheduler.cs`와 최소 상태/Unity 어댑터: 두 필드 원자 상태, 24시간·동시 실행·실패/취소·유휴/재로드·테스트 실행 배제.
- [ ] `Editor/Persistence/StaticCatalogPaths.cs`, `CatalogIndex.cs`, `StaticCatalogPipeline.cs` 및 필요한 경로 소비자: 실제 게시·인덱스의 격리, 완료 기록, 기본/legacy 호환, 대상 한정 저장·실패 롤백.
- [ ] 기존 UI/Data·창의 최소 연결: 선택과 다른 마지막 성공 기록, 원본 버전 차이만 수동 가져오기 안내, 새 창/요청 없음, 구독 해제.
- [ ] `Tests/EditMode/UI/TftDataAnalyzerEndToEndTests.cs`: 실제 버전→가져오기 승인/취소→검증/게시/인덱스 재로드→수집 승인/취소→경기 두 개→익명 저장→집계→UI. 원본 식별자/가짜 키 미저장, opt-in 해제, 동일 import/집계 재생성의 바이트 불변 확인.
- [ ] `Tests/EditMode/Compliance/ForbiddenFeatureSurfaceTests.cs`: 실제 제품 타입·메뉴·UI의 금지 표면, 승인된 부정 고지와 별도 긍정 문장의 반례.
- [ ] 집중 RED/GREEN·전체 EditMode·새 Console 오류·보호 해시·창/프레임/포커스·실제 PNG와 수동 가짜 E2E. 파일 symlink 등 환경별 건너뜀을 분리한다.
- [ ] Phase1/2/3 최종 대응표: 전체 identity/원본 버전 구분·공식 값 우선/opt-in·검증/게시/복구·idempotence, 익명화/중복/상한/범위·기간/분모·99/100/Wilson/Legend 차단·CSV, 정책/빌드 콜백과 startup 두 필드/24시간. 각 조건의 최종 head 테스트/수동 증거를 명시한다.
- [ ] 선택적 공개 버전 네트워크 smoke는 기존 명시적 테스트에 취소 기한을 둔 뒤 한 번 실행하고 기본 가짜 테스트와 구분한다. 실제 Riot API/개인 seed 요청은 추가 승인 없이 실행하지 않는다.
- [ ] 독립 코드 검수·별도 QA·문서 검증 후 함께 커밋·PR 요청, 최신 head 검수·미해결·CI 확인 후 승인 범위에 따라 병합한다. PR push 뒤 대기 루프는 만들지 않는다.

현재는 사용자의 전체 계획 완료 요청에 따른 Task6 구현 단계다. 실행하지 않은 항목은 완료로 표시하지 않는다. 초기 조건과 예외 복구를 약한 단위 테스트 묶음으로 대체하지 않는다.
