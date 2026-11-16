# 실제 경기 분석 지원 체크포인트

2026-11-16 1단계 병합(현재 위치): PR #38의 CodeRabbit 1차 검수(`79d88c9`)가 Minor 2건(벽시계 기반 요청 간격, 링크된 저장소 루트)을 냈고 `a6ad2b3`에서 반영했다(단조 `Stopwatch` 간격, 저장소 루트·상위 경로의 reparse point 거부). 구현 세션 기록은 Diagnostics 105/105, 회귀 161/161이다. 최신 head 재요청은 한 번 사용량 제한으로 처리되지 않았고, 다시 요청해 79d88c9..a6ad2b3 검수에서 새 지적 0건, 두 스레드 해결, CodeRabbit 체크 SUCCESS를 확인했다. 검수 head와 일치할 때만 병합하도록 지정해 main `965077d`에 병합했다. 병합 확인 중 테스트는 재실행하지 않았다. Notion [12. 실제 경기 형태의 비식별 진단 도구](https://app.notion.com/p/41a55d93ee23817e8fc2d1d040968850)를 작성했다. 다음은 TODO 9: 사용자가 API 키 재설정을 알리면 진단을 한 번 실행하고 분류·저장소 파일 트리 해시를 확인한다.

2026-11-16 독립 검수 반영: 독립 코드검수(Claude 서브에이전트 `opus`·high)가 P1 1·P2 1·P3 7건을 냈다([raw/Reviewer.md](raw/Reviewer.md)). P1(1ms 미만 대기 버림으로 간격 루프가 정지 판정 → 예산 소진으로 오분류)은 Master가 코드로 확인했다. P2(단독 IPv4 앞 두 그룹 원값 노출)는 계획 내 규칙 충돌을 개인정보 우선으로 정리해 계획을 정정했다(앞 두 그룹 1~2자리일 때만 원값, IPv4 형태면 자릿수만). 같은 구현 에이전트가 커밋 2개(`8634d36`, `2aacee0`)로 8건을 반영했고(P3-6은 TODO 9의 수동 파일 트리 해시 대조로 보완), RED 101 중 21 실패 후 GREEN Diagnostics 101/101, 회귀 161/161(`MatchAnonymizerTests` 61, `RiotApiClientTests` 60, `MatchCollectionServiceTests` 40, Explicit 제외)이다. 모든 실행에서 Riot 환경 변수를 제거했고 XML에 Explicit 테스트 실행 기록이 없음을 Master가 확인했다. 전체 EditMode는 변경이 `Editor/Diagnostics/`에 한정돼 재실행하지 않았다(직전 전체 1,438/1,434/0/4). 같은 검수자가 수정 커밋(`3b83f0f..2aacee0`)을 재검수해 P1·P2 없음, P3 2건(글자가 붙은 IPv4 감지 한계를 계획에 기록, 계획 문서를 코드와 함께 커밋)을 냈고 둘 다 반영했다. 다음은 push·PR·CodeRabbit 요청이다.

2026-11-16 구현 완료·독립 검수 중: 작업트리 `Library/wt/ki003`(main `320e632`, 체크아웃 시 줄바꿈 변환 끔), 브랜치 `claude/ki003-shape-diagnostic`에서 구현 서브에이전트(Claude `opus`·high, 약 40분, 토큰 338,809, 도구 104회)가 TODO 1~7을 TDD로 구현해 로컬 커밋 5개(`f30bd82`~`3b83f0f`, 39개 파일 +3,582)를 만들었다. 새 진단 테스트 88개가 통과했고, 회귀는 `MatchAnonymizerTests` 61/61, `IncompleteMatchExtractionTests` 39/39, `RiotApiClientTests` 60/60, `MatchCollectionServiceTests` 40 통과·1 Explicit 제외다. 공용 파서 가시성 변경(`StrictReader`·`StrictSyntaxReader`·`BoundedInput` internal) 때문에 전체 EditMode도 실행해 1,438개 중 1,434개 통과·실패 0·제외 4였다. 짧은 경로와 LF 체크아웃으로 E2E와 `LegacyCatalogResolverTests`도 통과했다(KI-004 미재현). TODO 8(독립 코드검수·CodeRabbit)은 진행 중, TODO 9는 키 재설정 알림 대기.

**승인 범위 이탈 1건**: 구현 에이전트가 기준선 확인용으로 네임스페이스 필터(`TFTAnalyzer.Tests.RiotApi` 등)를 실행해 `[Explicit]` 테스트 `MatchCollectionServiceTests.ConfiguredSeedCanCallOneIdPage`가 선택됐고, 사용자 환경 변수의 키·seed로 실제 목록 요청 1회(asia, count=1)가 나가 401로 실패했다(약 06:19Z). 받은 데이터는 없고 키 값은 읽거나 출력하지 않았다. 이후 실행은 `RIOT_API_KEY`·`RIOT_TFT_SEED_PUUIDS`를 제거하고 클래스 필터만 썼다. PR #34에 이은 같은 유형의 두 번째 사고다. 이 요청은 1단계 진단 실행(TODO 9)이 아니다.

2026-11-16 승인: 사용자가 1단계 구현 계획 3판을 승인했다. 단계를 개발로 전환한다. 실제 실행(TODO 9)은 사용자의 키 재설정 알림 후에만 한다. 먼저 기획 기록과 기획 B 역할 변경 지침을 문서 PR로 반영하고, 최신 main에서 짧은 경로의 격리 작업트리를 만들어 TODO 1~8을 TDD로 구현한다.

2026-11-16 재검토 완료·승인 대기: 사용자 결정으로 실제 실행은 N=20, 사용자가 만료된 API 키를 재설정했다고 알린 뒤에만 한다. 수정안 2판을 A·B에 재검토받아 A pass, B revise(P1 1·P2 4·P3 1)였다. 반영한 3판을 B가 pass(P3 3건)로 확인했고 P3도 반영했다. A의 3판 재확인은 하지 않았다. 승인 대상은 [ImplementationPlan.md](refined/ImplementationPlan.md) 3판이다. 제품 코드 변경 없음, 실제 API 호출 없음. 재개 시 첫 행동: 사용자 승인 여부를 확인하고, 승인되면 격리 브랜치에서 TODO 1~8을 TDD로 구현한다(TODO 9는 키 재설정 알림 후).

2026-11-16 기획 통합: 기획 A(Antigravity, `gemini-4-argon-medium`, SUCCESS 95초)와 기획 B(Claude 서브에이전트 `opus`·high, 313초)가 모두 revise를 반환했다. 원문은 [raw/PlanningAgentA.md](raw/PlanningAgentA.md), [raw/PlanningAgentB.md](raw/PlanningAgentB.md). 통합 결과 [PlanningReview.md](refined/PlanningReview.md)와 1단계 수정안 [ImplementationPlan.md](refined/ImplementationPlan.md)를 작성했다. 방향(형태 진단 우선)은 유지하고, 1단계를 수집 경로와 분리된 진단 클래스·전송 예산 래퍼·비식별 서명·집계 보고서로 다시 설계했다. 사용자 결정 대기: 실제 1회 실행 허용과 경기 수 N(권장 20), 수정안의 A·B 재검토 여부. 2단계 방향(C/D)·증강 지표 가치는 진단 후 확정 가능. 제품 코드 변경 없음. Antigravity CLI는 실제 응답 생성까지 확인했고, `agy\bin`을 `.claude/settings.local.json`의 `additionalDirectories`에 추가했다(개인 설정, 커밋 안 됨). 재개 시 첫 행동: 사용자 결정을 계획에 반영하고, 재검토를 택하면 A는 새 동결 입력으로, B는 같은 서브에이전트에 수정안을 보내 확인받는다.

2026-11-16 기획 시작: 사용자가 KI-003 후속인 실제 경기 저장·분석 지원 진행을 지시했다. 같은 날 기획 B 담당을 GPT(Codex)에서 Claude로 바꾸도록 지시해 `AGENTS.md`, 워크플로 README, Antigravity 협업 README, 지침 색인을 수정했다(미커밋).

- 단계: 기획. 기획 A/B 독립 검토 진행 중. 제품 코드 변경 없음.
- 승인된 범위: 기획 초안 작성과 A/B 검토까지. 1단계 진단 구현, 실제 Riot API 호출, 2단계 설계·구현은 미승인이다.
- 기준: main `ba49c69`. 작업 트리에는 `Assets/DefaultVolumeProfile.asset`(사용자 변경, 건드리지 않음)과 이번 기획·지침 문서 변경이 있다.
- 기획 초안: [PlanningDraft.md](refined/PlanningDraft.md). 권장안은 형태 진단(1단계) 후 결과에 따라 2단계(정상 경로 확장 또는 별도 참고 분석)를 따로 기획하는 것이다.
- 기획 A: Antigravity CLI 1.5.9, 모델 목록 조회 정상, 요청 모델 `gemini-4-argon-medium`. 동결 입력 `AntigravityCollab/tasks/2026-11-16-real-match-planning/` 5개(inputDigest `7b43ab44…`, 20,044바이트), dry-run 통과 후 실제 요청 전송.
- 기획 B: Master와 분리된 Claude 서브에이전트(Opus, effort high), 같은 동결 입력과 기준 커밋 코드 읽기 전용, A 결과 열람 금지.
- 변경하면 안 되는 것: 기존 관측·불완전 기록·카탈로그·인덱스, `Assets/DefaultVolumeProfile.asset`, API 키.
- 재개 시 첫 행동: A의 `outputs/review.json`·`runtime.json`과 B의 결과를 확인해 `raw/PlanningAgentA.md`, `raw/PlanningAgentB.md`에 기록하고 통합 검토를 작성한다.
