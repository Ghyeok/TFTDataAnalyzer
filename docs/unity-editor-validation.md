# Unity EditorWindow 검증 절차와 실패 사례

갱신일: 2026-10-18

이 프로젝트에서 MCP·CLI·EditMode 테스트로 EditorWindow를 열거나 UI를 캡처할 때 적용한다. 목적은 검증 뒤 사용자 창과 작업 환경을 보존하고, 재현 가능한 증거로 결과를 판단하는 것이다. 기본 규칙은 [AGENTS.md](../AGENTS.md)의 Unity 검증 창 정리를 따른다.

## 이번 작업에서 반복하지 않을 판단

| 관찰한 실패 | 원인·잘못된 판단 | 다음 작업의 기준 |
|---|---|---|
| 열기와 캡처를 별도 호출로 나누어 검증 창이 남음 | 다음 호출에서 닫을 것이라는 가정 | 생성부터 정리까지 제한 시간이 있는 한 작업으로 묶고 `finally`에서 정리 |
| 초기화 실패 때 창 정리가 누락되거나 표시 전 `Close()`가 실패 | 정상 TearDown만 고려하거나 native parent가 없는 객체를 표시한 창처럼 취급 | Show 전에 소유 등록, 초기화 예외 정리, 표시 여부에 맞는 종료 |
| EditorWindow 수는 같아도 빈 바깥 프레임이 남음 | 표시한 창을 `DestroyImmediate`로 없애고 객체 수만 확인 | 표시한 창은 `Close()` 우선, native ContainerWindow ID도 비교 |
| Inspector를 열거나 포커스를 바꿔도 delayCall 대기가 간헐적으로 실패 | UI 반영을 Inspector 처리 단계에 의존 | 현재 두 탭은 일회성 Editor update를 사용하고 테스트도 같은 실제 이벤트 경계를 기다림 |
| UI 테스트 통과 뒤 실제 포함 카탈로그가 연결되지 않음 | schema 2 가짜 데이터만으로 기존 schema 1 계약까지 검증했다고 판단 | 검증된 Catalog+Identity 쌍을 전달하고 두 형식 및 실제 포함 카탈로그를 확인 |
| 캡처 JSON은 성공이지만 작은 창 PNG는 다른 화면 | geometry·라벨 상태와 화면 픽셀의 일치를 동일시 | 각 PNG를 직접 열어 대상 화면인지 확인, 불일치하면 화면 검증 미완료 |

이전 실패 실행에서 생긴 빈 프레임 4개는 당시 정확한 ID와 활성 창 연결 없음을 확인한 뒤 닫았다. 기존 사용자 창 9개와 주 프레임은 보존했다. 그때의 instance ID를 다음 실행의 정리 대상으로 재사용하지 않는다.

## 실행 전

1. 현재 작업 트리와 보호 파일을 확인한다. Unity 명령은 순차 실행하고, 테스트가 실행 중일 때 `Assets` 파일을 수정하지 않는다. 실제 키·seed·원본 경기 응답을 읽거나 기록할 필요가 없는 검증은 가짜 제공자를 주입한다.
2. 기존 EditorWindow ID, native ContainerWindow ID와 포커스를 기록한다. 사용자 창을 `GetWindow` 등으로 재사용하거나 위치·크기를 바꾸지 않는다. Inspector를 검증 대기용으로 열지 않는다.
3. 새 창은 생성 즉시 정확한 객체를 소유 목록에 넣고 초기화·Show한다. 창 제목이나 타입만으로 소유권을 판단하지 않는다.

## 테스트와 캡처

- 자동 테스트는 [OwnedEditorWindowScope](../Assets/TFTAnalyzer/Tests/EditMode/UI/OwnedEditorWindowScope.cs)를 사용한다. SetUp 실패에도 정리하고, binding·VM 정리가 실패해도 창 정리가 실행되도록 finally를 구성한다. 한 창의 종료 실패 때문에 나머지 소유 창의 정리를 건너뛰지 않는다.
- 현재 DataManagerTab·CollectionTab은 일회성 `EditorApplication.update`로 UI 반영을 모은다. 콜백은 실행 전과 Dispose 때 해제한다. 테스트의 [WaitForRender](../Assets/TFTAnalyzer/Tests/EditMode/UI/EditorWindowTestSupport.cs)는 실제 update 경계를 기다리며, geometry·repaint 완료까지 보장하지 않는다. Task 6의 별도 startup `delayCall` 계약까지 일괄 변경하지 않는다.
- 시간이 초과되면 단계·예약 콜백·실제 창 상태를 먼저 조사한다. 근거 없이 대기 시간만 늘리거나 Inspector focus/menu 전환을 반복하지 않는다. 전역 콜백 수동 실행이나 제품용 테스트 우회 함수를 추가해 통과시키지 않는다.
- 수동 확인은 [PowerShell 진입점](../AgentScripts/Invoke-TftAnalyzerVisualValidation.ps1)과 [C# 실행부](../AgentScripts/TftAnalyzerVisualValidation.cs)를 이용한다. 생성·크기 변경·캡처·정리를 한 번의 제한된 작업에서 수행한다. `Library`의 과거 분리 open/capture 스크립트는 재사용하지 않는다.
- 진입점의 `-ProbeFailure`는 표시 후 실패, `-ProbeInitializationFailure`는 표시 전 실패의 정리를 확인한다. 기본 실행의 성공 JSON은 캡처 내용의 시각 검수까지 대신하지 않는다. 내부 취소 기한·CLI 타임아웃·연결 끊김의 실제 검증 여부도 따로 기록한다.
- 각 PNG를 열어 대상 창·크기·핵심 라벨·스크롤·고지가 맞는지 확인한다. `Focus()` 호출, 임의 시간 대기, PNG 파일 생성만으로 대상 픽셀을 확보했다고 판단하지 않는다.
- 새 UI 연결은 입력 초안·유효 요청·실행 snapshot·화면 재생성·닫기/취소를 함께 검증한다. schema 1의 `Manifest.Identity`는 null일 수 있으므로 loader가 검증한 행의 전체 identity를 함께 전달한다. 기존 카탈로그를 변경해 테스트 조건에 맞추지 않는다.

## 정리와 완료 판정

1. 정상·예외·취소·시간 초과 경로 모두 `finally`에서 예약 콜백과 새 소유 창을 정리한다. 표시한 창은 `Close()`로 종료하고, 표시 전 객체 또는 종료 후 남은 정확한 소유 객체만 파괴한다. 파괴 fallback이 실행됐다는 사실만으로 바깥 프레임까지 정리됐다고 판단하지 않는다.
2. 살아 있는 원래 포커스를 복원한다. 전후 ID의 추가·누락을 대조해 기존 창 보존과 소유 객체·native 프레임 잔류 0개를 확인한다. 현재 창 수가 같다는 사실만으로 정리 성공을 기록하지 않는다.
3. 쉘의 보조 정리는 해당 실행에서 기록한 정확한 ID와 임의 고유 이름이 함께 일치하는 객체에만 적용한다. CLI timeout 뒤 비동기 작업이 계속 실행될 수 있으므로, 연결 끊김이나 정리 증거 부재는 미확인으로 남기고 같은 캡처를 중첩 실행하지 않는다.
4. CLI 종료 코드와 바깥 `success`뿐 아니라 안쪽 실행 결과·컴파일 상태·테스트 Total>0·Failed=0을 확인한다. 현재 Pipeline의 컴파일 정상 종료 상태는 `completed` 또는 `up_to_date`이며, `failed=false`·`compilationFailed=false`·오류 없음도 함께 확인한다. `up_to_date`는 새 컴파일을 수행했다는 뜻이 아니다. 의도한 RED, 제품 결함, 검증 도구 오류, 수정 후 GREEN을 구분한다.
5. 결과에는 기능 테스트, 창·프레임 정리, geometry, PNG 시각 검수를 각각 기록한다. 관련 테스트 후 최종 코드가 고정되면 전체 suite를 실행하며, 새 변경·실패·미해결 근거가 없으면 같은 검증을 반복하지 않는다.

## 현재 증거와 남은 확인

2026-10-18 재개에서 최신 legacy identity 수정 포함 전체 EditMode 467/467을 확인했다. 테스트 전후 기존 창 9개·native 프레임 1개의 ID가 같고 포커스도 복원됐다. 정상·표시 전 실패·표시 후 실패 작업도 새로 실행해 소유 창 잔류 0개와 기존 창·프레임 보존을 확인했다. 현재 940px·500px PNG는 모두 대상 UI·전체 identity·고지가 보이고 작은 창에는 스크롤이 있음을 직접 열어 확인했다. 이전에 다른 화면이 찍힌 간헐적 원인의 해결을 주장하지 않으며, 앞으로도 매번 PNG를 검수한다. 세 경로의 정리 성공을 연결 끊김이나 모든 timeout 경로까지 실증한 것으로 확대하지 않는다.

Console에는 검증 전 발생한 Pipeline `/api/commands` 통신 오류 2건이 남아 있다. 현재 컴파일 실패는 false, 경고 0이며 최종 cursor 이후 새 경고·오류는 0건이다. 기존 로그를 지워 전체 오류 0으로 만들거나 제품 코드 오류로 바꾸어 기록하지 않는다.

과거 JSON·PNG는 Git 제외 `Library/TftAnalyzerValidation/`에 있으며 지워질 수 있다. 현재 규칙의 근거와 당시 계획 변경은 [계획 재검토](../Assets/Plan/Workflow/Cycles/2026-10-16-collection-tab/refined/PlanReReview.md), 최신 검증과 재개 위치는 [현재 체크포인트](../Assets/Plan/Workflow/Cycles/2026-10-16-collection-tab/Checkpoint.md)를 참고한다. 과거 통과를 새 실행 결과로 보고하지 않는다.

등록 범위: 프로젝트 지침·색인은 전역 스킬 설치와 구분한다. 문서 변경은 내용·링크·Markdown 형식을 확인하며, 위 실행 결과는 별도 재개 검증에서 얻었다. 작성 실행의 실제 모델·추론·사용량은 독립 확인 정보가 없다.
