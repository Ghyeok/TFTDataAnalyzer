# Cycle Checkpoint

사이클: `2026-09-23-source-profile-identity`
갱신일: 2026-10-16
현재 상태: 구현 커밋·코드 검수·오프라인 Unity QA 통과, 최종 사용자 완료 승인 대기

## 현재 기준

- 승인 범위와 결정: [PlanningReview](refined/PlanningReview.md).
- 실행 순서와 D1–D6 완료·검수·QA 조건: [ImplementationPlan](refined/ImplementationPlan.md). 별도 TODO 상태는 관리하지 않는다.
- 당시 구현·검수·실행 근거: [2026-09-24 개발 보고서](refined/DevelopmentReport.md)와 `raw/`. 과거 모델 배정·프롬프트·환경 오류를 현재 작업 지침으로 적용하지 않는다.
- 새 검증: [2026-10-16 QA 보고서](refined/QAReport.md). 사용자의 다음 구현 지시에 따라 [Phase 2 Task 1](../2026-10-16-anonymous-observation-contracts/Checkpoint.md)을 진행했다. 외부 배포·기존 데이터 삭제는 범위 밖이다.

## Git과 보호 상태 — 2026-10-16 확인

- 구현 커밋: `3715893 feat: add composite static catalog identity` (2026-09-24).
- 확인 시 브랜치: `codex/coderabbit-review-workflow`.
- 확인 시 HEAD: `ec3f043 docs: document CodeRabbit review workflow`. 이후 Git 작업 시 다시 확인한다.
- 기존 사용자 변경: `Assets/DefaultVolumeProfile.asset`. 문서 정리의 수정·stage 대상에서 제외한다.
- `AGENTS.md`, `docs/`, `HermesCollab/`와 Workflow의 Unity 메타 파일은 미추적 상태다. 저장소 반영은 별도 Git 작업이다.
- 기존 `TFTSet18/18.2` catalog/manifest 및 두 메타 파일, 사용자 volume profile의 SHA-256을 다시 확인했고 개발 보고서의 보호 해시와 모두 일치했다.
- 기존 제품 코드는 유지했고 후속 관측 모델만 새로 추가했다. 실제 Unity 검사에서 별도 임시 asset과 검증 창을 사용한 뒤 정리했다. 기존 생성 데이터는 수정하지 않았다.

## 완료한 작업

- 복합 identity, 프로필·원본 버전별 경로와 인덱스, legacy 호환, 발행 무결성, Import UI 구현.
- 구현 담당 self-review와 독립 읽기 전용 리뷰어의 수정 검수 승인. 당시 검수 지적은 해결된 것으로 기록됨.
- 구현 커밋 `3715893` 반영. 이전 체크포인트의 “개발 커밋 없음”과 staging 권한 오류는 과거 이력이다.
- 2026-10-16 문서 중복 정리: 실행 계획·TODO 통합, 모델 선택 기준 일원화, 현재 상태와 과거 기록 구분.
- 이후 같은 날 실제 Unity 컴파일·기본 EditMode 82/82, 기존 legacy/index 로드·네 identity 인덱스 저장, 실제 Import 기본화면 확인과 독립 오프라인 QA를 완료했다. 후속 관측 모델 포함 전체는 114/114 통과했다.

## 남은 검증과 단계

- [x] 실제 Unity 재컴파일와 기본 EditMode 전체 실행.
- [x] 기존 index/legacy 로드와 별도 임시 인덱스의 실제 AssetDatabase 직렬화 확인. 발행/복구는 오프라인 회귀로 확인.
- [x] 실제 Import 기본화면 관찰, identity·동의·취소·오류의 presentation 회귀 확인.
- [x] D1–D6 대비 독립 오프라인 QA 증거 검토와 다음 로컬 구현 진행 판정.
- [ ] QA 결과를 반영한 최종 문서화와 사용자 완료 승인.

2026-09-24 보완 결과는 당시 이력이다. 앞선 문서 정리 후 실제 검증을 수행했으며 현재 Pipeline은 연결 가능하다. 실제 네트워크 import·네이티브 모달 클릭·production index writer 전체 발행의 네이티브 실행은 미실행이다. 이 제한을 포함한 QA 판정 근거는 위 보고서에 기록했다.

## 다음 작업

현재 진행 위치는 [익명 관측 모델 체크포인트](../2026-10-16-anonymous-observation-contracts/Checkpoint.md)다. 이 사이클의 구현·코드 검수를 다시 수행하지 않는다. 새 변경 검증에는 [표준 명령](../../../Implementation/TFTDataAnalyzerImplementationPlan.md#standard-verification-commands)을 사용하고 과거 결과를 재실행 결과처럼 기록하지 않는다.

## 확정된 핵심 결정

`CatalogIdentity = (setId, patchId, sourceGameVersion, sourceProfile)`.

기존 `{setId}/{patchId}` 공식 전용 출력은 이동·수정하지 않고 검증된 legacy alias로 보존한다. CommunityDragon은 매 실행 명시적으로 선택한다.
