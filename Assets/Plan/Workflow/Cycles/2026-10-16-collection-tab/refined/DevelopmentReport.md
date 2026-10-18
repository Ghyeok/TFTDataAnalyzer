# 경기 수집 탭 개발 결과

갱신일: 2026-10-18
상태: 코드 검수·실행 검증·독립 QA·문서 대조 통과, 통합 커밋 완료·PR 검수는 별도

## 변경

승인된 Phase3 Task3을 구현했다. 수집 탭은 데이터 관리에서 검증한 Catalog+전체 Identity 쌍을 받아 플랫폼·큐·티어 기록 라벨·상세 시도 상한을 확인하고 기존 bounded 수집 서비스를 호출한다. 키·seed는 설정 여부만 표시한다. 한 client를 유지해 재실행 사이에도 호출 제한 상태를 보존한다.

입력 초안과 승인 요청을 구분하고, 확인 대기·실행·취소 동안 실행 snapshot을 고정한다. 창 재생성은 VM을 보존하고 binding만 교체하며 닫기는 취소·구독 해제를 수행한다. 최종 요약이 늦은 취소보다 우선하고 종료 후 진행 알림은 무시한다. 401/403/429·부분 실패·탐색 상한을 안전한 문구와 숫자로 표시하며 429는 수동 재시도 UTC만 안내한다.

실제 schema1 `Manifest.Identity`가 null인 계약을 반영해 검증 row identity를 별도 전달한다. 누락·프로필·패치·원본 버전·세트 번호 불일치를 거부하며 schema2도 함께 검증한다. 생성 데이터나 backend 계약은 변경하지 않았다.

사용자 지적에 따라 구현 계획을 독립 재검토하고, 테스트 소유 창 scope·finally 정리·native 프레임 비교를 추가했다. 두 UI binding은 Inspector에 의존하는 delayCall 대신 일회성 Editor update를 사용한다. startup scheduler는 이번에 구현하지 않는다. 수동 캡처는 추적 가능한 한 작업과 셸 fallback으로 묶고, PNG 시각 검수를 JSON 결과와 구분한다.

## 실행 근거

아래 파일은 Git 제외 `Library/TftAnalyzerValidation/`의 실제 실행 산출물이다. 사라질 수 있으므로 주요 결과를 이 문서와 QA에 남긴다.

| 구분 | 결과 | 산출물 |
|---|---|---|
| 최초 TDD | 의도한 RED8 → 초기 집중43/전체453 통과 | collection-red-tests.json, 초기 green 결과 |
| 입력 재생성 결함 | 실제 UI RED2 → 수정 후 집중45/전체455 | collection-rebuild-red.json |
| 정리 도구 수정 | 초기 RED4, UI78/82→81/82 조사 후 update·Close 수정으로81/81·전체458 | collection-render-final-ui.json, collection-cleanup-full.json |
| legacy 소비 계약 | 집중54 중 실제 RED5 확인 → 수정 후 UI90/90 | collection-legacy-red.json, collection-legacy-final-ui.json |
| 2026-10-18 컴파일 확인 | up_to_date, failed=false, compilationFailed=false, errors=[]; 새 컴파일은 필요 없었음 | collection-legacy-final-compile.json |
| 최신 전체 | 467/467, 실패·skip·inconclusive0; UI90 포함 | collection-legacy-final-full.json |
| 전체 테스트 창 | 기존9개·native1개 전후 ID 동일, 추가·누락0, 포커스 복원 | collection-legacy-full-windows-check.json 및 before/after |
| 실제 포함 카탈로그 화면 | 940px·500px PNG 모두 대상 UI·전체 identity·고지 확인, 작은 창 스크롤 확인 | collection-visual-final.json, collection-visual-inspection.json |
| 수동 정리 | 정상·표시 전 실패·표시 후 실패에서 소유 창0, 기존 창·프레임·포커스 보존, 셸 fallback0 | collection-visual-final.json, collection-visual-initialization-failure-cleanup.json, collection-visual-failure-cleanup.json, fallback JSON |
| 보호 | Volume 및 생성 데이터1,386개 SHA256 변경0 | collection-protected-check.json |
| Console | 컴파일 실패false·경고0, 이전 Pipeline 통신 오류2건, 최종 cursor 이후 새 경고·오류0 | collection-console-final.json, collection-console-delta.json |

초기 RED와 해결된 회귀·검증 도구 오류를 현재 실패 수로 합산하지 않는다. 이전 두 UI 실행의 빈 프레임4개는 정확한 소유·활성 창 연결 없음을 확인해 닫았으며, 기존 사용자 창을 일괄 종료하지 않았다. 현재 실행의 ID는 당시 값과 다르므로 과거 ID를 재사용하지 않는다.

## 제한과 후속

- 실제 Riot 네트워크·실제 자격 증명·네이티브 확인창 클릭은 미실행이다. 모듈 경계는 fake transport→실제 익명화/저장으로 확인했다.
- 과거500px PNG의 다른 화면 캡처 원인을 해결했다고 주장하지 않는다. 이번 PNG는 직접 검수했고 앞으로도 각 이미지를 확인한다.
- 정상/통제된 초기화·표시후 실패의 정리 증거를 OS crash·연결 끊김·모든 timeout까지 확대하지 않는다.
- 비차단 P3: 선택된 카탈로그 라벨과 별개로 Idle 기본 상태 안내가 카탈로그 선택을 요청할 수 있다. 시작 가능 여부·확인·요청은 올바른 identity를 사용한다. 후속 문구 개선으로 기록한다.
- 분석·집계 자동 갱신·CSV·정책 설정·빌드 gate·startup 확인·Hermes 일일 자동화는 이번 범위 밖이다.

모델은 기획/구현에 Sol/High, 데이터 무결성 검수에 Astra/High, 최종 QA에 Sol/High를 요청했다. 실제 런타임 모델·추론·토큰 사용량은 독립 확인 정보가 없다. 이번 재개에서는 제품 코드 수정 없이 전체1회, 정상 캡처1회, 실패 probe2회를 실행했다. 전체 테스트의 기록된 duration은54.71초이며 과거 작업의 총 소요 시간·비용은 확인하지 못했다.
