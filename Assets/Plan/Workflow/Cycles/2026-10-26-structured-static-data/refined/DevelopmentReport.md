# 구조화 정적 데이터 개발 기록

상태: 진행 중

2026-10-26 사용자가 call_2208e223ba4c465ab9e566d320436868에 ‘이 계획대로 구현 진행’으로 통합 절까지 명시 승인했다. 기준 f997472, 브랜치 codex/structured-static-data. root가 executing-plans·TDD로 구현하고 기존 코드검수·QA 담당을 재사용한다.

Ruling: 추가 worktree/Editor를 만들지 않고 승인 계획의 전용 브랜치에서 단일 연결 Editor로 검증한다 — 현재 Unity PID32384/6000.3.8f1/port7800 ready와 브랜치를 확인했으며 사용자 변경을 명시 보존한다 — 잘못된 경로/중복 Editor 검증을 피하고 파일별 명시 stage로 사용자 변경 혼입을 막는다.

Ruling: 프로젝트 워크플로의 단일 TODO/Checkpoint/개발 기록을 실행 ledger로 사용한다 — 같은 계획의 중복 상태 파일을 만들지 않는다 — 재개 시 TODO와 실제 실행 산출물을 함께 대조해야 한다.

Pre-flight: TODO1 모델/JSON/출처 → TODO2 reader가 동일 null/배열 규칙을 공급, TODO3 병합은 선택 leaf의 출처를 전달, TODO4 writer는 새 값 보존과 구 projection을 분리, TODO5 UI는 새 값/출처를20행·원문4096자 페이지로 표시한다. 구간 scalar key필수/null허용/항상출처필수, Sources pruning 금지와9조합은 승인된 마지막 통합 절을 따른다. 별도 충돌은 발견하지 못했다.

현재 단계: TODO1 기존 serializer로 schema3 golden 동결 후 기능 RED. 제품 모델 수정 전 실제 Unity 실행으로 원본 생성 경로·SHA·창 상태를 남긴다. 실제 모델/추론은 미확인이고 실행하지 않은 테스트를 통과로 기록하지 않는다. 사용자 Volume/키 문서·팩·관측·Chrome·사용자 창은 보존한다.


## 2026-10-26~27 TODO1·TODO2 실행 결과

- TODO1: baseline 실제 serializer의 schema3 골든 SHA8f671fbd575971178ad39eb3f037dbde0d6a1a36f415bad673001ef822382727을 고정했다. 저장시험7RED→추가후8RED→9GREEN, 출처7RED→전체Serialization32GREEN, metadata coercion3RED를 확인했다. strict 검사의 etag를 eTag로 잘못 쓴 구현 오타로41개 중12개가 실패했고 실제 기존 DTO의 `JsonProperty("etag")`와 대조해 수정했다. 최종 **structured-static-task1-final-after-etag-green.json의41/41 통과**를 확인했다. earlier verified-green/final-green 파일명과 관계없이 실제 Summary를 판정 근거로 삼는다. schema1/2/3 bytes/hash·null/0/false·배열순서·불변복사·확장정렬+출처이동·잘못된 metadata 타입/확장 예산을 포함한다.
- TODO2: 창작 structured-ko_kr/en_us fixture 두 개와 실제 양 어댑터의12RED→12GREEN을 확인했다. StructuredStaticReader가 알려진 자료를 읽고 양locale의 known pointer를 extension에서 제외하며 unknown subtree를 문서별 비실행 JSON으로 보존한다. null구간 key 필수와 출처, raw슬롯 순서, unique:false, 실제 secondary 출처, 손상 타입/중복 JSON키/중복변수/깊이·예산을 검증했다. 원본캐시를 새 fixture에 복제하지 않았다.
- NetworkStaticCatalogSource의 공식 오류/경고 유실은 추가3RED(총15중3실패)로 확인했다. ParseWithDiagnostics로 실제 공식 진단을 전달하고 공식 오류 때 보완 fetch를 하지 않게 했다. **structured-static-source-diagnostics-green.json 15/15 통과**다. 보완 포함 실제 pipeline의 오류 선검증/게시·재로딩은 TODO4에서 별도 검증해야 하며 완료로 확대하지 않는다.
- 테스트 인식0건은 기능 RED가 아니다. 새 시험의 namespace 누락 컴파일 오류를 수정했고, 기존 완료 상태/진행 중 native 실패 flag만으로 새 test type 로딩을 보장하지 못하는 사례를 확인했다. Library 전용 harness에 최소 발견 수와 실제 type 로딩/기존job 부재 조건을 넣었다. compiling 중의 이전 native 실패 flag는 같은 컴파일을 완료까지 관찰하며 새 컴파일/테스트를 중복 시작하지 않는다. 상태 조회/준비 probe 실패는 실행 전 중단됐고, 별도 관찰로 ready·job없음을 확인한 뒤 실행했다. 실패 응답 원문을 버린 초기 helper의 세부 통신 원인은 미확인으로 남긴다.
- 자동 승인검토의 사용량 오류로 새로고침1회가 실행 전 차단됐다. 안내시각 이후 같은 require_escalated/auto-review 경로의1회 재확인이 성공했고 뒤의41/41·15/15도 실행됐다. 우회·정책 변경0이며 현재 차단 상태가 아니다. 차단된 명령을 Unity 실패나 검증 통과로 기록하지 않는다.

다음은 TODO3 구조화 병합의9조합·목록단위 공식우선·false·확장출처·차이 비교의 기능 RED다. 현재 adapter는 schema4를 내보내지만 CatalogMerger/Writer/Differ/UI의 연결은 아직 미구현이므로 제품 전체 동작 완료를 주장하지 않는다. 실제 다운로드/API/분류등록/새 EditorWindow/신규agent는0이다. 보호/창의 최종 사후 대조와 전체 suite는 최종 동결 후 수행한다.


## 2026-10-27 TODO3~5와 독립 검수 수정

- TODO3:9조합·목록 단위 공식 우선·false·확장 정렬/출처·새 필드 diff의22RED→22GREEN. 추가 validator6RED→관련68GREEN, adapter별 미소비 필드/flatnull 보존2RED→관련70GREEN. 구간 잘못된 경계/누락 key5사례도 현재112건에 포함해 통과했다.
- 소비 pointer는 실제 adapter의 기존 매핑을 따른다. 공식에서 아직 읽지 않는 traits/nested ability.desc까지 지원되는 것으로 간주해 버리던2RED를 수정했다. 기존 flat effects의 null/object는0으로 바꾸지 않고 비실행 확장으로 남긴다. 관측한 캐시의522아이템 효과에는 null5개가 있었지만 URL/locale/패치는 인증하지 않았다. 해당 캐시는 fixture로 복제하지 않았다.
- TODO4: 실제 fake transport→양adapter→merge→icon staging→임시 게시→loader→browser와 구schema1/2/3 bytes/hash/Sources/기존origins projection·동일identity충돌·실패/취소/복구의15건을 검증했다. 최초14실패 중1개는 TaskCanceledException 파생형을 고려하지 않은 시험 assertion이었고 나머지13개는 기능 누락이었다. 최종 structured-static-task4-first-green.json15/15. 합성n100에서도 unclassified_augment와 비율차단을 유지한다.
- 기존 ingestion143건 중1실패와 persistence125건 중5실패는 지원하게 된 시너지 warning/schema4 기대 및 구schema fixture에 새 구조를 남긴 정의 오류였다. 구 형식 fixture는 기존 scalar/Sources를 유지하며 새 구조를 명시 제외했다. 수정 후 기존 publication7/7·shape pipeline3/3을 확인했다. 구schema3 원본serializer 골든은 재생성하지 않았다.
- TODO5: 새 VM/API/UXML의13RED→13GREEN, 기존 UI namespace **226/226**. 구조화·확장 목록을20행, 원문을4096자씩 넘기며 surrogate pair를 보존한다. 선택/필터/카탈로그 변경 reset과 Dispose 후 예약callback/구독 정리를 검증했다. 기존 브라우저 Project의 origin 조회는 레코드별 lookup을 한 번 만들어 사용한다.
- 화면 도구에 -StructuredStatic 합성 schema4/2 모드를 추가했다. 정상8PNG(940/500px 각 기물끝페이지·null시너지·unique:false와확장원문3/3·구형식안내)를 각각 실제 열어 확인했다. 작은 창 아래 자료는 스크롤로 접근하며 한 PNG에 모든 행이 보인다고 확대하지 않는다. 외부앱의 좌측 작은 overlay가 있었지만 대상값/출처/페이지/고지는 확인했다. 정상·표시전/후 예외 모두 기존9창/native1 ID보존·포커스복원·소유잔류0, 수집/분석 호출0. JSON: structured-catalog-visual-{final,initialization-failure-cleanup,failure-cleanup}.json. 실제 카탈로그/API/관측은 사용하지 않았다.
- 기존 코드검수 담당의 최초 필수3건을 같은 사이클에서 재현했다. 미선택 locale effects가 null/string/number이면 오류를 누락하는6RED, 날짜같은 unknown 문자열의 .000Z가 Z로 변하는2RED, 개별40KiB+40KiB 병합이 report 대신 ArgumentException으로 끝나는1RED다. 양locale strict검사, DateParseHandling.None+중복key거절, 합산 UTF8 상한의 전용예외와 고정 structured_extension_budget_exceeded issue 전달로 최소수정했다. **structured-static-review-all-green.json112/112**.
- 1차 전체 structured-static-full-editmode.json은935개/933통과/실패0/링크권한2제외였으나 마지막 날짜/상한 수정 전 증거다. 최종 소스 동결 후 전체 회귀를 별도 파일로 다시 확인해야 한다. 새 변경 근거가 있어 재실행하며 같은 소스의 무근거 반복은 하지 않는다.

현재 구현은 완료 검수 중이다. 별도 helper StructuredCatalogJson/StructuredCatalogMerge/CatalogStructuredBrowserState는 schema4 책임과 기존 파일 크기를 분리하는 최소 구성으로 기록한다. 실제 모델·추론·token과 root 전체 재작업 비용은 독립 확인할 수 없으며 추정하지 않는다. A의 기획 호출25190tokens/144.241초와 GPT 실제 사용량 미확인을 구분한다. 새 agent/재위임/Hermes/실제API0, 사용자 Chrome·Volume·키문서는 보존한다. 최종 QA/PR/병합/사용자 결과 승인은 아직 별도다.


## 최종 회귀·재개 환경 분리

structured-static-final-full-editmode.json은 **938건/936통과/실패0/호스트 symlink 권한2제외**다. 이전935건 결과를 대체하는 최종 수정 후 실행이다. structured-static-final-source-freeze.json의227개 소스/fixture는 현재까지 변경0이다. structured-static-final-integrity.json에서 보호1389개 전후동일·변경/추가0, 키문서본문열람false를 확인했다.

PID32384의 최종 환경은 기존9창/native1/focus0 보존·Console오류1/경고0이었다. 이1건은 원문/종류를 확인하기 전 실행 재개 시점에 기존 Editor가 없어져 원인 미확인으로 남긴다. Console을 지우거나 제품 결함이 해결됐다고 기록하지 않는다. 2026-10-27 13:12KST 재개에서는 기존 PID가 더 이상 Unity가 아니었고 새 PID20116의 Editor 시작을 관찰했다. 중복 실행/강제종료 없이 같은 프로젝트 Pipeline 준비를 기다렸고 ready 확인 후 별도 resume환경/compile/안전Console을 기록했다. 새환경은9창/native1/focus0·Console0/0·compile idle/failedfalse/compilationFailedfalse다. 재시작 원인은 미확인이며 이전1오류를 새Console0으로 소급해 지우지 않는다. 소스불변과 현재컴파일상태가 확인돼 같은 전체suite를 다시 반복하지 않았다. 창정리 증거는 이전PID의 해당실행 전후비교이고 다른PID사이 ID동일을 요구하지 않는다.

독립 코드검수는 최초3건 중 locale수정 후2건 revise→동일담당 delta pass/잔여필수0이다. 최종112/112JSON을 직접 확인했고 Unity를 대신 실행하지 않았다. 실제 GPT모델/추론/usage미확인. 다음은 동일 최종QA 담당의 승인계획·diff·전체938·PNG8·정리·구팩보존 대조다. 아직 PR/병합·사용자 최종 결과승인은 하지 않았다.


최종 독립QA도 pass/필수0, frozen227SHA와938/936P0F2skip·PNG8·정리·보존 증거를 직접 대조했다. [QA](QAReport.md)와[사용법](FinalDocumentation.md)을 저장했다. 제품 소스는 최종freeze 이후 수정하지 않았다.


통합 전 형식 확인: Unity가 빈 metadata 값의 행 끝 공백을 자동 재생성해 meta 일괄 정리를 중단하고 fixture meta3개를 최종freeze의 정확한 SHA로 복원했다. 제품·시험·fixture227개 변경0을 재확인했고 생성meta 외 git diff --check와 소유107파일 stage·내부링크/meta·사용자2파일제외를 확인했다. 기능 변경0으로 같은 suite를 반복하지 않았다.


## PR23 CodeRabbit 보완 결과

최초 e42e43b 검수4건을 현재상태문서2곳·실제공개prompt raw·정제문서허용상태·구상세출처잘림으로 분류해 모두 수용했다. 500개 구조화출처 때문에 기존 /cost와 /name/enUs가4096자에서 사라지는 실제RED1(pr23-field-sources-red.json)을 확보했다. schema4에서 구조화포인터만 구상세에서 제외하고 전용20행 출처 페이지는 유지했다. 기존 actualpipeline 시험은 구상세 /cost와 구조화행 /baseStats/hp의 공식출처를 각각 확인한다. 새 자료를 숨기거나 구1~3출처를 바꾸지 않았다.

관련113/113, 최종 pr23-review-full-editmode.json **939개/937통과/실패0/호스트2제외(85.21초)** 및 UI227통과다. pr23-review-source-freeze.json227SHA·보호1389변경0. 새8PNG를 root와 동일QA 담당이 직접 확인했고 좌측overlay는 없으며500px하단은스크롤이필요하다. 새PID20116 정상캡처는 기존9창/native1·focus보존·소유0이다. 이전표시전/후예외probe는 변경없는도구의 이전실행증거로만 보존한다.

현재Console1/0은 pr23-console-sanitized.json에서 `Unity.Pipeline`의 `/api/exec` 요청 `Thread was being aborted` 오류로 분류했고 제품/컴파일 언급은 false다. 도구 요청 중단의 발생 계기는 미확인이며 해결/로그삭제를 주장하지 않는다. 이전PID32384의 미확인오류1건과 같은오류로단정하지않는다. 독립코드검수delta와최종QA가모두pass/필수0을확인했다. 실제모델·추론·usage미확인,새agent0. 실제API·분류등록0·실제n100·성급/장착의미·KI-001·사용자최종승인은계속잔여다.
