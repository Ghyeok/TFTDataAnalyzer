# 실제 검증 결과 발췌

2026-10-19 KST. root가 연결된 Unity6000.3.8f1에서 순차 실행했다. 아래 JSON/PNG 원본은 Git 제외 `Library/TftAnalyzerValidation/`에 있으며 삭제될 수 있다. AI 검수만으로 통과를 대신하지 않았다.

| 원본 | 확인 결과 |
|---|---|
| analysis-red-tests.json | 초기 신규12/12 실패, 컴파일 정상. 버튼/필터 연결 누락과 미구현 snapshot/CSV의 의도한 RED |
| analysis-first-green-tests.json / analysis-csv-namespace-red.json | 각각180초/60초 CLI 시간 초과. 정상 취소나 정리 완료 증거가 아님. 사용자 Editor 종료·재시작 후 수정 |
| analysis-path-attribute-trace-bounded.txt | 같은 Mono에서 장치 루트 부모가 자신을 반환하는 것을 유한8단계로 확인 |
| analysis-csv-pathfix-green.json | 5/5, skipped0. 장치/슬래시 namespace·짧은 경로 별칭 차단, 기존 실제 Project 디렉터리 밖 정상 저장, UTF-8·invariant·기존 CSV 보존 |
| analysis-focused-green.json / analysis-modal-isolated.json | 중간27/28 및 단독 modal 실패. analysis-focused-final.json 역시 당시27/28 실패 이력 |
| analysis-ui-final.json | 최종7/7, 저장 도중 복수 필터 변경은 쓰기0·렌더 전후 안내 유지 |
| analysis-final-compile.json | status completed, failed false, compilationFailed false, errors[] |
| analysis-full-final.json | outer/inner success true, TFTAnalyzer.Tests.EditMode, Total495 Passed495 Failed0 Skipped0 Inconclusive0, Duration54.24s |
| analysis-full-windows.json | 기존 창9·native 프레임1의 ID 전후 동일, FocusId0→0 |
| analysis-visual-final.json | 최신 정상 Passed true, LoadCalls1/CollectionCalls0, 소유 창0, 기존 창·프레임·Editor 포커스 보존 |
| analysis-visual-initialization-failure-cleanup.json | 현행 도구 표시 전 실패 Passed true, 추가/누락 창·프레임0, 소유0 |
| analysis-visual-failure-cleanup.json | 현행 도구 표시 후 실패 Passed true, 추가/누락 창·프레임0, 소유0 |
| analysis-visual-fallback-cleanup.json | 현행 shell fallback Verified true, OwnedRemaining0 |
| analysis-window-{940,500}-{top,rows,pairs}.png | 최신6장을 root가 직접 열어 확인. 두 크기 대상 UI·전체 identity·기간·조합/쌍·표본99 억제와100 비율/구간·고지·작은 창 줄바꿈/스크롤 |
| analysis-protected-final.json | Files1386 Changed0 Paths[] |
| analysis-console-final.json | 같은 session, since32→cursor52, entries[], dropped/reset false. 기존 error2/warn0 보존, 새 경고/오류0 |

화면 이력: 첫 쌍 캡처는 heading만 보였고 다음 실행은 다른 앱을 찍었다. 모두 화면 완료로 처리하지 않았다. Native 창 caption 일치 방식은 안전하게 실패해 소유0 정리를 확인한 후 폐기했다. 최신 도구는 실행 전 PID별 HWND 집합과 비교해 새 프레임 하나만 있을 때 가시성을 확보하며, 사용자 프레임을 조작하지 않는다. 최종6장은 모두 Analyzer 화면으로 직접 확인했다.

미실행은 실제 네트워크·비밀값·실데이터 분류 생성·네이티브 저장 대화상자 클릭·OS 교체 실패 주입과 모든 연결 끊김/timeout 복구다. 이전467/467이나 중간 실패 파일명을 최신 통과로 재사용하지 않는다.
