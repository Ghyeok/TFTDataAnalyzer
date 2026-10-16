# 익명 관측 저장 체크포인트

갱신일: 2026-10-16
상태: Task 4 구현·실제 Unity 검증·독립 검수 통과. PR 검수·병합과 최종 사용자 완료 승인은 후속 단계다.
승인 근거: 익명 관측 저장·중복 제거·인덱스 복구를 다음 작업으로 안내한 후 사용자가 “진행해줘”라고 요청했다.

- 단일 실행 계획: [Phase 2 Task 4](../../../Implementation/2026-09-17-tft-match-analytics.md#task-4-persist-anonymous-observations-and-deduplication-index).
- 기준 main `678b05a`(PR #4 병합), 작업 브랜치 `codex/anonymous-observation-store`.
- 구현: 허용 필드 NDJSON, 전체 scope 해시 경로, 저장소 전체 경기 해시 중복 제거, 관측 기준 인덱스 재구성, 원자적 인덱스 교체, flush 전 취소 복구, 미완성 끝부분 복구와 완성된 손상 기록의 오류 처리.
- 실제 Unity 집중 32/32, 전체 EditMode 261/261 통과. 새 저장소 테스트 35개를 포함한다. 테스트의 동기식 NUnit 예외 검증이 Unity를 막은 문제를 비동기 검증으로 수정했고, 승인받은 정확한 Editor PID만 종료한 뒤 테스트 모드에서 재검증했다.
- 독립 검수 Critical 0 / Important 0 / Minor 0. 검수자가 Unity 실행·실제 산출물 검증을 대신한 것은 아니다.
- 증거: [개발 보고서](refined/DevelopmentReport.md), [QA 기록](refined/QAReport.md), [검수 요청](raw/ReviewRequest.md), [검수 응답](raw/Reviewer.md).
- 보호 대상: 사용자 `Assets/DefaultVolumeProfile.asset` 변경과 기존 생성 카탈로그·아이콘. 사용자 변경은 커밋에서 제외한다.
- 다음 구현은 Task 5의 과거 집계·Wilson 신뢰구간이다. 이번에는 집계·수집·UI·실제 API/키/seed 조회를 실행하지 않았다.
- PR 생성 또는 수정 push 후 링크·검증 결과·다음 행동을 알리고 종료한다. CodeRabbit 반복 조회·대기·자동 재개는 하지 않는다.

재개 시 실제 Git/PR 상태와 최신 커밋의 검수 결과부터 확인한다. 저장소에 전달할 관측은 익명화기가 승인한 값으로 제한한다. 저장 API가 임의 생성 도메인 값의 카탈로그 소속을 다시 검증하는 것은 아니다.
