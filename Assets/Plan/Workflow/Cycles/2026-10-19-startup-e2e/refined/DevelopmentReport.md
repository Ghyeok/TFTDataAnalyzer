# Task6 개발·실행 검증 보고

현재 상태(2026-10-20 PR14 비동기 진단 수정): **수동 E2E 간헐 실패의 원인 미확인으로 QA 검증 실패·병합 보류**. 두 검수 수정·비동기 진단 경로 제거와 최종620개/619통과/실패0/제외1은 [최신 수정 보고](PR14RevisionReport.md)를 따른다. 아래 이전 실행의 조건부 통과·수치는 당시 이력이며 최신 승인으로 적용하지 않는다.


2026-10-19. 상태: 구현·독립 코드 검수 통과, 별도 QA 조건부 통과·blocker0. 기준 ab63a3c, 브랜치 codex/startup-e2e. 사용자 Volume 변경은 커밋 대상에서 제외한다. 사용자 최종 승인·최신 PR 검수·병합은 아직 아니다.

## 변경 결과

- 시작 시 유휴 상태에서 원본 버전 목록만 확인한다. 자동 확인은24시간 gate와 두 필드 원자 상태를 사용하며 예약 전/후 취소·동시 실행·유한 기한·늦은 응답 폐기·공개 TestRunner 실행 배제를 연결했다. 안내는 기존 데이터 탭에 표시한다.
- 성공 가져오기 완료 UTC는 인덱스에만 기록하고 마지막으로 성공한 카탈로그를 비교 기준으로 쓴다. legacy 시각을 꾸며내지 않는다. 같은 버전 재가져오기는 게시된 bytes를 보존한다.
- Generated/cache/index와 아이콘 검증에 같은 주입 루트를 연결하고 대상 인덱스만 저장한다. 실제 파이프라인·AssetDatabase·익명 저장·집계·분석 binding을 통과하는 가짜 전송 E2E를 추가했다.
- 금지 제품 타입·메뉴·실제 UI 문구를 검사한다. 기본 미분류 증강 차단과 정책 통과 행의 표본 부족을 분리했다. 신뢰 분류를 명시 주입한 합성 비율·Wilson 표시는 실데이터 공급 증거가 아니다.
- 실제 Antigravity 기획 A와 GPT 기획 B의 동일 입력 검수를 적용했다. [통합 결과](../../../../../../AntigravityCollab/tasks/2026-10-19-task6-planning/result.md). CLI의 강제 접근 제한은 미검증이며 일반 격리 자동화·예약을 활성화하지 않았다.

## 실제 실행 증거

아래 원본은 Git 제외 Library/TftAnalyzerValidation에 있다. 경로는 프로젝트 루트 기준이며 Library 삭제 시 사라질 수 있다. 외부 결과나 과거 head의 통과를 현재 결과로 쓰지 않았다.

| 검증 | 결과·원본 파일 |
|---|---|
| 최종 컴파일 | completed/failed=false/compilationFailed=false; task6-window-compile-status.json |
| 최종 전체 EditMode |616개,615통과/0실패/1환경별 제외,111.18초; task6-final3-suite.json |
| 제외 사유 | ComplianceSettingsStoreTests.ExistingFileLinkIsRejectedWithoutReadingOrOverwritingDestination: Host does not permit unprivileged symlinks. 통과로 세지 않는다. 명시 네트워크 테스트는 기본 실행 대상 밖이다. |
| 공개 버전 목록만 실제 네트워크 | 명시1개 통과,3.01초,15초 취소 기한; task6-public-versions-smoke.json. 실제 Riot Match API·개인 seed 요청 없음 |
| scheduler 회귀 |20/20; task6-final-scheduler.json. 복원 알림 누락2개 RED→GREEN |
| 실제 E2E/아이콘/validator |4/4,1/1,11/11; task6-green3-e2e.json, task6-green3-icons.json, task6-green3-validator.json |
| 인덱스 계약 분리 |11/11; task6-final2-index.json. 외부 프로젝트 차단과 현재 프로젝트의 누락 파일 검증을 각각 확인 |
| CLI 오프라인 회귀 |3 method 통과(형식 오류4개·외부 hardlink4경로 포함); task6-antigravity-unit-tests.txt. 외부 AI 요청 없음 |
| 실제 새 창 재생성 | task6-new-window.json1/1. 첫 창 Close→새 VM/scheduler/다른 instance 창에서24시간 내 캐시 안내·추가HTTP0·양 창의 정확한 Changed 구독 해제 |
| 실제 가짜 수동 E2E | e2e-visual-final.json Passed=true,6 PNG. 실제 pipeline/AssetDatabase, 전송만 가짜 |
| 표시 전·후 예외 | e2e-visual-initialization-failure-cleanup.json 및 e2e-visual-failure-cleanup.json 각각 Passed=true |
| 합성 분석 수동 표시 | analysis-visual-final.json Passed=true,6 PNG. 신뢰 분류를 주입한 별도 fixture |
| 창·프레임·포커스 | 전후 창9개/native1개 정확한 ID 동일,focus0; task6-final-before-windows.json 및 task6-final3-windows.json. 각 수동 작업 소유 잔류0/추가·누락0 |
| 데이터 보존 | 보호1,386개 변경0, Assets/TftAnalyzerE2E_* 잔류0; task6-final3-protected.json |
| Console | compilationFailed=false, warnings0. cursor193→227 사이 오류2개는 Pipeline /api/commands transport/header 오류(seq226/227). 제품 예외로 분류하지 않았으며 전체 Console 오류0도 주장하지 않는다. task6-final-console-errors.json. 이후 cursor227→241 추가 오류0,compilationFailed=false; task6-final3-console.json |

root는 e2e-window-{940,500}-{import,collection,analysis}.png 및 analysis-window-{940,500}-{top,rows,pairs}.png 총12장을 직접 열었다. 대상 창·identity·가져오기 성공/수집2경기/표본 부족·정책 사유·하단 고지·작은 창 스크롤, 합성 n100/Wilson 구간을 확인했다. 일부 큰 캡처 좌측에는 겹쳐 보이는 문자 흔적이 있어 완전한 픽셀 무결성이나 원인 해결을 주장하지 않는다. 핵심 대상과 수치의 식별 여부는 PNG와 대조했다.

## 수정과 독립 검수

처음의 fixture 날짜 자동 변환/Windows 경로 단언 오류를 교정했다. 실제 E2E는 주입 아이콘 루트와 기본 validator가 충돌해 실패했고, validator/loader/import에 동일 루트를 전달해 해결했다. 전체 첫 실행614개 중 한 실패는 Temp 프로젝트를 사용한 누락 파일 테스트가 새 프로젝트 guard에서 먼저 차단된 것으로, 두 계약을 분리했다. 새 전체 실행 결과가 최종 증거다.

독립 코드 검수의 P2 세 건(캐시 복원 Changed 누락, CLI 구조화 응답 검증, 로컬 control/output 경로 링크 경계)을 수정하고 재검수에서 종결했다. 원문은 [검수 기록](../raw/Reviewer.md)에 있다. 별도 QA는 실제 새 창 재생성의 증거 누락을 지적했다. 제품 변경 없이 해당 회귀를 추가해 단일1/1과 최종616개 suite를 실행했다. 후속 QA 판정은 별도 QAReport에 기록한다.

## 모델·사용량·제약

- A: agy1.4.2, 요청/시작 구성 gemini-4-argon-medium/medium,21,402 tokens,57.353초. B: metadata gpt-6.1-sol/high,214,154 tokens(캐시 입력 포함). 실제 runtime과 한계는 협업 결과 참조.
- 구현 재개 요청은 GPT-6.1 Sol/High, 독립 코드 검수 spawn 설정은 GPT-6 Astra/High(데이터 보존·경로 실패 영향). 제공되지 않은 backend 식별·개별 사용량/가격은 추정하지 않는다. root의 정확한 모델·추론·이번 변경만의 사용량도 독립 확인 불가다.
- 초기 Python 임시 폴더 권한 실패 때 권한 확장 실행은 자동 승인 검토의 사용량 한도 오류로 실행 전 거절됐다. 이후 승인 검토가 복구되어 정확한 소유 디렉터리5개를 링크 거부·empty-only 삭제로 모두 정리했다. Antigravity outputs/offline-cleanup.json verified=true/잔류0. 현재 오프라인 테스트도 소유 workspace 폴더를 정상 정리한다. 초기 실패와 복구를 실행 이력으로 남겼다.
- 실제 Match API·외부 Riot 등록 승인·실데이터 증강 분류 공급·집계 입도 개선은 미검증 또는 별도 기획 범위다. Hermes 예약은 중단 유지, 호출0이다.

기록 시점 목표 누적 사용량은1,309,044 tokens/4,962초(get_goal)다. 현재 목표 전체의 누적값이며 이번 Antigravity 호출이나 이번 수정만의 비용으로 귀속하지 않는다. 개별 호출 값은 위 실제 runtime을 따른다.
