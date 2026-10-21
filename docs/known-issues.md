# 현재 알려진 문제

## KI-002: Unity 내부 빌드 프로세스 종료 후 검증 중단

- 관측일: 2026-10-21(KST). 현재 상태: **새 Editor 연결·컴파일 정상, 충돌 근본 원인 미확정**. 아래는 당시 중단과 복구 이력이다. 최소 표본 설정 UI 연결 뒤 `bee_backend.exe`가 `ExitCode: -1073741819`로 종료했고 `BuildFinishedMessage`를 받지 못했다는 오류가 기록됐다. C# 오류가 없는 것만으로 새 코드의 컴파일 성공을 주장하지 않는다.
- 실행 중인 Bee 프로세스가 0개임을 확인한 뒤 재컴파일을 한 번 요청했지만, CLI는 이전 실패 상태 때문에 실제 컴파일을 시작하지 않았다. 두 번째 네이티브 충돌로 세지 않는다. 추가 Unity 호출·테스트 재실행·캐시 삭제·강제 종료는 멈추고 사용자에게 저장 후 Editor 재시작을 요청했다.
- 마지막 유효 증거는 정책35/35·서비스12/12·VM22/22 통과 및 UI 연결 전12개 중5실패/7통과다. 연결 후 UI GREEN·전체 회귀·새 화면 캡처는 아직 미실행이다. 이 상태로 PR 통과나 병합을 진행하지 않는다.
- 로그·명령 원문: `Library/TftAnalyzerValidation/minimum-samples-bee-failure-log.txt`, `minimum-samples-bee-failure.traceevents`, `minimum-samples-task2-ui-{green-status,recovery-compile,recovery-status}.json`. 기존 KI-001 가져오기 간헐 실패와는 별개다.
- 재개: 새 Editor 연결·현재 컴파일 상태를 먼저 확인하고, 실제 UI 회귀에서 CSV 대화상자 중 `100→200→100` 변경의 저장 차단을 확인한다. 이는 현재 정적 검수의 미검증 가설이며 실행된 실패로 기록하지 않는다. 재현되면 같은 테스트를 기준으로 수정한다.

## KI-001: 수동 전체 흐름 검증의 마지막 카탈로그 가져오기 간헐 실패

- 상태: **미해결, 개발 진행 허용**. 2026-10-20 사용자가 “그럼 일단 실패 기록만 남기고 개발을 이어서 진행해보자”라고 지시했다. 이 문제의 원인 규명을 이후 개발의 선행 조건으로 두지 않는다. 해결·전체 QA 통과·병합 완료를 뜻하지 않는다.
- 관측: 가짜 전송을 쓰는 수동 E2E에서 마지막 원본16.18.2 가져오기가 두 번 Failed/ImportFailed가 됐다. 당시 내부 예외 기록이 없어 다운로드·게시·동기 화면 알림 중 실패 계층을 특정할 수 없다. analysis500은 마지막 캡처 단계이며 오류 위치를 뜻하지 않는다.
- 영향: 새 카탈로그 가져오기가 간헐 실패할 가능성이 남아 있다. 이 오류로 기존 카탈로그 열람·수집·분석 전체가 실패했거나 기존 데이터가 손상된 증거는 확인하지 못했다. 실제 사용자 환경의 발생 범위는 미확정이다.
- 후속 확인: 최신 진단3회는 마지막 가져오기까지 정상 완료했다. 추가 관찰자 있는2회와 없는1회를 구분하며, 성공 반복을 해결 근거로 사용하지 않는다. 기존 창/native frame/포커스와 보호1,386개는 보존됐다.
- 대응: 반복 재현만을 위한 실행은 현재 종료한다. 다른 개발·검증 중 같은 증상이 재발하면 안전한 오류 형식·메서드와 실행 상태를 보존해 재개한다. 같은 원인 단서 없는 오류가 반복되거나 창 정리/연결이 실패하면 중단해 알린다. 사용자에게 비밀값이나 식별 가능한 경기 원본을 요청하지 않는다.
- 별도 사항: 실제 Riot API 수집 검증·증강 신뢰 분류 공급·표본 입도 개선은 이 결함과 분리한다. 이번 진행 허용을 이 기능들의 구현·검증 완료로 확대하지 않는다.

[조사·사용자 결정·실행 근거](../Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/raw/OriginalFailureAudit.md) · [현재 체크포인트](../Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/Checkpoint.md)

### KI-002 재시작 장애 조사 — 2026-10-21

사용자가 Editor 재시작을 시도했으나 기존 인스턴스 때문에 열리지 않는다고 알렸다. 정확한 프로젝트 인자와 생성시각을 대조해 PID40568(시작 UTC2026-10-20T05:14:26.5182493Z)을 확인했다. Windows Restart Manager는 Temp/UnityLockfile의 유일한 소유자로 같은 PID를 보고했다. 네이티브 조회는 ExitCode1/WaitState258이며 표시된 Editor 창 없이 숨은 ConsoleWindowClass만 있었다. 종료 처리의 구체적인 내부 대기 원인은 미확정이다.

PID/시작시각/기존ExitCode/단일잠금소유를 재검증한 뒤 해당 잔류 프로세스에만 TerminateProcess를1회 시도했다. Windows 오류5로 거부됐고 WaitState258 및 잠금 소유가 유지됐다. 이는 자동 승인 검토의 거절이 아니라 실제 Windows API 결과다. 다른 프로세스 종료·파일 삭제·Unity 컴파일/테스트는 하지 않았다. 반복 종료를 멈추고 사용자에게 다른 작업 저장 후 Windows 다시 시작을 안내한다. PC 재시작은 자동 실행하지 않는다.

원문은 Library/TftAnalyzerValidation/editor-restart-{native-inspection,one-termination-result,exact-processes}.json이다. 진단 중 시작 로그 원문에 인증값이 포함됨을 발견해 이후에는 지정된 상태 필드만 출력했다. 그 값은 문서나 협업 폴더로 복사하지 않았다. 앞으로 Editor 시작/종료 로그 tail을 그대로 출력하지 않는다. Microsoft TerminateProcess 문서(https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess)의 종료/대기 및 오류5 의미를 참고했으며 특정 I/O·드라이버가 원인이라고 단정하지 않는다.


2026-10-21 후속: 새 정상 Editor PID15640/정확한 프로젝트 잠금소유를 확인해 컴파일정상·UI회귀/화면검증을 재개했다. 이전충돌의근본원인해결로확대하지않는다. 사용자현재지시로Chrome창/탭은보존하며Windows재시작·Chrome종료를진행하지않는다. 이복구검증에서해당조작0이다.
