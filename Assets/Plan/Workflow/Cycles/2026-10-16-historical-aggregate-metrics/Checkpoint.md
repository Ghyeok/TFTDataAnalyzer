# 과거 경기 집계·신뢰구간 체크포인트

갱신일: 2026-10-16
상태: Task 5 구현·실제 Unity 검증·독립 검수 통과. PR 검수·병합과 최종 사용자 완료 승인은 별도다.
승인 근거: 다음 작업을 과거 집계·Wilson 신뢰구간으로 안내한 후 사용자가 “부탁해”라고 요청했다.

- 단일 실행 계획: [Phase 2 Task 5](../../../Implementation/2026-09-17-tft-match-analytics.md#task-5-calculate-aggregate-and-pairwise-placement-metrics).
- 기준 main `af69eaf`(PR #5 병합), 브랜치 `codex/historical-aggregate-metrics`.
- 구현: 참가자별 정확한 순위 합계·분자/분모, 같은 로비의 조합 쌍 비교와 모호한 로비 제외 건수, Wilson 신뢰구간, 전체 scope 분리와 결정적 정렬, 중복 경기/충돌 검증, scope별 원자 집계 저장.
- 실제 Unity 컴파일 오류 없음. Wilson 집중 16/16, 집계·저장 집중 30/30, 전체 EditMode307/307 통과. 새 테스트46건 포함.
- 독립 읽기 전용 검수 Critical 0 / Important 0 / Minor 0. 증거: [개발 보고서](refined/DevelopmentReport.md), [QA 기록](refined/QAReport.md), [검수 요청](raw/ReviewRequest.md), [검수 응답](raw/Reviewer.md).
- 보호 대상: 사용자 `Assets/DefaultVolumeProfile.asset` 변경과 기존 생성 카탈로그·아이콘. 사용자 변경은 커밋에서 제외한다.
- 다음 구현은 Task 6의 최소 표본100건·정책 필터다. 이번에는 정책·수집 서비스·UI·내보내기·실제 API/키/seed 조회를 실행하지 않았다.
- PR 생성/수정 push 후 링크·검증·다음 행동을 알리고 종료한다. CodeRabbit 반복 조회·대기·자동 재개는 하지 않는다.

재개 시 실제 Git/PR 상태와 최신 커밋의 검수를 먼저 확인한다. raw 집계의 비율을 정책 필터 없이 사용자 화면이나 내보내기에 연결하지 않는다. 기간 메타데이터는 실제 수집 시각이 아닌 경기 시각이다.
