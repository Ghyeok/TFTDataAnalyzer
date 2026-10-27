# 구조화 정적 데이터 구현 계획

> **For agentic workers:** root가 `superpowers:executing-plans`로 구현하고 기존 코드검수·QA 담당을 재사용한다. 신규 에이전트 생성·재위임은 하지 않는다.

상태: 진행 중 — 사용자 승인 범위 TODO1~5 구현·관련112건/UI226건·화면8PNG 검증, 최종938건 중936통과·실패0·호스트2제외, 독립 코드검수·QA 통과, PR 통합 준비

**Goal:** 공개 정적 자료의 기물 능력치·스킬 수치 배열·시너지 활성 구간·아이템 제한 정보를 출처와 함께 보존하고 기존 카탈로그 탐색에서 읽게 한다.
**Architecture:** schema4의 선택적 불변 구조를 실제 소스 어댑터→공식 우선 병합→검증→불변 게시→재로딩→탐색·패치 비교에 연결한다. 구 schema1/2/3은 바이트와 의미를 보존하고, 의미를 확인하지 못한 원본 필드는 출처가 있는 비실행 확장 자료로 격리한다.
**Tech Stack:** Unity6000.3.8f1, C#, Newtonsoft.Json, NUnit EditMode, 기존 UI Toolkit.
**Spec:** [상위 설계](../../../../TFTDataPipeline.md) §4·6·7·10·12, [워크플로](../../../README.md) §3.1.6. 기준 main `f9974726021bb35edfdb3b5392904db42d1c270f`.

## Global Constraints

2026-10-26 사용자 승인: call_2208e223ba4c465ab9e566d320436868에 ‘이 계획대로 구현 진행’으로 마지막 통합 절까지 승인했다. 아래 ‘승인 전’ 문구는 기획 당시 경계이며 현재 이 문서의 범위만 구현한다. 실제 다운로드/API/분류 등록과 다음 기능으로 확대하지 않는다.

- 기존 카탈로그를 덮어쓰거나 자동 마이그레이션하지 않는다. 전체 identity `(setId, patchId, sourceGameVersion, sourceProfile)`를 유지한다.
- Riot 공식 값 우선, 없는 값에만 사용자가 선택한 CommunityDragon 보완을 적용한다. 0과 false는 실제 값이며 빈 값으로 취급하지 않는다.
- 공개된 자료를 보존할 뿐 전투 실행·성급 배율 계산·미래 예측·게임 중 추천을 추가하지 않는다. 미확인 수치 배열의 슬롯을 성급으로 부르지 않는다.
- 카탈로그 수록·unique·isAugment·확장 자료는 증강 정책 허용 근거가 아니다. 분류 미등록 차단과 최소 표본100 하한을 유지한다.
- 실제 다운로드·Match API·환경 변수 변경·실제 분류 등록은 이번 범위 밖이다. 실제 수집 검증은 사용자 요청대로 미룬다.
- 사용자 `Assets/DefaultVolumeProfile.asset`, `docs/riot-api-key.md`, 기존 팩·관측·아이콘·Chrome·Unity 사용자 창을 보존한다. 키 문서 본문은 읽지 않는다.

## 문제·선택·실제 공급 한계

현재 Unit/Trait/ItemRecord는 단일 numeric Effects만 지원한다. CommunityDragonAdapter는 stats·ability.variables·시너지 effects 배열을 미지원 경고로 남긴다. 따라서 파싱만 추가하거나 UI에 원본 JSON만 붙이면 상위 설계의 정적 탐색·비교·출처 요구를 충족하지 못한다.

선택안은 기물·시너지·아이템을 함께 schema4로 확장하는 것이다. 종류별 반복 schema 변경과 중간 단계의 값 유실을 줄인다. 무제한 raw 모델은 타입·비교·출처 검증을 약화하고, 각 배열을 기존 Effects로 평탄화하면 배열 순서·구간·미상 값이 사라져 채택하지 않는다. 이 선택은 root의 제안이며 사용자 승인 전이다.

로컬 캐시의 해시 `4627a0e4…c2ebdb`와 현재 공식 카탈로그를 읽기 전용으로 대조했다. 선택 세트 기물91개에 stats가 있고 float/int/null이 섞인다. ability.variables는 name/value 배열이며 관측된 value 길이7은 성급 의미의 증거가 아니다. 시너지 effects에는 minUnits/maxUnits/style/variables가 있으며 minUnits null도 있다. 공식 ID로 제한한 아이템522개는 unique bool, incompatibleTraits 토큰0개다. 다른 전역 아이템의 제한 토큰을 현재 세트 관계로 유입시키지 않는다. 이 캐시의 URL·locale·실제 버전은 인증하지 못했으며 게시·다운로드하지 않았다.

공개 원본에 명시된 성급별 수치/중복·장착 조건이 없으면 ‘자료 없음/의미 미확인’으로 표시한다. 현재 원본에 없는 성급 표나 제한 규칙을 합성하지 않는다. 이번 완료는 아래에 적은 필드의 실제 공급·보존·표시를 뜻하며 모든 패치의 성급/장착 규칙 확보나 서비스 전체 완료를 뜻하지 않는다.

## 데이터·형식 계약

1. UnitRecord에 선택적 `BaseStats: IReadOnlyDictionary<string,double?>`, `AbilityVariables: IReadOnlyList<StaticNumericSeries>`를 추가한다. StaticNumericSeries는 `Name:string`, `Values:IReadOnlyList<double?>`이며 원본 슬롯 순서와 null을 보존한다. null/누락과0을 구별하며 값이 없는 슬롯을 계산하지 않는다. array 길이로 성급·AP·AD 등 의미를 추정하지 않는다. 기존 Effects와 설명은 유지한다.
2. TraitRecord에 `Breakpoints:IReadOnlyList<TraitBreakpoint>`를 추가한다. 각 항목은 `MinUnits:int?`, `MaxUnits:int?`, `Style:int?`, `Effects:IReadOnlyDictionary<string,double?>`다. 배열 순서를 보존한다. 알려진 구간 경계는0이상 Int32, 양쪽이 있으면 min<=max다. null은 미상으로 보존·경고하며 활성 조건으로 계산하지 않는다. 완전한 동일 구간의 중복은 오류다. style은 원본 코드로만 표시한다.
3. ItemRecord에 `Unique:bool?`를 추가한다. 명시적 false를 보존한다. unique를 장착 수1개 또는 모든 중복 금지로 확대 해석하지 않는다. incompatibleTraits/associatedTraits/tags 등 아직 의미·안정 ID 연결을 입증하지 못한 제한은 다음 확장 자료에 원문으로 남긴다. typed 관계에 표시 이름·패턴·다른 세트 토큰을 넣지 않는다. 검증된 기존 ComponentIds는 유지한다.
4. 네 종류 레코드에 `Extensions:IReadOnlyList<StaticExtensionField>`를 추가한다. 항목은 `SourceName`, `Locale`, `Pointer`, `CanonicalJson`이다. 소비하지 않은 원본 필드/하위 필드를 문서별로 남기며 typed 필드와 구분한다. 같은 sourceName/locale/raw pointer는 유일해야 한다. 출처별 원본을 함께 보관하는 것이며 충돌한 공식 값을 보완 값으로 대체하는 경로가 아니다. `$type`·URL·설명 태그도 실행·다운로드하지 않는다. 원본 데이터는 UI rich text 없이 표시한다.
5. Extensions는 JObject 키를 ordinal 정렬하고 배열 순서는 보존한 비실행 JSON이다. 레코드별 합계 UTF8 64KiB, 깊이16을 상한으로 두며 초과·비유한 수·중복 키는 고정된 진단 오류로 게시를 막는다. 조용한 절삭·일부 성공은 하지 않는다. 원본 Match 응답·비밀·개인정보를 입력받는 경로를 만들지 않는다.
6. schema4 JSON은 unit의 baseStats/object와 abilityVariables/array, trait의 breakpoints/array, item의 unique/bool-or-null, 네 종류의 extensions/array를 정해진 순서로 기록한다. 빈 자료는 빈 map/list와 unique:null로 표현한다. schema1/2/3 직렬화에는 새 키를 쓰지 않으며 구 schema JSON에 새 키가 case-insensitive로 주입되면 거절한다. schema4의 기존 필드·새 필드 모두 타입/중복/유한값을 검증한다. 의미가 없는 null을0/false로 강제 변환하지 않는다.
7. FieldOriginFields는 schema 인수를 받아 schema3의 필수 pointer 집합을 고정 보존하고 schema4의 선택된 leaf까지 확장한다. 명시적 null stat/slot도 원본의 null로 출처를 기록한다. unique:null 기본값에는 출처를 만들지 않는다. `/baseStats/<escaped key>`, `/abilityVariables/<index>/name`, `/abilityVariables/<index>/values/<index>`, `/breakpoints/<index>/...`, `/unique`, `/extensions/<index>/canonicalJson`을 검증한다. 정렬된 최종 인덱스와 출처가 반드시 함께 이동한다. missing/duplicate/dangling·manifest 밖 출처는 오류다.

## 실제 어댑터·병합 계약

- DataDragon과 CommunityDragon의 동일 stable ID·기존 범위 검증 경계 안에서 원본 `stats` object를 BaseStats로 읽는다. DataDragon의 기존 stats→Effects 동작도 보존한다. CommunityDragon의 `ability.variables[].{name,value}`와 `effects[].{minUnits,maxUnits,style,variables}`를 각각 스킬 수치·시너지 구간으로 읽는다. item.unique는 명시 bool만 허용한다. 새 가상의 원본 필드명을 추가하지 않는다.
- 각 새 구조는 기존 en 우선 primary 문서에서 읽고 해당 필드 자체가 없을 때만 ko 문서에서 읽는다. locale별 서로 다른 비문자열 구조는 진단 경고와 선택한 en 출처를 남기며 슬롯별 locale 혼합은 하지 않는다. 알려진 필드의 손상 타입을 미지원 extension 성공으로 숨기지 않는다. 고정 코드/안전한 메시지와 bounded ID만 보고한다.
- DataDragon에도 `ParseWithDiagnostics(snapshot,patch)` 결과 경로를 추가하고 기존 Parse wrapper는 오류가 있으면 고정 예외로 거절한다. NetworkStaticCatalogSource가 양 어댑터의 Issues를 실제 pipeline의 전후 validation/report/manifest 경고 수까지 전달한다.
- BaseStats는 key별 공식의 non-null 값 우선이다. 공식에 키가 없거나 값이 null이면 보완 non-null 값으로 채울 수 있다. 선택된 null 역시 출처는 원래 문서다. 0은 보완하지 않는다. 스킬 수치·구간은 의미가 섞이지 않도록 각각 전체 비어 있지 않은 목록 단위로 공식 우선 선택한다. 공식 목록을 보완 슬롯/구간으로 늘리지 않는다. Unique는 HasValue 기준이며 false가 우선이다. 충돌·보완 선택을 기존 MergeConflict 보고서에 기록한다.
- provenance 선택은 최종 선택된 값에서 직접 계산한다. dot split으로 숫자 key·`/`·`~`를 손상시키거나 공식 값에 보완 출처를 붙이지 않는다. Extensions는 source/locale/pointer 기준 정렬하여 합치고 같은 키의 내용 상충은 오류다.

## 불변 게시·비교·탐색

- CatalogAssetWriter의 schema 검증·정규화·원본 stamp 정규화와 pipeline/icon staging의 모든 record 복사에서 새 구조를 보존한다. CatalogDiffer는 새 구조의 값 변경을 안정적인 field path로 보고한다. dictionary 순서는 해시에 영향이 없고 수치 배열·구간 순서/값 변경은 해시·diff에 반영된다.
- 구 팩과 동일 identity를 가져올 때 구 schema 의미의 projection으로만 비교한다. projection에서 새 구조/확장과 그 출처만 제거하고 구 schema3 출처·기존 Effects·문서 stamp는 보존한다. 기존 의미가 같으면 구 팩의 실제 구 형식/해시를 반환하고 ‘기존 카탈로그에 구조화 자료 없음’을 표시한다. 기존 의미가 다르면 충돌로 거절하며 기존 팩/인덱스를 덮어쓰지 않는다. 같은 identity의 구 팩을 재사용해 새 자료가 저장됐다고 표시하지 않는다. schema4 동일 identity의 새 값 변경도 불변 충돌이다.
- 기존 데이터 관리의 카탈로그 탐색 상세에 ‘구조화 자료’와 ‘원본 확장 자료(의미 미확인)’를 추가한다. leaf 값과 선택 출처를20행씩 넘기며 모든 항목에 도달할 수 있게 한다. 전체 DetailsText의4096자 절삭에 새 데이터를 붙여 숨기지 않는다. 배열은 ‘원본 슬롯0…’로 표시하며 성급을 붙이지 않는다. null은 ‘자료 없음’, false는 ‘아니요’다. 원문 확장 JSON은 값을 별도 선택해4096자씩 넘기며 전체 내용에 도달할 수 있게 한다. URL·태그 실행/자동 아이콘 다운로드/새 창은 없다.
- 카탈로그/선택 레코드/검색을 바꾸면 상세 페이지·원문 페이지를 초기화하고 Dispose에서 예약 callback/구독을 해제한다. 구 schema1/2/3 화면의 기존 이름·설명·효과·관계·출처는 보존한다.

## 파일별 책임

| 파일 | 책임 |
|---|---|
| Core/Domain/{UnitRecord,TraitRecord,ItemRecord,AugmentRecord,FieldOrigin}.cs | 선택적 immutable 새 값과 schema별 출처, 기존 생성자 호출 호환 |
| Core/Domain/StaticNumericSeries.cs, TraitBreakpoint.cs, StaticExtensionField.cs (신규) | 원본 수치 순서·구간·비실행 확장 자료의 불변 모델 |
| Core/Serialization/CanonicalCatalogSerializer.cs, Core/Validation/CatalogValidator.cs, Core/Diff/CatalogDiffer.cs | schema4/구 형식 보존·검증·정규화·값 비교 |
| Editor/Ingestion/StructuredStaticReader.cs (신규), ParsedFieldOrigins.cs, DataDragon/DataDragonAdapter.cs, CommunityDragon/CommunityDragonAdapter.cs | 실제 알려진 필드 strict 파싱, 소비 필드 추적·확장 격리·정확한 출처 |
| Editor/Ingestion/DataDragon/DataDragonParseResult.cs (신규), StaticCatalogPipeline.cs, CatalogMerger.cs | 양쪽 진단 전달·공식 우선 선택과 leaf 출처 |
| Editor/Persistence/CatalogAssetWriter.cs 및 새 record를 복사하는 실제 icon 경로 | 새 값 보존·구 projection·실제 저장 형식 반환 |
| Editor/UI/Data/{CatalogBrowserViewModel,CatalogBrowserTab}.cs, CatalogStructuredDetails.cs (신규), Editor/UI/TftDataAnalyzerWindow.{uxml,uss} | 20행 상세/원문 페이지와 미상 안내·수명 정리 |
| Tests/EditMode/{Domain,Serialization,Ingestion,Validation,Diff,Persistence,UI}/StructuredStatic*Tests.cs, Tests/Fixtures/Static/structured-*.json | 아래 TODO별 기능 RED/GREEN과 golden·E2E |
| AgentScripts/Invoke-TftAnalyzerCatalogVisualValidation.ps1 및 직접 참조 fixture/helper | 승인 후 합성 구조화 자료 capture 모드만 추가, 기존 read-only 로컬 검증 유지 |

위 경로는 `Assets/TFTAnalyzer/` 기준이며 문서·meta는 해당 사이클과 기존 색인에서 관리한다. 모듈 전면 재구성이나 새 의존성 설치는 하지 않는다.

구현 인터페이스: `StructuredStaticReader.Read(string recordType, string stableId, JObject ko, JObject en, SourceStamp koSource, SourceStamp enSource, IReadOnlyCollection<string> consumedPointers): StructuredStaticReadResult`. 결과에는 위 BaseStats/AbilityVariables/Breakpoints/Unique/Extensions와 immutable Origins/Issues를 담는다. 기존 adapter가 이미 소비한 source pointer를 전달하여 미소비 하위 필드만 격리한다. 새 reader가 소비한 known field와 기존 reader의 필드를 중복 extension으로 넣지 않는다. `DataDragonParseResult`는 Catalog/Issues이며 기존 ParseWithDiagnostics→Parse wrapper 관계를 따른다. `FieldOriginFields.Required(object record, int schemaVersion = 3)`는 구 호출 계약을 유지한다. UI의 `CatalogStructuredDetails.Project(StaticCatalog catalog, string recordType, string stableId)`는 Field/Value/Sources/선택적 ExtensionJson 행을 반환하며 browser VM은 StructuredPage/SetStructuredPage(int), SelectStructuredRow(int), ExtensionPage/SetExtensionPage(int)를 관리한다. JSON 중복 키는 JObject 생성 시점의 DuplicatePropertyNameHandling.Error로 감지하여 후단 reader가 이미 유실된 중복을 검사했다고 주장하지 않는다.

## TODO·완료 조건·검수·QA

### 1. 구 계약 동결과 schema4 모델/저장
- [x] 승인 후 **새 serializer 수정 전** 기준 f997472의 실제 serializer로 schema3 synthetic golden을 생성하고 고정 SHA를 기록한다. 기존 schema1/2 golden은 수정하지 않는다. 생성 결과의 원본 실행 경로/해시를 남긴다.
- [x] `StructuredStaticSerializationTests`에서 null vs0/false, immutable copy, 배열 순서, escaped keys, 구 schema 주입 거절, 새 구조 왕복과 결정성의 기능 RED를 실행한다. 기존 fixture를 기대값에 맞춰 다시 생성하지 않는다.
- [x] 새 모델·DTO·FieldOrigin 계약을 구현하고 GREEN을 확인한다. schema1/2/3의 golden JSON·내용 해시와 schema4 key 삽입/수치 변경에 따른 해시 차이를 검증한다.

### 2. 실제 어댑터 공급과 비실행 확장
- [x] `StructuredStaticIngestionTests`의 창작 ko/en 최소 fixture에 stats={hp:100,armor:0,mana:null}, ability 수치=[0,10,null,30], null 경계 구간, unique:false, 원문 제한 token과 미지원 중첩 필드를 넣어 실제 parser의 누락 RED를 확인한다.
- [x] 알려진 필드 reader·확장 격리·DataDragon 진단 결과를 연결한다. 잘못된 토큰/중복 변수명/비유한 수/중복 JSON키/초과 깊이·64KiB, locale 누락/상충과 공식 전역 ID 밖의 자료를 검증한다. 공식 ID 밖 root item은 확장 보존 경로로도 유입되지 않는다.
- [x] 기존 flat Effects/설명/아이콘·selected-set 관계와 새로운 null·순서·출처를 함께 GREEN으로 확인한다. 미지원 경고를 무조건 없애지 않고 실제 격리·오류 결과로 대체한다.

### 3. 병합·검증·패치 비교
- [x] `StructuredStaticMergeTests`의 공식0/false 우선, null만 보완, 배열 전체 공식 우선, escaped key별 출처/충돌 보고, 확장 정렬 RED→구현→GREEN을 확인한다.
- [x] `StructuredStaticValidationTests`와 `StructuredStaticMergeTests`의 diff 사례에서 구간 min>max/동일 구간 중복/미상 경계, 필수 출처 누락·교체·잘못된 인덱스, 구조 값/순서만 변경한 diff를 확인한다. 출처 검증이 생산자가 선언한 목록을 그대로 믿지 않는지 검수한다.

### 4. 실제 pipeline·불변 저장·재로딩
- [x] `StructuredStaticPublicationTests`의 fake transport→NetworkStaticCatalogSource→실제 병합→아이콘 staging→임시 게시→PublishedCatalogLoader→browser에서 새 값/leaf 출처가 그대로 도달하는 RED→GREEN을 확인한다.
- [x] schema1/2/3 재사용 시 실제 구 형식 반환·출처 projection, 동일 identity 기존 값 변경/새 schema4 값 변경 충돌, 실패/취소/인덱스 복구·기존 파일 불변을 검증한다. 별도 내부 shortcut source로 실제 adapter를 건너뛴 것을 E2E로 부르지 않는다.
- [x] 미분류 증강을 포함한 합성100건이 여전히 정책 차단되는지 확인한다. 실제 분류나 실제 n100 사용성 검증으로 보고하지 않는다.

### 5. 사용자 탐색·수명·화면
- [x] `StructuredStaticBrowserTests`의20/21행 경계,4096자 초과 확장 원문 마지막 페이지, 선택/검색/카탈로그 변경 reset, 빈 구조/구 schema 안내, 악의적 rich text 무실행, Dispose 후 callback0의 RED→GREEN을 확인한다.
- [x] 940/500px 실제 PNG에서 기물 수치·시너지 구간·아이템false/미상 제한·20행 페이지와 긴 원문을 확인한다. 기존 owned-window 도구에 합성 입력 모드를 추가하며 화면 보존과 예외 전/후 정리를 각각 검증한다. 기존 사용자 창과 native frame·focus를 전후 대조하고 잔류0을 증명한다.

### 6. 회귀·독립 검수·PR 통합
- [x] 관련 tests GREEN 후 제품 소스를 동결하고 전체 EditMode1회와 Console/창/보호 파일 해시를 확인한다. 새 실패/변경 없이는 전체 suite 반복을 하지 않는다. 호스트 symlink 제외와 실제 실패를 구분한다.
- [x] 동일 코드검수·QA 담당이 승인 TODO·diff·실행 증거를 검토한다. 필수 수정 시 같은 담당을 재사용한다. 실제 모델/추론/사용량 확인 불가 시 미확인으로 기록한다.
- [ ] 문서/raw/meta·사용법·공급 한계·전체 서비스 잔여를 갱신해 커밋/PR. 최신 head CodeRabbit·미해결·CI·실행 증거를5분마다 확인해 승인 범위대로 병합한다. 사용자 최종 결과 승인은 별도이며 Hermes 예약은 등록하지 않는다.

Unity 검증 명령: 기존 CLI 절대 경로로 `command run_tests --project-path C:/Unity/TFTDataAnalyzer --mode editor --filter_type testName --filter StructuredStatic --timeout 180 --format json`, 전체는 `--filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout 300`. 소스 변경 후 recompile 완료·실패0을 먼저 확인하고 단일 Unity 작업의 terminal 결과를 기다린다. 캡처 전 [창 정리 지침](../../../../../../docs/unity-editor-validation.md)을 따른다.

## Review Focus

null/0/false 혼동과 배열의 성급 오해는 TODO1/2/5, 원본 locale·순서·JSON pointer 출처 엇갈림은 TODO2/3, 구 schema projection으로 실제 구 팩을 새 팩처럼 보고하는 문제는 TODO4/5, 전역 제한 토큰의 세트 유입과 실행 가능한 승격은 TODO2/4/5, 큰 원문·페이지 변경/창 정리의 잔류는 TODO2/5에서 직접 검증한다.

## 승인·잔여 요구 추적

현재는 문서 작성·읽기 전용 조사만 수행했다. 사용자 명시 승인 전 제품 코드/새 테스트/Unity 실행은 하지 않는다. 이번 후보는 상위 §6.2/6.3/6.4의 실제 알려진 구조와 §7.5 확장 격리를 연결한다. 원본에 명시된 성급/장착 규칙이 확보되지 않은 부분, 실제 정적 다운로드/Match API, 실제 증강 분류 등록0, 실제 표본 입도/n100 달성 가능성, KI-001 기록, 사용자 최종 확인은 계속 잔여다. 공개 배포·정책 등록은 사용자가 외부 제공을 요청할 때 별도 gate다.

### 독립 기획 B 반영 — null 구간의 출처 계약

검토 원본 snapshot은 변경하지 않는다. 구간의 minUnits/maxUnits/style는 **원본 key 필수, 명시적 null 허용**으로 확정 제안한다. 셋 중 하나라도 key가 없으면 고정 진단 오류로 게시를 막는다. null이더라도 해당 scalar의 FieldOrigin은 항상 필수다. schema4 역직렬화에서도 세 키의 존재를 검사하고, validator는 구간 항목마다 `/breakpoints/<index>/minUnits`, `/maxUnits`, `/style` 출처를 독립 요구한다. 따라서 누락 key를 null 기본값으로 만들어 출처를 조작할 여지가 없다. TODO2/4에 key누락 오류·명시null 왕복 성공·null 출처 제거 후 실패의 실제 adapter→serialize→loader 테스트를 포함한다. root가 B의 P2를 수용한 수정이며 사용자 승인 전 계약이다.

### 기획 A 통합 — 출처·null·미소비 필드의 확정 계약

이 절은 앞의 축약 규칙을 구체화하며 승인 대상에 포함된다. 원본 A/B 동결 snapshot과 최초 verdict는 보존한다.

1. A의 구 팩 Sources 제거 제안은 **현재 구현 근거에 따라 채택하지 않는다**. 현재 PairSources는 ko/en 문서를 항상 반환하고, MergeSources는 값 채택 여부와 관계없이 official.Concat(supplement)를 보존한다. manifest.Sources도 요청에서 읽은 문서를 보존한다. 이 사이클에서는 새 endpoint/SourceStamp 종류를 추가하지 않는다. projection은 동일 요청·같은 문서 bytes와 기존 의미가 같을 때 이 기존 Sources 집합을 그대로 보존한다. fieldOrigins로 Sources를 재산출/절삭하면 구schema 의미와 해시가 바뀌므로 금지한다. 새 원본 SHA/문서 집합이 실제 달라진 경우는 불변 충돌이며 재사용 성공을 위해 지우지 않는다. TODO4에는 ‘같은 보완 profile/같은 문서 집합에서 오직 새 구조만 추가된 입력→schema3 재사용 성공’, ‘문서 SHA만 바뀌면 충돌’, ‘구 schema3의 기존 leaf 출처까지 변하면 충돌’을 검증한다.
2. 구간의 정확한 필수 pointer는 `/breakpoints/<index>/minUnits`, `/breakpoints/<index>/maxUnits`, `/breakpoints/<index>/style`, 그리고 존재하는 effect key마다 `/breakpoints/<index>/effects/<escaped key>`다. 세 scalar는 key필수/null허용/항상출처필수다. effect map은 빈 map을 허용하고 명시 null인 effect entry도 key가 있으므로 출처필수다. effect key가 아예 없으면 그 pointer를 만들지 않는다. 배열 전체/빈 map의 가상 출처를 만들지 않는다.
3. BaseStats 병합은 아래 표대로 확정한다. ‘값’은0을 포함한 non-null finite number다. 모든 null entry는 보존되는 경우 해당 원본 출처를 가진다. SupplementFill의 null은 ‘수치 확보’가 아닌 ‘원본의 명시적 결측 보존’으로 보고한다. 후속 reader/validator/보고서가 동일 규칙을 쓰며 TODO3은9조합과 동일/상이한 non-null 값을 검증한다.

| 공식 key | 보완 key | 최종 key/값·출처 | MergeConflict |
|---|---|---|---|
| 없음 | 없음 | 없음·출처 없음 | 없음 |
| 없음 | null | null·보완 | SupplementFill(명시 결측) |
| 없음 | 값 | 값·보완 | SupplementFill |
| null | 없음 | null·공식 | 없음 |
| null | null | null·공식 | 없음 |
| null | 값 | 값·보완 | SupplementFill |
| 값 | 없음 | 값·공식 | 없음 |
| 값 | null | 값·공식 | 없음 |
| 값 | 값 | 공식 값·공식 | 두 수가 다를 때만 OfficialWins |

4. Extensions의 known-field 제외는 ‘채택한 값’이 아니라 **지원 schema1~4가 인식하는 원본 pointer** 기준으로 ko/en 양쪽 모두에 적용한다. Typed parser가 선택하지 않은 locale의 같은 known 필드도 extension으로 중복 보관하지 않는다. 부분 소비 컨테이너는 컨테이너 전체를 복제하지 않고 unknown 하위 pointer만 따로 격리한다. 예: ability.desc/variables[].name/value는 양쪽에서 제외하고 ability.other/variables[].other만 보존한다. trait effects 배열도 minUnits/maxUnits/style/variables의 known leaf를 제외하고 unknown 하위만 보존한다. 알려진 필드의 잘못된 타입은 extension으로 우회하지 않고 오류다. TODO2는 en값 선택·ko known필드 중복0·양 locale unknown nested 보존·잘못된 known타입 게시 차단을 검증한다.
