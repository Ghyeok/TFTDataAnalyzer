# 현지화 저장 개발 기록

상태: 진행 중

사용자 call_d5412b46c27542e99a05dffe990ea1c4가 통합 계획 구현을 명시 승인했다. 기준main7922fa6, 로컬기획265bdf1/branch codex/localized-catalog-storage, Unity20116/6000.3.8f1/port7800 ready를 실제확인했다. root executing-plans/TDD, 기존동일code-review/QA담당재사용·새agent/재위임0.

Ruling: 이미 승인된 단일 연결 프로젝트의 전용브랜치에서 구현하며 새worktree/Editor를 만들지 않는다 — 기존 사용자창/Chrome보존과 정확한프로젝트경로를유지한다 — 잘못되면사용자변경혼입이므로명시파일stage/보호SHA를계속대조한다.
Ruling: 실행 순서와 TODO·검수 조건·QA 증거 및 체크 상태는 [통합 계획](ImplementationPlan.md)에서 관리한다. 이 개발 기록은 당시 실행 이력이고 [체크포인트](../Checkpoint.md)는 재개 위치 요약이다. 재개 시 실제 job 종료와 산출물을 확인한다.

다음은제품변경전실제schema4serializer의네kind창작golden동결이다. 이전1~3golden을재생성하지않고비밀값·원본경기를읽지않는다. 실제모델·추론·root전체사용량은미확인이다.


원본4골든은제품수정전실제serializer로생성했다. SHA6152c706c5e36c459c81cb08bbd4d384c18141f0f69240d9ee734ebc7021cd7e,네kind/한영/개행/날짜/구조화/null/false/논리출처를포함한다. 생성보조의첫실행은PatchManifest마지막인수누락CS1729로execute0이었고실제12인수와대조후문자열JSON표기도정정해성공했다. 이를제품결함이나기능RED로세지않는다. baseline은PID20116/기존9창/native1/focus0/Console7·0이며이전로그를지우지않았다. 보호1389현재SHA를새로기록했다.


TODO1: localized-catalog-serialization-red.json40건 중24통과/16실패로새5왕복·fallback·구1~3의table주입허용결함을확인했다. schema4골든과기존4의unknown-root거절은이미통과하므로전체40을RED로세지않는다. schema5표를write/expand하는helper,DTOcoercion전기존strict guard,1~5범위와5의구조화출처검증을연결했다. 첫GREEN후41중40통과/1실패는escaped-ID fixture가정렬된relatedIds[0]을잘못교체해원래Unit참조를남긴정의오류였다. 실제골든은Item→Unit순서라정확한기존ID를찾아교체하도록시험만고쳤다.

Ruling: 새schema5의stableId키는기존Domain.RequireStable이이미정규화한공백없는형식만허용한다 — 두raw ID가trim후같은Domain키로합쳐지면문자열표의일대일연결이깨진다 — 기존serializer가생성한팩은영향없으며구1~4reader의trim동작은유지한다. padded키/정규화후중복2사례를실제RED로확인후5helper에서만거절했다. rawJSON중복키1사례도추가해silent last-wins를차단한다. 새schema5의실제Source/게시/loader연결은TODO2에남아있다.


TODO1 최종직렬화namespace84/84통과(localized-catalog-task1-serialization-green.json)이며새Localized43건이포함된다. 구1~4bytes/hash·false/null/slot·표flag/타입/키·논리출처를검증했다. 다음TODO2는실제adapter/current-schema와writer5/old4projection이아직연결되지않은부분을실제pipeline에서RED로확인한다.


TODO2 실행: localized-catalog-publication-red.json15건 중1통과/14실패로 actual adapter가4에머물고 writer가staged5를미지원으로거절하는연결누락을확인했다. 양adapter의성공/오류manifest5·writer5검증·old4전값/전origin보존projection을연결했다. 기존actual adapter의current-schema기대만5로바꾸고 명시적schema4의merge/모델/serializer/golden시험은4로보존했다. 검증출력은완전JSON을보존하면서실패메시지표시만4행/500자로제한해반복stack출력을줄였다.


TODO2 최종localized-catalog-publication-green.json15/15통과다. actual양adapter→공식우선merge→icon→새5게시→hash검증→loader/browser에서문자열/fallback/논리출처/구조화자료와inert localizations원문을확인했다. 독립old1~4모델fixture로원본bytes/hash·실제구형식반환·Sources·old4전구조/전origin보존과변화충돌,5문자열변경충돌·취소·index복구를검증했다. 관련회귀는 ingestion151/151, persistence141중140P0F/hostsymlink1skip이다. UI제품코드/레이아웃변경0이므로같은PNG를재생성하지않고BrowserVM실제소비와기존UI전체회귀를사용한다. 다음은소스동결/전체suite와기존담당의독립코드검수다.


첫 전체검증 localized-catalog-full-editmode.json은997개/995P0F/호스트2skip, 동결232SHA와 보호1389개 변경/추가0, 동일PID20116 전후 기존9창/native1/focus 보존·Console1/0→1/0이었다. 이는 다음 독립검수 수정 전 결과다.

기존 코드검수 담당의 첫 호출은 사용량 한도로 실행실패했고 안내시각 이후 같은담당을1회 재개했다. 독립 검수가5 manifest의 대소문자별칭이 DTO 바인딩에서검증된5/정수타입을덮어써구조화값유실가능성을 지적했다. SchemaVersion=3 및 SetNumber="19" 추가의 실제RED2/2를확인했다. schema5에서만 manifest의정확한16속성을DTO전검사하고DTO후version5일치도확인했다. 구1~4경로는유지한다. localized-catalog-manifest-alias-green.json 관련60/60, 같은담당delta pass/필수0. 새제품변경이있으므로이전997건을최종증거로사용하지않고동결/전체회귀를다시실행한다. 사용량한도실패를검수pass로바꾸지않았고새agent/우회0이다.

현재root 모델은 재개 시점 developer 런타임에 gpt-6.1-terra로 명시돼 있다. 이전root 단계와 코드검수담당의 실제모델·추론·usage는 독립 확인정보가 없어 미확인으로 유지한다.

최종 변경 후 직렬화 86/86, 전체 EditMode 999건 중 997 통과·실패 0·호스트 심볼릭 링크 제한 2건 건너뜀이다. 증거는 localized-catalog-final-serialization.json 및 localized-catalog-final-full-editmode.json이며 최종 제품 소스 232개를 동결했다. 이전 997건 결과는 수정 전 이력이다.

최종 전후 기존 EditorWindow 9개, native ContainerWindow 1개와 포커스가 유지됐다. 보호 대상 1389개 중 1388개는 동일하고 version-check.json 한 파일이 변경됐다. 기존 스케줄러와 시각이 부합하나 원인으로 확정하지 못했으며 이전 파일 내용은 보관하지 않았다. Console 오류 1→3 중 새 2건은 Unity.Pipeline /api/commands 통신 오류로 분류했으며 발생 원인은 미확정이다. 제품 컴파일 오류로 식별되지는 않았고 전체 테스트는 정상 종료했다. 실제 경기 API 호출은 하지 않았다.
