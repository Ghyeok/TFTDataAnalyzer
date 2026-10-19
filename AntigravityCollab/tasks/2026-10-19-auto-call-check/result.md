# 자동 호출 확인 결과

2026-10-19. 판정: **준비 조건 미충족 — Antigravity 실제 호출 미실행**.

| 확인 항목 | 실제 확인 결과 |
|---|---|
| 데스크톱 앱 | `C:/Users/gulww/AppData/Local/Programs/antigravity/Antigravity.exe` 파일과 실행 중 프로세스 확인 |
| 현재 PATH 명령 | `agy`, `antigravity`, `antigravity-cli` 모두 발견하지 못함 |
| 공식 Windows CLI 기본 경로 | `C:/Users/gulww/AppData/Local/agy/bin/agy.exe` 없음 |
| 추가 실행 파일 위치 | 사용자 `.local/bin`, npm, WinGet 링크의 해당 이름 파일을 발견하지 못함 |
| 앱의 포함 실행 파일 | `resources/bin`에 `language_server.exe`, `webm_encoder.exe`가 있음. 이를 공식 기획 검수 CLI로 취급하거나 실행하지 않음 |
| 프로젝트 호출 runner | AgentScripts와 AntigravityCollab 파일 목록에서 구현된 호출 runner 없음; 제안 문서만 있음 |
| Codex 등록 MCP | 목록 조회 성공. `cua_repl` 비활성, `node_repl`·Unity 관련 연결 활성. Antigravity 연결 없음 |
| 현재 세션 Antigravity 호출 도구 | 노출된 도구 목록에서 발견하지 못함 |
| 실제 요청/응답/모델/사용량 | Antigravity 요청0건. 응답·실제 모델·실행 토큰은 확인 대상 없음. Codex 점검 자체의 사용량과 구분 |

다른 임의 설치 위치에 CLI가 전혀 없다고 단정하지 않는다. 다만 현재 확인한 경로와 등록 연결로는 자동 호출을 실행할 수 없다. 데스크톱 앱이 켜져 있거나 기획 검수안이 저장돼 있다는 사실은 자동 호출 성공 증거가 아니다.

MCP 목록 조회 시 Codex 임시 PATH alias 생성/정리의 접근 거부 경고가 있었으나 목록은 반환됐다. 이 경고를 Antigravity 서버 오류나 로그인 실패로 바꾸어 기록하지 않았다. 사용자 앱·Unity·자격 증명을 변경하거나 종료하지 않았다.

다음 순서: [자동 호출 제안](../../2026-10-19-automatic-planning-review-proposal.md)에 따라 CLI 경로 확인 또는 설치 → 최초 로그인·모델 확인 → 제한된 단발 시험 → runner 연결·결과 검증. 설치·로그인 상태·도구 제한·실제 검수 성공은 아직 미검증이다. Hermes의 정기 확인은 중단 상태를 유지한다.
