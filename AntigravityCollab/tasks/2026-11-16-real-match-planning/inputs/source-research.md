# 실제 경기 저장 지원 근거 조사

상태: 진행 중

## 확인된 프로젝트 동작

- 사용자가 승인한 제한 단일 진단은 세트18·큐1100 일치, game_version의 기존 숫자 패턴 불일치, data_version 1~5 외, 참가자 augments 누락을 확인했다. 이전에 실패한 5건과 동일한 경기인지는 미확인이다.
- MatchAnonymizer는 data_version 2, 숫자로 해석 가능한 원본 게임 릴리스, 참가자의 명시적 augments 배열을 요구하며 검증되지 않은 관측은 저장하지 않는다. 파싱 실패가 앞서면 카탈로그 릴리스 비교는 수행되지 않는다.
- MetricPolicy.AugmentRestriction은 증강 ID 목록이 비어 있으면 제한 사유 없이 통과한다. 따라서 출처가 제공하지 않은 증강을 빈 배열로 바꾸면 미확인 증강을 ‘증강 없음’으로 잘못 해석해 비율 노출에 영향을 줄 수 있다. 자료 없음과 실제 빈 배열은 다른 상태로 보존해야 한다.

## 외부 근거와 제한

- [Riot TFT 공식 설명](https://developer.riotgames.com/docs/tft#match-history)은 Match History의 data_version 1·2 도입 이력을 설명하지만, 이번 응답의 1~5 외 버전과 누락된 증강의 의미를 확인해 주지는 않는다.
- [Riot 개발자 관계 저장소 이슈 #1185](https://github.com/RiotGames/developer-relations/issues/1185)는 외부 개발자가 세트18·큐1160(Double Up)의 일부 응답에서 숫자 패치를 판별할 수 없는 game_version과 augments 누락을 관측했다고 보고한다. 이 제보에는 Riot의 확인 답변이 없고, 이번 진단은 큐1100이므로 같은 원인·동일한 Riot 정책·전체 응답의 동작이라고 단정할 수 없다. 제보자는 세트나 경기 날짜로 패치를 추정하지 않았다.
- [Riot 개발자 관계 저장소 이슈 #1171](https://github.com/RiotGames/developer-relations/issues/1171)의 댓글에서는 다른 개발자가 세트18 랭크 큐1100의 game_version에 숫자 버전을 확인할 수 없는 표시가 나온다고 제보했다. 해당 이슈의 원래 단위·시너지 누락 문제는 작성자가 해결됐다고 하며 종료했지만, 댓글의 버전 문제에 대한 Riot의 공식 확인이나 우리 경기의 정확한 값까지 입증하지는 않는다.
- [Riot 개발자 관계 저장소 이슈 #780](https://github.com/RiotGames/developer-relations/issues/780)은 2023년 외부 개발자가 관련 필드 문서의 누락을 제보한 이력이다. 현재 스키마의 의미나 누락 필드가 빈 값이어도 된다는 공식 보증은 아니다.

## 실제 저장 지원에 필요한 근거

1. 데이터 버전의 공식 계약과 해당 경기 형식이 지원 대상인지 확인한다.
2. 응답에서 선택한 카탈로그와 실제 게임 릴리스를 신뢰성 있게 연결할 근거를 확보한다. 세트 번호·경기 시각만으로 패치를 추정하지 않는다.
3. 증강 누락을 ‘없음’과 구분해 저장·집계·CSV·정책 필터에 전파하는 설계를 마련하고, 불명확한 분류의 지표 노출을 차단한다.
4. 실제 대응에 필요한 공식 문서 또는 Riot의 확인이 없으면 미검증으로 남기고, 입증되지 않은 응답을 저장하지 않는다.

이번 조사는 공개 문서와 기존 코드만 사용했으며 Riot에 메시지를 보내거나 추가 경기 API를 호출하지 않았다. 공개 이슈도 작성자 보고일 뿐 Riot의 답변이나 데이터 명세 확정으로 취급하지 않는다.

## 저장·분석 경로 독립 조사

GPT 담당의 읽기 전용 조사: AnonymousParticipant는 null 증강을 빈 목록으로 처리하고 CompositionFingerprintBuilder는 그 목록을 지문에 포함한다. MetricPolicy는 빈 목록에 제한을 적용하지 않으며 AggregateBuilder·AnalysisService·CSV까지 결과가 전달된다. 따라서 미제공을 빈 목록으로 바꾸는 수정은 정책을 우회할 수 있다. 실제로 명시된 빈 배열의 기존 의미도 임의 변경하지 않는다.

AnonymousMatchObservation은 game version 빈 문자열을 허용하더라도 MetricScope는 선택 카탈로그를 보유하고 AggregateBuilder는 그 식별자로 집계한다. 원본 릴리스가 불명확한 경기를 그 카탈로그에 넣으면 다른 패치가 섞일 위험이 있다. 저장 schema1은 증강 배열과 버전 문자열만 기록하며 기존 빈 배열을 사후에 미제공으로 추정할 수 없다. 집계 cache schema2도 기존 지문에 결합된다. 미제공을 저장하려면 별도 completeness와 검증되지 않은 릴리스의 격리 경로, 기존 파일/지문/캐시 호환, 정책·상대 지표·CSV·UI 차단까지 새로운 설계와 검증이 필요하다. 이는 구현 완료 주장이 아니다.

근거: Assets/TFTAnalyzer/Core/Analytics/AnonymousParticipant.cs, CompositionFingerprintBuilder.cs, MetricPolicy.cs, AnonymousMatchObservation.cs, MetricScope.cs, AggregateBuilder.cs, Editor/Persistence/ObservationStore.cs. 실제 API나 Unity를 다시 실행하지 않았다.
