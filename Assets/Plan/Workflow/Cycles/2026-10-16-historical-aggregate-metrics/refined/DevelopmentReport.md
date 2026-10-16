# 과거 경기 집계·신뢰구간 — 개발·검증 보고서

날짜: 2026-10-16
상태: Task 5 구현·실제 Unity 전체 검증·독립 검수 통과. PR 및 최종 사용자 완료 승인은 별도다.
계획: [Phase 2 Task 5](../../../../Implementation/2026-09-17-tft-match-analytics.md#task-5-calculate-aggregate-and-pairwise-placement-metrics).

## 결과

`AggregateBuilder.Build`는 익명 관측에서 카탈로그 전체 식별자·지역·큐·티어·조합별 과거 최종 순위를 재집계한다. `AggregateMetric`은 참가자 수·순위 합계·Top4/1위 분자, 평균과 비율·Wilson 구간을 제공한다. `PairwiseMetric`은 같은 로비의 서로 다른 두 조합의 상대 최종 배치만 비교한다.

- 관측마다 8명과 서로 다른1–8위를 다시 검증한다. 같은 경기 해시의 같은 집계 입력은 한 번만 계산하며 참가자 열거 순서는 무시한다. 같은 해시의 scope·결과·시각·버전이 충돌하면 입력 순서에 기대지 않고 거부한다.
- scope와 조합/쌍은 구조화 키와 ordinal 순서로 정렬한다. 경기 해시·원본 보드는 집계 출력에 남기지 않는다. scope와 지표별 경기 시각 범위, scope별 정렬된 데이터/게임 버전을 보존한다.
- 같은 조합 쌍은 한 로비당 최대 한 번 비교한다. 같은 조합이 여러 참가자에게 있어 비교 대상이 모호하면 해당 쌍의 `AmbiguousLobbyCount`에 기록하고 분자·분모에서 제외한다. 같은 조합 자체의 쌍은 만들지 않는다. 일반 순위 지표에는 해당 참가자를 모두 포함한다.
- Wilson은 기본95% quantile을 사용한다. 빈 표본·잘못된 분자·유효하지 않은 z는 거부하며, 아주 큰 유한 z에서도 NaN 대신0–1의 한계 구간을 반환한다. 비교 가능 로비가0인 쌍은 비율/구간을 null로 둔다.
- `AggregateStore.SaveAsync`는 한 `ScopeAggregate`를 `aggregates/1/{scopeHash}.json`에 저장한다. 관측과 같은 scope 해시·경로 제한·OS lease를 사용하고 임시 파일 flush 후 원자 교체한다. 분자·분모와 제외 건수를 보관하며 원본 관측과 다른 scope 파일은 수정하지 않는다.

## 실제 실행 증거

| 단계 | 결과 |
|---|---|
| RED | 테스트 선작성 후 Unity 컴파일 CS0246: ScopeAggregate/PairwiseMetric 없음 |
| 컴파일 GREEN | completed, failed=false, errors=[], compilationFailed=false |
| Wilson 집중 | 16 passed / 0 failed:56/100,0/100,100/100,0/1,1/1,50/100, 사용자 z·극단값·잘못된 입력 |
| 집계·저장 집중 | 30 passed / 0 failed: 정확한 계수·28쌍·scope 분리·충돌/중복·빈 입력·바이트 재생성·실제 게시 실패 |
| 전체 회귀 | 실제 Unity EditMode307 passed /0 failed /0 skipped. 기존261+새46 |
| 독립 검수 | Critical 0 / Important 0 / Minor 0. [공개 응답](../raw/Reviewer.md). 검수자는 Unity를 실행하지 않음 |
| 보존 | 기존 생성 카탈로그 diff 없음. 사용자 volume SHA-256 `4789B43834DD5EFB32EEC4C3B436BAF0895C9F8CF2A0624121DA8DD812EE984A` 유지 |

명령은 `unity command recompile`, `recompile_status`, `run_tests --mode editor --filter_type testName --filter WilsonIntervalTests`, `--filter Aggregate`, 전체는 `--filter_type assembly --filter TFTAnalyzer.Tests.EditMode`이며 모두 `--project-path C:/Unity/TFTDataAnalyzer --format json`을 사용했다. 결과 JSON은 Git 제외 `Library/TftAnalyzerValidation/task5-*.json`에 있다. Task4에서 확인한 동기식 예외 검증의 Unity 교착을 반복하지 않도록 실제 파일I/O 실패 검증도 await helper로 처리했다.

## 판단과 제한

1. 설계3.5의 분모는 로비 수다. 중복 조합이 있는 로비에서 best/worst보드를 임의 선택하거나 모든 참가자 쌍으로 표본을 부풀리지 않는다. 명시적 모호성 제외는 표본을 줄이며, 이 비용은 제외 건수로 관찰 가능하게 했다.
2. 입력에는 실제 수집 시각이 없다. `FirstPlayedAt/LastPlayedAt`은 경기 시각이며 수집 시각으로 표시하지 않는다. 데이터·게임 버전을 정렬해 유지하지만 게임 빌드별 하위집계나 정확한 핫픽스 대응까지 보장하지 않는다.
3. 내부 집계 자료는 작은 표본에도 raw 비율·구간을 계산한다. Task6에서 최소100건·Legend관련 정책을 적용하기 전 UI/내보내기에는 연결하지 않는다. 전투 재현·미래 예측은 포함하지 않는다.
4. 저장 원자성은 scope 파일 하나 단위다. 여러 scope의 동시 트랜잭션·사라진 scope 캐시 자동 삭제·집계 읽기/UI연결은 이번 API가 제공하지 않는다. `SaveAsync`는 선택한 scope만 교체한다.
5. 사전 취소와 실제 파일 잠금/게시 실패의 보존·재시도를 확인했다. 부분 쓰기 시점의 취소·장치 flush실패·전원 차단·악의적 동일계정 경로 교체는 주입하지 않았다. 임시 파일/경로 검사·OS 권한은 별도 공격 격리를 보장하지 않는다.
6. 대량 입력은 해시 signature와 scope/조합 쌍을 메모리에 보관한다. 성능·메모리 한계나 명목95% 구간의 실제 표본 독립성은 검증하지 않았다. 수치는 과거 참가자 관측의 계산 결과다.
7. 카탈로그 소속·식별 정보 제거는 선행 익명화 계약을 신뢰한다. 임의 도메인 값을 실제 카탈로그와 재대조하거나 외부 root선택 권한·파일 인증을 추가하지 않았다.

실행 주체는 Codex다. 루트 실제 모델·추론과 총 사용량/시간은 독립적으로 확인할 수 없어 추정하지 않는다. 데이터 보존·정확도 검수에는 `gpt-6-astra` · High를 요청했다. 기존 liveEditor를 사용했고 새 패키지·외부 네트워크·키·seed를 사용하지 않았다. 독립 검수 후 제품 코드 재작업은 없었다. 검수자가 판단을 유보한 실제 Unity 실행은 구현자의307/307 증거로 확인했고, 중간 쓰기 취소·프로세스 종료·장치 영속성·악의적 경합·성능·다중scope트랜잭션·정책/실서비스는 위 제한으로 유지한다. 최종 검사에서 C#9개 파일 함수56/56의 XML 설명·형식과 Markdown8개 파일의 상대 링크, C#/Markdown 공백, 보호 파일 보존을 확인했다.
