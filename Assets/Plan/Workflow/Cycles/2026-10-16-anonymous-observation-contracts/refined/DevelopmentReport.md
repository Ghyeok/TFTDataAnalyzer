# 익명 경기 관측 계약 — 개발 보고서

날짜: 2026-10-16
상태: Task 1 구현·검증·독립 코드 검수 완료, Git 체크포인트 대기
계획: [Phase 2 Task 1](../../../../Implementation/2026-09-17-tft-match-analytics.md#task-1-define-anonymous-observation-contracts).

## 결과

`Core/Analytics/`에 `AnonymousMatchObservation`, `AnonymousParticipant`, `AnonymousUnit`, `AnonymousTrait`, `MetricScope`, `CompositionFingerprint`, `CompositionFingerprintBuilder`를 추가했다. 컬렉션을 읽기 전용 사본으로 보관하며 개인 식별 속성을 정의하지 않는다.

- scope 동등성은 기존 CatalogIdentity의 세트·패치·원본 버전·출처 프로필과 지역·큐·티어 구간을 모두 포함한다.
- match hash는 64자리 SHA-256 hex만 허용하고 소문자로 정규화한다. 원본 Match ID는 입력값을 오류에 노출하지 않고 거부한다.
- fingerprint는 `fingerprint-v1|` + compact JSON 배열 `[units, activeTraitIds, augmentIds]`의 SHA-256이다. 기물·성급·장착 아이템과 활성 시너지·증강을 ordinal 정렬한다. 구분자가 포함된 ID도 경계를 유지하고, 중복 기물·아이템 수를 보존한다.
- 최종 순위·마지막 라운드와 비활성 시너지는 조합 키에서 제외한다. 재생성한 동일 scope·조합은 JSON 왕복 후에도 동일하게 비교된다.
- API 클라이언트·실제 원본 응답 처리·경기 저장·집계·지표 노출은 아직 구현하지 않았다.

## 실제 검증

| 단계 | 결과 |
|---|---|
| 기존 Unity 기본 EditMode | 82 passed / 0 failed |
| RED | 테스트를 먼저 작성한 뒤 재컴파일. Analytics namespace와 예정 타입이 없어 CS0234/CS0246 발생 |
| GREEN 컴파일 | `recompile_status`: completed, failed=false, compilationFailed=false, errors=[] |
| 집중 `AnonymousObservationTests` | 32 passed / 0 failed |
| 전체 `TFTAnalyzer.Tests.EditMode` | 114 passed / 0 failed, Explicit 네트워크 테스트 제외 |
| 독립 최종 코드 검수 | Critical/Important/Minor 없음. [실제 요청·응답](../raw/Reviewer.md) |
| 보호 파일 | 사용자 volume profile 및 기존 catalog/manifest/두 메타의 SHA-256이 기존 보호 값과 동일 |

실행 명령은 `unity command run_tests --project-path C:/Unity/TFTSimulator --mode editor --filter_type testName --filter AnonymousObservationTests --timeout 300 --format json`, 전체는 `--filter_type assembly --filter TFTAnalyzer.Tests.EditMode`를 사용했다. 전체 JSON 로그는 로컬 `Library/TftAnalyzerValidation/task1-focused.json`, `task1-full.json`에 저장했다.

## 판단과 남은 범위

- 연결된 Unity와 기존 변경을 보존하기 위해 현재 codex 브랜치에서 범위를 한정했다. 위험은 같은 브랜치에 다른 변경이 있다는 점이며, Git 반영 시 파일 범위를 분리한다.
- 선행 단계는 실제 index 직렬화·파일 발행 회귀·실제 UI 기본화면을 결합한 오프라인 QA로 판정했다. 실제 네트워크 import·네이티브 모달 클릭·production index writer 전체 발행의 실제 실행은 검증하지 않았다.
- 검수자가 구분한 완전한 로비·배치 일관성, 경기 버전·알 수 없는 ID·카탈로그 멤버십은 Task 3에 남겼다. 현 모델을 단독 경기 수집 검증기로 사용하면 부적절한 관측을 허용할 수 있으므로 실제 수집 경로는 Task 3 검증 뒤 연결한다.
- API 안전성·저장 복구·중복 제거·집계·정책은 계획의 Task 2–7이다. 현재 모델 구현이 해당 기능 완료를 뜻하지 않는다.

실행 주체는 Codex이며 루트 세션의 실제 모델·추론은 별도로 확인하지 못했다. 독립 리뷰에는 `gpt-6-astra` · High를 요청했다. 사용량·총 소요 시간은 집계하지 않았다. 커밋·push·merge는 이번 보고서 기준 미수행이다.
