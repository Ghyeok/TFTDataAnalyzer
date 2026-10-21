# 최소 표본 설정 사이클 최종 독립 QA

2026-10-21. 기존 `analysis_final_qa` 역할을 재사용했다. 기준 main `0c6c1d55d1978d6e737e7a736c3c9f098664e447`, 작업 브랜치 `codex/analysis-minimum-samples`의 현재 작업 트리를 검토했다. 요청 모델/추론과 별개로 이 worker의 실제 runtime 모델·추론·토큰/비용/총 활성 시간은 독립 확인할 수 없어 미확인으로 남긴다.

**판정: 제품·실행·화면 QA 통과, 필수 제품 수정 0건.** 최종 기록 통합에서는 DevelopmentReport의 중단 당시 문단을 과거 시점으로 명시하고 최신 전체 결과/Task3 상태를 반영해야 한다. root가 이 문서 최신화와 링크 확인을 맡기로 했으며, 해당 통합·커밋·PR·최신 head 검수·병합까지 완료됐다는 판정은 아니다.

읽기 전용으로 승인 ImplementationPlan/DevelopmentReport, 제품 6파일·테스트 4파일·도구 4파일의 실제 diff와 관련 전체 코드를 확인했다. 독립 Task2 재검수 결과, 원본 실행 JSON과 최신 PNG 6장도 직접 열었다. Unity/네트워크/Git 쓰기/추가 에이전트 실행은 하지 않았다. `docs/riot-api-key.md`와 `Assets/DefaultVolumeProfile.asset`의 내용은 열지 않았고 사용자 변경을 수정하지 않았다. 이 worker가 작성한 파일은 이 QA 원문뿐이다. 아래 Unity 실행 결과는 coordinator의 저장된 실행 증거를 확인한 것이며 QA worker가 직접 Unity를 실행한 결과로 바꾸어 기록하지 않는다.

## 승인 TODO와 구현 대조

- `MetricPolicy.WithMinimumSamples`는 불변 검토 metadata를 보존한 새 정책을 만든다. 생성자 100 하한을 재사용하고 기존 정책을 변경하지 않는다. 서비스는 잘못된 요청을 provider/관측/캐시 접근 전에 거부하고 요청값과 주입 정책 중 큰 기준을 사용한다. provider 성공/미등록/invalid 경로에도 실제 기준을 유지한다. `BuildSnapshot(policy)`와 결과의 `MinimumSamples`가 사용한 정책을 기록한다.
- VM 기본값 100, 99/0/음수의 입력값 보존과 오류 안내, 갱신/CSV 차단, 100/200/int.MaxValue 수용을 확인했다. 실제 값 변경은 세대 증가·취소 요청·snapshot/rows/pairs 초기화를 수행한다. 같은 값은 멱등적이다. 요청 시작값 캡처, 더 낮은 기준 snapshot 거부, 더 엄격한 결과 수용, 늦은 완료/실패/취소의 과거 결과 부활 차단을 확인했다.
- 입력은 busy 필터 영역 밖에 있어 실행 중에도 변경·취소할 수 있다. 현재 VM 안에서 catalog 변경과 UI 재구성은 기준을 보존하고, 옛 필드/Dispose binding 이벤트는 해제한다. 새 영속 설정·자동 수집·자동 새로고침·네트워크·실데이터 분류를 추가하지 않았다. 실제 적용 기준 안내는 조합의 참가자 관측 수와 쌍의 비교 가능 로비 수를 구분한다.
- CSV 형식과 presentation-only 경계를 보존한다. 200 기준 n199/n200의 조합·쌍을 실제 UI 테스트에서 다루며 부족표본의 비율 필드를 복원하지 않는다. 저장 대화상자 중 100→200→100 변경은 snapshot을 무효화한다. 모달 직후와 일회성 Editor update에서 현재성 검사를 하고 정확한 delegate를 실행 전 해제한다. 새 저장/Dispose/Rebuild는 자기 예약을 취소한다. 예약 취소 뒤 새 binding의 정상 저장도 회귀에 포함됐다.
- 변경 범위는 승인된 최소 표본 설정·서비스 계약 이관·검증 도구 확장에 맞는다. 실제 표본 확보, 새 그룹 집계, 범위 혼합, 하한 완화, 실제 API·분류 등록을 완료했다고 해석하지 않는다.

## 직접 확인한 실행 증거

| 증거 | 실제 결과 |
|---|---|
| `minimum-samples-task1-policy-green.json` | 35/35, 실패/제외/미결 0, 8.09초 |
| `minimum-samples-task1-service-green.json` | 12/12, 실패/제외/미결 0, 51.73초 |
| `minimum-samples-task2-vm-green.json` | 22/22, 실패/제외/미결 0, 12.96초 |
| `minimum-samples-reopened-ui-tests.json` | 12건 중 11P/1F. 왕복 기준 변경 테스트에서 Expected 0 / But was 1인 실제 저장 결함 재현 |
| `minimum-samples-export-fix-ui-tests.json` | 수정 후 13/13, 실패/제외/미결 0, 7.22초. 왕복 변경과 Rebuild 예약쓰기 취소 포함 |
| `minimum-samples-full-tests.json` | Total726 / Passed725 / Failed0 / Skipped1 / Inconclusive0, 129.4초. outer/inner success, tool errors/warnings 없음 |
| 같은 전체 Results의 관련 namespace 직접 집계 | Analytics153/153 및 UI190/190 Passed. 별도 추가 실행으로 중복 집계한 것이 아님 |
| 전체의 유일한 제외 | 기존 `ComplianceSettingsStoreTests.ExistingFileLinkIsRejectedWithoutReadingOrOverwritingDestination`, `Host does not permit unprivileged symlinks.` 환경 사유. 이 테스트를 통과했다고 기록하지 않음 |
| `minimum-samples-final-compile-status.json` | up_to_date, failed=false, compilationFailed=false, errors[]. 새 컴파일 완료 상태로 바꾸어 주장하지 않음 |

이전 Bee backend 중단과 사용자 Editor 재시작 이력은 최신 성공 결과로 소급해 지우지 않는다. 근본 원인 해결이나 모든 timeout/연결 끊김 복구를 입증한 증거로 확대하지 않는다. 독립 코드 검수 의견은 실제 RED/GREEN 실행을 대체하지 않았다.

## 실제 PNG와 소유 창

`analysis-window-{940,500}-{minimum-top,minimum-rows,minimum-invalid}.png` 6장을 직접 열었다. 전부 Analyzer 대상 화면이며 다음을 확인했다.

- top: 입력 200과 실제 적용 기준 200, 참가자 관측/비교 가능 로비 구분, 수동 갱신 버튼, 관측 경기 기간, 역사적 관측 고지가 읽힌다.
- rows: n100/n99 조합이 200 기준에서 표본 부족이며 퍼센트/분자/분모/신뢰구간이 노출되지 않는다. 허용된 평균과 관측 기간은 유지한다.
- invalid: 99가 그대로 남고 100 이상 오류 안내, 결과/기간 초기화, 갱신 차단이 보인다. 940px에서는 CSV 버튼 비활성도 화면에서 직접 보인다. 500px에서는 하단 CSV 버튼이 viewport 아래이므로 그 버튼의 차단은 helper 상태 확인과 UI 테스트 증거로 판단했으며 픽셀에서 보았다고 과장하지 않는다.
- 두 폭에서 줄바꿈·스크롤·지속 footer를 확인했다. 현재 최소 표본 모드의 창 높이는 두 폭 모두 700이며 PNG는 title 포함 726px다. 이전 500×300 캡처를 이번 증거로 재사용하지 않았다.

`analysis-minimum-visual-final.json`은 Passed=true, MinimumSettingsValidation=true, LoadCalls3, CollectionCalls0, SyntheticDataOnly/NoObservationStore/OfflineCredentials=true다. 입력 200/적용200과 99/결과0, invalid 갱신·CSV 차단 및 입력만 변경했을 때 호출 증가 없음이 helper에 검증된다. 전후 기존 EditorWindow9개/native ContainerWindow1개 ID가 정확히 같고 추가·누락0, 소유 창0, 원래 focus 복원이다.

표시 전/후 `analysis-visual-initialization-failure-cleanup.json`과 `analysis-visual-failure-cleanup.json`도 ExpectedProbeFailure/Passed=true, cleanup failure 없음, 기존 창·프레임 동일/소유0이다. `analysis-visual-fallback-cleanup.json`은 outer/inner success와 Verified=true/OwnedRemaining0이다. 도구 diff는 기존 정확한 소유 객체/이름 및 새 HWND 하나의 PID별 baseline 확인, Close 우선 finally 정리를 유지한다. 이 세 경로의 결과를 모든 transport timeout 정리 성공으로 일반화하지 않는다.

## 동결·보호·Console

`minimum-samples-final-source-freeze.json`의 허용된 14개 소스만 이 QA에서 SHA256을 다시 계산했다. 동결값과 14/14 일치, 변경0이다. `minimum-samples-final-evidence.json`의 보호 파일1389/변경0은 coordinator의 전후 대조 결과로 확인했다. 금지된 사용자 파일 내용이나 실제 관측을 직접 열어 확인한 것으로 바꾸어 주장하지 않는다. 기존 자동 버전확인 상태 등의 별도 합법적 변경이 있었다면 root가 최종 기록에서 구분해야 하며, 제공된 최종 대조에는 보호 변경 목록이 비어 있다.

전체 실행의 before/after 원본과 최종 evidence는 같은 기존 창9/native1/focus0을 기록한다. Console 원본은 session `e773983bf1794d65a56e2e538bc8bf40`, cursor12→20, reset/dropped=false, 새 error entries0이다. 별도 warning 조회 entries[]와 현재 error/warn count0, compilationFailed=false도 확인했다. 실제 Match API0·Chrome 조작0은 coordinator가 기록한 범위이며 이 QA가 외부 실행 이력을 조사한 것은 아니다.

변경된 승인 제품/테스트/도구 경로에 한정한 `git diff --check`는 exit0(줄바꿈 변환 경고만)이다. 사용자 변경 파일 내용은 해당 검사에 포함하지 않았다.

## 최종 통합 조건과 남은 범위

Task3의 구현·관련/전체 회귀·정상/예외 창 정리·실제 이미지·독립 QA 게이트는 위 근거로 충족한다. DevelopmentReport 상단과 초기 Task2 문단의 “전체 검증 준비”, “GREEN/PNG 미실행” 등은 중단 당시의 상태임을 명시하고 최신 726건 중725P/0F/1호스트제외 및 이 QA 결과로 현재 상태를 갱신해야 한다. root가 2026-10-21 메시지로 이를 통합하고 문서 내용/링크를 확인하기로 했다. 아직 통합 전 문서 최신화나 링크 확인까지 이 QA가 완료했다고 기록하지 않는다.

커밋/PR·최신 head CodeRabbit/미해결 의견/CI 확인·승인 조건 병합은 root의 후속 단계다. 실제 API·키·실데이터 증강 분류 등록·실제 충분 표본 검증·OS 실패 주입·Bee 재발 원인 검증은 별도 남은 과제다. Hermes 예약이나 추가 독립 모니터를 등록하지 않는다.
