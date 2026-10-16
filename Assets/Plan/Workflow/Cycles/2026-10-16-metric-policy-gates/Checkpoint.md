# 최소 표본·정책 필터 체크포인트

갱신일: 2026-10-16
후속 상태: PR #7은 최신 `57bb21d` CodeRabbit SUCCESS·미해결 thread 0·사전 검사5개와 재개 시 실제 Unity340/340 확인 후 main `eb8f5d7`로 병합됐다. 현재 작업은 [Task 7](../2026-10-16-bounded-match-collection/Checkpoint.md)이다. 아래는 Task 6 당시 기록이다.
상태: Task 6 구현·실제 Unity 검증·독립 검수 통과. PR 검수·병합과 최종 사용자 완료 승인은 별도다.
승인 근거: 최소 표본100건·정책 필터를 다음 작업으로 안내한 뒤 사용자가 “진행해줘”라고 요청했다.

- 단일 실행 계획: [Phase 2 Task 6](../../../Implementation/2026-09-17-tft-match-analytics.md#task-6-enforce-metric-policy-and-minimum-samples).
- 기준 main `afcaebb`(PR #6 병합), 브랜치 `codex/metric-policy-gates`.
- 구현: 최소100건 및 상향 설정, 참가자/상대배치 공통 정책, Legend 기반 비율·Legend 값 차단, 분류 미확인 시 보수적 비율 차단, 카탈로그·필터 검증, 숨긴 수치가 없는 표시 DTO와 전용 JSON 직렬화.
- 정책 판단에 필요한 증강 ID를 집계에 연결하고 새 스키마2 캐시에 보존한다. 기존 스키마1·관측·정적 카탈로그는 수정하지 않는다. 실데이터 분류 레지스트리는 생성하지 않았다.
- 실제 Unity 컴파일 오류 없음. 정책 집중31/31, 전체 EditMode340/340 통과(기존307+정책31+출처/스키마2건).
- 독립 검수 Critical 0 / Important 0 / Minor 0. 증거: [개발 보고서](refined/DevelopmentReport.md), [QA 기록](refined/QAReport.md), [검수 요청](raw/ReviewRequest.md), [검수 응답](raw/Reviewer.md).
- 보호 대상: 사용자 volume 변경과 기존 생성 카탈로그·아이콘. 커밋에서 사용자 변경을 제외한다.
- 다음 구현은 Task 7 수집 서비스다. 이번에는 UI·실제 파일 내보내기·실제 API/키/seed 조회·현행 외부 정책 확인을 실행하지 않았다.
- PR 생성/수정 push 후 링크·검증·다음 행동을 알리고 종료한다. CodeRabbit 반복 조회·대기·자동 재개는 하지 않는다.

재개 시 최신 PR 커밋의 검수·미해결 항목·실행 증거부터 확인한다. raw 집계를 직접 표시하지 않는다. 증강이 있는 조합은 선택한 카탈로그에 맞는 검토된 분류 메타데이터를 연결하기 전까지 비율이 차단된다.
