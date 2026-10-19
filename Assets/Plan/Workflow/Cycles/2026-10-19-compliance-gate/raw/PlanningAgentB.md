# 기획 B 공개 기록

2026-10-19. 요청 모델 GPT-6.1 Sol / Medium, fork_turns none. 작업명 `/root/compliance_plan_technical`.

## 실제 요청

Task5 기획 B: 기술 구조·검증 가능성 독립 읽기 전용 검토. 작업 루트 C:/Unity/TFTDataAnalyzer. PR12가 6a9027c에 병합됐고 Task5 기획만 준비하며 제품 코드는 승인 전이다. 아래 파일만 읽고 한 번에 결과를 반환하라. 자료: Assets/Plan/Workflow/Cycles/2026-10-19-compliance-gate/refined/ImplementationPlan.md; Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md Task5(252~300줄); Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs의 탭 연결/해제 부분; Assets/TFTAnalyzer/Editor/Persistence/AnalyzerStorePaths.cs. 파일 수정/Unity/네트워크/Hermes/추가 에이전트 호출 금지. 과거 raw·보고서·세션 기록을 읽지 말 것. 빌더 관점으로 연결 계약의 모순, 실패 시 배포 차단, 읽기 부작용, 명시적 저장, 설정 변경 반영, 기존 CSV 보존과 테스트 가능한 완료 조건을 검토하라. 기존 경로 보호 재사용으로 충분한지와 새 복잡성/불필요한 범위를 지적하라. 결과는 1) PASS 또는 수정 필요 2) 필요한 지적 최대5개(계획 행번호와 최소 수정) 3) 핵심 테스트 조건 4) 실제 확인 가능한 모델/추론만 보고. 900자 내외로 간결히. 후속 대기 루프 금지.

## 받은 공개 응답

1) **수정 필요** — 범위와 분리는 적절하나 연결 계약 3곳을 명확히 해야 한다.

2) 필요한 수정
- **52·58~59행:** `AnalyzerStorePaths` 생성자는 경로 접근 오류를 던질 수 있고 `EnsureSafe()`는 `compliance.json` 자체를 검사하지 않는다. `Load()` 내부에서 생성·접근 실패를 `Unavailable`로 변환하고, 기존 `RejectReparsePoints`를 설정 파일·임시 파일·교체 직전 대상에 직접 적용한다고 명시한다. 새 경로 보호 체계는 불필요하다.
- **53·65~68행:** 정책 판단은 설정만 받고 읽기 상태는 받지 않는다. 빌드 허용은 `ReadResult.Valid && CanPublish(...)`이며 나머지는 모두 `BuildFailedException`으로 변환한다고 명시한다. 기본 생성자를 사용하는 Unity 콜백과 주입형 테스트가 같은 경로를 호출해야 한다.
- **75·79행:** 현재 `SelectTab()`은 표시만 바꾸므로 설정 변경 후 기존 About 재진입이 자동 재조회되지는 않는다. About 선택 시 `Refresh()`로 재조회하고, 바인딩은 `RebuildUI`와 `OnDisable` 모두에서 해제하도록 계약을 적는다. 주기 갱신은 필요 없다.

3) 핵심 테스트 조건
설정 없음·손상·링크·접근 실패에서 빌드 차단, 읽기 후 파일/폴더 생성 없음, 저장 실패 후 기존 파일·표시 보존, 확인/철회 직후 실제 콜백과 About 재진입 반영, 재생성 후 중복 구독 없음, 세 등록 상태 모두 기존 개인 연구 CSV와 억제 필드 유지. 미래 확인 시각도 거부 조건에 명시하면 좋다.

4) 지정 자료만 읽었다. 수정·Unity 실행·네트워크 호출은 없었다. 실제 모델 식별자와 추론 설정은 이 실행에서 확인할 수 없어 보고하지 않는다.

행번호는 검토 당시 초안 기준이다. 이후 통합 결과는 정제 계획을 따른다.
