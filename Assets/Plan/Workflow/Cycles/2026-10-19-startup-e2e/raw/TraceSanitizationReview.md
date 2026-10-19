# PR14 진단 메서드 프레임 정제 최종 독립 검수

판정: 지정된 VM·테스트 2파일 diff와 유효 RED/GREEN, 실제 게시기 충돌 probe의 수정 후 결과에서 조치가 필요한 P0–P3 발견 사항 없음. 이번 메타데이터 출력 수정의 개발검수는 수용한다. 기존 간헐 ImportFailed의 원인 미확정 및 QA·병합 보류는 유지한다.

- 최종 구현: StackTrace(error, false)의 GetFrames()에서 GetMethod()를 얻고 DeclaringType.FullName과 Name만 조합한다. 진단 생성에 raw StackTrace.ToString(), Exception.StackTrace getter, Message, Data, 파일명·행번호 getter를 사용하지 않는다. 메서드 인수의 실제 값과 예외 객체도 저장하지 않는다. 프레임 부재·메서드 부재·형식화 예외는 기존 빈 문자열 처리로 끝난다.
- 기존 계약: 진단 clear 시점, 최상위 예외 type 저장, 고정 ImportFailed/InvalidInput 처리, 상태 변경, opt-in 초기화, End(operation), 취소·recovery 분기, Changed 구독/콜백/await 순서는 그대로다. 별도 observer·파일 쓰기·스레드 전환은 추가되지 않았다. Begin/finally/catch 내부에서 별도로 발생하는 알림 예외 및 inner exception 전체를 수집한다는 주장은 하지 않는다.
- 유효 RED: pr14-async-trace-red-current.json의 동일 필터 3건 중 2통과/1실패를 직접 확인했다. 실패한 True case는 실제 await 및 재throw 경로에서 원래 ToString 출력이 .cs 및 절대 경로를 남겼음을 보여준다. 앞서 컴파일 중 구 어셈블리의 2건만 실행한 pr14-async-trace-red.json은 증거에서 제외한다.
- GREEN: pr14-async-trace-green.json은 sync/async 두 case와 Succeeded subscriber case의 3건 모두 통과다. 실제 이름 있는 발생 helper frame·타입, 메시지/파일 경로 미포함, 다음 시작 시 clear, 성공 재실행, 기존 ErrorCode/IsBusy/opt-in 정리를 검증한다. 테스트 결과를 직접 읽었으며 독립 Unity 실행은 하지 않았다.
- 실제 게시 경로: 수정 후 probe/e2e-visual-failure-state.json은 System.IO.IOException, Failed, ImportFailed를 유지한다. 새 문자열은 System.IO.Directory.CreateDirectory, CatalogAssetWriter.Publish, StaticCatalogPipeline의 RunAsync state-machine MoveNext, VM의 ImportAsync state-machine MoveNext 등을 보존하며 .cs 및 C: 절대 경로를 포함하지 않는다. 앞선 동기 테스트만으로 경로 부재를 판단했던 한계가 실제 pipeline·비동기 시험으로 보완됐다.
- probe 의미: after-fix-summary.json은 ExpectedFailureCaptured/OriginFrameKept/AbsolutePathAbsent/SourceFileAbsent/StrictNormalReportStayedFailed/OwnedCleanupVerified가 모두 true이고 OriginalIntermittentCauseProven=false다. 충돌 주입은 예상 실패 export의 검증이며, 일반 시각 QA의 Passed 조건을 우회하지 않았다.
- 창 정리: fallback-cleanup.json을 직접 읽어 Verified=true, OwnedRemaining=0, 추가·누락 window/container 각 0, OriginalFocusRestored=true를 확인했다. 파일·디렉터리 삭제의 독립 존재 검사는 수행하지 않았다.
- 보존: 수정 전 경로 잔류를 보여준 기존 probe 검수 문서 Library/pr14-owned-probe-review.md는 철회 경위와 한계를 담은 과거 증거로 유지한다. 이 문서는 해당 개인정보 계약 수정의 후속 검수이며 과거 실행 결과를 새 코드의 완료 증거로 대신 적용하지 않는다.

제품·테스트·probe 파일 수정, Unity 실행, Git·네트워크 쓰기 및 추가 에이전트 실행은 하지 않았다. 검수 결과 문서만 작성했다. 진행 중인 전체 회귀 테스트 결과와 최종 QA 판정은 이번 검수에 포함하지 않는다. 실제 모델·토큰 사용량은 독립 확인할 수 없어 추정하지 않았다.
