# Riot API 클라이언트 — 개발·검증 보고서

날짜: 2026-10-16
상태: Task 2 구현·검증·독립 검수 후 수정 완료. 최종 사용자 완료 승인은 별도다.
계획: [Phase 2 Task 2](../../../../Implementation/2026-09-17-tft-match-analytics.md#task-2-implement-api-key-safety-routing-and-rate-limit-handling).

## 구현 결과

- `EnvironmentRiotApiKeyProvider`: 키를 호출 시점에만 조회하고 공백·제어문자를 검증한다. 생성자는 키를 읽지 않으며 파일·EditorPrefs·로그에 저장하지 않는다.
- `RiotRouting`: 승인된 TFT 지역과 HTTPS Match API 경로만 구성하며 식별 경로 값은 percent-encode한다. ID 목록의 count는 1–100으로 제한한다.
- `IRiotApiTransport`와 `UnityRiotApiTransport`: 인증은 `X-Riot-Token` 헤더 하나로 제한한다. 고정 호스트·기본 포트를 검증하고 리디렉션을 끈다. 응답은 메모리에서만 처리하고 실패·취소 시 정리하며, 성공 스트림은 호출자가 소유·Dispose한다.
- `RateLimitState`: 지역별 app/method 창을 원자적으로 예약한다. 기존 예약을 오래된 count 응답으로 줄이지 않는다. 429와 `Retry-After`를 기억해 이후 요청을 차단한다.
- `RiotApiClient`: 429는 안전한 재시도 시각을 반환하고 즉시 종료한다. 500/502/503/504만 1·2·4초 후 최대 세 번 재시도한다. 그 외 HTTP 오류는 재시도하지 않는다. 예외에 원본 URL·키·본문·내부 예외를 넣지 않는다.
- 네이티브 요청 완료와 취소가 겹쳐도 이미 받은 제한 헤더를 먼저 보존한다. 성공 요청도 호출자 취소를 전달하기 전에 클라이언트가 제한 정보를 관측한다.

## 실제 검증

| 단계 | 실행 결과 |
|---|---|
| 초기 RED | 테스트 작성 후 Unity CS0234/CS0246: 예정된 RiotApi 타입이 없음 |
| 초기 집중 실행 | 53/55. 두 NUnit 취소 예외 검사가 하위 TaskCanceledException을 허용하도록 수정 |
| 클라이언트 취소 회귀 RED | 완료된 429 직후 취소: 예상 rate_limited 대신 transport_error |
| 1차 GREEN | 집중 57/57, 전체 실제 Unity EditMode 171/171 |
| 독립 검수 | Critical 0, Important 1, Minor 1. [응답 기록](../raw/Reviewer.md) |
| 네이티브 취소 회귀 RED | 집중 58/60. 응답 완료 우선·취소 우선 두 순서 모두 헤더 전달 전 취소 예외 |
| 최종 GREEN | 전체 실제 Unity EditMode 174/174. API 관련 60건 모두 통과. 컴파일 오류 없음 |
| 보존 | 기존 생성 카탈로그 변경 없음. 사용자 volume profile SHA-256 `4789B43834DD5EFB32EEC4C3B436BAF0895C9F8CF2A0624121DA8DD812EE984A` 유지 |

명령은 `unity command recompile`, `recompile_status`, `run_tests --mode editor --filter_type testName --filter RiotApiClientTests`, 전체는 `--filter_type assembly --filter TFTAnalyzer.Tests.EditMode`를 사용했다. 모두 `--project-path C:/Unity/TFTSimulator --format json`이며 테스트 timeout은 300초다. 로그는 Git 제외 `Library/TftAnalyzerValidation/task2-*.json`에 있다. 컴파일 도중 한 번 실행한 `NativeWait` 필터는 0건이어서 증거에서 제외하고, 컴파일 완료 후 전체 테스트 클래스로 RED를 재현했다.

## 구현 판단과 남은 제한

1. 기존 정적 HTTP 예외는 URL·상세 오류를 포함하므로 별도 인증 전송 계약을 추가했다. 잘못 판단했다면 전송 구현 유지 비용이 늘어난다.
2. 429는 자동 재시도 대신 재시도 시각을 반환한다. 헤더가 없거나 잘못되면 60초 차단한다. app/method 창이 더 길면 그 시각까지 기다린다. 보수적인 설정으로 수집 처리량이 줄 수 있다.
3. 테스트 어셈블리에 내부 완료 대기 함수 접근을 허용해 실제 전송 계층이 사용하는 취소 경계를 네트워크 없이 검증했다. HTTP 엔진 전체를 대체하는 추상화는 추가하지 않았다. 실제 Unity 네트워크와 helper 사이 동작 차이는 실서비스 검증에 남는다.
4. 실제 키·seed를 조회하거나 Riot에 호출하지 않았다. TLS·서버의 실제 응답·네이티브 리디렉션 동작은 코드 설정만 확인했으므로 통합 성공을 보장하지 않는다. 응답 헤더와 창 경계는 가짜 시각·전송으로 검증했다.
5. 익명화·원본 경기 승인·저장·집계는 Task 3–7이다. Task 2 스트림을 저장에 바로 연결하면 식별 원본이 저장될 위험이 있으므로, 다음 익명화 단계를 거쳐야 한다.
6. 관리 메모리의 즉시 영점화와 악의적인 주입 구현 격리는 제공하지 않는다. 이 계약은 메모리 처리·소유권 정리에 한정되며 프로세스 내 비신뢰 코드의 비밀 접근을 차단하지 못한다.
7. 독립 검수자는 테스트를 실행하지 않았다. 위 실행 결과는 구현자의 실제 Unity 실행 증거다. 요청 모델은 `gpt-6-astra` · High이며 루트 실제 모델·추론과 총 사용량·시간은 독립 확인하지 못해 추정하지 않는다.

**보류한 경미한 사항:** 키 조회 실패가 실제 요청 없이 알려진 quota를 예약한다. 키를 복구해도 창이 끝날 때까지 추가 요청이 보수적으로 차단될 수 있다. 호출 제한 초과·키 노출 문제는 아니므로 이번 최종 수정에서는 보류했다.

지역과 호출 제한 해석은 [Riot TFT 공식 문서](https://developer.riotgames.com/docs/tft)와 [포털 호출 제한 문서](https://developer.riotgames.com/docs/portal)를 확인했다. 플랫폼 별칭은 승인된 계획을 유지했고 실제 호출 성공으로 검증한 것은 아니다.
