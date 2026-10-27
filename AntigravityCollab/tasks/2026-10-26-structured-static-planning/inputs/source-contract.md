# 구조화 정적 데이터 검수의 공통 기준

기준 commit: f9974726021bb35edfdb3b5392904db42d1c270f (PR22 병합 main). 검수 대상은 함께 제공한 implementation-plan.md 초안이며 제품 변경은 아직 미승인이다. 원본 저장소/비밀/웹/Unity를 열지 않고 이 두 문서만 검토한다.

## 사용자와 상위 설계

TFTDataAnalyzer는 Unity Editor의 패치별 정적 자료·익명 과거 경기 분석 도구다. 전투 재현/예측/인게임 추천/플레이어 프로필은 제외한다. 사용자 목표는 미구현 계획의 구현이며 CodeRabbit 최신 head를5분마다 확인해 증거 충족 시 병합·다음 작업을 허용했다. Hermes 예약은 삭제 유지다. 새 제품 기획은 A=Antigravity/B=GPT가 같은 snapshot을 독립 검토하고 사용자 명시 승인 후 구현한다. 실제 API 수집 검증은 사용자 요청으로 미뤘다.

상위 TFTDataPipeline.md §6.2 기물: 공개 기본·성급별 능력치, 스킬 설명·정적 효과. §6.3 시너지: 활성 구간·구간별 효과. §6.4 아이템: 조합/능력치/고유·중복·장착 제한. §6.5 증강: 등급·설명·효과·관련 stable IDs. §4/7: 공식 우선, 비어 있을 때만 보완, 필드 출처, 충돌 보고, 모든 관계 stable IDs, 미포함 원본 필드는 출처 있는 비실행 확장 자료, 같은 입력→같은 canonical JSON/hash. 기존 패치 팩은 덮어쓰지 않는다. 최소 비율 표본100/Wilson95%, Legend/미분류 증강 차단을 유지한다.

## 현재 실제 코드 경계

UnitRecord: StableId/Name/Cost/TraitIds/IconPath/Sources/Role/Description/flat Effects. TraitRecord: ID/Name/Description/flat Effects/Icon/Sources. ItemRecord: ID/Name/Description/ComponentIds/flat Effects/Icon/Sources. AugmentRecord: ID/Name/Tier/Description/RelatedIds/flat Effects/Icon/Sources. 새 구조·확장 필드는 없다.

CanonicalCatalogSerializer는 schema1..3만 읽고 쓰며 schema3는 root fieldOrigins 필수 배열이다. schema1/2에 fieldOrigins가 있으면 거절한다. FieldOriginFields.Required(record)는 실제 값에서 필수 pointer를 독립 산출하며 validator는 selected leaf의 출처가 record/manifest sources에 있어야 통과시킨다. schema3 golden은 아직 없고 schema1/2 골든이 있다. 새 테스트에서 기존 serializer를 사용한 schema3 골든 동결이 필요하다.

DataDragonAdapter.Parse는 schema3 official-only를 만들고 en primary/ko fallback을 사용한다. Unit의 stats/effects object를 기존 flat Effects로 읽으며 이를 없애면 구 팩 의미가 바뀐다. ParsedFieldOrigins.Add는 현재 Required(record)의 nonlocalized pointer를 primary 출처로 연결한다. 새 구조에 무조건 적용하면 secondary에서 선택한 값의 출처가 틀릴 수 있다. 현재 DataDragon에 별도 진단 result는 없다.

CommunityDragonAdapter.ParseWithDiagnostics는 schema3 catalog+Issues를 반환한다. selected setData.number의 명시 소속을 사용하며 namespace 접두사로 멤버를 추측하지 않는다. 표시 이름 trait는 동일 locale/선택 세트에서 유일한 정확 일치만 stable ID로 변환한다. nested ability.desc는 기존 flat desc가 없을 때 locale별 보완한다. set-scoped items/augments 또는 root.items를 읽는다. root 경로는 동일 요청의 검증된 official-only catalog와 PatchKey(setNumber포함) 일치가 필요하며 isAugment bool+동일 종류 official stable ID 교집합만 허용한다. 범위 밖 글로벌 보완 전용 항목은 제외한다. stats/ability.variables/trait effects 배열은 현재 미지원 count 경고다. composition은 stable ID 문자열 배열이며 기존 validator가 참조 검증한다. 비용은1..9, 손상과 정상 범위 밖 제외를 구분한다.

NetworkStaticCatalogSource→CatalogMerger→StaticCatalogPipeline는 실제 adapter/병합/ValidationReport→icon staging→writer→index/refresh→PublishedCatalogLoader로 연결된다. loaded.Issues는 아이콘 전후 검증과 보고서/manifest 카운트에 반영된다. 오류 시 게시 차단, 취소/실패 staging 정리, 게시 후 index/refresh 실패 복구가 있다. 새 필드가 중간의 record 재생성에서 빠지지 않아야 한다.

CatalogMerger는 기존 text/number/list/effects의 공식 우선과 MergeConflict를 만든다. 현재 origin 선택은 conflict FieldName의 dot→slash 변환을 쓰므로 새 key/배열/escaped pointer에는 주의가 필요하다. schema3일 때만 merged manifest3을 만드는 비교도 확장해야 한다. CatalogAssetWriter의 VerifyPublishedHash는 schema2/3 identity 경로와 strict provenance를 확인한다. 기존 schema3 입력과 schema1/2 팩 비교는 AsSchema projection을 사용하고 PublishVerified는 실제 저장된 구 형식의 catalog/hash를 반환한다. 새 schema4를3으로 projection할 때 기존 fieldOrigins까지 없애면 안 된다. CatalogDiffer는 기존 필드만 비교한다.

CatalogBrowserViewModel은 기존 이름/설명/관계/effects/source를 DetailsText에 투영한다. 각 field4096자 Clip이 있어 새 구조를 단일 문자열에 합치면 후반이 숨는다. UI는 ListView50개 검색 페이지, Label richText=false, callback Dispose 해제를 사용하며 새 상세 페이지는 아직 없다. 카탈로그 검증 스크립트는 소유 창을 생성·추적하고 Close/finally, 기존 EditorWindow/native frame/focus 보존을 확인한다. 사용자 창/Inspector/Chrome을 닫거나 변형하면 안 된다.

## 이번 읽기 전용 자료 조사 (버전 호환성 인증 아님)

로컬 static cache SHA256=4627a0e4ded9f884c351e74d929d5da5b5aa403296b4d75dea488973a3c2ebdb와 현재 기존 official-only catalog를 root가 읽었다. URL·locale·실제 source version은 인증하지 못했으며 네트워크 다운로드/게시0이다. 원본/아이콘/이름·효과 본문은 검수 입력에 복사하지 않는다.

선택 number18 기물91개(stats91), stats 값 float832/int66/null12. ability.variables element keys name/value, 관측19변수의 value는 list이며7슬롯씩 float133. selected unit의 starStats key0. 시너지 effects의 minUnits는int90/null1, maxUnits/style는int91, variables는dict91(float212). 전역items4221의 unique는bool이며 incompatibleTraits183토큰은string이다. 공식 item IDs와 isAugment=false의 교집합522개는 unique bool522, incompatibleTraits토큰0. 즉 전역 제한 토큰을 선택 세트 관계로 연결할 근거가 없다. 7슬롯의 의미·별수별 공식 배율·unique의 장착 수 의미를 이 자료에서 확정하지 않는다.

기존 공식 Set18/18.2/source16.18.1 catalog는64units/36traits/522items/485augments이며 바이트/해시 보존 대상이다. 실제 증강 분류 승인0이며 현재 데이터 수록으로 Legend 배제/Standard를 인정하지 않는다. 서비스 전체 완료에는 실제자료·API·분류·n100입도·사용자 확인 등이 별도 남는다. KI-001 간헐 실패는 알려진 문제로 보존하며 원인 규명은 다음 기능의 선행조건이 아니다.

## 검수 요청 범위

계획의 사용자 가치·완료 조건과 데이터 손상/비밀/과구현 위험을 점검한다. 단순 파일 위치/코딩 스타일보다 승인 전 해결해야 하는 계약 모호성을 최대6개로 보고한다. 없는 원본 의미를 상상해 새 star field/제한 로직을 요구하지 않는다. 같은 raw자료로 정책 분류 재검토/실제 API 실행/새 감사 역할을 제안하지 않는다. 개발 방법은 root 구현·기존 담당 재사용이며 신규 에이전트 생성0이다. 사용자 승인/제품 QA/실제 패치 호환 인증은 기획 pass와 별개다.
