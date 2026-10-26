기준 커밋: 54a29f35fad8d5c3624654dd2a281052bec7a340 (PR21 문서 보완 head; main 병합 전 기획 초안)

# 동결 소스 근거

기획 입력은 제공한2문서뿐이다. 제품 코드/네트워크/Unity/추가 agent 호출 없이 읽기 전용 독립 검토한다. 상대 결과는 제공하지 않는다. 실제 모델·추론·사용량을 확인 못하면 미확인으로 기록한다.

상위 설계 TFTDataPipeline.md §7.4는 모든 저장 관계를 stable ID로 요구한다. 원본 표시 이름을 문서 안의 명시적 유일한 stable ID로 해석하는 것을 허용할지와 검증 범위를 검토해야 한다. §6.2~6.4의 전체 구조 필드는 아직 미완성이며 이번 좁은 호환 작업이 전체 완성을 뜻하지 않는다.

CommunityDragonAdapter.Parse는 patch로 setData.number를 고른 후 모든 종류 Join에서 BelongsToSet(id)가 patch.SetId/TFT<번호>_/TEST<번호>_로 시작하는지 필터한다. Records는 set[name] JArray가 빈 배열이어도 root[name]를 가린다. Augments는 독립 augments만 읽는다. ReadEffects는 numeric JObject만, unit 설명은 flat desc/description만 읽는다. Index는 현재 중복을 덮어쓴다.

NetworkStaticCatalogSource.LoadAsync는 DataDragon Fetch/Parse로 official을 먼저 만들고 opt-in일 때만 보완 Fetch/Parse/merge한다. 기존 Parse는 koJson,enJson,PatchKey 3인수다. StaticSourceLoadResult는 Catalog/RawResponses/MergeConflicts만 보유한다. StaticCatalogPipeline은 CatalogValidator.Validate(catalog)의 ValidationReport로 게시를 판단하고 기존 보고서에 결과를 전달한다. ValidationReport(IEnumerable<ValidationIssue>)는 immutable 정렬/중복제거/ErrorCount/WarningCount/CanPublish를 갖는다. 생성자 선택 인수 추가로 기존 fake provider 호환이 가능하다.

FieldOrigins는 새schema3의 record별 pointer/source를 저장한다. ParsedFieldOrigins.Add는 실제 locale JObject와 stamp를 받아 4개 정확한 locale pointer만 locale로 분기하고 나머지는 en??ko primary다. 관계의 source는 실제 선택 document다. CatalogMerger는 공식 값 우선/빈 값만 보완/보완 전용 record 지원을 가지며 필수 origins가 없으면 validator가 차단한다. 구schema1/2 byte/hash는 보존하며 같은 전체identity는 writer가 구schema 투영 해시로 비교해 다르면 immutable conflict다.

진단 로컬 캐시2문서 각각: raw byte SHA는 filename과 일치하나 source URL/locale/version 근거 미확인. root items,setData,sets; number18 선택1개. champions91/traits36/set.items0/root.items4221. 현재 adapter Parse 진단 출력 네종류 모두0. 네트워크0/게시0/기존쓰기0. 91champions 중90개 cost1~9,1개cost0,공식ID와64개일치. trait36개는 공식ID와36개일치. unit trait 참조157개는 모두 stable ID일치0,같은 문서 trait.name과 정확히유일매칭157,각 문서 중복 trait.name0. root.items는4221개 모두 isAugment bool, apiName string; composition584tokens 모두string. 공식 itemID와522개/augmentID와485개일치. 이는 특정패치 인증이 아니라 shape 진단이다. 비용0이 공식ID에속하는지는 아직 미검증이며 구현시 창작fixture와 정확한 검증을 해야한다.

기존 unit 모델은 Cost,TraitIds,Role,Description,flat Effects,IconPath 등, trait 모델은 Description,flat Effects,IconPath 등이다. stats는 object10keys, ability는 desc/icon/name/variables, trait effects는 array(minUnits,maxUnits/style/variables)이므로 새typed 모델 없이는 구조 보존을 완료했다고 말할 수 없다. 글로벌 isAugment는 Legend 정책분류와 별개이며 승인분류 미공급은 계속 차단해야 한다.

보호 파일1399개/현재 창9/native1/포커스를 앞 사이클에서 보호 검증했으나 새 작업의 사전 snapshot은 구현 승인 후 다시 고정한다. 실제 경기 검증/환경변수는 사용자 요청으로 연기됐다. 숨겨진 추론이나 비밀값은 기록하지 않는다.
