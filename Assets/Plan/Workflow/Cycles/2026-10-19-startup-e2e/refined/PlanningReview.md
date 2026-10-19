# Task6 독립 기획 통합

## 현재 역할 적용

2026-10-19 사용자 확정에 따라 기획 A는 실제 Antigravity, 기획 B는 GPT로 같은 입력 해시를 검수했다. [현재 통합 결과](../../../../../../AntigravityCollab/tasks/2026-10-19-task6-planning/result.md)의 정책 차단/표본 부족/신뢰 합성 증거 분리, 캐시 안내 복원과 import 후 갱신, 재개 시 보호 점검을 [정본 계획](ImplementationPlan.md)에 반영했다. 실제 데이터 분류 공급·집계 입도 개선은 후속 범위다. 강제 CLI 접근 제한은 미검증이며 일반 격리 자동화·예약을 활성화하지 않았다. 아래 초기 두 GPT의 기록은 당시 이력으로 보존하며 Antigravity가 실행한 것으로 바꾸지 않는다.

## 초기 기획 이력

2026-10-19. 사용자 전체 작업 계획 완료 요청에 따라 기존 Task6 범위를 구체화했다. PR13 검수·병합 게이트는 유지하며 제품 코드는 변경하지 않았다.

기획 A와 B 모두 초안의 연결 계약 수정이 필요하다고 판단했다. root는 실제 CatalogIndex.cs·StaticCatalogPipeline·StaticCatalogPaths·DataDragonAdapter와 설치 Test Framework 소스를 대조해 다음을 확정했다.

- 요청 전 예약 저장과 24시간 차감, 손상/미래 상태 원자 복구 후 보류, 읽기/저장 실패 시 요청 없음.
- manifest 응답 시각은 가져오기 완료 시각이 아님을 확인했다. 인덱스의 선택적 완료 기록과 legacy 단일 버전 fallback을 사용하고 여러 미상 버전은 추측하지 않는다.
- 임시 ProjectRoot만으로 실제 AssetDatabase 인덱스를 격리할 수 없었다. Generated와 raw/staging 캐시 모두 주입하고 대상 인덱스만 저장하며 실제 게시/재로드를 검증한다.
- 기존 데이터 탭의 비차단 Label과 최소 내부 서비스/확인 주입 경계, 공개 TestRunner 콜백으로 자동 HTTP와 테스트 실행을 분리한다.

범위/개인정보 검토는 Sol Medium, 복구·전역 초기화·실제 저장 경계 검토는 Sol High를 요청했다. 실제 런타임·토큰은 아직 집계하지 않았다. 원래 공개 요청·응답은 raw/에 보존했으며 추가 기술 검토는 B 보고서의 보충에 기록돼 있다. 본 문서는 Master의 통합 결정이며 수정본의 독립 재검수·제품 실행 통과를 주장하지 않는다.

정본은 [구현 계획](ImplementationPlan.md)이다. 다음은 PR13 최신 검수 통과·병합 후 새 기능 브랜치에서 실패 테스트부터 실행하는 것이다.
