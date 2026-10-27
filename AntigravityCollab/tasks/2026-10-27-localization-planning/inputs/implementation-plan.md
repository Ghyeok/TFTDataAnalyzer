# 현지화 문자열 분리 저장 구현 계획

> **For agentic workers:** 승인 후 root가 superpowers:executing-plans/TDD로 구현하고 기존 코드검수·QA 담당을 재사용한다. 새 에이전트·재위임은 하지 않는다.

상태: 초안

**Goal:** 상위 설계§6.6의 현지화 문자열 분리 저장을 새 카탈로그 형식에 연결하고 안정ID/필드별 한영 값과 영어 대체 여부를 보존한다.
**Architecture:** 새 schema5의 레코드 JSON에서 이름/설명 문자열을 빼고 localizations 표로 저장한다. 로드할 때 검증한 표를 기존 immutable LocalizedText API에 재연결한다. 기존1~4 형식과 모든 구조화 값·출처·불변 저장 계약을 보존한다.
**Tech Stack:** Unity6000.3.8f1/C#/Newtonsoft3.2.2, 새 package 없음.
**Spec:** [상위 설계](../../../../TFTDataPipeline.md) §6.6, [워크플로](../../../README.md). 기준 main7922fa61cc0d5fe99460ffe9deea5752c7d6089f(PR23 병합), 전용 branch codex/localized-catalog-storage.

## 문제·선택

한영 이름/설명 표시와 영어 대체 표시는 이미 동작한다. 하지만 상위§6.6은 안정ID와 필드명으로 문자열을 레코드 본문과 분리한다고 요구하고, 현재 정규 JSON은 각 record.name/description 안에 koKr/enUs를 중첩한다. Phase1의 상세 승인 모델 LocalizedText와 상위 표현 계약의 차이를 확인했다. 현재 동작에 맞춰 상위 문구를 줄여 완료로 간주하지 않는다.

root 권고는 저장 형식만 정규화하고 기존 Domain/UI API는 유지하는 것이다. 모든 Domain record를 새 mutable 언어 서비스로 치환하면 수명/참조/출처 범위를 불필요하게 넓힌다. 기존 형식에 별도 표를 중복 추가하면 두 원본이 충돌해 채택하지 않는다. 이 선택은 root의 제안이며 A/B·사용자 승인 전이다.

대상은 현재 LocalizedText인 네 종류의 name/description이다. stableId·role·tier·변수명·URL은 번역 문자열로 해석하지 않는다. 단일 manifest.setName이나 미공급 언어를 임의로 번역하지 않는다. UI 레이아웃·언어 선택·Unity Localization package·다운로드·API·증강 분류·집계는 추가하지 않는다.

## Global Constraints

- 기존 schema1/2/3/4 JSON·해시·문서Sources·선택FieldOrigins·카탈로그/관측/아이콘은 보존한다. 동일 identity를 덮어쓰거나 자동 migration하지 않는다.
- 공식우선·실행별 보완 opt-in, 현재 source profile·원본 버전 구분·분류 미등록 차단·최소100 하한을 유지한다. 새 source endpoint나 외부 데이터 전송 기능을 제품에 추가하지 않는다.
- 실제API/실제증강등록0/실제n100/성급·장착 미공급 의미/KI-001/사용자 최종확인을 이번 완료로 대체하지 않는다. 키/PUUID/원본경기응답은 입력·출력·협업자료에 넣지 않는다.
- 사용자Volume·키문서·Chrome·Unity사용자창을 보존한다. docs/riot-api-key.md 본문 열람 금지. 소유창/프레임 정리는 기존 지침을 따른다.
- 제품 코드는 사용자 명시 승인 후 시작한다. 기획 검수와 사용자 승인을 실행 증거로 취급하지 않는다.

## schema5 저장 계약

1. root에 localizations 배열을 마지막 고정 속성으로 둔다. 각 항목은 recordType(unit|trait|item|augment), stableId, field(name|description), koKr:string, enUs:string, usedEnglishFallback:bool의 정확한6속성이다. type/id/field는 ordinal 복합키이고 구분자로 이어붙여 충돌시키지 않는다. 두 언어의 공백·개행·날짜 같은 원문 문자열을 그대로 보존한다.
2. 모든 실제 record에 name/description 두 항목을 요구하며 둘 다 빈 설명도 유효한 항목이다. 표 개수는 정확히2×전체레코드 수다. 누락/중복키/알수없는kind·ID·field/불필요한항목/잘못된속성·타입/인라인name·description 중복을 고정 JsonSerializationException으로 거절한다. 레코드 자체의 같은kind 내 중복ID도 모호한 표 연결이므로 거절한다. 서로 다른kind의 같은ID는 구분한다.
3. usedEnglishFallback은 string.IsNullOrWhiteSpace(koKr) && !string.IsNullOrWhiteSpace(enUs)와 반드시 같아야 한다. 거짓·누락·null·숫자/문자열 bool을 복구하지 않는다. 독자적으로 저장flag가 UI의 다른 값을 덮어쓰지 않는다.
4. 표는 type/id/field ordinal 정렬, 각 row 속성은 위 순서로 기록한다. 기존 구조화 map/list/null/false·원본 확장·source stamps·fieldOrigins의 canonical 규칙은 그대로다. 줄끝은LF+마지막개행이며 동일입력은동일bytes/hash다.
5. schema5의 record JSON에는 name/description을 기록하지 않는다. read는 정확한 표를 검사한 뒤 기존 LocalizedText와 record API를 복원한다. Domain의 Name/Description/UsedEnglishFallback은 기존 동작이다. 저장된 표와 인라인을 병렬의 두 진실로 만들지 않는다. 별도의 공개 Domain key/index API는 이번에 추가하지 않는다.
6. FieldOrigins는 기존 논리 필드 /name/koKr, /name/enUs, /description/...를 유지한다. 표의 물리 JSON 위치로 source pointer를 바꾸지 않는다. 빈 문자열에 가상 source를 만들거나 문서Sources를 잘라내지 않는다. schema5에서도 schema4의 모든 구조화/extension 출처 검증을 실행한다.
7. schema1~4 writer/reader는 기존형식을 유지하고 root의 localizations가case-insensitive로 주입되면 거절한다. 구 형식을 새 형식으로 읽었다고 표시하지 않는다. 새 schema5의 표 key는 exact-case이며 잘못된locale key(ko-KR 등)나 추가속성을 허용하지 않는다. duplicateJSONkey·DateParseHandling.None·metadata strict타입은 기존 규칙과 같다.

## 인터페이스와 파일 책임

Assets/TFTAnalyzer 기준:

| 파일 | 책임 |
|---|---|
| Core/Serialization/LocalizedCatalogJson.cs(신규) | internal Write(StaticCatalog catalog,string structuredJson):string — schema4 구조가 있는 중간 JSON에서 인라인 두필드 추출/제거·표 기록. internal Expand(JObject root):JObject — schema5 표/record key 검증 후 deep clone에 기존 인라인 LocalizedText JSON 복원, localizations 제거. 호출 root를 변경하지 않는다. |
| Core/Serialization/CanonicalCatalogSerializer.cs | 범위1~5. schema5 root/metadata 타입을 DTO coercion 전에 확인하고 Expand한 JSON으로 기존 DTO/구조화 layer를 사용. Serialize5는 StructuredCatalogJson.Write 다음 LocalizedCatalogJson.Write. Serialize1~4 경로는 그대로다. |
| Core/Serialization/StructuredCatalogJson.cs | 기존 schema4 strict guard/Attach/Write 보존. Expand한 논리 모양을 guard할 때 metadata 검증을 건너뛰지 않는다. 일반적인 >4 조건으로 타입 검증을 우회하지 않는다. |
| Core/Validation/CatalogValidator.cs | schema5 허용·FieldOrigins 및 schema4 확장source결합 검증 유지. 현재 ==4 경계를 >=4로 확장할 책임을 명시한다. |
| Editor/Ingestion/{DataDragon/DataDragonAdapter,CommunityDragon/CommunityDragonAdapter}.cs | 새 actual import manifest5. 매핑/known pointer/locale 우선순위·오류/보고서·Sources는 바꾸지 않는다. |
| Editor/Persistence/CatalogAssetWriter.cs | 게시hash5 허용, AsSchema의4projection 추가.4projection은 모든 기존 scalar/LocalizedText/구조화자료/원본확장/모든FieldOrigins/Sources를 보존한다. 기존1~3projection은 새의미를 지우거나 옛의미를 바꾸지 않는다. |
| Tests/EditMode/Serialization/LocalizedCatalogSerializationTests.cs(신규), Persistence/LocalizedCatalogPublicationTests.cs(신규) | below TDD·actual 경로·구형식골든/재사용·실패/복구 |
| 기존 Ingestion/StructuredStaticIngestionTests.cs, Persistence/FieldOriginPublicationTests.cs·StructuredStaticPublicationTests.cs | actual current-schema 기대만5로 갱신. 명시적schema4 fixture/시험을5로 바꾸어 구4coverage를 지우지 않는다. |
| Tests/Fixtures/Static/schema4-localization-compat.json(신규), .gitattributes | 제품serializer 변경 전 실제7922fa6의4serializer로 창작골든 생성·SHA/LF동결. 구1~3golden 재생성 금지. |

변경 이유 없이 다른 모듈을 재구성하지 않는다. Domain/UI 코드가 꼭 바뀌어야 하면 이유와 검증을 이 계획에 반영하며 새 UI기능으로 확대하지 않는다.

## TODO·검수·QA

### 1. 원본4 동결과 schema5 문자열 표
- [ ] 승인 후 제품수정 전에 실제4serializer로 네kind·이름/설명·구조화값/출처의 창작golden을 생성하고 원본실행·SHA를 기록한다. 기존1~3golden은 보존한다.
- [ ] LocalizedCatalogSerializationTests의 LiteralRoundTripAndFallbackRecords, InvalidLocalizationTable, LegacySchemasRejectInjectedTable, CanonicalOrderingAndLegacyBytes에서 실제RED를 확보한다. ko없음/en유효·ko우선·양쪽공백·빈설명·날짜/개행·동일ID다른kind·/~/| ID·표순서·누락/중복/추가/인라인·타입·flag불일치를 검증한다. schema5 missing구조화출처/extensionSource변조도 거절해야 한다.
- [ ] 새 helper와 serializer/validator 연결 후 GREEN. 기존1~4bytes/hash·fieldOrigins와 새표/문자열변경hash를 비교한다. schema5 오류를 string coercion이나 field 초기값으로 숨기지 않는다.

### 2. 실제 가져오기와 불변 게시
- [ ] LocalizedCatalogPublicationTests에서 fakeIHttpTransport→actual양adapter→공식우선merge→icon→writer→loader→browser까지 공급되는 RED→GREEN. 한영이름/설명·fallback·표없어진인라인·논리출처·구조화수치·확장값을 확인한다. Raw source의 localizations라는 미지원필드는 확장JSON일뿐 내부표/권한으로 승격하지 않는다.
- [ ] 현재schema5가 새pack에 도달하고 VerifyPublishedHash가5를 검증한다. 구1/2/3/4 같은identity·같은기존의미는 실제구형식/해시로재사용하고 파일/인덱스를보존한다. 구4의구조화값·sourceSHA·기존leaf출처가 달라지면 충돌이며 구4projection에서 이를 빼지 않는다.
- [ ] 새5의 같은identity 다른문자열/표값도 충돌. 취소·쓰기실패·index복구와 기존bytes보존을 확인한다. 실제 API·분류 등록은 하지 않는다. 새pack5와 구pack재사용을 혼동하지 않는다.

### 3. 회귀·독립 검수·통합
- [ ] actual source 기대만 갱신하고1~4명시fixture coverage를 유지한다. 관련GREEN 후 소스동결→전체EditMode1회·Console/보호/기존창/nativeframe 대조. 새 실패/변경 없는 전체suite 반복 금지.
- [ ] 기존 동일 코드검수·QA 담당을 재사용해 TODO/실제diff/증거 확인. UI레이아웃 변경은 없으므로 같은PNG를 반복하지 않는다. UI동작 변경이 필요하면 기존소유창도구의 한작업/finally 캡처·예외정리를 사용한다.
- [ ] 문서/raw/meta/사용법·공급한계·최신상태를 갱신해 commit/PR. 최신head CodeRabbit·미해결·CI·실행증거를5분간격직접확인하고 충족시승인범위main병합. Hermes0·사용자최종결과승인별도.

검증명령은 CLI절대경로의 command run_tests --project-path C:/Unity/TFTDataAnalyzer --mode editor --filter_type testName --filter LocalizedCatalog --timeout180 --format json, 전체는 --filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout300. recompile 완료/실패false·새시험인식>0·진행job없음을 확인하고 단일jobterminal까지 기다린다.

## Review Focus

잘못된kind/ID로다른레코드문자열이연결되는문제(TODO1/2), derivedfallbackflag불일치·빈설명/공백누락(TODO1), schema5에서4출처검증을건너뛰는경계(TODO1), 구4projection에서구조화값/출처를빼서재사용에성공시키는문제(TODO2), 실제adapter가4에머물고단위helper만5인미연결(TODO2)을 반드시검수한다. A/B검토와 사용자명시승인전 제품·새시험·Unity실행은하지않는다.
