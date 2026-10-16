# 정적 데이터 관리 탭 개발 보고

날짜: 2026-10-16
상태: 구현·보완 후 컴파일 오류0·집중23/23·loader11/11·전체410/410·최종 독립 검수·QA 대조 통과. PR 검수와 사용자 최종 확인은 별도다.

## 변경

데이터 관리 탭에 세트·TFT패치·원본버전 입력, 매번 선택하는 CommunityDragon 동의, 명시적 원본 버전 확인, fullidentity/저장위치 확인 후 import, 취소와 안전한 오류 안내를 연결했다. 목록은 전체identity·출처·standalone 수집UTC·검증상태·hash·위치를 표시하며 검증된 행을 이 창의 활성 카탈로그로 선택한다. 두 검증된 카탈로그의 이전→이후 counts와 field변경을 펼쳐 비교한다.

작은 서비스 경계와 탭 binding, published loader로 기존 DataDragonAdapter/StaticCatalogPipeline/LegacyCatalogResolver/CatalogAssetWriter/CanonicalCatalogSerializer/CatalogDiffer를 재사용했다. VM은 HTTP/AssetDatabase를 직접 호출하지 않고 활성선택을 열린 창 안에서만 기억한다. CreateGUI는 VM을 유지하고 binding을 교체하며 OnDisable은 구독·예약화면갱신을 해제하고 작업을 취소한다.

## 실행·재작업

- 기준 PR #9 `eb771fe`: CodeRabbit SUCCESS·미해결0, 실제376/376 후 main `cd1b67f` 병합.
- 테스트5파일을 먼저 추가한 뒤 실제 Unity RED: Data namespace 및 신규 타입 부재 CS0234/CS0246 총11건. 담당에게 결과를 전달한 뒤 제품을 구현했다.
- 제품6파일 추가·기존window CS/UXML/USS 연결 후 실제 컴파일 오류0.
- 첫 집중 DataManager21건 중17통과/4실패. Tab 테스트 fixture의 승인값·카운트가 다음 테스트에 남았고, VM 상태/작업은 완료됐지만 단일 yield프레임 뒤 예약된 delayCall 렌더가 아직 실행되지 않은 상태에서 검사했다.
- 제품이나 기대값을 완화하지 않고 fixture 초기화와 실제 EditorApplication.delayCall barrier로 테스트를 수정했다. pending 작업은 Changed 이벤트에서 busy 해제 관측 후 화면갱신을 기다린다. timeout/finally로 테스트 구독을 해제한다.
- 최종 컴파일 오류0, DataManager21/21, published loader11/11 통과. loader는 실제 writer로 만든 임시 schema1/2 파일·메모리index를 사용하며 실제generated에 쓰는 CatalogIndexWriter를 사용하지 않는다.
- 보호 해시1386개 변경0을 확인했다.
- 전체408건 중405통과/3실패. 전부 UI SetUp의 delayCall barrier5초 timeout이며, 뒤2UI테스트는 통과했다. 대기시간확대·반복실행으로 덮지 않고 실제 Editor 스케줄 경계를 조사한다.
- Astra/high 독립검수에서 import 후 목록재조회 실패가 Succeeded로 덮이는 실제 상태표시 문제를 확인했다. 회귀테스트 RED 후 최소 수정할 예정이며 아직 수정완료를 주장하지 않는다. Astra실행은 사용량한도로 최종보고 전에 중단돼 Sol/high로 대체검수를 요청했다.

## 보완 후 최종 결과

위 항목은 중간 실행 이력이다. 최종 결과는 다음과 같다.

- 목록재조회 실패 회귀는 실제1건 RED(Expected Failed, actual Succeeded) 후 ReloadRows 결과로 성공 안내를 분기해 GREEN. 카탈로그가 저장됐다는 사실과 LocalCatalogsUnavailable·목록 복구 안내를 함께 유지하며 파일을 삭제하지 않는다.
- Sol/high 검수의 추가 Important는 승인 중 저장위치 변경이었다. 실제 VM17건 중 이 경우만 RED(다운로드1, expected0) 후 승인직후 같은 immutable request의 resolved directory를 재확인해 달라지면 DestinationChanged로 중단한다. 기존 pipeline의 원자적 filesystem 설계를 바꾸지는 않는다.
- 테스트 대기는 SetUp의 불필요한 barrier 제거·host repaint만으로는 해결되지 않았고 집중23건 중19통과/4실패가 남았다. 대기시간을 늘리거나 실패를 무시하지 않고 native 스케줄을 재분석했다.
- 실제 probe에서 non-batch/unfocused Editor에 delayCall35개가 쌓이고 테스트밖 marker도 실행되지 않았다. 전체뷰repaint는 충분하지 않았고 실제 Inspector활성화 후 marker실행·queue0을 확인했다. [Unity6000.3 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorApplication-delayCall.html)의 Inspector업데이트 뒤 호출 조건과 [공개 소스](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3/Editor/Mono/EditorApplication.cs)를 확인했다. 테스트 helper만 실제 Inspector업데이트를 요청하고 기존5초 barrier·finally구독해제·이전창포커스복원을 유지한다. 제품 pump/hook이나 전역delegate수동호출은 없다.
- 최종 실제 컴파일 오류0, DataManager23/23, loader11/11, 전체 EditMode410/410(실패0). 최종 독립 재검수는 Important2건·스케줄 보완 모두 addressed, 추가 지적 없음으로 승인했다.
- 실제 메뉴·legacy목록1행·수집UTC `2026-09-17T10:18:56.3864991Z`·전체identity선택을 확인했다. 최종코드에서 CreateGUI의 VM/선택 유지 및 Close의 Dispose를 다시 확인했다. 940×700 및 최소500×300 어두운테마 화면에서 입력·스크롤·고지 가독성을 확인했다.
- 최종 Console 현재 error0/warning0·compilationFailed=false, smoke구간 신규error/warning0. 의도한 RED와 이전 transport 오류의 버퍼 이력은 현재 오류와 구분했다. 최종 보호 해시1386개 변경0.

최종 증거는 `data-manager-inspector-{compile,focused}.json`, `data-manager-verified-full.json`, `data-manager-loader-focused.json`, `data-manager-{open,lifetime,minimum-capture,console-final,protected-check}.json`이다. 정제 [QA](QAReport.md)와 raw 검수 이력에서 중간 실패와 최종 성공을 구분한다.

중간 실행 이력은 Git 제외 `Library/TftAnalyzerValidation/data-manager-{baseline,red-compile,final-compile,focused-final,loader-focused,full,protected-check}.json`에 있다. 당시 전체/화면/검수 미완료를 집중 통과로 대체하지 않는다. 최종 증거는 아래 보완 후 결과의 파일을 따른다.

## 모델과 범위

기획2명·구현1명 요청은 gpt-6.1-sol/high. 기존 연결이 여러 모듈·취소·복구·데이터 무결성에 걸쳐 high를 선택했다. 독립 데이터 무결성 검수는 프로젝트 선호에 따라 gpt-6-astra/high로 요청했다. 요청값과 실제 runtime 식별·토큰/시간 확인불가를 구분한다.

실제 네트워크 import/version조회·native 확인창 클릭·실포인터·수동domainreload·밝은테마는 미실행이다. 테스트의 fake transport/확인delegate·실제UITK NavigationSubmit 및 직렬화/재생성 증거와 구분한다. 다음 Task의 경기수집/분석/CSV/compliance/시작네트워크는 구현하지 않는다.
