# 독립 읽기 전용 검수 응답

날짜: 2026-10-16
요청: [ReviewRequest](ReviewRequest.md)

첫 검수는 `gpt-6-astra` High를 요청했으나 사용량 한도 오류로 실행되지 않았다. [모델 지침](../../../../../../docs/agent-model-policy.md)에 따라 `gpt-6.1-sol` High를 대체 요청했다. 실제 런타임 식별·토큰·시간은 독립 확인하지 못한다.

## 공개 응답 요약

실제 구현에서 개인정보 유출·상한 위반·확정 데이터 손실 결함은 발견하지 못했다.

- Critical 없음.
- Important: 저장 장애 후 서비스 중단·복구 통합 검증 누락. 인덱스 경로를 디렉터리로 막고 M1/M2 목록으로 첫 실패 후 M2 미요청·확정 관측 보존·장애 해제 후 중복 없는 재개를 검증할 것. 구현 결함이 아닌 중요한 회귀 검증 공백이다.
- Minor: 전역 페이지 예산 테스트가 한 seed만 사용한다. 여러 seed의 짧은 페이지를 합쳐 최대10회인지 추가 검증을 권한다. 현재 구현은 state.IdPages를 공유하므로 올바르게 보인다.
- 확인 경계: full CatalogIdentity·queue·응답 match hash, 참가자 탐색 없음, 취소 시 스트림 해제, 수치 전용 결과, 기본 가짜 transport와 Explicit 실제 호출 분리.

## 구현자 처리

- Important: `IndexFailureStopsFurtherDownloadsAndResumeRecoversCommittedObservation`을 추가했다. 제품 코드는 변경하지 않았다.
- Minor: `ShortPagesAcrossSeedsShareOneGlobalScanBudget`을 추가했다. 제품 코드는 변경하지 않았다.
- 진행률 소비자 예외 격리를 확인하는 테스트도 추가했다. 실제 실행 결과는 후속 QA 기록으로 판정한다.

## 판단 유보와 적용 범위

검수자는 Unity·네트워크·디렉터리 이동을 실행하지 않았다. 실제 테스트 성공, Pipeline API 로그의 최종 원인, 실제 Riot 응답·키·seed·사용자 저장소, 폴더 변경 후 실행은 구현자가 검증한다. 기존 실행 기록을 이번 실행 결과로 대체하지 않는다. 실제 서버 호출은 이번 범위에서 실행하지 않는다. Unity 검증과 폴더 이동은 이번 QA의 완료 조건으로 유지한다.
