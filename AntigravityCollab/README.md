# Antigravity 협업 및 기획 검수 폴더

갱신일: 2026-10-19

이 폴더는 **Antigravity가 수행한 프로젝트 기획 검수안 및 관련 산출물**을 보관하고, 개발 워크플로의 기획 검수 지침과 연결하기 위한 공간입니다.

2026-10-19 사용자 확정으로 **기획 A는 Antigravity**, **기획 B는 GPT**가 담당합니다. 기획 A는 제품 범위·정책·사용자 가치·위험을, 기획 B는 기술 구조·예외/복구·테스트·완료 조건을 독립 검토하고 Codex가 결과를 통합합니다. 구체적인 모델·추론은 실행마다 확인해 기록합니다. 이전 사이클의 역할별 실행 기록은 당시 사실대로 보존합니다.

## 보관 문서

| 문서 | 설명 |
|---|---|
| [2026-10-19 프로젝트 전체 구현 계획 검수안](2026-10-19-project-plan-review.md) | 전체 설계(`TFTDataPipeline.md`)와 Phase 1~3 구현 계획에 대한 게임 기획자 관점의 검수 결과 및 4대 보완 지침 |
| [기획 A 자동 호출 제안](2026-10-19-automatic-planning-review-proposal.md) | 최초 제안과 현재 적용 범위. 기획 문서 한정 단발 실행 성공, 강제 도구 제한은 미검증 |
| [초기 자동 호출 준비 점검](tasks/2026-10-19-auto-call-check/result.md) | CLI 설치 전 당시의 준비 점검 이력 |
| [agy 실제 자동 호출 시험](tasks/2026-10-19-agy-live-probe/result.md) | agy1.4.2에서 Gemini 4 Argon Medium의 상수 응답 SUCCESS·정상 종료. 도구 제한은 별도 미해결 |
| [현재 Task6 기획 A/B 실제 적용](tasks/2026-10-19-task6-planning/result.md) | 동일 입력의 실제 Antigravity A·GPT B 독립 검수와 통합. 기획 문서 한정 실행이며 강제 접근 제한은 미검증 |

## 연결 상태

- 현재 확인: 프로젝트 검수 문서와 역할 배정이 반영됐고 데스크톱 앱·사용자 agy 세션이 실행 중입니다.
- 공식 경로 `C:/Users/gulww/AppData/Local/agy/bin/agy.exe`의 CLI1.4.2를 절대 경로로 호출했습니다. 모델 목록 조회와 상수 요청 한 건의 응답·정상 종료를 실제 확인했습니다. 현재 Codex의 PATH에서는 명령을 찾지 못하므로 절대 경로를 사용합니다.
- 상수 시험 이후 [현재 Task6의 실제 기획 검수](tasks/2026-10-19-task6-planning/result.md)도 수행했습니다. 비밀값 없는 기획4개를 제공해 Antigravity A의 SUCCESS 응답을 받았고, 같은 입력의 GPT B 결과와 통합했습니다. 두 결과는 제품 검증·사용자 최종 승인을 대체하지 않습니다.
- SDK 경로는 별도 API 키가 필요해 전송 전 중단했고, CLI hook 차단도 증명하지 못했습니다. 이번에는 사용자 요청에 따라 제공한 기획 문서만 검토하는 plan 모드 한정 실행으로 진행했습니다. 도구 시도0건을 접근 차단 증거로 바꾸지 않습니다.
- [CLI 진입점](../AgentScripts/Invoke-AntigravityPlanningReview.py)은 `--scoped-text-review`를 명시해야 실행하며 manifest/입력 해시·48KiB 상한·기존 결과 덮어쓰기 방지·모델 일치·구조화 응답·소유 프로세스 정리를 확인합니다. `--dry-run`은 요청을 보내지 않습니다. 일반적인 강제 격리 검수·예약은 활성화하지 않았습니다. Hermes 정기 모니터도 계속 중단 상태입니다.

## 운영 및 반영 원칙

- 이 폴더의 기획 검수안은 [개발 워크플로](../Assets/Plan/Workflow/README.md)의 기획 단계(`§3.1 기획`)와 [Phase 3 구현 계획](../Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md)의 기획 검수 지침에 반영합니다.
- 제품 범위(전투 시뮬레이션·미래 승패 예측 제외), 기존 카탈로그 불변 보존, 비밀값·개인 식별 정보 비저장 원칙은 루트 [AGENTS.md](../AGENTS.md)를 그대로 따릅니다.
- Hermes와의 파일 기반 요청·응답 전달은 기존대로 [HermesCollab](../HermesCollab/README.md)을 사용합니다.
