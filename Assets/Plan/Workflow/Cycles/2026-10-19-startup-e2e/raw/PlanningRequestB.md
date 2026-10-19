# Task6 기획 B 요청

요청 모델: GPT-6.1 Sol / High. 작업 루트: C:/Unity/TFTDataAnalyzer.

실제 발송 메시지: `Library/TftAnalyzerValidation/task6-planning-request-technical.md`를 읽고 읽기 전용 기획 검토를 수행하라. 추가 에이전트·Unity·Git 쓰기·네트워크·Hermes 호출 없이 지정 보고서만 작성하고 1200자 이내로 응답하라.

## 검토 계약

- 사용자가 전체 계획 끝까지 진행하도록 요청했으며 PR13 6281238 검수는 PENDING이다. 기존 문서 의견은 resolved다. 제품 코드는 통과·병합 전 변경하지 않는다.
- `Library/TftAnalyzerValidation/task6-planning-draft.md`와 기존 Phase3 Task6 구간을 먼저 읽는다. 필요한 기존 소스만 좁게 읽는다: DataDragonAdapter.FetchVersionsAsync, StaticCatalogPipeline, CatalogAssetWriter/CatalogIndexWriter, PublishedCatalogLoader, DataManagerService, 실제 UI 창 연결. 과거 기록·세션 로그는 읽지 않는다.
- 핵심은 미확정 세 계약의 구체적 결정이다. ①두 필드 상태로 24시간 제한·실패/취소/재로드·손상/미래 상태·IO 실패를 처리할 최소 구조와 시험, ②유휴 delayCall과 재로드 취소·배치/재생 배제·중복 실행·새 창 없는 안내, 마지막 가져온 검증 카탈로그 선택, ③실제 게시/인덱스/익명 수집/분석 경로를 연결하는 가짜 E2E의 격리 경계와 구체적 기존 클래스/주입점.
- 특히 전역 초기화의 실제 버전 요청과 가짜 테스트의 관계를 검토한다. 테스트 통과를 위해 제품의 실동작을 영구 비활성화하는 방식, 가짜 메타데이터를 진짜 확인 기록으로 남기는 방식, 실제 게시 단계를 stub으로 바꾸는 방식은 권고하지 않는다. 기본 테스트가 외부 키/seed를 읽지 않도록 한다.
- 복잡한 새 프레임워크·반복 polling·무한 콜백·손상 파일 무조건 삭제·현재시각/Unity정적API를 직접 박는 설계를 피한다. 기존 경로 보호·전송·저장/동의 경계를 재사용한다.
- `Library/TftAnalyzerValidation/task6-plan-technical.md`에 2000자 내외로 PASS/수정 필요, 결함 최대5개, 위 세 결정의 구체적 API/근거·객관적 테스트를 기록한다. 실제 확인하지 못한 모델·사용량을 단정하지 않는다. 이번 작업은 설계 검토이며 실행 통과가 아니다.

## 실제 추가 확인 요청

통합용 좁은 확인 2건만 추가해줘. ① 마지막 가져오기는 IndexEntry의 선택적 ImportCompletedUtc(성공한 Upsert 때 주입 clock으로 기록)로 추적하고 immutable catalog/manifest는 변경하지 않는 안을 생각한다. 동일 데이터 재가져오기도 최근 성공 기록은 갱신하되 게시된 catalog bytes의 idempotence는 유지한다. 기존 유효 행들의 원본 버전이 한 종류면 legacy timestamp 없어도 비교 가능, 여러 미상 버전이면 보류하도록 할 때 기존 실제 테스트/스키마와 충돌하는지 확인. ② Unity TestRunnerApi.IsRunActive는 설치된1.6소스에서 internal이고 Editor asmdef는 Core/Newtonsoft만 참조한다. 내부reflection보다 기존 설치 UnityEditor.TestRunner의 공개 ICallbacks/RunStarted·RunFinished를 작은 bootstrap어댑터로 구독해 active 테스트에서 자동HTTP를 취소/보류하는 안의 실제 API를 로컬소스로 확인해줘. 일반 Editor 시작 확인은 유지하고 테스트 런처/배치 구분만 최소 의존성으로 처리하고 싶다. 구조를 다시 크게 설계하지 말고 필요한 공개 API/참조와 제약, 이보다 단순하고 실제 지원되는 대안이 있으면 보고서에500~1000자 보충하고 짧게 응답하라. 제품수정/Unity/네트워크 금지는 유지한다.
