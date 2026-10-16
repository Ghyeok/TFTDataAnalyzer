# 경기 익명화 — 개발·검증 보고서

날짜: 2026-10-16
상태: Task 3 구현·실제 Unity 검증·독립 검수 후 수정 완료. 최종 사용자 완료 승인은 별도다.
계획: [Phase 2 Task 3](../../../../Implementation/2026-09-17-tft-match-analytics.md#task-3-stream-and-anonymize-match-dtos-before-persistence).

## 결과

`Editor/RiotApi/MatchAnonymizer`는 caller 소유 UTF-8 스트림을 한 번 읽어 허용 필드만 DTO에 담고, 검증 후 익명 관측으로 투영한다. 개인정보 속성은 DTO에 선언하지 않아 건너뛴다. Core의 `AnonymizationResult`는 승인된 관측 또는 격리 진단만 반환한다.

- 원본 Match ID는 `SHA256("match-v1|" + rawMatchId)`로 변환한다. 이름·PUUID·Riot ID·태그·원본 Match ID·자유 형식 빌드 주석은 결과에 포함하지 않는다.
- 활성 카탈로그 전체 식별자와 출처 프로필을 확인한다. 8명의 순위가 1–8로 중복 없이 구성돼야 하며, 기물·아이템·시너지·증강은 선택한 카탈로그에 존재해야 한다.
- `MatchDataVersion`과 숫자만 남긴 `MatchGameVersion`을 관측에 추가했다. 기본값은 빈 문자열로 기존 생성자와 JSON 읽기를 유지한다. 신규 수집 경로는 버전을 필수 검증한다.
- 알 수 없는 ID가 있으면 경기 전체의 `Observation`을 null로 두고 분류와 ID 해시만 반환한다. 진단의 원문 문자열 유출을 막으며 중복 ID는 같은 분류·해시로 합친다.
- 8 MiB·깊이 32·필드별 컬렉션 상한을 적용한다. 중복 속성·정수의 소수/문자열 변환·숫자 ID·후행 내용·잘못된 UTF-8을 거부한다. 원본 파서·I/O 예외는 내부 예외 없이 고정 코드로 바꾼다.
- 읽기 성공·실패 모두 호출자 스트림을 닫지 않는다. 파싱 중 실패한 DTO도 참조를 보유해 `finally`에서 정리한다. 실제 원본 응답 메모리는 호출자가 Dispose해야 한다.

## 실행 증거

| 단계 | 실제 결과 |
|---|---|
| 초기 RED | 테스트를 먼저 작성한 후 Unity 재컴파일 CS0246: 예정된 MatchAnonymizer/AnonymizationResult 없음 |
| 집중 GREEN | MatchAnonymizerTests 44 passed / 0 failed |
| 최초 전체 회귀 | TFTAnalyzer.Tests.EditMode 218 passed / 0 failed |
| 검수 회귀 RED | 45/52: 비표준 숫자·작은따옴표·따옴표 없는 키·후행 쉼표·잘못된 escape·원시 제어 문자 7건이 잘못 승인됨 |
| 최종 GREEN | 집중 52/52, 전체 실제 Unity EditMode 226/226. 유효 escape와 1·17바이트 단위의 비탐색 읽기 포함 |
| 컴파일 | completed, failed=false, compilationFailed=false, errors=[] |
| 독립 검수 | Critical 0, Important 1, Minor 0. [응답](../raw/Reviewer.md). Important는 같은 수정 단계에서 RED→GREEN으로 보완 |
| 보존 | 생성 카탈로그 diff 없음. 사용자 volume profile SHA-256 `4789B43834DD5EFB32EEC4C3B436BAF0895C9F8CF2A0624121DA8DD812EE984A` 유지 |

명령: `unity command recompile`, `recompile_status`, `run_tests --mode editor --filter_type testName --filter MatchAnonymizerTests --timeout 300`, 전체는 `--filter_type assembly --filter TFTAnalyzer.Tests.EditMode`. 모두 `--project-path C:/Unity/TFTSimulator --format json`을 사용했다. 전체 JSON은 Git 제외 `Library/TftAnalyzerValidation/task3-*.json`에 있다. 픽스처의 모든 식별 정보는 가짜이며 환경 변수·실제 경기 원본을 읽지 않았다.

## 판단과 제한

1. 계획의 Core 파서와 Editor DTO 배치는 asmdef 의존성이 충돌한다. 파서와 DTO를 Editor에 함께 두고 결과만 Core에 둔다. 향후 호출자는 Editor namespace를 사용해야 한다.
2. 카탈로그를 패치 문자열만으로 선택하지 않는다. 호출자가 전체 식별자와 카탈로그를 명시하며, 원본 게임 버전의 major/minor와 세트 번호를 비교한다. TFT 패치 `18.2`와 source `16.18.1`은 직접 비교하지 않는다. 같은 릴리스 내 핫픽스와 정확한 빌드 대응까지 보장하지 않으며 숫자 빌드를 보존해 후속 분석에서 구분할 수 있게 했다.
3. 격리 보고서에 원본 unknown ID를 노출하는 대신 `SHA256("static-id-v1|" + kind + "|" + rawId)`를 남긴다. 정확한 이름 복구는 불가능하며 후보 카탈로그 ID를 해싱해 대응해야 한다. 다른 입력 필드에 개인정보가 들어가도 진단 원문이 유출되지 않는 쪽을 택했다.
4. data version `2`, 명시적인 `itemNames`·traits·augments 배열과 8인 단독 순위를 지원한다. 누락 필드를 빈 목록으로 추정하거나 예전 숫자 아이템 ID를 묵시적으로 변환하지 않는다. 동률 배치 모드·스키마 변경·더 큰 응답은 제외되므로 실제 수집 연결 전에 지원 범위를 확인해야 한다.
5. 스트림 소유권을 바꾸지 않는다. 호출자가 응답을 Dispose하지 않으면 원본 메모리가 남는다. 관리 문자열·버퍼의 즉시 영점화를 보장하지 않는다.
6. 네트워크·실서비스 응답·원본 저장·중복 제거·집계·UI는 실행하지 않았다. Task 4–7의 책임이며 이번 테스트가 해당 통합을 검증한 것은 아니다.
7. Json.NET은 `010`을 8로 해석하고 다른 JavaScript 확장도 허용한다. 토큰 검사에 앞서 숫자 표기·인용부호·escape·후행 쉼표를 스트리밍으로 검증한다. 원문 전체를 추가 문자열로 적재하거나 새 JSON 패키지를 도입하지 않았다. 이 보조 검사를 유지해야 하며 잘못된 거부는 지원 입력을 줄이는 비용이 있다.

검수자가 판단을 유보한 실서비스 스키마·카탈로그 정확성은 가짜 픽스처만으로 보장하지 않는다(판단 4·6). 정확한 핫픽스 대응은 판단 2, 관리 메모리와 미완성 중첩 DTO의 물리적 소거는 판단 5의 제한에 남긴다. 붙어 있는 DTO 참조 정리와 즉시 영점화는 다르며, 미완성 중첩 객체는 GC 수명에 의존할 수 있다. 전체 컴파일·Unity 실행 증거는 구현자가 직접 확인했고 검수자의 별도 프로세스 probe가 이를 대체하지 않는다. 호출자의 Dispose·저장·중복 제거·집계는 후속 작업에서 보장해야 한다. 키 처리는 Task 2에 그대로 두었으며 이번에 실제 키를 읽지 않았다. AGENTS 변경은 사용자가 승인한 PR 대기 방식이며 volume 파일은 해시와 Git diff로 별도 보존 확인했다.

실행 주체는 Codex다. 루트의 실제 모델·추론과 총 토큰·시간은 독립 확인하지 못해 추정하지 않는다. 독립 보안 검수에는 `gpt-6-astra` · High를 요청했다. [Riot 공식 TFT 문서](https://developer.riotgames.com/docs/tft)의 Match History 및 data version 안내를 확인했으며 실제 키로 응답 성공을 확인하지 않았다.
