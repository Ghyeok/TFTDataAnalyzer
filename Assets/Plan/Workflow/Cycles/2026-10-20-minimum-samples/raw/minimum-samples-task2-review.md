# 최소 표본 설정 Task 2 재검수

2026-10-21. 기존 코드검수 담당 재사용. 현재 AnalysisTab/AnalysisViewModel/UXML 및 UI 회귀 테스트를 읽기 전용으로 검토했다. 실제 실행 모델·추론은 독립 확인할 수 없다. 이 검수에서는 Unity·네트워크·추가 에이전트를 실행하지 않았고 제품 파일을 수정하지 않았다.

## 판정

**Task 2 코드 검수 통과, 필수 수정 0건.** 이전 P2(CSV 대화상자 중 100→200→100 변경을 반영하기 전에 저장)는 실제 재현과 수정 후 회귀 증거를 확인하여 해결로 판정한다. 이전 `minimum-samples-task2-static-review.md`의 미해결 판정은 이 재검수 결과로 갱신한다.

- `AnalysisTab.cs:89`의 공통 현재성 검사는 CanExport, snapshot/행/쌍 참조, 전체 identity와 세트 번호, 필터 값을 대조한다. 모달 복귀 직후 검사하고, 중첩 입력 이벤트가 처리된 다음 일회성 EditorApplication.update에서 다시 검사하여 저장한다. 왕복 입력도 VM 결과를 무효화하므로 이전 snapshot의 저장이 차단된다.
- `AnalysisTab.cs:105`의 callback은 실행 전에 자기 구독을 제거하고, `_pendingExport`와 정확한 delegate 참조가 같을 때만 진행한다. 새 내보내기는 이전 예약을 취소하며 Dispose/Rebuild도 자기 예약을 취소한다. 갱신·카탈로그·표본 기준 변경은 최종 현재성 검사로 오래된 저장을 거부한다. 기존 CSV 형식과 presentation-only 경계는 유지한다.
- 최소 표본 필드는 busy 중 비활성화되는 필터 영역 밖에 있으며 입력 이벤트 구독/해제와 SetValueWithoutNotify를 적용한다. VM은 실제 값 변경 시 세대·취소·결과 초기화를 수행하고 같은 값은 멱등적으로 처리한다. 더 높은 실제 적용 기준은 보존·표시하며 이전 입력과 종료된 binding의 이벤트는 모델을 변경하지 않는다.
- 한글 안내는 입력 하한, 수동 갱신 필요, 실제 적용 기준, 조합 참가자 관측/쌍 비교 가능 로비를 구분한다.

## 직접 읽은 실행 증거

- `minimum-samples-reopened-ui-tests.json`: 12건 중 11통과·1실패. `MinimumChangeDuringDialogRejectsExportEvenIfTheInputReturnsToItsOldValue`에서 Expected 0 / But was 1을 확인했다. 이전 정적 지적의 실제 재현이다.
- `minimum-samples-export-fix-ui-tests.json`: **13/13 통과, 실패·건너뜀·미결 0**. 같은 왕복 변경 테스트 및 새 `RebuildCancelsPendingCsvWriteAndNewBindingCanExport`가 모두 Passed다. 정상 내보내기 테스트는 실제 update 경계를 기다린다.
- 이전에 확인한 VM 22/22, Task 1 정책 35/35·서비스 12/12 결과는 해당 범위의 증거로 유지한다. 이번 작업에서 재실행했다고 주장하지 않는다.

이 판정은 Task 2 코드와 확인한 UI 회귀에 한정한다. 전체 suite, `-MinimumSamples` 캡처 모드, 실제 PNG 및 창·native frame·포커스 정리 QA는 별도 실행·검토 대상이며 통과로 확대하지 않는다. Editor PID/잠금 소유권·복구와 Chrome 무조작 여부는 coordinator가 확인한 사항이고 이 검수에서 별도 조사하지 않았다.
