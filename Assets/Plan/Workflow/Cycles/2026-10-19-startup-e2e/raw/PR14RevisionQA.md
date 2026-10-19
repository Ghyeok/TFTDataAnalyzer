# PR14 수정 별도 QA

상태: 검증 실패 / 병합 보류. 아래 초안 대조표와 최종 증거 갱신을 함께 적용한다.

기준: HEAD `814c635`와 현재 미커밋 revision diff. 요청된 실행 모델·추론은 GPT-6.1 Sol / Medium이며 이 QA 환경에서는 실제 모델 식별값·사용량·비용을 독립 확인할 수 없어 추정하지 않는다. QA는 소스·원본 산출물 읽기와 이 보고서 작성만 수행했다. Unity·네트워크·테스트·Git 쓰기는 실행하지 않았고 root 실행 증거를 대조했다.

## 범위와 확인 결과

| 승인된 수정/검증 | 대조한 증거 | QA 판단 |
|---|---|---|
| 요청 전 terminal SUCCESS 거부 | Python entry의 `requestSent` guard와 offline 회귀; `pr14-python-red.txt` 4개 중 1실패, `pr14-python-green.txt` 4통과 | 해당 결함 RED/GREEN 확인. 실제 Antigravity 연결 성공 증거로 확대하지 않는다. |
| 안내 갱신 시 이미 로드한 Rows 재사용 | `NoticeFor`, 실제 창의 `LastImportedSourceVersion.Select(_dataManager.Rows)`; `pr14-notice-red.json` callback 0기대/2실제 실패, green 1통과 | 창 생성·Changed·GUI 재구성·성공 import 뒤 안내 해제·HTTP 1회 조건을 회귀가 검사한다. |
| 전체 EditMode 회귀 | `pr14-revision-suite.json`: 617전체/616통과/0실패/1제외 | 해당 실행 통과. 제외는 호스트가 비관리자 symlink를 허용하지 않은 `ExistingFileLinkIsRejectedWithoutReadingOrOverwritingDestination` 한 건이다. 최종 제품 고정 뒤 새 전체 결과는 대기 중. |
| 수동 fake E2E | 현재 `e2e-visual-final.json`: Passed=true/Completed, 6 captures, 가짜 전송만 사용/실제 pipeline·AssetDatabase/사용자 credentials 미사용 | 이 실행의 성공은 확인된다. PNG 실제 픽셀은 이 QA가 아직 검수하지 않았다. |
| 소유 창 정리 | 위 JSON: 잔류0, 기존 창 보존=true, 원포커스 복원=true, 추가/누락 window·container 빈 배열 | 해당 성공 실행의 구조적 정리 증거 확인. 최종 전역 창 대조·Console·보호 해시는 대기 중. |
| 실패 진단 보강 | harness는 고정 Stage/DataState/DataErrorCode만 별도 JSON에 기록 | 임시 VM 진단 구현이 최종 제품에 남지 않는지 최종 diff 대조 필요. |

## 미해결 필수 게이트

수동 E2E 실패가 두 번 보존되어 있다. `pr14-visual-first-failure/e2e-visual-failure-detail.txt`와 `pr14-visual-warm-failure/e2e-visual-failure-detail.txt`는 둘 다 실제 scenario의 마지막 원본 버전 `16.18.2` import 뒤 `DataManagerState.Succeeded` assertion(현재 테스트 421행)에서 실패한다. warm 실패의 `e2e-visual-failure-state.json`은 Stage=`analysis500`, DataState=`Failed`, DataErrorCode=`ImportFailed`다. Stage는 마지막 캡처 단계이며 분석 렌더링 자체가 실패 원인이라는 뜻으로 해석하지 않는다.

이후 성공 실행과 임시 진단에서의 미재현은 위 실패의 원인 규명·수정·안정성 회귀 증거를 대신하지 않는다. 현재 자료로 제품 결함인지 검증 환경/AssetDatabase 조건인지 확정할 수 없다. 실패를 모두 통과로 요약하거나 필수 수동 전체 흐름 게이트를 닫으면 안 된다. 원인에 대한 유한한 조사에서 근거를 확보하고, 원인에 맞는 최소 수정 또는 환경 원인 입증과 회귀 검증이 필요하다. 무근거 반복 실행을 추가 필수로 요구하지 않는다.

## 최종 인수 전 추가 대조

1. 임시 진단 제거 후 최종 diff와 새 compile/전체 EditMode/Python 결과를 연결한다.
2. 사용자 `Assets/DefaultVolumeProfile.asset` 사전 hash 보존, 기본 Generated 등 보호 파일 비교, 검증 임시 Assets 잔류0, 기존 창·native frame·포커스·새 Console 오류를 확인한다.
3. 수동 PNG 6개 실제 픽셀 확인과 구조적 UI/정리 결과를 구분한다.
4. 별도 코드 검수 결과 및 문서의 TODO·완료 주장이 간헐 실패를 누락하지 않는지 확인한다. 이 QA는 PR 최신 head의 CodeRabbit/CI/미해결 thread를 조회하지 않았으므로 병합 승인 근거를 제공하지 않는다.

`ImplementationPlan.md`의 기존 체크 표시는 이전 실행 이력이다. 현재 revision 최종 통과를 대신하지 않는다. 최종 증거는 아래 갱신 절에 반영했다.

## 최종 증거 갱신

상태: **검증 실패 / 병합 보류**. 이 절이 위 초안의 최종 증거 대기 문구를 대체한다. 두 제한된 리뷰 수정의 회귀와 최종 자동 검증은 확인됐으나 필수 수동 E2E의 간헐 실패 원인은 미규명이다.

- `pr14-final-compile-status.json`: completed, failed=false, compilationFailed=false, errors 빈 배열.
- `pr14-final-python.txt`: 4개/4통과. `pr14-final-suite.json`: 617전체/616통과/0실패/1호스트 symlink 제외, 77.23초. 최종 자동 검증을 수동 간헐 실패 해소의 근거로 확대하지 않는다.
- `pr14-final-revision-code-review.md`: 제한된 revision 범위의 확정 finding 0건. 제품 VM diff 0을 QA도 직접 확인했다. 임시 진단과 decorator 제거는 별도 검수가 확인했다.
- 최종 suite 종료 뒤 `pr14-final-protected.json`: 보호 1,386개 변경0, 임시 E2E Assets 디렉터리0. 사용자 Volume SHA256도 사전 baseline과 현재 모두 `4789b43834dd5efb32eec4c3b436baf0895c9f8cf2a0624121da8dd812ee984a`로 QA가 직접 대조했다. 원래 사용자 diff는 보존해야 한다.
- `pr14-final-windows.json`과 `pr14-before-windows.json`의 nested result를 QA가 직접 비교해 동일 확인: 기존 창9개 instance IDs, native container1개 ID, focus0 모두 동일. 현재 수동 성공 결과도 소유 잔류0·창/프레임 추가누락0을 기록한다.
- root는 수동 PNG6개를 실제로 열어 안내, 수집2건, 분석 표본부족/정책차단, 500폭 스크롤을 확인했다고 전달했다. 기존 왼쪽 문자 겹침 잔존도 보고했다. 이 QA는 PNG 픽셀을 독립 검수하지 않았으며 JSON 구조와 root 직접 확인 보고를 구분한다.
- `pr14-final-console.json`: since261 뒤 cursor277, 새 error2건(seq270/271)은 Pipeline `/api/commands` transport 연결/headers 예외다. 현재 groundTruth는 compilationFailed=false, consoleErrors=0, consoleWarnings=0이다. 새 오류2건의 근원을 제품 예외로 단정하지 않으며 전체 Console 오류0으로도 요약하지 않는다.

추가 필수는 원인 미규명 수동 E2E 실패의 근거 기반 조사와 그 결과에 맞는 검증이다. 이번 QA는 검증 실패를 유지하며 병합을 승인하지 않는다. 새 자동 전체 검증을 불필요하게 반복하는 것은 요구하지 않는다. 준비된 리뷰 수정과 정확한 진행 중 기록의 push 여부는 root의 현재 승인 범위 판단이며 QA 통과와 구분한다. PR 최신 head의 CodeRabbit/미해결 thread/CI는 이 QA가 조회하지 않았다.
