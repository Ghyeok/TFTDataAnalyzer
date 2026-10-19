# PR14 가져오기 진단 독립 개발검수

판정: 검토한 3파일 diff에서 조치가 필요한 P0–P3 발견 사항 없음. 기존 원인조사에 필요한 최소 관측 개선으로 개발검수 수용 가능하다. 원인 해결·전체 QA·병합 승인에 대한 판정은 아니다.

- 범위: DataManagerViewModel.cs, DataManagerViewModelTests.cs, TftAnalyzerEndToEndVisualValidation.cs의 현재 diff와 지정 RED/GREEN JSON만 읽었다. 제품 코드·Unity·네트워크·Git 쓰기는 수행하지 않았다.
- VM: 내부 getter/private setter 문자열 2개만 추가했고, busy/disposed guard 뒤 실제 작업 시작 전에 초기화한다. generic catch에서 최초 포착된 최상위 예외 type과 메서드 frame만 보존하며, StackTrace 형식화 실패도 삼켜 기존 고정 ErrorCode/상태 처리로 진행한다. 임시 파일 쓰기 코드는 남아 있지 않다.
- 비밀값·노출: Exception 객체, Message, Data, ToString(), PDB 파일 경로를 새 진단에 보관하지 않는다. 공개 UI 속성이나 바인딩은 추가되지 않았고 하네스가 내부 값을 반사 조회하여 기존 Library 실패 JSON에만 추가한다. 예외 인수의 실제 값도 메서드 frame 출력에 포함하지 않는다.
- 정리·문맥: 취소 처리, opt-in 해제, End(operation), 창 소유권/Close/Dispose/focus 복원, await 및 callback 등록·해제 순서는 변경하지 않았다. 진단은 실패 경로의 동기 문자열 생성이며 별도 observer·스케줄러·스레드 전환을 만들지 않는다.
- 테스트: 실제 이름 있는 helper throw의 발생 frame, 비밀 메시지 미포함, 파일 경로 미포함, 다음 시작 시 clear, 성공 재실행을 확인한다. Succeeded 알림에서만 throw하는 subscriber도 generic catch 진단과 ImportFailed/IsBusy=false/opt-in=false를 검증하고 finally에서 구독을 해제한다.
- 실행 증거: pr14-diagnostic-red.json은 동일한 새 테스트 2개가 진단 type 빈 문자열로 실패(2/0P/2F), pr14-diagnostic-green.json은 2/2P/0F이며 오류·경고 배열이 비어 있음을 직접 읽었다. 독립 Unity 재실행은 하지 않았다.
- 한계: Begin의 알림·finally End의 알림·catch 안의 알림 예외, 별도 recovery/cancellation 분기, inner exception 전체를 수집하는 기능은 아니다. 현재 ImportFailed의 generic catch 관측으로 범위를 한정하면 이 한계는 승인 범위에 부합한다.

원인 미확정 및 기존 QA/병합 보류는 유지한다. 실제 런타임 모델·토큰 사용량은 독립 확인할 수 없어 추정하지 않았다.
