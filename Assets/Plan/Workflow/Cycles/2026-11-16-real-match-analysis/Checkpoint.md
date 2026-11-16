# 실제 경기 분석 지원 체크포인트

2026-11-16 승인(현재 위치): 사용자가 1단계 구현 계획 3판을 승인했다. 단계를 개발로 전환한다. 실제 실행(TODO 9)은 사용자의 키 재설정 알림 후에만 한다. 먼저 기획 기록과 기획 B 역할 변경 지침을 문서 PR로 반영하고, 최신 main에서 짧은 경로의 격리 작업트리를 만들어 TODO 1~8을 TDD로 구현한다.

2026-11-16 재검토 완료·승인 대기: 사용자 결정으로 실제 실행은 N=20, 사용자가 만료된 API 키를 재설정했다고 알린 뒤에만 한다. 수정안 2판을 A·B에 재검토받아 A pass, B revise(P1 1·P2 4·P3 1)였다. 반영한 3판을 B가 pass(P3 3건)로 확인했고 P3도 반영했다. A의 3판 재확인은 하지 않았다. 승인 대상은 [ImplementationPlan.md](refined/ImplementationPlan.md) 3판이다. 제품 코드 변경 없음, 실제 API 호출 없음. 재개 시 첫 행동: 사용자 승인 여부를 확인하고, 승인되면 격리 브랜치에서 TODO 1~8을 TDD로 구현한다(TODO 9는 키 재설정 알림 후).

2026-11-16 기획 통합: 기획 A(Antigravity, `gemini-4-argon-medium`, SUCCESS 95초)와 기획 B(Claude 서브에이전트 `opus`·high, 313초)가 모두 revise를 반환했다. 원문은 [raw/PlanningAgentA.md](raw/PlanningAgentA.md), [raw/PlanningAgentB.md](raw/PlanningAgentB.md). 통합 결과 [PlanningReview.md](refined/PlanningReview.md)와 1단계 수정안 [ImplementationPlan.md](refined/ImplementationPlan.md)를 작성했다. 방향(형태 진단 우선)은 유지하고, 1단계를 수집 경로와 분리된 진단 클래스·전송 예산 래퍼·비식별 서명·집계 보고서로 다시 설계했다. 사용자 결정 대기: 실제 1회 실행 허용과 경기 수 N(권장 20), 수정안의 A·B 재검토 여부. 2단계 방향(C/D)·증강 지표 가치는 진단 후 확정 가능. 제품 코드 변경 없음. Antigravity CLI는 실제 응답 생성까지 확인했고, `agy\bin`을 `.claude/settings.local.json`의 `additionalDirectories`에 추가했다(개인 설정, 커밋 안 됨). 재개 시 첫 행동: 사용자 결정을 계획에 반영하고, 재검토를 택하면 A는 새 동결 입력으로, B는 같은 서브에이전트에 수정안을 보내 확인받는다.

2026-11-16 기획 시작: 사용자가 KI-003 후속인 실제 경기 저장·분석 지원 진행을 지시했다. 같은 날 기획 B 담당을 GPT(Codex)에서 Claude로 바꾸도록 지시해 `AGENTS.md`, 워크플로 README, Antigravity 협업 README, 지침 색인을 수정했다(미커밋).

- 단계: 기획. 기획 A/B 독립 검토 진행 중. 제품 코드 변경 없음.
- 승인된 범위: 기획 초안 작성과 A/B 검토까지. 1단계 진단 구현, 실제 Riot API 호출, 2단계 설계·구현은 미승인이다.
- 기준: main `ba49c69`. 작업 트리에는 `Assets/DefaultVolumeProfile.asset`(사용자 변경, 건드리지 않음)과 이번 기획·지침 문서 변경이 있다.
- 기획 초안: [PlanningDraft.md](refined/PlanningDraft.md). 권장안은 형태 진단(1단계) 후 결과에 따라 2단계(정상 경로 확장 또는 별도 참고 분석)를 따로 기획하는 것이다.
- 기획 A: Antigravity CLI 1.5.9, 모델 목록 조회 정상, 요청 모델 `gemini-4-argon-medium`. 동결 입력 `AntigravityCollab/tasks/2026-11-16-real-match-planning/` 5개(inputDigest `7b43ab44…`, 20,044바이트), dry-run 통과 후 실제 요청 전송.
- 기획 B: Master와 분리된 Claude 서브에이전트(Opus, effort high), 같은 동결 입력과 기준 커밋 코드 읽기 전용, A 결과 열람 금지.
- 변경하면 안 되는 것: 기존 관측·불완전 기록·카탈로그·인덱스, `Assets/DefaultVolumeProfile.asset`, API 키.
- 재개 시 첫 행동: A의 `outputs/review.json`·`runtime.json`과 B의 결과를 확인해 `raw/PlanningAgentA.md`, `raw/PlanningAgentB.md`에 기록하고 통합 검토를 작성한다.
