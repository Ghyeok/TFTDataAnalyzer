# Task6 독립 코드 검수 요청

2026-10-19. 요청 모델 GPT-6 Astra / High. 기존 자산 쓰기 경로 격리와 손상·취소 복구의 실패 영향 때문에 이 검수에만 적용한다. 실제 모델·추론은 실행 metadata로 별도 확인한다.

## 실제 발송할 메시지

`Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/raw/CodeReviewRequest.md`를 읽고 Task6 변경을 읽기 전용 검수하라. 기준 ab63a3c 대비 diff·새 파일과 지정된 최신 실행 증거만 확인한다. Unity·네트워크·Git 쓰기·Hermes·추가 에이전트는 사용하지 않는다. 결과는 `Library/TftAnalyzerValidation/task6-code-review.md`에 저장하고 1500자 이내로 요약하라. 발견 사항은 우선순위/정확한 파일·줄/재현 조건·영향/최소 수정안을 제시하고 실제 실행하지 않은 것을 통과했다고 쓰지 않는다.

## 입력과 검수 계약

- 정본은 `refined/ImplementationPlan.md`다. 다른 사이클·세션·개인 키/seed/원본 경기 데이터는 읽지 않는다. 작업 지침 색인의 필요한 절차와 코드 검수 지침만 적용한다.
- 최신 실행 증거는 root가 전달한 `Library/TftAnalyzerValidation/task6-*.json` 중 지정한 최종 파일만 본다. 파일 전체 테스트 목록을 응답에 복사하지 않는다.
- 두 필드 원자 상태, 예약 전/후 취소, 손상/미래/IO 실패, 정확히24시간, 동시 Task, 유한 HTTP 기한과 재로드 뒤 늦은 완료, 구독·CTS·예약 정리를 확인한다.
- Unity 공개 TestRunner callback 연결의 배치/재생/테스트/리로드/오류/quit 경계, 무한 delayCall 반복 없음, 테스트 중 자동 HTTP 방지와 일반 기능 유지가 같은 경로인지 확인한다.
- index/canonical/legacy/icon/raw/staging/recovery가 동일 주입 경로를 쓰는지, default Generated/cache/index로 fallback하지 않는지 확인한다. traversal·장치 경로·reparse·링크의 부모 검사와 루프 종료, exact 소유 경로/GUID 정리, SaveAssetIfDirty 대상 한정 저장을 특히 검수한다.
- 완료 시각은 index에만 저장하고 읽기 시 migration하지 않는지, 최신 성공 기록/legacy fallback/동률/롤백/동일 import 출력 bytes 보존을 확인한다.
- 실제 DataManager→pipeline→AssetDatabase 저장·재로드→collection→익명 관측→집계→분석 binding이 연결되는지 확인한다. GUI 생성 전 fake configuration 주입, 각각 승인/취소, opt-in reset, two-match 표본 억제, 식별자 미저장/집계 byte 안정성, finally 창·fixture 정리를 확인한다.
- 금지 표면 검사가 제품 public 타입/메뉴와 실제 UI copy를 검사하는지 확인한다. UXML뿐 아니라 동적 C# UI 문자열·실제 화면도 포괄하고, 허용 부정문 뒤 긍정 주장 추가가 탐지되는지 본다.
- 수동 도구 AgentScripts/TftAnalyzerEndToEndVisualValidation.cs와 wrapper의 단일 소유 창, PID별 HWND 차이, deadline/finally, 정확한 ID/name fallback, 사용자 창 보존 계약을 확인한다. 캡처 PNG는 root의 직접 시각 검수와 구분한다.
- Antigravity 기획 연동은 `AgentScripts/Invoke-AntigravityPlanningReview.py`와 `AntigravityCollab/README.md`, 현재 task의 manifest/result/runtime만 검토한다. 입력 해시·경로·크기 제한, 응답 식별/형식, 실패 시 성공 오인 방지, 소유 프로세스 정리와 강제 접근 제한 미검증 고지의 정합성을 확인한다. 실제 A 실행과 일반 진입점 dry-run을 구분하고 추가 유료 호출은 하지 않는다.
- 새 범위를 제안하지 말고 필수 계약 위반·실제 회귀만 지적한다. 모든 코드는 그대로 보존하고 보고서만 작성한다.
