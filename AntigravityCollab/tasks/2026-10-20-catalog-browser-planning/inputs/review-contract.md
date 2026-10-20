# 고정 기획 검수 계약

2026-10-20. 기준 main57891e062863d0fc0eabfbc842775ea1ef79d516. 전달 자료는 이 폴더의 implementation-plan.md, completion-audit.md, review-contract.md 3개다. 두 검토자는 같은 원시바이트/해시를 사용하고 서로의 결과를 보지 않는다. 경로는 출처 정보이며 폴더 밖 파일을 읽을 권한이 아니다.

## 현재 사용자 목표

“현재 서비스의 구현 되지 않은 계획을 지침에 따라 구현해줘. CodeRabbit의 PR 검수는 5분에 1번씩 체크해주고 검수가 통과되면 병합하고 다음 작업으로 넘어가면 돼.”

새 기능의 기획은 A/B검토→통합→사용자 명시 승인 후 구현한다. PR검수5분확인·통과후병합은 이번목표에서 승인됐다. Hermes예약은삭제상태이며재등록하지않는다. 기획은 권한이나 제품통과를 대신하지않는다.

## 제품·운영 경계

- Unity Editor의 정적 카탈로그와 익명 과거 경기 분석 도구. 실제 전투/미래 예측/실시간 추천/플레이어 프로필은 범위 밖.
- 주요 기존 기능은 구현됐고 PR16은main57891e0에병합됐다. 실제 증강 분류등록0·실제MatchAPI 미검증·KI-001미해결기록 후개발계속은 유지한다. 이번 계획의 제품코드/새테스트/Unity실행은 아직없다.
- 원본 경기 응답/키/PUUID는 디스크·로그·협업자료에 기록하지 않는다. 사용자 Volume과 기존 카탈로그/아이콘/익명관측을 보존한다.
- 정적 기록의수록·이름·설명은정책분류나해당세트등장가능성의인증이아니다. 실제미분류/Legend/표본100하한차단은그대로다. 탐색화면은정적자료열람이다.
- 기존 지문별 집계는 매우 세밀하고 표본확보는별도검증이필요하다. 이번 화면이통계유용성/실데이터검증을완료했다고주장하지않는다.
- 창검증은 기존창/포커스기록·소유객체만finallyClose·native프레임대조를따른다. 기존사용자Inspector재사용/이동/일괄종료금지. 새화면은실제PNG검증이필요하다.
- 신규에이전트생성없이기존담당재사용. root가구현/Unity/통합을맡고코드검수·QA는독립읽기전용역할이다. 역할표가모든역할새호출을요구하지않는다.

## 기존 코드의 확인된 연결 지점

- DataManagerViewModel.TryGetActiveSelection(out StaticCatalog catalog, out CatalogIdentity identity)가 검증된 선택을 제공한다.
- 창은 `_dataManager.Changed`에서 SyncCollectionCatalog로 선택을수집/분석에연결하고CreateGUI에서binding을Dispose/재생성한다. 새VM도같은선택에연결해야한다.
- 현재 UXML data-tab은입력/버전확인/카탈로그행/활성선택/패치diff뿐이다. 분석dropdown은통계행필터이며원본상세탐색이아니다.
- UnitRecord: StableId, LocalizedText Name/Description, Cost, Role, TraitIds, Effects, IconPath, Sources.
- TraitRecord: StableId, Name/Description, Effects, IconPath, Sources. 활성구간별도구조는없으므로없는정보를추정하지않는다.
- ItemRecord: StableId, Name/Description, ComponentIds, Effects, IconPath, Sources.
- AugmentRecord: StableId, Name/Description, Tier, RelatedIds, Effects, IconPath, Sources. 개별정책분류필드없음.
- LocalizedText는KoKr/EnUs/DisplayValue/UsedEnglishFallback을제공한다. DisplayValue는한국어공백이면영어로대체한다. Sources에는소스명/URL/조회UTC/ETag/SHA256/보완여부가있으나legacy정규화본문시각은epoch일수있다. 실제수집시각은standalonemanifest와구분한다.

## 응답

기획A:제품범위/사용자가치/정책/위험. 기획B:기술/예외/검증/완료조건. 각자최대3개의유효findings와verdict(pass/revise/blocked), 미확인사항을반환한다. 모델/추론/사용량은실제로확인가능한것만기록한다. 코드/네트워크/Unity실행은하지않는다.
