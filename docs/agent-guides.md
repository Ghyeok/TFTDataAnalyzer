# 작업 지침·관련 스킬 색인

갱신일: 2026-10-18

프로젝트 지침의 기본 진입점은 루트 [AGENTS.md](../AGENTS.md)다. 이 색인은 작업에 필요한 상세 문서와 관련 스킬을 찾는 용도다. 실제 승인 범위와 실행 권한은 사용자 지시 및 현재 작업 상태를 따른다.

## 프로젝트 지침

| 필요한 내용 | 읽을 문서 |
|---|---|
| 제품 범위·사용법·현황 | [README](../README.md) |
| 개발 단계·독립 검수·QA·기록 | [개발 워크플로](../Assets/Plan/Workflow/README.md) |
| 단계별 구현 계획 찾기 | [구현 계획 개요](../Assets/Plan/Implementation/TFTDataAnalyzerImplementationPlan.md) |
| Unity EditorWindow 테스트·캡처·창 정리 | [검증 절차와 실패 사례](unity-editor-validation.md) |
| 모델·추론 선택의 상세 기준 | [모델 선택 지침](agent-model-policy.md) |
| Hermes 요청·자료·결과 전달 | [Hermes 협업 규칙](../HermesCollab/README.md) |
| CodeRabbit 5분 점검·알림 범위 | [검수 모니터 지침](coderabbit-monitoring.md) |

현재 진행 위치는 해당 작업 사이클의 `Checkpoint.md`, 당시 증거는 날짜가 있는 보고서와 `raw/`에서 확인한다. 현재 코드와 더 새로운 실행 결과가 체크포인트보다 앞서면 그 차이를 기록하고 갱신한다.

## 관련 스킬

스킬은 각 폴더의 `SKILL.md`에 정의된다. 실제 사용 가능 여부·경로·전체 목록은 현재 세션에서 제공되는 스킬 카탈로그로 확인한다. 아래는 이 프로젝트와 관련된 항목의 안내이며, 전역 스킬의 설치 상태를 보장하는 목록은 아니다.

| 작업 | 관련 스킬 |
|---|---|
| Unity CLI·연결된 Editor 제어 | `unity-cli` |
| Unity MCP 사용 | `unity-mcp-orchestrator` |
| UXML·USS·UI Toolkit 작업 | `ui-uitk` |
| 실패·간헐적 오류의 원인 조사 | `superpowers:systematic-debugging` |
| 완료 주장 전 실행 증거 확인 | `superpowers:verification-before-completion` |
| 재사용 스킬 작성·수정 | `skill-creator`, `superpowers:writing-skills` |

이번 EditorWindow 교훈은 프로젝트 검증 도구와 테스트 계약에 맞춘 **프로젝트 지침**으로 등록했다. 전역 스킬을 새로 설치하거나 기존 스킬을 수정하지 않았다. 새 교훈은 관련 상세 문서에 반영하고 이 색인에서 연결해, 같은 내용을 여러 지침에 복제하지 않는다.
