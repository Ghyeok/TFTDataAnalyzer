# 제한된 수집·명칭 변경 QA

날짜: 2026-10-16
검증 주체: Codex 구현·통합 담당. 독립 검수는 코드 검토이며 Unity 실행을 대신하지 않는다.

## 실제 실행

| 완료 조건 | 증거 |
|---|---|
| 선행 PR | PR #7 최신57bb21d 검수 SUCCESS·미해결 thread0·사전 검사5개, 실제340/340 후 main eb8f5d7 병합 |
| RED | 서비스 선작성 테스트의 CS0246 신규 타입 부재 |
| 컴파일 | recompile_status completed, failed=false, errors=[], compilationFailed=false |
| 집중 | MatchCollectionServiceTests26/26 |
| 최종 전체 | TFTAnalyzer.Tests.EditMode366 passed /0 failed /0 skipped |
| 수집 상한 | 고유 시도 상한, 반복 full페이지10회 종료, seed11개의 짧은 페이지도 합계10회 |
| 범위·익명화 | full CatalogIdentity 요청 불일치, source release/queue 불일치 제외, unknown ID 격리, 잘못된 JSON·응답 match hash 거부 |
| 중복·탐색 | 두 seed 중복 ID 한 번 다운로드, 참가자 PUUID를 seed로 사용하지 않음, 재개 시 저장소 중복 제거 |
| 취소 | 사전 취소 seed읽기/전송0, 활성 응답 해제, 첫 저장 후 취소와 재개 |
| 장애 | 401/403/429 종료·UTC 재시도시각, 키 부재 전송0, 인덱스 발행 실패 후 다음 경기 미요청·확정 관측 보존·재개 복구 |
| 출력 | 진행률/결과/실제 store 파일에 가짜 seed·키·PUUID·이름·원본 match ID·build note 미포함 |
| 검수 지적 | Important1 테스트 공백·Minor1 seed간 예산 검증을 각각 테스트로 보완, 실행26/26로 확인 |
| 표시 이름 | productName·WSA packageName/applicationDescription을 Unity API로 갱신. 이름 회귀 테스트 통과 |

첫 집중22/23은 외부 Pipeline `/api/commands` 응답 쓰기 중 연결 종료 로그가 실패로 잡힌 기록이다. 재시도에서 패키지나 LogAssert를 바꾸지 않았다. 페이지 검증의 불필요한101개 디스크 기록을2개로 줄였으며 최종 집중·전체에서 해당 오류는 재현되지 않았다. 최초 이름 변경 전체365/366의 기존 이름 기대값도 새 요구사항에 맞게 갱신해 최종366/366을 확인했다.

명령은 `unity command recompile`, `recompile_status`, `run_tests --mode editor --filter_type testName --filter MatchCollectionServiceTests`, 전체는 `--filter_type assembly --filter TFTAnalyzer.Tests.EditMode`다. 실행 시점의 프로젝트 경로를 사용하며 최종 원본 JSON은 Git 제외 `Library/TftAnalyzerValidation/task7-focused-final.json`, `task7-full-final.json`이다. 과거 문서의 경로를 현재 명칭으로 정리했으며 원본 JSON 경로까지 변경·재실행했다고 주장하지 않는다.

## 보존·이동

- 사용자 Volume의 SHA-256은 `4789B43834DD5EFB32EEC4C3B436BAF0895C9F8CF2A0624121DA8DD812EE984A`로 유지했다. 이 사용자 변경은 커밋에 넣지 않는다.
- 생성 카탈로그·아이콘은 변경하지 않았다. source Markdown에서 이전 명칭0건을 확인한다. 과거 기록은 경로 표기만 정규화했다.
- 물리 폴더 이동은 현재 Codex의 디렉터리 잠금으로 차단됐다. 에디터 종료 검사에서 같은 파일명인 CLI를 에디터로 잘못 센 부분을 실제 실행 경로 검사로 바로잡았다. 해당 프로젝트의 MCP 보조 프로세스를 종료해도 Codex 자체 잠금이 남았다. Codex 강제 종료는 하지 않는다.
- 일회성 이동 도구는 PowerShell 구문 검사와 DryRun만 통과했다. Codex 종료를 기다린 뒤 이동·보호 파일 해시·새 경로 전체 검증·Unity 열기를 수행할 준비이며, 실제 이동과 새 경로 테스트는 미실행이다. 결과는 `Library/TftAnalyzerValidation/project-rename-result.json` 및 `task7-post-rename-full.json`으로 확인한다.

실제 API·키/seed·라이브 경기·현행 외부 정책·UI·CSV·대규모 성능·새 경로 실행을 이번 완료 증거에 포함하지 않는다. 제품 코드 검증 통과와 물리 폴더 이동 대기를 구분한다.
