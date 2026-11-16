# 현재 알려진 문제

## KI-004: 전체 회귀 중 가짜 E2E 첫 가져오기 실패

- 상태: **미해결, 개발 진행 허용**. 2026-11-13 사용자가 재현되지 않은 실패는 오류 기록을 남기고 다음 작업으로 진행하도록 지시했다. 원인 규명을 후속 개발의 선행 조건에서 제외하되, 테스트 실패 판정과 필수 검수·CI·병합 조건은 유지한다.
- 관측: 공식 ID 충돌 처리 PR #34의 전체 회귀에서 가짜 E2E 첫 승인 가져오기가 `ImportFailed`로 실패했다. 당시 XML에 내부 예외가 없어 실패 계층과 변경 연관성은 미확정이다. 마지막 가져오기에서 발생한 KI-001과 동일 원인이라고 단정하지 않는다.
- 후속 확인: PR 코드 단독 1/1, 기준 코드 단독 1/1 및 직전 테스트를 포함한 E2E 묶음 6/6이 통과했다. 실패 재현이 없어 예외 종류·실패 메서드는 수집되지 않았다. 성공을 원인 해결이나 변경과의 무관함으로 해석하지 않는다.
- 대응: 첫 가져오기 성공 단언에 기존 `LastImportFailureType`과 메서드 전용 `LastImportFailureStack`을 포함해 재발 시 근거를 남긴다. 예외 메시지·파일 경로·키·식별 가능한 경기 원문을 진단에 추가하지 않는다. 새 단서 없는 재현 반복은 중단하며 다른 작업 중 재발하면 조사한다.
- 근거: `Library/TftAnalyzerValidation/official-duplicate-full.xml`, `official-duplicate-d-diagnostic-e2e.xml`, `official-duplicate-d-baseline-e2e.xml`, `exception-diagnostic-e2e-fixture.xml`. 이는 로컬 실행 자료이며 원격 CI 통과 증거가 아니다.
- 2026-11-16 재발: `Library/TftAnalyzerValidation/official-duplicate-id-worktree` 작업트리의 전체 EditMode 실행 2회에서 모두 같은 테스트가 실패했고, 진단 단언이 `System.IO.DirectoryNotFoundException`과 `CatalogAssetWriter.StoreRawSources` → `File.WriteAllBytes` 경로를 기록했다. 이 PC는 Windows 긴 경로가 꺼져 있고(`LongPathsEnabled=0`), 이 작업트리에서 E2E 원본 파일 경로는 268~284자로 260자 제한을 넘는다(같은 계산으로 main 프로젝트는 207~223자). 이전 실패도 비슷한 깊이의 `static-augment-worktree`에서 났고, 짧은 경로(D 드라이브 임시 프로젝트)에서는 통과했다. **원인 후보는 깊은 작업트리 경로의 260자 제한**이다. 같은 코드를 짧은 경로에서 다시 돌려 대조하지는 않았으므로 확정하지 않는다. 같은 실행에서 함께 실패한 `LegacyCatalogResolverTests`의 해시 차이는 이 작업트리의 `core.autocrlf=true` 줄바꿈 변환 때문이며(작업트리 파일 CR 36,096개, 커밋 원본 0개) 별개 문제다.

## KI-003: 실제 경기 수집 후 익명화 입력 오류

- 상태: **원인 미확정, 진단 반복 중단**. 실제 수집에서 상세 시도 5·가져옴 5·저장 0·중복 0·필터 제외 0·실패 5가 보고됐다. 앞선 401은 사용자 환경 변수와 Unity 프로세스의 키 불일치를 확인해 재시작 후 일치시켰고, 이후 상세 5건을 가져왔다.
- 재현 범위를 한 건으로 제한한 별도 읽기 전용 진단은 경기 목록과 상세를 요청해 메모리에서 기존 익명화기를 실행했다. 결과는 `invalid_match_payload`이며 관측·경기 원문·식별자·키를 저장하거나 출력하지 않았다. 이는 진단한 한 건의 결과이지 최초 5건 모두의 개별 오류 코드가 아니다.
- 해당 오류는 형식·필수 값·버전 문자열 등 여러 지점에서 발생할 수 있으므로 구체적인 원인을 단정하지 않는다. 버전 불일치로 판정된 경우는 필터 제외이지만, 파싱 오류가 먼저 발생하면 그 판정에 도달하지 않을 수 있다. 저장 단계 결함이나 자료 손상으로도 단정하지 않는다.
- 사용자 승인 후 합성 안전 검증을 거쳐 단일 추가 진단을 수행했다. 해당 한 건은 세트 일치, game_version 타입/형식 오류 범주, data_version 1·2 외, 참가자 증강 항목 누락이 확인됐다. 증강 누락은 기존 필수 속성 계약과 충돌하지만 원래 5건 각각의 원인이나 최초 예외 위치는 미확정이다. 세부 근거는 [진단 계획](../Assets/Plan/Workflow/Cycles/2026-11-02-match-failure-diagnosis/refined/ImplementationPlan.md)에 기록한다. 추가 경기 요청은 중단한다. 원문을 남기지 않는 안전한 세부 진단과 필요한 승인 범위를 검토한 후에만 재개한다. 진단 스크립트는 `Library/TftAnalyzerValidation/riot-single-match-diagnostic.cs`이며 실제 사용자 자료를 포함하지 않는다.
- 2026-11-16 1단계 형태 진단 실행: 승인된 비식별 진단 도구(PR #38)로 경기 20건을 1회 측정했다(요청 21회, 저장소·캐시·카탈로그 해시 불변). 20건 모두 `game_version`이 기존 정규식과 맞지 않아 릴리스를 추출할 수 없었고, `data_version`은 한 자리 값 6이며, 참가자 `augments` 필드가 없었다. 결과 분류는 `c_or_d`(`release_unextractable`)다. 최초 실패 5건이 같은 경로로 실패했는지는 경기 ID를 저장하지 않아 대응할 수 없으므로 원인은 여전히 확정하지 않는다. 다음은 2단계 기획이다. 근거: [체크포인트](../Assets/Plan/Workflow/Cycles/2026-11-16-real-match-analysis/Checkpoint.md), [집계 보고서](../Assets/Plan/Workflow/Cycles/2026-11-16-real-match-analysis/raw/match-shape-report-20261116T102829Z-20b8e538.json).

## KI-002: Unity 내부 빌드 프로세스 종료 후 검증 중단

- 관측일: 2026-10-21(KST). 현재 상태: **새 Editor 연결·컴파일 정상, 충돌 근본 원인 미확정**. 아래는 당시 중단과 복구 이력이다. 최소 표본 설정 UI 연결 뒤 `bee_backend.exe`가 `ExitCode: -1073741819`로 종료했고 `BuildFinishedMessage`를 받지 못했다는 오류가 기록됐다. C# 오류가 없는 것만으로 새 코드의 컴파일 성공을 주장하지 않는다.
- 실행 중인 Bee 프로세스가 0개임을 확인한 뒤 재컴파일을 한 번 요청했지만, CLI는 이전 실패 상태 때문에 실제 컴파일을 시작하지 않았다. 두 번째 네이티브 충돌로 세지 않는다. 추가 Unity 호출·테스트 재실행·캐시 삭제·강제 종료는 멈추고 사용자에게 저장 후 Editor 재시작을 요청했다.
- 당시 중단 시점의 증거는 정책35/35·서비스12/12·VM22/22 통과 및 UI 연결 전12개 중5실패/7통과다. 이후 재시작과 검증을 거쳐 최소 표본 기능은 PR #18로 병합됐다. 후속 전체 회귀와 화면 검증은 [체크포인트](../Assets/Plan/Workflow/Cycles/2026-10-20-minimum-samples/Checkpoint.md)를 확인한다. 이는 Bee 충돌의 근본 원인 해결을 의미하지 않는다.
- 로그·명령 원문: `Library/TftAnalyzerValidation/minimum-samples-bee-failure-log.txt`, `minimum-samples-bee-failure.traceevents`, `minimum-samples-task2-ui-{green-status,recovery-compile,recovery-status}.json`. 기존 KI-001 가져오기 간헐 실패와는 별개다.
- 당시 재개 조건은 새 Editor 연결·컴파일 상태 확인과 CSV 대화상자 중 `100→200→100` 변경의 저장 차단 검증이었다. 현재 연결 상태와 새 실행 결과는 별도로 확인해야 한다.

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

2026-11-02 KI-003 추가 진단: 사용자가 한 건의 제한 조회를 추가 승인해 세트·큐 조건 일치, 게임 버전 문자열 패턴 불일치, data_version 1~5 외, 참가자 증강 누락을 확인했다. 원래 5건과 동일한 경기인지는 확인하지 않았고 원문·식별자는 저장하지 않았다. 실제 저장 가능 여부와 미지원 자료의 공식 의미는 미확인이다.
