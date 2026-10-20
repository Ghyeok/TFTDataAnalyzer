# Task6 구현 요청

2026-10-19. 요청 모델 GPT-6.1 Sol / High. root가 개발 리드·통합·유일한 Unity 실행을 맡는다. 사용자의 전체 작업 계획 완료 요청으로 기존 Task6 범위를 수행한다.

## 실제 발송 메시지

`Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/raw/DevelopmentRequest.md`를 먼저 읽고 Task6을 구현하라. 승인된 실행 계획과 명시된 안전 경계를 따른다. 첫 단계는 핵심 실패 테스트와 컴파일 스텁을 작성한 뒤 RED_READY에서 편집을 멈추는 것이다. Unity·네트워크·Git 쓰기·Hermes·추가 에이전트 호출을 하지 않는다. root의 RED 확인 뒤 최소 구현하고 GREEN_READY에서 다시 멈춘다. 공개 결과는 1500자 이내로 반환한다.

## 계약과 쓰기 범위

- 정본: [실행 계획](../refined/ImplementationPlan.md). 기준 main `ab63a3c1a4c1add290f293e9c54cb48261050a0b`, 브랜치 `codex/startup-e2e`. PR13 최신6281238은 CodeRabbit SUCCESS·미해결0을 확인한 뒤 병합했다.
- 전체 계획과 필요한 실제 소스만 읽고 과거 기록·세션 로그는 읽지 않는다. UI Toolkit·TDD·간결한 변경 지침을 적용한다. 제품/테스트 코드·필요한 asmdef/meta만 쓰며 문서와 AgentScripts는 root가 맡는다.
- 범위: 버전 scheduler/두 필드 상태/Unity 연결·최소 UI 안내, 실제 E2E를 위한 경로·인덱스 완료 기록·최소 내부 주입점, scheduler/금지 표면/실제 E2E 테스트. 사용자 Volume과 기존 Generated는 바꾸지 않는다.
- 첫 RED 단계에서 실제 publisher/index가 기본 사용자 경로에 쓰이지 않도록 한다. 신규 경로 주입 생성자는 컴파일 스텁에서 미구현 예외로 중단하고, E2E는 기본 경로로 fallback하지 않는다. 실제 E2E 실행은 경로 격리를 구현하고 root가 writer/경로 코드를 확인한 뒤에만 허용한다. 테스트는 의도된 기능 실패를 단언하며 하드코딩 실패·소스 문자열 검사로 기능을 대신하지 않는다.
- 핵심 정책/스케줄/경로/인덱스/UI/금지 표면 실패 테스트와 컴파일 가능한 스텁 후 `RED_READY` 및 정확한 필터를 보낸다. root 테스트 중 Assets를 편집하지 않는다. root만 컴파일·Unity 테스트·수동 검증을 실행한다.
- 예약 전 취소 미소비, 예약 후 오류/취소 24시간 소비, 상태 손상/미래 시각 복구 후 24시간 보류, IO 실패면 요청 없음. 목록 요청만 수행하며 URI를 외부 상태에서 만들지 않는다. 단일 실행 Task 공유·CTS 기한·재로드 뒤 늦은 결과 폐기를 검증한다. 빈 결과·이상 버전도 안전하게 처리한다.
- 실제 설치된 TestRunner 공개 콜백 API를 쓰고 내부 IsRunActive 반사를 추가하지 않는다. 테스트 실행 중 자동 HTTP 보류/취소와 재로드·오류·종료 정리를 작은 어댑터로 분리한다. 실제 Library 상태에 가짜 시각을 넣거나 일반 Editor 기능을 영구 비활성화하지 않는다.
- 인덱스만 선택적 UTC 완료 기록을 추가한다. 기존 immutable catalog/manifest/identity/hash는 유지하고 기존 항목을 읽는 것만으로 저장/시각 생성하지 않는다. 최신 유효 기록·동률 identity·legacy 단일 버전 fallback·여러 미상 버전 보류를 테스트한다.
- 실제 AssetDatabase index는 고유 `Assets/TftAnalyzerE2E_<guid>/Generated`에서 저장·재로드한다. raw/staging/cache도 고유 Library 경로로 격리하고 익명 관측·버전·compliance는 임시 프로젝트형 루트를 쓴다. `SaveAssetIfDirty(index)`로 대상만 저장하고 글로벌 SaveAssets는 쓰지 않는다. 생성 경로와 GUID를 추적하며 finally 정리 전 허용 루트 내부임을 검증한다.
- E2E는 전송·시계·경로만 가짜이며 실제 DataManagerService→정적 pipeline/publisher/index→CollectionService/MatchCollectionService→ObservationStore→AnalysisService/AggregateStore→AnalysisTab을 연결한다. 키·seed도 가짜 제공자를 GUI 생성 전에 주입한다. 가져오기와 수집 각각 실제 binding의 승인/취소를 거친다. 두 경기에서 생긴 실제 익명 관측·정책 억제·범위·UI 라벨을 단언한다. 두 경기를 표본100으로 가장하지 않는다.
- root 수동 검증에서도 같은 가짜 전체 흐름을 재사용할 수 있도록 테스트 어셈블리에만 작은 시나리오/fixture helper를 둔다. root가 직접 소유 생성한 Window를 GUI 구성 전에 configure하고 flow를 실행할 수 있게 한다. 제품에 테스트 전용 공개 기능을 추가하지 않는다. 준비되면 helper/생성자/호출법을 짧게 알린다.
- 기존 DataDragonNetworkSmokeTests는 무기한 CancellationToken.None 대신 유한 취소 기한을 사용하도록 보완한다. 실제 실행은 root가 별도로 하며 기본 suite와 구분한다. 실제 Riot API/개인 seed는 요청하지 않는다.
- 금지 표면 검사는 제품 어셈블리/메뉴와 실제 UI copy를 대상으로 하고 허용 부정 고지+긍정 주장의 반례를 테스트한다. 검사기/테스트 데이터 자체는 제품 기능으로 오인하지 않는다.
- 구현 후 `GREEN_READY`와 경로/인덱스 안전 검토 위치·집중 필터·수동 helper API를 보내고 Assets 편집을 멈춘다. 실제 root 결과 없이 통과/완료를 주장하지 않는다. 실패 시 해당 근거만 수정하고 다시 동기화한다.
