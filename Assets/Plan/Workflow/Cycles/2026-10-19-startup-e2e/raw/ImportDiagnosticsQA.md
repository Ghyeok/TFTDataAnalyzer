# PR14 가져오기 진단 변경 독립 QA

상태: 진단 변경 한정 QA 통과. 원래 간헐 실패의 전체 사이클 QA 실패·병합 보류 유지.

기준 HEAD: `b213d074e4164d95afcd60c445d626353896302a`. 2026-10-20 현재 3파일 diff를 읽기 전용 검수했다. 제품 코드 수정, Unity 실행, 네트워크, Git 쓰기는 수행하지 않았다. 사용자 변경 `Assets/DefaultVolumeProfile.asset`은 열람·수정하지 않았다. Karpathy 검수 지침으로 범위와 확인 가능한 실행 증거를 대조했다. 실제 런타임 모델·추론값·토큰·비용은 독립 확인할 수 없어 추정하지 않는다.

## 판정

진단 변경 자체에서 조치가 필요한 결함을 발견하지 않았다. 최종 전체 suite·수동 실행·정리·보존 증거를 확인해 이 관측 개선의 QA를 통과시킨다. 이는 간헐적 `ImportFailed`의 원인 규명이나 원래 버그 수정의 승인에 해당하지 않는다. 원인 미확정으로 전체 사이클 QA 실패·병합 보류를 유지한다.

## 범위·보존·예외·회귀

- `DataManagerViewModel`은 internal getter/private setter 문자열 2개만 보관한다. 실제 가져오기 시작 시 초기화하며 busy/disposed guard로 거절한 호출은 기존 진단을 유지한다.
- generic catch에서 최상위 예외 타입과 `StackTrace(error, false)` 메서드 frame만 생성한다. 예외 객체·Message·Data·ToString을 보관하지 않고 제품 디스크 쓰기나 UI 공개 바인딩을 추가하지 않았다. 메서드 frame은 실제 인수값을 출력하지 않으며 파일 경로 수집을 끈다. 형식화 실패 시 빈 문자열로 처리해 기존 실패 상태 처리를 이어간다.
- 기존 ErrorCode/상태/취소/복구 분기, opt-in 해제, End(operation), await 및 Changed 통지 순서를 변경하지 않았다. 별도 observer·우회·예약 콜백·스레드 전환도 추가하지 않았다.
- 수동 하네스는 기존 고정 Library 실패 상태 JSON에 두 문자열만 반사 조회한다. 창 생성·소유권·Close/Dispose·focus 복원 경로는 이번 diff에서 동일하다.
- 새 테스트는 helper throw의 frame·비밀 메시지/파일 경로 미포함·다음 시작 시 clear·성공 재실행과 Succeeded 통지 중 subscriber 예외의 기존 실패 처리/정리를 확인한다. 테스트용 subscriber는 finally에서 해제한다.
- Begin 알림, catch 내부 알림, finally End 알림의 모든 예외를 새로 수집하는 기능은 아니다. 취소/복구 전용 분기나 inner exception 전체의 수집도 범위 밖이다. 현재 generic catch의 관측 개선이라는 범위와 일치한다.

## 직접 확인한 실행 증거

- `pr14-diagnostic-red.json`: 동일한 새 테스트 2개가 진단 타입 빈 문자열로 실패. Total 2 / Passed 0 / Failed 2.
- `pr14-diagnostic-green.json`: Total 2 / Passed 2 / Failed 0. 두 테스트의 이름 및 통과 결과를 직접 확인했다.
- `pr14-diagnostics-final-compile-status.json`: completed, failed=false, compilationFailed=false, errors=[].
- `pr14-diagnostics-final-suite.json`: Total 619 / Passed 618 / Failed 0 / Skipped 1 / Inconclusive 0, 84.8초. Summary를 직접 읽었다.
- `e2e-visual-final.json`: Passed=true, Stage=Completed, 캡처 6개, Failure/CleanupFailure 빈 값, OwnedWindowsRemaining=0, Missing/AddedWindowIds 및 Missing/AddedContainers 모두 빈 배열, OriginalFocusRestored=true를 직접 읽었다. root는 최종 컴파일 후 추가 컴파일 없이 수동 실행했다고 전달했다.
- `pr14-diagnostics-final-windows.json`과 `pr14-oct20-before-windows.json`의 data.result.result를 직접 대조했다. 기존 창 9개 ID 및 ContainerWindow 1개 ID가 같고 FocusId=0으로 동일하다.
- `pr14-diagnostics-final-protected.json`: Count 1386, Changed=[], E2EAssetDirectories=[]를 직접 확인했다. 비교 대상 원본 파일 자체를 열지 않았다.
- `pr14-diagnostics-final-console.json`: cursor=307, 반환 오류 6개(seq 278/279/280/281/288/289)는 모두 Pipeline `/api/commands` transport/header 오류이며, groundTruth는 compilationFailed=false, consoleErrors=0, consoleWarnings=0이다. 로그가 전혀 없다고 해석하지 않는다.
- 6개 PNG의 픽셀 확인은 root가 직접 수행했다는 전달에 근거한다. identity·고지·수집 2건·분석 표본부족/정책차단·500px 스크롤 고지를 확인했고 이번 실행에는 겹침이 보이지 않았다고 전달받았다. QA 에이전트가 PNG를 독립 재검사했다는 주장은 하지 않는다.
- 위 실행 증거는 root가 실행한 결과를 읽은 것이며 독립 Unity 재실행은 하지 않았다.

## 한계 및 후속 판단

원래 실패가 재발하지 않아 새 failure-state JSON 두 필드의 실제 재발 export는 미실행이다. 내부 type/stack은 강제 fault 단위 테스트 2개로 검증했고, 하네스의 반사 조회/직렬화 연결은 코드로 확인했다. 최종 수동 성공을 과거 2건의 간헐 실패 해결로 판단하지 않는다.

최종 검수 파일 SHA256(직접 산출):

- `AgentScripts/TftAnalyzerEndToEndVisualValidation.cs`: `E167208BAA758EB9AFC1069FA05C65EFC53FAFA579E4481F2084D26F4E6A86D1`
- `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerViewModel.cs`: `7336C23C88836E64003B440237865F44B7920451E2A35F5E4CDFE5405B433DDF`
- `Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerViewModelTests.cs`: `1E56613253B6B95F08ACAE70CE2514125546E5E1758E1A642C4FD9F823024B9A`
