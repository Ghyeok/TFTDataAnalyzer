# 익명 관측 저장 — 개발·검증 보고서

날짜: 2026-10-16
상태: Task 4 구현·실제 Unity 전체 검증·독립 검수 통과. PR 검수·병합 및 최종 사용자 완료 승인은 별도다.
계획: [Phase 2 Task 4](../../../../Implementation/2026-09-17-tft-match-analytics.md#task-4-persist-anonymous-observations-and-deduplication-index).

## 결과

`ObservationStore.AppendIfNewAsync`는 승인된 익명 도메인 관측을 허용 필드로만 직렬화해 scope별 NDJSON에 저장한다. 원본 스트림·DTO·키·플레이어 식별자를 받는 API는 없다. `ReadAllAsync`는 검증된 스냅샷을 반환하고 소비자가 순회하는 동안 쓰기 잠금을 보유하지 않는다.

- 기본 루트는 `Library/TftAnalyzerStore`이며 대체 경로는 프로젝트 외부만 허용한다. 기존 링크·junction 경로는 생성 시와 파일 접근 시 거부한다. 관측 경로에는 전체 카탈로그·출처 프로필·지역·큐·티어의 버전된 구조 해시만 사용한다.
- `processed-match-hashes.json`은 정렬된 경기 해시만 담는 파생 인덱스다. 임시 파일을 flush한 후 같은 디렉터리에서 원자 교체한다. 인덱스 내용은 신뢰하지 않고 매 접근 시 완성된 관측에서 재구성하므로 누락·손상·유령 해시가 중복 제거 판단을 바꾸지 않는다.
- 인스턴스 내부 비동기 gate와 OS 배타적 파일 lease로 쓰기를 보호한다. 다른 인스턴스가 동시에 접근하면 안전한 오류를 반환하며 자동 대기하지 않는다. 순차 접근은 다른 인스턴스의 변경을 재구성한다.
- NDJSON을 쓰고 durable flush하기 전 취소·오류가 나면 원래 길이로 되돌린다. flush 후에는 취소로 완료를 뒤집지 않는다. 인덱스 게시 실패는 오류로 알리되 저장된 관측을 유지하며, 재시도는 같은 경기를 중복 저장하지 않는다.
- 개행으로 끝나지 않은 마지막 조각만 중단된 쓰기로 간주해 제거한다. 완성된 잘못된 JSON·중복 기록·잘못된 scope 파일·알 수 없는 속성은 원문 없는 오류로 거부하며 해당 기록을 건너뛰거나 조용히 삭제하지 않는다. 관측 한 건은 8 MiB로 제한한다.

## 실행 증거

| 단계 | 실제 결과 |
|---|---|
| RED | 선작성 테스트의 Unity 재컴파일 CS0246: ObservationStore와 AnalyzerStorePaths 없음 |
| 첫 컴파일 GREEN | completed, failed=false, errors=[], compilationFailed=false |
| 집중 검증 중단 | NUnit 동기식 Assert.ThrowsAsync가 실제 FileStream.WriteAsync의 Unity 문맥 복귀를 차단. 저장 커밋 전 테스트가 멈춤 |
| 원인·수정 | 임시 테스트 폴더의 인덱스 대상 디렉터리와 0바이트 대기 관측, test_status 시간 초과로 경로 확인. 예외 검증을 await하는 테스트 helper로 교체 |
| 환경 복구 | 사용자 승인 후 해당 프로젝트 Editor PID 23660만 종료. 이후 headless Pipeline run_tests로 실제 Unity 재실행 |
| 집중 GREEN | ObservationStoreTests 32/32 |
| 전체 회귀 | TFTAnalyzer.Tests.EditMode 261/261, 새 저장소 35건 포함. 추가 3건은 읽기/쓰기 재진입·경쟁 lease·크기 상한 |
| 독립 검수 | Critical 0 / Important 0 / Minor 0. [실제 공개 응답](../raw/Reviewer.md). 검수자는 Unity·네트워크를 실행하지 않음 |
| 파일 보존 | 생성 카탈로그 diff 없음. 사용자 volume SHA-256 `4789B43834DD5EFB32EEC4C3B436BAF0895C9F8CF2A0624121DA8DD812EE984A` 유지 |

최종 명령은 `unity run C:/Unity/TFTSimulator --command run_tests --timeout 360 --format json -- --mode editor --filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout 300`이다. 집중 검증은 `--filter_type testName --filter ObservationStoreTests`를 사용했다. 전체 JSON과 로그는 Git 제외 `Library/TftAnalyzerValidation/task4-*.json`, `task4-*-stderr.log`에 있다. 테스트 종료 로그의 debugger listen 경고는 결과 실패가 아니며 최종 261 passed / 0 failed를 직접 확인했다.

실제 파일 기반 검증은 중복·동시 중복·scope 분리·재오픈·손상/누락/유령 인덱스·인덱스 게시 실패 후 재시도·불완전 끝부분·완성 기록 손상·파일 잠금 오류·사전 취소·빈 저장소·문화권별 바이트 결정성·경로 제한을 포함한다. 가짜 식별 정보가 있는 기존 Match API 픽스처를 실제 익명화기에 통과시킨 뒤 저장 폴더의 모든 파일을 스캔했다. 실제 경기 응답이나 비밀값은 사용하지 않았다.

## 판단과 제한

1. 저장소 전체에서 같은 경기 해시는 한 번만 저장한다. 첫 승인 scope를 유지하며, 다른 카탈로그/필터로 같은 경기를 다시 처리하려면 별도 저장소가 필요하다. scope가 다르면 통계를 섞는다는 의미는 아니다.
2. 매 접근의 전체 관측 스캔과 스냅샷은 단순하고 안전한 복구를 우선한 선택이다. 처리량은 건수에 비례하고 전체 누적 append는 이차 비용이 될 수 있으며, 읽기 메모리도 저장량에 비례한다. 대규모 운영 성능은 검증하지 않았다.
3. 임의로 만든 도메인 문자열이 실제 카탈로그 값인지 저장소가 다시 확인하지 않는다. 호출자는 `IsAccepted=true`인 익명화 결과만 전달하고 원본 스트림을 Dispose해야 한다. 이번 익명화→디스크 테스트는 가짜 픽스처 경로의 증거다.
4. 사전 취소와 실제 파일 잠금/인덱스 실패를 검증했다. 쓰기 중 정확한 시점의 취소·부분 쓰기·flush 자체 실패·전원 차단은 결정적으로 주입하지 않았다. rollback과 개행 끝부분 복구 코드는 이 경계를 담당하며 OS/장치의 영속성 보장까지 측정한 것은 아니다.
5. 경로 링크를 보수적으로 거부하지만 동일 계정의 악의적 프로세스가 검사 직후 경로를 바꾸는 공격에 대한 격리 경계는 아니다. 권한 있는 프로세스의 직접 파일 변조나 메모리 관찰을 막지 않는다.
6. 매 경기 해시·시각·완전한 보드는 연결 가능성이 있는 로컬 자료다. 외부 공유·내보내기·보존 정책과 비식별성 평가는 이번 범위가 아니며 실제 수집·집계·UI 연결도 미실행이다.

실행 주체는 Codex다. 루트 실제 모델·추론과 총 사용량·시간은 독립적으로 확인할 수 없어 추정하지 않는다. 독립 검수에는 개인정보/데이터 보존 위험에 따라 `gpt-6-astra` · High를 요청했다. 재작업은 테스트 예외 검증 helper 수정과 승인된 Editor 재시작이며, 독립 검수 후 제품 코드 재작업은 없었다. 검수자가 판단을 유보한 upstream 익명화, 악의적 파일 경합, 장치 영속성, 대규모 성능은 위 제한에 남기고, 실제 Unity261/261과 가짜 식별 정보 저장 스캔은 구현자가 직접 확인했다.

## PR 재개 검수 — 설명 주석 보완

2026-10-16 사용자 “진행해줘” 요청으로 [PR #5의 최신 검수](https://github.com/Ghyeok/TFTDataAnalyzer/pull/5#issuecomment-6207399065)를 한 번 확인했다. 기준 커밋 `1e0ac8aa696f534f2cfb8ff0c89ff19c17d5c2d6`의 CodeRabbit 상태는 SUCCESS였고 검수 스레드와 기능 수정 요청은 없었다. 다만 pre-merge 설명 주석 비율이 17.78%로 기준 80%에 못 미쳐 병합 전에 보완했다.

저장소 3개 파일의 내부 경로·복구·직렬화 함수와 테스트 파일의 각 검증·helper에 목적과 동작을 설명하는 XML summary를 추가했다. 로컬 검사에서 함수 45/45에 주석이 있고 XML 구문이 유효하며, 공백/주석을 제외한 코드가 `1e0ac8a`와 동일함을 확인했다. C#/Markdown 공백 검사도 통과했고 사용자 volume 해시와 기존 카탈로그를 보존했다. 최초 비교 스크립트의 다중행 공백 정규식이 빈 줄까지 소비해 잘못 차이를 보고한 것을 줄 단위 비교로 수정한 뒤 재확인했다.

이번 변경은 주석과 작업 기록뿐이므로 Unity 테스트·독립 검수를 반복하지 않았다. 실제 테스트 결과는 앞선 261/261이며 새로 실행한 것으로 표현하지 않는다. CodeRabbit이 수정 커밋에서 계산할 비율은 아직 확인하지 않았고, push 후 사용자 요청까지 반복 조회·대기·자동 재개하지 않는다. 모델 전환·사용량·비용은 확인할 수 없어 추가 주장하지 않는다.
