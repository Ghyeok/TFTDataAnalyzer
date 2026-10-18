# PR12 저장 작업 경합 수정

작성일: 2026-10-19. 상태: 구현·독립 코드 검수·실행 검증·독립 QA 통과. PR12 수정 커밋을 통합하며 최신 head 재검수는 별도다. 기준 커밋0dacaaeba792610228c257da16c27475468478e0. [CodeRabbit 의견](https://github.com/Ghyeok/TFTDataAnalyzer/pull/12#discussion_r4307683758)을 실제 저장소 코드와 대조한 후 기존 Task4 결함 수정으로 처리했다.

관측 저장과 분석 복구 읽기·집계 저장은 같은 store.lock을 사용하지만 각 저장 객체의 semaphore는 따로였다. 따라서 분석 갱신 도중 수집 저장이 겹치면 lease 획득 실패로 한쪽 작업이 종료될 수 있었다. ObservationStore만 공유해도 별도 AggregateStore 저장 구간은 보호되지 않는다.

Editor 프로세스 안에서 공유하는 작은 작업 조정자로 수집 전체와 분석 읽기·계산·모든 집계 저장을 상호 배제한다. 창이나 binding을 새로 만들거나 닫아도 실제 작업이 끝날 때까지 소유권이 유지된다. 기다리는 작업은 취소할 수 있고, 이미 시작한 작업은 그 Task가 끝난 뒤 finally에서 다음 작업에 넘긴다. 수집이 네트워크 응답을 기다리는 동안에도 분석은 대기할 수 있으며 대기 중 취소가 가능하다. 서로 다른 외부 저장소도 현재 Editor에서는 함께 직렬화된다.

기존 파일 lease·백엔드 수집 상한·API 재시도·취소 결과·정책 DTO·UI 레이아웃은 바꾸지 않는다. 다른 프로세스와의 lease 충돌을 자동 재시도로 숨기거나, 작업 취소 요청만으로 잠금을 조기에 해제하지 않는다. Hermes 자동 확인도 다시 켜지 않는다.

## 실행 결과

- 신규5건은 수정 전 모두 실패했다. 동시에 시작함, 대기 취소 무시, 기존 작업 중 수집 HTTP 실행(취소 여부2건), 집계 캐시 저장 전 수집 시작을 확인했다.
- 활성 작업이 취소를 무시하고 늦게 끝나도 다음 작업이 기다리는 회귀1건을 추가했다. 최종 집중6/6, 컴파일 completed/failed=false/compilationFailed=false/errors[]다.
- 최종 전체 EditMode501/501, skipped/inconclusive0, outer/inner success true,62.71초. 이전495/495는 최초 PR 구현의 이력이다.
- 전체 실행 전후 기존 EditorWindow9개·native ContainerWindow1개의 ID와 Editor 포커스가 같다. 보호 대상1,386개 SHA256 변경0, 기준 Console cursor 이후 새 경고/오류0(reset/dropped false)이다. 기존 Pipeline 통신 오류4건은 새 제품 오류로 계산하지 않는다.
- 이번 수정은 UI 레이아웃/창 검증 도구를 바꾸지 않아 수동 캡처를 반복하지 않았다. 최신 전체 suite의 창 전후 비교를 별도로 확인했고, 원래6장 화면과 예외 probe는 최초 Task4 검증 이력으로 유지한다.

독립 검수는 소스·RED·컴파일·집중6/6을 읽고 승인했다. 별도 QA는 전체501건과 보호·창·Console·문서10개 링크를 독립 확인해 PASS로 판정했다. root가 구현과 유일한 Unity 실행을 맡았다. 요청 모델은 복구/취소 검수 Sol High, 명확한 증거 대조 QA Sol Medium이며 실제 runtime 모델/추론·토큰·총 활성 시간은 미확인이다. 독립 QA와 원본 증거는 [후속 공개 기록](../raw/StoreCoordinationReview.md)에 보존했다. 실제 Riot 호출이나 외부 프로세스 간 재시도 검증은 수행하지 않았다.

재개 때 최신 head의 CodeRabbit·미해결 thread·CI를 한 번 확인한다. 수정 커밋 push 뒤 반복 대기하지 않는다. 검수 통과 전 병합하거나 Task5/6으로 넘어가지 않는다.
