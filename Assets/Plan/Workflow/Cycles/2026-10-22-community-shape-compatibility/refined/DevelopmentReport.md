# 보완 정적 자료 형식 호환 개발 기록

상태: 진행 중

## 목적·결정

선택한 세트의 명시적 소속과 같은 요청의 공식 ID로 보완 자료 범위를 확인한다. 기존 필드의 실제 공급 경로를 복구하고 미지원 구조·제외·손상을 보고한다. 직접 선택한 보완 가져오기에만 적용하며 공식 우선 병합과 schema1/2/3·불변 팩은 유지한다.

## 사용법·적용 범위

데이터 관리에서 보완 자료 사용을 선택해 가져온다. 같은 요청의 공식 catalog가 전역 item/augment 범위를 제한한다. 상세 설명·효과·안정 ID 관계와 실제 source는 기존 탐색에서 읽고 제외·미지원 구조는 기존 정적 가져오기 보고서의 오류·경고에서 확인한다. 이전3인수 Parse는 오류를 던져 빈 성공을 숨기지 않으며 실제 source는 catalog+issues를 반환해 최종ValidationReport까지 전달한다.

## 승인·실행 기준

사용자는 call_16b237d5587847698b6f91044400c6d5에 ‘이 계획대로 구현 진행’으로 명시 승인했다. 이전 PR21 최신54a29f3을 CodeRabbit SUCCESS·미해결0·CLEAN·최신coveredCommitId 확인 후 UTC2026-10-22 10:04:32 main5a67afb679bd9c2e91cf71f3bc83b2ce80622bcc로 병합했다. main과 기획head54a29f3의 전체트리가 같고 사용자파일 변경은 별도로 보존한다. 실제 재검수 댓글은 사용자 call_cc893c5c112c40bc9619dfa1b46fda7e 승인 후1회만 게시했다. PR21 조회는 종료했다.

같은 연결 Unity6000.3.8f1/PID29408/port7800에서 별도 Editor 없이 전용branch codex/community-shape-compatibility로 진행한다. 단일 프로젝트 검증 경계를 유지하려 추가worktree는 만들지 않았다. root inline 구현, 기존 /root/analysis_code_review와 /root/analysis_final_qa 재사용, 신규agent/재위임0. root/GPT의 실제모델·추론·사용량은 미확인이다. A의 실제 runtime·11940tokens·강제접근제한미검증은 [기획검토](../../../../../../AntigravityCollab/tasks/2026-10-22-community-shape-planning/result.md)에 있다. 모델 전환을 확인했다고 주장하지 않는다.

## 현재 실행 증거

- Library/TftAnalyzerValidation/community-shape-task1-red.json: 신규4/4 기능 실패. set namespace 누락, 중첩설명·관계 누락, 비용 부재와 중복set 미거절. 컴파일은 성공한 실제 어댑터 시험이다.
- community-shape-task1-green.json:4/4 통과. selected-set membership·유일한 동일locale 이름→stable ID·nested설명과 source 기록·손상 오류를 구현했다.
- community-shape-task2-red.json:8개 중4통과4실패. 전역 범위 필요, 다른patch scope, root item/augment 유실과 실제 NetworkStaticCatalogSource의 효과 공급 누락을 재현했다.
- community-shape-task2-green.json:8/8 통과. 실제 같은요청 official을 전달하고 종류별 ID교집합·명시적bool·진단 경로를 구현했다.
- community-shape-boundary-tests.json:22개 중20통과2실패. setName이 빈값으로 바뀐 회귀와 numeric composition 토큰의 비구조 InvalidOperationException을 확인해 각각 원래set이름 보존·진단 오류로 보완했다.
- community-shape-ingestion-green.json:관련97/97 통과. 새22개와 기존 가져오기 회귀를 포함한다. 실제 기본source→병합→임시staging→게시→loader→browser에서 필드출처를 읽고, 보고서경고가manifest count에도 남는지 확인했다. 같은입력해시·idempotent와 변경된값의 불변충돌·원본bytes 보존·staging정리를 확인했다. isAugment=true인 실제정적종류도100개창작경기에서 unclassified_augment·비율비노출을 유지한다.
- community-shape-review-source-freeze.json:제품3/새시험2/창작fixture2의7SHA를 고정해 기존코드검수담당에게 전달했다. 전체suite·최종QA는 아직미실행이다. 전체 시작 전에 검수수정이 있으면 새동결을 기록한다.

## 보존·한계

보호1399개의 새사전SHA는 community-shape-protected-before.json, 창/native/focus와 Console사전수는 community-shape-environment-before.json이다. 기존9창/native1·focus0, Console오류2/경고0이며 앞사이클의 원인미확정 Unity Pipeline 전송 오류2건을 지우지 않았다. 새창·Inspector·Chrome 조작0. UI layout은 바꾸지 않았고 보고서/탐색의 실제viewmodel을 시험하며 새화면캡처는 반복하지 않는다.

fixture2개는 창작한 작은ko/en형식이다. 실제원본 인증·다운로드·API·환경변수변경·증강분류등록0. 기존캐시 URL/locale/version이 검증되지 않았다는 한계를 유지하며 게시하지 않는다. 기물stats/스킬변수, 시너지구간은 새모델이 없어 경고로 알리고 평탄화/수치추정을 하지 않는다. 범위를 확인할 수 없는 전역 보완전용항목은 제외한다. 기존 동일identity팩과 새값이 다르면 덮어쓰지 않고 기존 불변충돌을 유지한다. 실제경기·n100실사용·아이템제한모델·KI-001·사용자전체검증은 별도잔여다. 서비스전체 완료를 주장하지 않는다.


## 2026-10-26 진행도 대조

독립 코드검수에서 공식범위의 SetNumber 누락과 빈trait참조의빈locale이름매칭 P2를 지적했다. root는 setData.number 과대정수도 확인했다. community-shape-review-red.json은27개중24통과3실패이며, 명시적SetNumber비교·공백참조거절·int.TryParse 범위검사를 적용한 community-shape-review-green.json은27/27통과다. 같은기존담당은후속pass/지적3건해소/추가필수0으로응답했고 최종7SHA를확인했다. 이전97/97과실행시점/소스범위를구분한다.

전체suite명령은자동승인검토사용량한도로 **실행되지않았다**. 검토실패이며unsafe판정은아니고우회하지않았다. 전체결과파일/실행세션이없으며최종QA도미실행이다. 현재7소스해시는최종동결과같고보호1399개파일도사전SHA와같다. 새Unity사후창/Console확인은수행하지않았다. 현재변경은미커밋이며새PR검수는아직없다. 다음행동은한도해소후전체실행증거를확보하는것이다.


## 제외 기물 ID 경고의 잔여 조건

승인계약2는 개수와정적ID를요구하지만 현재코드는개수만기록한다. 두번째TODO를재개하고회귀시험1개를추가했다. 미실행이므로RED/GREEN을주장하지않는다. 이관찰은기존검수의완료범위를소급해확대하지않는새잔여다. 제품3파일은불변이며최종전체동결은미정이다. 원래27시험결과와7SHA는당시증거로보존하고 현재추가시험delta를별도기록했다. 실행제한해소후실제RED와최소구현을먼저수행해야한다.


## 2026-10-26 승인 경로 복구·최종 전체 실행

앞선승인검토오류의reset안내는미래였으나,같은require_escalated경로로현재상태를1회확인한결과정상승인돼Unity32384/ready였다. 권한모드/승인우회는0이다. 도구가반환한usage한도를별도승인전용할당량으로단정하지않는다. 한도차단과미실행기록은그당시이력이며현재전체실행성공을가리키지않는다.

새단일ID시험은 community-shape-warning-id-red.json1실패,표본상한시험포함 community-shape-warning-id-bounds-red.json2실패였다. 제외ID최대5개·원문64UTF16자/생략표시·제어문자공백·전체건수와표본외건수를보존하도록최소구현했다. 512자report경계안에들도록한구체값이다. community-shape-final-ingestion-green.json104/104와신규29통과. 같은검수담당delta pass/필수0으로현재7SHA와원본ID비변경을확인했다. 최종동결은 community-shape-final-source-freeze.json이며옛27회귀동결과별개다.

community-shape-full-tests.json은826개824통과·실패0·호스트symlink2제외(143.15초),신규29모두pass다. 앞선전체실행시도는실행전거절돼세션이없었고,이번실제전체실행은session20621의정상종료로확인했다. 실행중재시작·Assets편집은0이며새실패가없어전체를반복하지않는다.

community-shape-final-evidence.json:소스7변경0/재개사전보호1399변경0/창9·native1·focus동일/Console전후오류0경고0. 재개전에이미 Library/TftAnalyzerStore/version-check.json1개가10월22사전SHA와달랐고옛Editor29408과현재32384가달라,그차이는재개중변경이나원인해결로표현하지않는다. 새UI layout/창생성/PNG0이며실제pipeline·report/browser viewmodel시험과구분한다. 실제RiotAPI/정적다운로드/분류등록/환경변수변경0. 최종QA와새PR통과는이전체시험만으로완료판정하지않는다.


커밋준비문서검증: QA후 .gitattributes에이번동결기획입력2개만 text eol=lf를추가했다. core.autocrlf=true/core.eol=crlf의실제checkout-index에서수정전45/19CRLF·둘다manifest SHA불일치(community-shape-planning-checkout-red.json)를재현했고,추가후CRLF0·기존SHA일치(community-shape-planning-checkout-green.json)를확인했다. 기존골든2파일규칙과제품7소스는불변이다. 이문서저장메타데이터검증은기존QA판정이후root가별도로수행했으며전체suite를불필요하게반복하지않았다.
