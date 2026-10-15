# Riot API 클라이언트 체크포인트

갱신일: 2026-10-16
상태: Task 2 구현·실제 Unity 테스트·독립 코드 검수 후 수정을 Git 체크포인트로 기록한다. 후속 PR 검수·병합 결과는 GitHub 기록을 따른다. 최종 사용자 완료 승인은 별도다.
후속 상태: PR #3은 CodeRabbit 검수 통과 후 main `e176b57`로 병합됐다. 현재 구현 위치는 [Task 3 체크포인트](../2026-10-16-match-anonymizer/Checkpoint.md)다. 아래 내용은 Task 2 당시 기록이다.
승인 근거: 사용자의 “다음 구현 부탁할게”에 따라 승인된 Phase 2의 다음 구현 단위인 Task 2를 진행했다.

- 단일 실행 계획: [Phase 2 Task 2](../../../Implementation/2026-09-17-tft-match-analytics.md#task-2-implement-api-key-safety-routing-and-rate-limit-handling). 별도 TODO를 만들지 않는다.
- 증거: [개발·검증 보고서](refined/DevelopmentReport.md), [독립 검수 응답](raw/Reviewer.md).
- 기준 main `4141492`(PR #2 병합), 작업 브랜치 `codex/riot-api-client`.
- 구현: 환경 변수 키 제공자, TFT 지역·경로 제한, 메모리 응답 소유권, 호출 제한·429 대기 시각, 500/502/503/504 제한 재시도, 취소 시 제한 헤더 보존.
- 실제 Unity EditMode 전체 174/174 통과. 이 중 API 클라이언트는 60건이다. 실제 Riot 호출·키/seed 조회·경기 원본 저장은 실행하지 않았다.
- 보류한 경미한 사항: 키 조회가 실패하면 전송 전에 예약한 알려진 호출량이 창 만료까지 남을 수 있다. 서버 한도 초과나 키 노출은 발생하지 않으며, 이후 호출을 보수적으로 막는 방향이다.
- 다음 구현은 Task 3의 메모리 내 익명화·전체 로비 검증·카탈로그 대응 검증이다. 저장·집계·수집 UI는 아직 연결하지 않는다.
- 보호 대상: 사용자 `Assets/DefaultVolumeProfile.asset` 변경과 기존 생성 카탈로그·아이콘. 사용자 변경은 커밋에서 제외한다.

재개 시 실제 Git/PR 상태와 계획 체크 상태를 확인한다. Task 1·2를 반복 구현하거나 실제 키·원본 응답을 로그·파일·협업 폴더로 복사하지 않는다.
