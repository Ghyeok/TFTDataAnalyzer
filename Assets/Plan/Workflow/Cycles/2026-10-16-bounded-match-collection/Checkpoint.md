# 제한된 경기 수집 체크포인트

날짜: 2026-10-16
상태: Task 7 구현·실제 Unity366/366·독립 검수 지적 보완 완료. PR 검수·병합과 사용자 최종 승인은 별도다. 프로젝트 물리 폴더 이동은 아래 잠금 때문에 대기한다.
승인 근거: 다음 작업을 수집 서비스로 안내한 뒤 사용자가 "좋아 이제 다음 작업 진행해보자"라고 요청했다.

- 실행 계획은 [Phase 2 Task 7](../../../Implementation/2026-09-17-tft-match-analytics.md#task-7-orchestrate-bounded-collection-from-user-supplied-seeds) 한곳에서 관리한다.
- 기준 main `eb8f5d7`: PR #7은 최신 `57bb21d` 검수 SUCCESS·미해결 thread 0·사전 검사5개 통과, 재개 시 Unity340/340 통과 후 병합했다.
- 작업 브랜치 `codex/bounded-match-collection`. 연결된 Unity 프로젝트에서 기존 작업 방식을 유지하며 사용자 Volume 변경은 제외한다. 별도 checkout은 만들지 않았다.
- 사전 인터페이스 확인: RiotApiClient의 caller-owned Stream → MatchAnonymizer → 승인된 Observation → ObservationStore. 요청은 전체 CatalogIdentity에 묶으며 서로 다른 TFT 패치라도 같은 원본 릴리스면 같다고 추정하지 않는다.
- 판단: 고유 경기 상한만으로 중복된 ID 페이지의 무한 조회를 막을 수 없다. 실행당 ID 페이지도 최대10회(각100개), seed 최대100개로 제한하고 스캔 상한 도달을 숫자/불리언 결과로 알린다. 후속 실행으로 더 수집할 수 있으며 전수 수집을 보장하지 않는다.
- 판단: requested는 고유 경기 상세 요청 시도 수, fetched는 응답 스트림 획득 수다. duplicate는 실행 중 중복 ID 및 저장소 중복을 센다. failed는 실패한 목록/경기/저장 작업 수이며 filtered는 범위 불일치·알 수 없는 정적 ID의 경기 수다.
- 이번 범위에는 UI·집계 자동 갱신·실제 Riot 호출·실제 키/seed 조회가 없다. 네트워크 확인은 Explicit 테스트로만 정의하고 기본 실행에서 제외한다.
- 모델·추론·사용량: 루트 런타임 상세는 독립 확인 불가. 기록 가능한 실제 테스트 결과만 완료 증거로 사용한다.
- 실행 증거: 신규 타입 부재 RED → 컴파일 GREEN → 집중26/26 → 이름 변경 회귀366/366. [개발 보고서](refined/DevelopmentReport.md), [QA](refined/QAReport.md), [독립 검수](raw/Reviewer.md)를 확인한다.
- 후속 사용자 요청: 프로젝트 표시 이름·Windows Store 메타데이터를 TFTDataAnalyzer로 변경하고 Markdown23개 파일의 이전 명칭을 정리했다. 기존 이름 기대 테스트를 갱신했다.
- 실제 폴더 이동은 현재 작업 Codex 프로세스의 디렉터리 잠금으로 Windows가 거부했다. Unity는 정상 종료할 수 있고 미저장 scene은 없었다. Codex를 강제 종료하지 않는다. 현재 물리 폴더 이동을 완료했다고 주장하지 않는다.
- Git 제외 `Library/TftAnalyzerValidation/rename-project-after-codex.ps1`은 Codex 프로세스 종료 후 정확히 승인된 두 경로만 이동하고 솔루션·로컬 MCP 경로 갱신, 보호 해시 확인, 새 경로의 전체 EditMode 검증 및 Unity 열기를 수행하도록 준비했다. 구문·DryRun 확인만으로 이동/새 경로 테스트 성공을 주장하지 않는다. 실행·대기 상태와 최종 결과는 `project-rename-result.json`에서 확인한다.
- 다음 재개 시 우선 실제 경로·위 이동 결과 JSON·최신 PR 검수부터 확인한다. CodeRabbit 대기 반복·자동 AI 재개는 하지 않는다. 새 PR 검수 통과 후 다음 제품 작업은 Phase 3 통합 Editor UI다.
