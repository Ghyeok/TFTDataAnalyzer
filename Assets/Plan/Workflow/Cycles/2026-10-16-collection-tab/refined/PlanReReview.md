# 사용자 지적에 따른 구현 계획 재검토

작성일: 2026-10-16
상태: 승인됨 — 사용자가 요청한 계획 재검토와 검증 창 정리 보완. 후속 제품 기능 범위는 확대하지 않는다.

## 확인한 사실

의도된 초기 RED8건과 재생성 RED2건은 버그10건을 의미하지 않는다. 독립 검수에서 확인한 제품 결함은 입력 초안/화면 재생성 P2 한 건이며 수정 후455/455와 독립 재검수를 통과했다. 다만 수동 검증 창을 다음 도구 호출까지 남긴 절차는 사용자 불편을 만들었다.

| 발견 | 근거 | 반영 |
|---|---|---|
| 수동 창을 생성 후 반환하고 캡처가 나중에 재사용 | Library/TftAnalyzerValidation/collection-open.cs, collection-capture.cs | 단일 bounded 생성→대기→캡처→finally 정리, 예외 probe, 전후 ID 확인 |
| Inspector에 의존한 delayCall 경계가 간헐적으로 실행되지 않음 | 새 창 정리 검증 UI78/82→81/82, 5초 native 경계 timeout | 두 바인딩을 일회성 Editor update 예약으로 변경, 같은 실제 update 테스트 경계 사용. 렌더 검증은 Inspector를 열거나 포커스를 옮기지 않음 |
| fixture 정상 TearDown은 있으나 초기화/Dispose 예외 경로 미보장 | CollectionTabTests/CollectionWindowTests/DataManagerTabTests/TftDataAnalyzerWindowTests | 작은 공통 소유 창 scope, Show 전 등록, SetUp 실패·TearDown finally |
| 초안/유효 요청/실행 snapshot과 재생성 조합이 계획에 불명확 | [독립 제품 검수](../raw/Reviewer.md) | 원래 Task3 계획에 상태 표와 두 회귀 사례 연결 |
| 통합 검증이 마지막 E2E에 치우침 | 원래 Task4–6 | 각 Task의 모듈 경계 fake 검증, 마지막 전체 E2E 유지 |
| 후속 CSV 계약이 억제 필드·기간·용도에 모호함 | MetricPresentation.Ratios/FirstPlayedAt/LastPlayedAt와 Task4/5 | 억제 비율·분자·분모·구간은 빈 값, 경기 관측 기간, 로컬 개인연구 CSV 허용 유지 |

## 바뀐 실행 순서

현재 제품 기능을 유지하고 창 정리 회귀→최소 수정→집중 검증→최종 전체 한 번→정상/오류 수동 작업의 창 잔류0과 기존 창 보존→독립 QA→문서·PR 순서로 마무리한다. 새 변경·실패가 없으면 전체 suite를 반복하지 않는다. Task4는 이번 작업에 착수하지 않는다.

기존 사용자 창은 재사용·변형·종류별 종료하지 않는다. current inventory에 검증창이 없다는 사실을 에이전트가 닫은 증거로 해석하지 않는다. 창 정리 실패는 완료로 숨기지 않고 보고한다. 상시 규칙은 루트 [AGENTS.md](../../../../../../AGENTS.md)에 추가했다.

추가 조사에서는 EditorWindow 객체9개가 그대로여도 빈 native ContainerWindow 프레임2개가 한 UI 실행 뒤 남는 것을 발견했다. 이전 실행분까지 빈 프레임4개는 정확한 ID·검증 창 제목·활성 EditorWindow 연결 없음·NormalWindow 모드를 확인한 후 닫았고 기존 사용자 창9개와 주 창 프레임50466은 보존했다. 이후 종료 조건에는 native 프레임 ID도 포함한다. `DestroyImmediate`는 표시 전 객체 정리에 한정하고 표시한 창은 `Close`로 바깥 프레임까지 종료한다.

스케줄 변경은 UI 상태를 다음 Editor update에서 한 번 반영하는 용도다. 상시 polling이나 새 dispatcher를 만들지 않으며 기존 Unity 메인 스레드 전제를 유지한다. Inspector focus/menu 활성화를 반복하는 workaround는 제거한다. 화면 픽셀·geometry 완료를 update 콜백 하나만으로 보장한다고 주장하지 않는다.

실제 포함된 schema1 카탈로그를 수동 연결하면서 두 번째 제품 결함을 발견했다. schema1 `PatchManifest.Identity`는 설계상 null이고 loader가 검증한 `PatchRowViewModel.Identity`가 전체 식별자의 출처다. 기존 새 schema2 fixture만으로는 이 소비 계약 누락이 드러나지 않았다. 데이터 탭이 검증한 Catalog+Identity를 함께 넘기고 수집 VM·확인·실행·표시가 그 식별자를 별도 유지하도록 보완한다. 임의 legacy에 공식 전용 프로필을 추정하지 않는다. Task4도 같은 계약을 소비하도록 원래 계획에 명시하고 schema1/2 창→요청·실제 fake 수집 경로와 누락/불일치 거부를 추가 검증한다.

기획 A/B에 GPT-6.1 Sol/High를 요청해 독립 재검토했다. 실제 런타임 모델·추론·사용량은 독립 확인 정보가 없다. 이 문서는 발견과 결정이며 창 정리 수정이 이미 통과했다는 증거는 아니다. 실행 결과는 후속 개발·QA 보고서에서 확인한다.
