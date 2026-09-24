# Master Coordination Raw Log

이 파일은 숨겨진 내부 추론이 아니라 사용자와 Master AI 사이의 공개 지시, 에이전트 배정, 단계 판정만 기록한다. 비밀값과 개인정보는 기록하지 않는다.

## 사용자 워크플로우 지시

> 너는 Master AI로 모든 일의 총괄이야. 워크플로우는 기획, 개발, QA, 문서 순서다. 기획 에이전트 2명이 현재 기획을 크로스 체크하고 개발 TODO와 완료 조건을 작성한다. 승인된 기획을 개발에 전달한다. 개발 에이전트는 구현 에이전트와 읽기 전용 검수 에이전트를 만든다. 개발 후 QA는 TODO와 결과 및 예외를 확인하고 문제가 있으면 기획으로 되돌린다. 모든 기록은 정제 Markdown과 raw Markdown으로 나눈다. 체크포인트를 남겨 재개 가능하게 한다.

## Master 분류

- 작업 유형: 아키텍처 수준의 워크플로우 및 다음 개발 사이클 기획
- 현재 단계: 기획
- 구현 금지 경계: 기획 검토가 끝나기 전 제품 코드 변경 금지

## 에이전트 배정

### Planning Agent A

- canonical task: `/root/planning_product_review`
- model: `gpt-6-astra`
- reasoning effort: `high`
- 역할: 제품 범위, Riot/IP 정책 경계, 승인 단계, 로드맵 검토

### Planning Agent B

- canonical task: `/root/planning_technical_review`
- model: `gpt-6-sol`
- reasoning effort: `high`
- 역할: 기술 구조, TDD, 저장 identity, 마이그레이션, QA 완료 조건 검토

## Master 통합 판정

- 두 에이전트 모두 Phase 2 전에 source profile identity를 해결해야 한다고 판정했다.
- 경로뿐 아니라 manifest, request, index, UI selection 전체가 동일 identity를 사용해야 한다.
- Master 권고 identity는 `(setId, patchId, sourceGameVersion, sourceProfile)`이다.
- 기존 공식 전용 출력은 이동하거나 다시 쓰지 않는 legacy alias로 보존한다.
- 필드별 provenance, Match API, 통계, 전체 UI는 이번 사이클 범위에서 제외한다.

## 사용자 자율 진행 지시

> 외출할 일이 있어서 내 승인 없이 작업을 계속 진행해줘.

Master 판정:

- 이번 사이클의 단계별 사용자 승인 권한을 Master AI에 위임한 것으로 기록한다.
- 기획·개발 검수·QA 증거가 충족되면 다음 단계로 자동 진행한다.
- 외부 배포, 데이터 삭제, Match API, 비밀값 취급은 권한 확대에 포함하지 않는다.
- 되돌릴 수 없는 작업이나 보안 민감 작업이 필요하면 자동 진행을 중단한다.

## 사용자 복귀 후 승인 정책 변경

> 외출 다녀왔어. 이제 Master AI는 큰 작업에 대해서는 승인 요청을 부탁해. 토큰이 없어서 멈췄던 작업을 이어서 해줘.

Master 판정:

- 이미 승인되어 진행 중이던 `source-profile-identity` 사이클은 중단 지점부터 계속한다.
- 이 사이클 밖의 새로운 큰 범위(예: Match API, 분석 UI, 외부 배포, 자동 업데이트)는 시작 전에 사용자 승인을 요청한다.
- 기존 raw 기록의 자동 승인 문구는 당시 지시의 이력으로 보존하고, 현재 정책은 이 항목과 `Checkpoint.md`를 기준으로 한다.
