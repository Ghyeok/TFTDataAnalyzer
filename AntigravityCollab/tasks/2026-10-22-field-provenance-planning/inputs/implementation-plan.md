# 필드별 정적 데이터 출처 기록 구현 계획

상태: 초안. 기준mainf0058799355cd2754f193eff38d7dd415cd700e6(PR20 병합), branch codex/field-provenance. 기획 A/B 검토와 사용자 명시 승인 전 제품 변경0. 실제 경기 수집은 사용자 요청대로 미룬다.

**Goal:** 새 정적 카탈로그에 값별 선택 출처를 보존하고, 기존 카탈로그의 출처를 추측하지 않으면서 탐색 화면에서 확인하게 한다.
**Architecture:** StaticCatalog의 별도 불변 FieldOrigins 목록을 schema3 JSON과 콘텐츠 해시에 포함한다. 어댑터와 병합기가 실제 선택한 값을 추적하고 게시·다시 읽기까지 보존한다. schema1/2 바이트와 기존 식별자·경로는 그대로 읽으며 자동 마이그레이션하지 않는다.
**Tech Stack:** Unity6000.3.8f1, C#, 기존 Newtonsoft canonical serializer/hasher, UI Toolkit, NUnit EditMode. root가 executing-plans로 구현하고 기존 코드검수/QA 담당을 재사용한다.
**Spec:** [상위 설계](../../../../TFTDataPipeline.md) §4·6.2–6.6·7. 필드별 보완 출처 기록은 기존 요구사항이다. 현재 SourceStamp/record.Sources는 문서 단위 출처만 보유하고 CatalogMerger의 필드별 결정은 카탈로그에 저장되지 않는다.

## 범위와 표시

- 새 `FieldOrigin`은 RecordType, StableId, 정규화한 JSON pointer, 선택한 원본 SourceStamp 참조들을 가진다. `StaticCatalog.FieldOrigins`는 선택 인수로 추가해 기존 생성 호출을 유지한다. 4개 record.Sources의 기존 문서 목록은 바꾸지 않는다.
- 대상 포인터는 해당 record의 실제 모델 필드다. 예: `/name/koKr`, `/name/enUs`, `/description/koKr`, `/cost`, `/traitIds`, `/components`, `/role`, `/tier`, `/relatedIds`, `/iconPath`, `/effects/<escaped-key>`. 키의 `~`와 `/`를 이스케이프하며 표시 이름으로 관계를 연결하지 않는다.
- 값이 비어 있거나 파서가 공급하지 않은 필드는 출처를 만들지 않는다. 영어 대체는 실제 `/name/enUs` 등의 출처를 보여주고 존재하지 않는 한국어 원본을 만들어내지 않는다. 0인 정적 효과처럼 실제 존재하는 값은 누락과 구분한다.
- 탐색 상세에 ‘필드별 출처’를 추가해 포인터·공식/보완 구분·문서 URL·원본 SHA256을 일반 텍스트로 표시한다. schema1/2에는 ‘필드별 출처 정보 없음’을 표시하며 기존 문서 단위 출처와 구분한다. 새 링크 실행·다운로드·추천·증강 허용은 추가하지 않는다.

## 생성·검증 계약

- DataDragonAdapter는 실제 ko/en 문서와 필드 선택에 맞는 origin을 생성한다. CommunityDragonAdapter도 실제로 읽은 문서를 참조한다. 기존 변환 값·공식 우선·보완 opt-in·경기 정책은 바꾸지 않는다.
- CatalogMerger는 공식 값 유지, 빈 필드 보완, 동일 값 양쪽 존재, 보완 전용 record, 목록, effects의 키별 선택과 locale별 혼합을 정확히 추적한다. 선택한 입력 필드에 origin이 없으면 추측하지 않는다. 기존 MergeConflicts의 의미는 유지한다.
- schema3은 FieldOrigins를 명시한다. 중복 (종류,ID,pointer), 없는 record/필드, 존재하지 않는 source 참조, origin/source의 공식·보완 불일치, 필수 출처가 없는 실제 값은 검증 오류로 게시를 막는다. 원본 문서는 해당 record.Sources와 manifest.Sources에 연결돼야 한다.
- 구 스키마에는 빈 origin을 허용하고 metadata 부재를 값 출처의 공식 인증으로 바꾸지 않는다. 지원되지 않는 스키마나 구 스키마에 출처를 끼워 넣어 해시 검사를 우회하는 입력은 거절한다.
- 새 origin은 정규 정렬·불변 복사를 적용한다. 해시 정규화는 기존 규칙처럼 수집 시각·ETag를 제외하고 source URL·SHA256·공식/보완 여부와 필드 연결은 포함한다. 같은 입력은 같은 해시이며 출처만 달라도 새 schema3의 해시는 달라야 한다.
- 아이콘 staging과 모든 catalog/manifest 복사에서 origin을 보존한다. `/iconPath`는 원본 문서에서 온 아이콘 참조의 계보이며 변환된 로컬 경로를 원본 URL로 오해하지 않게 표시한다.

## 불변 게시와 구 스키마 호환

- 기본 네트워크 파이프라인의 새 팩은 schema3으로 게시한다. schema1/2 역직렬화·기존 정규 JSON·콘텐츠 해시는 동일하게 유지한다. 원본 schema1/2를 재저장하거나 metadata를 끼워 넣지 않는다.
- 같은 전체 identity의 기존 schema1/2가 있으면 새 모델을 해당 기존 스키마로 명시적으로 투영해 이전 해시 규칙으로 비교한다. 투영은 FieldOrigins만 제거하고 의미 값·기존 문서 출처·프로필을 유지한다. schema1은 기존 공식 전용 legacy 경로만 허용한다. 보완 팩을 공식 전용으로 투영하지 않는다.
- 기존 팩과 투영 해시가 같으면 기존 팩을 그대로 재사용한다. 같지 않으면 기존 불변 충돌 처리를 유지하며 덮어쓰지 않는다. 게시 결과는 실제 저장된 schema/catalog/hash를 반환해, 구 팩을 재사용했는데 새 필드 출처가 저장됐다고 표시하지 않는다.
- 기존 bool Publish 호출 호환은 유지하고 pipeline이 사용할 검증된 게시 결과(Catalog, ContentHash, SchemaVersion, Idempotent)를 별도로 제공한다. 검증 없는 파일 경로/DTO를 신뢰해 반환하지 않는다.
- 새 schema3도 동일한 전체 identity 경로를 사용하고 원자 staging·기존 실패 복구·취소·색인·AssetDatabase 갱신 계약을 유지한다. 카탈로그 schema3과 관측/집계 스키마는 별개이며 후자는 변경하지 않는다.

## 변경 파일

- 생성 `Assets/TFTAnalyzer/Core/Domain/FieldOrigin.cs`. 수정 `StaticCatalog.cs`, `CanonicalCatalogSerializer.cs`, `CatalogValidator.cs` 및 필요한 기존 manifest schema 검증. schema1/2 동작을 고정하는 fixture/시험을 먼저 추가한다.
- 수정 `Editor/Ingestion/DataDragon/DataDragonAdapter.cs`, `CommunityDragon/CommunityDragonAdapter.cs`, `CatalogMerger.cs`, `StaticCatalogPipeline.cs`; 출처 선택은 어댑터/병합 실제 결정에서 나온다.
- 수정 `Editor/Persistence/CatalogAssetWriter.cs`, `PublishedCatalogLoader.cs`와 실제 필요한 검증 경계. `LegacyCatalogResolver`의 legacy 경로 판정은 기존 공식 전용 규칙을 유지한다. 새 일반 저장 프레임워크나 외부 설정은 만들지 않는다.
- 수정 `Editor/UI/Data/CatalogBrowserViewModel.cs` 및 필요한 기존 상세 텍스트/UI만. 이미 존재하는 source-profile/read-only 탐색 구조를 사용한다.
- 시험은 기존 Domain/Serialization/Validation/Ingestion/Persistence/CatalogBrowser 시험에 배치하고 필요 시 `FieldOriginTests.cs`를 추가한다. 기존 카탈로그 검증 도구로 새 필드 표시를 합성 자료에서 확인할 수 있게 확장하되 원래 read-only 검증 모드는 유지한다.

## Review Focus

1. 기록한 origin이 실제 선택값의 source인지, 혼합 locale·목록·0인 효과·보완 전용 record·같은 값에서 추측이 들어가지 않는가.
2. schema1/2 바이트·해시·프로필과 idempotent 재가져오기가 보존되며 결과가 실제 게시 팩과 일치하는가.
3. 출처·필드 연결을 변조해도 검증/해시/loader가 거절하며 timestamp/ETag만 변한 입력은 동일한가.
4. 아이콘 변환·게시 취소/충돌·색인 복구·다시 읽기에서 metadata를 잃거나 사용자의 구 팩을 바꾸지 않는가.
5. UI가 구 자료의 부재를 인증으로 오인하지 않고 실제 PNG·창 정리·원본 보존을 별도로 검증하는가.

## Task1: 모델·정규 JSON·검증

- [ ] 기존 mixed-source 카탈로그의 필드 source를 저장/다시 읽을 수 없는 운영 경계에서 기능 RED를 확인한다. 컴파일 실패는 기능 RED로 세지 않는다.
- [ ] FieldOrigin과 선택적 catalog 목록, schema3 round trip·정규화·변조 거절·불변성·JSON pointer·source 연결 검증을 구현해 GREEN. 현재 보존 schema1/2 fixture의 JSON/해시는 이전 값과 같아야 한다.
- [ ] source timestamp/ETag 차이, origin 연결만 변경, 중복/없는 필드/없는 source·profile mismatch, 빈 값과0을 시험한다. 기존 코드검수 담당이 계약/RED·GREEN을 확인한다.

## Task2: 실제 생성·불변 게시·재읽기

- [ ] 실제 공식/보완 어댑터의 창작 ko/en payload와 병합 경로에서 locale별·효과별 source의 RED→GREEN을 확인한다. 테스트에만 origin을 주입한 성공으로 실제 공급을 대신하지 않는다.
- [ ] schema3의 source→merge→아이콘staging→게시→검증된loader round trip을 임시 루트에서 실행한다. 구 schema1/2 idempotent 재사용과 충돌/취소/복구·파일 링크 거절도 검증한다.
- [ ] 구 팩을 재사용하면 반환된 결과와 새 UI가 실제 구 팩의 ‘필드별 정보 없음’을 표시하는지 확인한다. 보호 대상 카탈로그/관측/사용자 파일을 테스트 조건에 맞춰 수정하지 않는다.
- [ ] 같은 기존 코드검수 담당이 다중 모듈 경계와 실제 실행 증거를 검수한다.

## Task3: 탐색 표시·최종 QA

- [ ] 새 source 정보/구 자료 부재/태그 비해석·기존 검색/선택/페이지 회귀. 화면 변경은 테스트 소유 창과 synthetic metadata로만 검증한다.
- [ ] 940/500px에서 새 provenance와 구 자료 부재 PNG4장을 직접 확인한다. 새로 만든 창과 예약 callback은 finally로 정리하고 전후 EditorWindow/native ID·포커스를 대조한다. 사용자 창·Inspector·Chrome은 보존한다.
- [ ] 소스 동결→관련 회귀와 전체 EditMode1회→기존 QA 담당의 실행·실제PNG·해시 대조. 새 수정/실패가 없으면 반복하지 않는다.
- [ ] 문서/원문/meta/링크·실제 모델/확인 가능한 사용량을 정리해 코드와 함께PR. 최신head CodeRabbit·미해결·CI를5분마다확인해조건충족시승인범위대로병합. 사용자 최종 결과 승인은 별도다.

## 권한·제외

실제 API/정적 다운로드/환경 변수 변경·실제 증강 분류 등록·n100 기준/집계 변경·공개 배포는 포함하지 않는다. 구 카탈로그와 관측, 사용자 `Assets/DefaultVolumeProfile.asset` 및 `docs/riot-api-key.md`를 보존하고 키 문서는 읽지 않는다. Hermes 예약은 삭제 상태다. KI-001의 원인 조사나 같은 분류 자료의 반복 AI 검토를 재개하지 않는다.
