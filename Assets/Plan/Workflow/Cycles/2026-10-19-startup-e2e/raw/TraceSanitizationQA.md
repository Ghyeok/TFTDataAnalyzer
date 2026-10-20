# PR14 비동기 가져오기 진단 경로 노출 수정 독립 QA

상태: 비동기 진단 경로 노출 수정 한정 QA 통과. 원래 간헐 issue 전체 QA 실패·병합 보류 유지.

## 정정 및 범위

앞선 `pr14-diagnostics-qa.md`의 `StackTrace(error, false).ToString()`이 파일 경로를 제외한다는 판정은 비동기 throw/rethrow 실행에 성립하지 않았다. `pr14-owned-import-fault-probe/before-fix/e2e-visual-failure-state.json`에서 실제 Publish의 IOException이 generic catch를 거쳐 원본 failure-state JSON에 절대 `.cs` 경로를 출력했음을 직접 확인했다. 앞선 동기/구독자 테스트만으로 비동기 진단 개인정보 검증이 충분하다고 판단한 점을 정정한다.

이번 QA는 `DataManagerViewModel.cs`와 `DataManagerViewModelTests.cs`의 경로 노출 수정 diff를 읽기 전용 검토한다. 제품 수정·Unity 실행·네트워크·Git 쓰기·추가 에이전트는 수행하지 않는다. 사용자 Volume 변경은 열람·수정하지 않는다. 강제 file collision은 원래 간헐 ImportFailed의 원인을 재현했다는 증거가 아니다. 원래 간헐 issue의 전체 QA 실패·병합 보류를 유지한다. 모델·추론·사용량은 확인되지 않아 추정하지 않는다.

## 코드 검토

- 새 renderer는 `StackTrace(error, false).GetFrames()`의 각 `GetMethod()`에서 `DeclaringType.FullName`과 `Name`만 조합한다. raw StackTrace.ToString, 예외 StackTrace/Message/Data/ToString 및 파일 경로 getter를 출력에 사용하지 않는다. 메서드 타입/이름 외의 런타임 인수값·PDB 경로·행번호가 출력 문자열에 들어가는 경로를 발견하지 않았다.
- frame이 없으면 빈 문자열이며 null method를 제외한다. reflection/열거/문자열 구성 실패도 기존 catch로 빈 문자열이 되어 기존 ImportFailed/InvalidInput 처리가 계속된다.
- 진단 초기화, 예외 타입 저장, 예외 분기, 상태/이벤트 통지, 비동기 작업 순서, opt-in/IsBusy/정리는 변경하지 않았다. 제품 디스크쓰기나 observer 우회는 추가하지 않았다.
- 기존 테스트를 동기 및 두 차례 await/yield 후 throw/rethrow 비동기 case로 확장했다. throw helper의 이름과 타입 정보는 남기되 privateMessage 및 `.cs`, Windows 경로 양식이 빠지는지 검증한다. 다음 실행 clear 및 성공 재실행 검증을 두 case에 유지했다.

## 직접 확인한 증거

- before-fix failure-state: Stage=ActualFabricatedPipeline, DataErrorCode=ImportFailed, ImportFailureType=System.IO.IOException, Publish/StaticCatalogPipeline의 절대 소스 경로 포함.
- 유효 RED `pr14-async-trace-red-current.json`: Total 3 / Passed 2 / Failed 1. 비동기 case만 `.cs`/절대 경로로 실패하며 나머지 두 case 통과.
- GREEN `pr14-async-trace-green.json`: Total 3 / Passed 3 / Failed 0, 비동기 case 포함.
- `pr14-async-trace-red.json`은 컴파일 중 구 assembly로 두 테스트만 실행됐다는 root의 설명에 따라 유효 RED 증거에서 제외한다.
- 수정 후 `pr14-owned-import-fault-probe/e2e-visual-failure-state.json`을 직접 읽었다. IOException/ImportFailed 및 `CatalogAssetWriter.Publish` frame을 유지하며 메서드 metadata만 출력하고 `.cs` 및 절대 경로는 없다. `after-fix-summary.json`의 ExpectedFailureCaptured/OriginFrameKept/AbsolutePathAbsent/SourceFileAbsent/StrictNormalReportStayedFailed/OwnedCleanupVerified는 모두 true이며 OriginalIntermittentCauseProven=false다. 실제 JSON export 미검증이라는 이전 한계는 이 제어시험 범위에서 해소했다.
- 강제 probe는 의도한 실패이므로 captures=0 및 strict visual Passed=false를 유지했다는 root 전달과 구분한다. 정상 UI 성공으로 취급하지 않는다. 원래 간헐 실패의 실제 재발 export와 원인 규명 증거도 아니다.
- `pr14-sanitized-trace-final-suite.json` Summary 직접 확인: Total 620 / Passed 619 / Failed 0 / Skipped 1 / Inconclusive 0, 81.73초.
- `pr14-async-trace-green-compile-status.json`: completed, failed=false, compilationFailed=false, errors=[]를 직접 확인했다.
- `pr14-sanitized-trace-final-windows.json`과 `pr14-probe-before-windows.json`의 nested result 직접 대조: 기존 창 9개 및 native ContainerWindow 1개의 ID가 동일하고 FocusId=0으로 동일하다.
- `pr14-sanitized-trace-final-protected.json`: Count 1386 / Changed=[] / E2EAssetDirectories=[]를 직접 확인했다. 보호 원본 파일은 열람하지 않았다.
- `pr14-sanitized-trace-final-console.json`: cursor 337, 신규 반환 2건(seq336/337)은 Pipeline `/api/commands` transport/header 오류다. groundTruth는 consoleErrors=2, consoleWarnings=0, compilationFailed=false이므로 오류 0건으로 보고하지 않는다. 이번 제품 경로의 스택/예외에서 유래한 로그라는 증거는 없다.
- 독립 Unity 재실행 및 새 정상 UI 캡처는 하지 않았다. UI 불변이라 이번에는 정상 캡처를 재실행하지 않았다는 root 전달을 기록한다. 이전 `bd2079f`의 6 PNG를 이번 수정의 새 화면 검증 증거로 재사용하지 않는다.

## 최종 판정

새 diff에서 조치가 필요한 결함을 발견하지 않았다. 유효 RED/GREEN, 실제 pipeline 강제 fault의 원본 JSON 경로 제거와 발생 메서드 정보 보존, 전체 회귀·창/임시 asset 정리·보호파일 보존을 확인했다. 이번 비동기 진단 경로 노출 수정 한정 QA를 통과시킨다. 원래 간헐 ImportFailed 원인은 미확정이며 전체 사이클 QA 실패·병합 보류를 유지한다.

검토한 파일 SHA256(직접 산출):

- `DataManagerViewModel.cs`: `B76991850406125ED3BE3947C5A2420A787ABC1A31BDA36C484677A13139D68B`
- `DataManagerViewModelTests.cs`: `64BB38E29D4FD6B059E579719683AF304BD316D6E17D521749FF92DF3897303A`
