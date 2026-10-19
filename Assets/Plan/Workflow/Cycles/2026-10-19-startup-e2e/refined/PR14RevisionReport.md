# PR14 검수 수정·미해결 검증 보고

## 2026-10-20: 실패 원본 진단 보완

현재도 **원인 미확정·전체 QA 검증 실패·병합 보류**다. 기준 b213d07 이후 진단 변경만 추가했다. 아래 2026-10-19 결과는 당시 이력이다.

- 새 컴파일 후 한정 수동 실행과 전체 suite 직후의 수동 실행 모두 실패가 재현되지 않았다. 테스트가 남긴 상태라는 가설은 입증되지 않았고 반복 성공을 원인 해결로 해석하지 않는다.
- VM의 기존 generic catch에서 포착한 최상위 예외 형식과 메서드 frame을 내부 읽기 전용 문자열로 보존한다. 다음 실제 import 시작 전에 초기화한다. 예외 객체/Message/Data/실제 인수/PDB 파일 경로는 보관하지 않으며 제품 디스크 쓰기·UI 노출·요청/정리 순서 변경이 없다. 형식화 실패도 기존 오류 처리를 막지 않는다.
- 수동 하네스는 기존 실패 상태 JSON에 이 두 문자열을 추가한다. Begin/catch 내부/finally 알림의 모든 예외나 inner chain 전체를 수집하는 기능은 아니다. 실제 간헐 실패가 재발할 때의 JSON 내보내기는 아직 관찰하지 못했고, 내부 값은 강제한 두 unit fault로 검증했다.

| 최종 증거 | 결과·Library/TftAnalyzerValidation 원본 |
|---|---|
| 진단 회귀 | pr14-diagnostic-red.json 2실패 → pr14-diagnostic-green.json 2통과. 실제 이름 있는 helper throw와 완료 subscriber 예외, 원래 ImportFailed/IsBusy=false/opt-in 해제·clear·비밀 메시지/파일 경로 미포함 |
| 컴파일·전체 | pr14-diagnostics-final-compile-status.json completed/실패없음. pr14-diagnostics-final-suite.json 619개 중618통과/실패0/호스트 링크1제외,84.8초 |
| 수동·PNG | pr14-diagnostics-final-visual.txt 및 e2e-visual-final.json 성공/6 PNG/소유 잔류0. root가 전부 직접 열어 identity/버전 안내/수집2건/분석 억제 사유/작은 창 스크롤·고지 확인. 이번에는 과거 문자 겹침을 관찰하지 못했으나 원인 해소를 주장하지 않음 |
| 창·보호 | pr14-oct20-before-windows.json/pr14-diagnostics-final-windows.json: 기존 창9/native1/focus0 정확히 동일. pr14-diagnostics-final-protected.json: 보호1,386개 변경0/임시 E2E 자산0. 사용자 Volume 변경 보존 |
| Console | pr14-diagnostics-final-console.json cursor277→307 새6건(seq278/279/280/281/288/289), 모두 Pipeline transport/header 오류. 현재 compilationFailed=false/groundTruth 오류·경고0. 전체 Console 오류0 주장은 아님 |
| 독립 판정 | [개발 검수](../raw/ImportDiagnosticsReview.md) finding0, [별도 QA](../raw/ImportDiagnosticsQA.md) 진단 변경 한정 통과. [배정·공개 요청](../raw/ImportDiagnosticsCoordination.md). 원래 간헐 오류의 QA/병합 보류는 유지 |

Astra/High 분석 호출은 사용량 제한으로 실행되지 않아 Sol/High 분석·개발 검수, Sol/Medium 별도 QA를 요청했다. 실제 runtime 모델·추론·토큰/비용 총계는 독립 확인 불가이며 추정하지 않는다. 추가 Antigravity/Hermes 호출·예약·CodeRabbit 재조회는 없었다.

재개 시 이미 수집한 성공을 반복하지 말고 현재 작업 트리와 실행 handle을 확인한다. 재발 시 e2e-visual-failure-state.json의 ImportFailureType/ImportFailureStack으로 서비스/게시/알림/정리 중 발생 계층을 좁힌 뒤 최소 재현·수정·회귀·QA를 수행한다. 재컴파일/원인 수정 없이 성공 횟수만 늘리는 실행은 하지 않는다. 최신 PR head 게이트·승인 범위 병합·사용자 최종 승인은 계속 남아 있다.

## 2026-10-19 수정 이력


2026-10-19 KST. 상태: 검증 실패, 병합 보류. 기준 head `814c635`, 브랜치 `codex/startup-e2e`. 두 검수 수정은 구현했으나 수동 E2E의 간헐 실패 원인은 미확인이다. 이전 조건부 QA 통과는 이번 수정의 최종 승인으로 사용하지 않는다.

## 검수와 변경

사용자는 최신 결과를 한 번 확인하고 통과했다면 기존 승인 범위에서 진행하도록 답했다. [PR #14](https://github.com/Ghyeok/TFTDataAnalyzer/pull/14)의 해당 조회는 CodeRabbit PENDING, 미해결 스레드 1개, 다른 CI check 없음이었다. MERGEABLE은 검수 통과가 아니므로 병합하지 않았다. Hermes 예약은 중단 유지하며 주기적 조회·추가 AI 호출을 예약하지 않았다.

- Antigravity CLI 결과는 모델 초기화 검증과 실제 요청 전송 완료 후에만 수락한다. 조기 SUCCESS를 거부하며 결과 파일을 발행하지 않는 회귀를 추가했다. 기존 경로·스키마·프로세스 정리 계약을 보존한다.
- 시작 안내는 이미 로드된 `DataManagerViewModel.Rows`에서 마지막 성공 버전을 선택한다. Changed 때 전체 카탈로그를 다시 읽는 callback을 사용하지 않는다. 기존 Notice/생성자 계약과 버전 비교를 유지하고 창 생성·Changed·재구성·가져오기 회귀를 추가했다.
- 수동 검증 실패 시 고정 Stage/DataState/DataErrorCode를 별도로 남긴다. 예외 메시지·키·seed·원본 응답을 기록하지 않는다. 조사 중 추가한 서비스 decorator와 제품 VM의 임시 진단은 제거했다. VM의 HEAD 대비 내용 차이는 0이다.
- CodeRabbit 요약의 docstring coverage 경고는 필수 저장소 check로 확인되지 않았다. 관련 없는 주석 대량 추가나 검사 설정 약화는 하지 않았다.

## 실행 증거

원본은 Git 제외 `Library/TftAnalyzerValidation/`에 있다. 과거 실행과 최종 실행을 구분하며 Library 삭제 시 원본은 사라질 수 있다.

| 검증 | 실제 결과·원본 |
|---|---|
| 조기 SUCCESS 회귀 | RED 4 methods 중 1실패 → GREEN 4/4; pr14-python-red.txt, pr14-python-green.txt. 최종 pr14-final-python.txt 4/4, 추가 유료 AI 요청 없음 |
| 로드된 Rows 회귀 | RED callback 기대0/실제2로 1실패 → GREEN 1/1; pr14-notice-red.json, pr14-notice-green.json |
| 최종 컴파일 | completed, failed=false, compilationFailed=false; pr14-final-compile-status.json |
| 최종 전체 EditMode | 617개 중 616통과/실패0/호스트 symlink 권한1제외, 77.23초; pr14-final-suite.json |
| 수동 실패 | 첫 실행과 warm 실행의 마지막 원본16.18.2 가져오기가 실패했다. pr14-visual-first-failure/, pr14-visual-warm-failure/에 보존. warm 상태는 Failed/ImportFailed이며 stack은 마지막 가져오기 성공 단언을 가리킨다. analysis500은 마지막 캡처 단계이며 원인 위치를 뜻하지 않는다. |
| 후속 수동 실행 | e2e-visual-final.json은 성공, 6 PNG와 소유 잔류0. 서비스/VM의 형식·stack만 기록하는 임시 진단 실행에서는 실패가 재현되지 않아 원인 예외를 확보하지 못했다. 이 성공으로 앞선 실패가 해결됐다고 판단하지 않는다. |
| PNG 직접 확인 | root가 940/500px 가져오기·수집·분석 6장을 열어 대상 화면, 새 버전 안내, 수집2건, 표본 부족/정책 차단, 작은 창 스크롤을 확인했다. 기존 좌측 문자 겹침 흔적은 남았으며 픽셀 완전성을 주장하지 않는다. |
| 창·데이터 보존 | pr14-before-windows.json/pr14-final-windows.json: 기존 창9/native1/focus0의 정확한 ID 동일. 각 수동 작업 소유 잔류0. pr14-final-protected.json: 보호1,386개 변경0, 임시 E2E 자산 폴더0 |
| Console | pr14-final-console.json cursor261→277: 새 오류2건(seq270/271), 모두 Pipeline /api/commands transport/header 오류. compilationFailed=false, 현재 groundTruth 오류/경고0. 전체 Console 오류0 또는 제품 오류라고 보고하지 않는다. |

독립 코드 검수는 최종 diff finding0이며 실행 통과와 구분한다. [공개 검수 기록](../raw/PR14RevisionCodeReview.md), [별도 QA](../raw/PR14RevisionQA.md), [수정 요청](../raw/PR14RevisionRequest.md)을 보존한다. 별도 QA는 간헐 실패의 미규명으로 병합 보류를 유지한다.

## 남은 작업과 재개 기준

1. 수동 마지막 가져오기 실패의 실제 예외·발생 조건을 확보하고 원인을 좁힌다. 단순 반복 성공이나 대기 시간 증가로 종결하지 않는다. 조사 중에도 실제 publisher/AssetDatabase를 fake로 대체하지 않는다.
2. 원인에 맞는 최소 수정과 재현 회귀를 검증하고 독립 검수·QA를 갱신한다. 비밀값·식별 가능한 경기 원본은 진단에 저장하지 않는다.
3. 수정 커밋 push 후 이번 대화를 종료한다. 다음 사용자 재개 때 최신 head의 CodeRabbit·미해결·CI를 한 번 확인한다. 옛 head의 결과를 재사용하지 않는다.
4. QA 및 최신 head 게이트 통과 후 기존 승인 범위에서 병합하고 사용자 최종 승인을 받는다. 전체 목표·사이클은 미완료다.

모델 배정은 구현·코드 검수·별도 QA에 GPT-6.1 Sol/Medium을 요청했다. 독립적으로 확인 가능한 실제 런타임 모델·추론·토큰·비용 총계는 제공되지 않아 추정하지 않는다. root가 Unity 실행을 전담했고 Assets 편집과 실행을 교대했다. 재작업은 두 review 회귀, 수동 실패 조사와 임시 진단 제거이며 전체 suite 실행시간은 위 실제 값만 기록했다.
