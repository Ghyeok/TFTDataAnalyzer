# Task6 별도 QA

2026-10-19. 기준 ab63a3c, codex/startup-e2e 작업 트리. 요청 모델 GPT-6.1 Sol/Medium; 실제 backend 모델·추론·개별 사용량은 이 QA에서 독립 확인 불가. 독립 코드 검수와 별도 역할로 지정 source/diff/artifact만 읽었다. Unity·네트워크·Git 쓰기·Hermes·추가 agent 실행 없이 이 보고서만 작성했다.

## 판정: 수정 필요 — 완료 조건 검증 blocker 1건

**P2 / 증거 불충분: 실제 새 EditorWindow에서 시작 안내 복원 검증 누락.** `Assets/TFTAnalyzer/Tests/EditMode/UI/TftDataAnalyzerEndToEndTests.cs:184,272`는 기존 window 객체의 `CreateGUI()`를 다시 호출한다. `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticVersionCheckSchedulerTests.cs:214`는 scheduler 재생성·캐시 복원만 검증하며 창이 없다. 기존 `TftDataAnalyzerWindowTests.cs:99`의 다른 instance는 선택 탭 직렬화 복원만 검증한다. 따라서 ImplementationPlan §1의 실제 창 재생성 및 §2의 닫기/재생성 구독 해제를 함께 실행했다는 증거가 아니다. `TftDataAnalyzerWindow.cs:151`의 OnDisable은 VM·구독을 해제하므로 동일 객체 GUI 재구성과 다른 경계다. 제품 고장은 확정하지 않는다. 최소 조치: 소유 기존 창을 닫은 후 새 소유 EditorWindow와 새 VM을 GUI 생성 전에 구성하고, 24시간 내 캐시 안내 복원·추가 HTTP 0·수동 동일 버전 import 후 안내 해제·이전 창 구독 해제 및 finally 소유 창/native/focus 정리를 실행한다. 해당 집중 회귀와 최종 suite 결과로 재판정한다. 새 제품 범위는 필요 없다.

## 직접 대조한 통과 근거

- `task6-final2-suite.json`: outer/inner success, 615개/614통과/0실패/1제외. Phase1 identity/공식 우선·opt-in/validator·immutable·복구, Phase2 익명화·중복·상한·범위·집계·Wilson·CSV, Phase3 실제 binding·정책·빌드 차단·금지 표면 테스트 그룹의 실행 결과를 확인했다. 컴파일 completed/failed=false/compilationFailed=false. 공개 버전 명시 smoke 1/1, scheduler 20/20, index 11/11, 실제 E2E 4/4를 원본 JSON과 대조했다.
- 실제 E2E source는 승인 취소 시 전송/게시 없음, 실행별 CommunityDragon reset, 실제 pipeline/AssetDatabase 재로드·index 완료 UTC, 동일 import/catalog bytes 및 aggregate 재생성 bytes 불변을 단언한다. 실제 ObservationStore를 다시 읽어 2경기·범위를 확인하고 저장 루트 전체에서 가짜 키/seed·원본 match/player 식별자 미저장을 검사한다. 기본 AnalysisService의 unclassified_augment PolicyBlocked, 증강 없는 행 InsufficientSample, 별도 신뢰 metadata 합성 99/100을 분리한다. 실데이터 분류 공급이나 실데이터 100건 달성 증거는 아니다.
- e2e/analysis 최종 JSON 각 Passed=true·6 PNG, 초기화 전/표시 후 예외 probe와 양쪽 fallback 정리 성공. 전후 window 9개 ID와 container 1개 ID 동일·focus 0 동일, 소유 잔류0. 보호1386개 변경0·E2E 자산 폴더잔류0. 실제 창/native 보존 증거로만 사용한다.
- 지정 500px PNG 4장을 직접 열어 identity(TFTSet18/18.2/16.18.1/official-only), 수집2/저장2, 표본 부족/비율 억제, 합성 상대배치100/100·95%96.3–100.0%, 고지와 스크롤을 확인했다. 일부 좌측/하단 문자 겹침 흔적이 있고 큰 캡처의 알려진 겹침 원인도 미확인이므로 전체 픽셀 무결성 통과로 확대하지 않는다. 나머지8장의 픽셀은 이 QA가 직접 확인하지 않았다.
- Antigravity manifest/digest/result/review/runtime은 실제 A 요청 SUCCESS·exit0·toolAttempts0·21,402tokens/57.353초를 기록한다. 새 Python entry는 schema·경로/하드링크 검증을 로컬에서 수행한다. 오프라인 3method 통과는 fake CLI events 검증이며 실제 A 호출·강제 OS/tool 격리와 구분한다. 독립 검수 P2 세 건 종결 기록과 후속 index fixture 분리 11/11을 대조했다.

## 유지할 한계와 다음 게이트

symlink 테스트1개는 host 권한 때문에 제외되어 통과가 아니다. Console cursor193→227의 새 오류2개는 Pipeline transport/header stack이며 제품 예외로 보이지 않지만 전체 오류0이 아니다. 초기 오프라인 소유 directory5개 정리는 미확인이다. CLI accessRestrictionVerified=false, 실제 Match API/개인 seed·외부 등록 승인·네이티브 확인창 클릭·실데이터 증강 분류와 100건 달성은 미검증이다. 실패 probe는 모든 timeout/연결단절 경로의 증명이 아니다.

README/CompletionGateAudit 갱신은 root 진행 중이므로 이 스냅샷의 '대기' 문구를 확정 결함으로 세지 않았다. 새 창 회귀 후 최신 실행값과 한계를 최종 문서에 반영해야 한다. PR·최신 외부 검수/미해결/CI·병합·사용자 최종 승인은 아직이며 QA만으로 전체 계획 완료를 선언할 수 없다.

## 최종 후속 판정: 조건부 통과 — QA blocker 0건

2026-10-19. 앞선 판정은 이 절로 대체한다. 요청에 따라 새 회귀 source와 아래 지정 artifact만 후속 대조했으며 전면 재검수·Unity 재실행·추가 가설 검토는 하지 않았다. 앞선 파일줄은 당시 스냅샷 위치다.

- `TftDataAnalyzerEndToEndTests.cs:31-109`의 `NewWindowRestoresCachedNoticeWithFreshModelsAndNoAdditionalHttp`는 실제 first.Close 후 다른 instance ID의 second를 만들고 fresh Data/Collection/Analysis VM·scheduler를 GUI 생성 전에 주입한다. 1시간 후 저장 캐시로 16.18.2/16.18.1 안내를 복원하며 HTTP 총1을 유지한다. 두 창 각각 scheduler Changed의 정확한 window target 구독/닫기 후 해제를 단언하고 OwnedEditorWindowScope 및 임시 루트를 finally 경로에서 정리한다. 기존 실제 E2E의 동일 버전 import 후 안내 해제 증거와 함께 누락된 새 창 경계를 보완하므로 P2 종결이다.
- `task6-new-window.json` outer/inner success·1/1, `task6-final3-suite.json` outer/inner success·616개/615통과/0실패/1환경 제외·111.18초이며 새 회귀도 Passed다. `task6-window-compile-status.json` completed/failed=false/compilationFailed=false다. 기존 615개 결과 대신 이 최종값을 사용한다.
- `task6-final3-windows.json`의 window ID9개·container50610·focus0은 앞서 대조한 기존 baseline과 동일하다. `task6-final3-protected.json` 보호1386개 변경0·E2E 자산 폴더0이다. `task6-final3-console.json`은 동일 session cursor227→241에 entries[]·dropped=false·reset=false·compilationFailed=false로 추가 오류0이다. 앞선 cursor193→227 Pipeline 오류2개 이력은 삭제하거나 오류가 없었던 것으로 바꾸지 않는다.
- Antigravity `outputs/offline-cleanup.json`은 정확한 소유 경계·reparse 거부·empty-only 삭제 방식, 기존 소유5개 이름, verified=true/remaining[]를 기록한다. 따라서 초기5개 정리 미확인 상태는 이 후속 증거로 해소됐다. 강제 OS/tool 격리 미검증은 그대로다.

제품 불변이므로 기존 수동 JSON/PNG 대조 결과를 재사용하며 재촬영·새 픽셀 완전성 검증을 주장하지 않는다. host symlink 제외1·큰 PNG 문자 겹침 원인 미확인·선택적 실데이터/등록 승인 미검증 한계를 유지한다. 조건부는 남은 제품 수정 blocker를 뜻하지 않으며, root의 최종 문서에 최신 실행값/한계를 반영하고 PR·최신 외부 검수/미해결/CI·병합·사용자 최종 승인 게이트를 별도로 수행해야 한다는 뜻이다. QA 자체로 전체 계획 완료를 선언하지 않는다.
