# 동일 입력 소스 계약 요약

기준7922fa61cc0d5fe99460ffe9deea5752c7d6089f, PR23검수head6acc2a0와전체tree동일. 사용자파일/환경변수/원본경기응답은읽지않았다. 코드인용/요약은읽기전용이며새실행증거가아니다.

- Assets/Plan/TFTDataPipeline.md §6.6: 문자열은 안정ID/필드명키로 레코드본문과분리,ko-KR/en-US함께보존하고영어대체기록. Phase1상세는LocalizedText(koKr,enUs) 및StableId별StaticCatalog를명시해현재nested JSON과표현차이가있다. 이차이를문서축소만으로없애지않는후속후보다.
- Core/Domain/LocalizedText.cs: readonly KoKr/EnUs, DisplayValue=IsNullOrWhiteSpace(KoKr)?EnUs:KoKr, UsedEnglishFallback=IsNullOrWhiteSpace(KoKr)&&!IsNullOrWhiteSpace(EnUs). 생성자는null을빈문자열로보존한다. 각 Unit/Trait/Item/Augment는 Name/Description의immutable LocalizedText를보유한다.
- Core/Serialization/CanonicalCatalogSerializer.cs: Settings InvariantCulture, TypeNameHandling.None, NullValueHandling.Include. Serialize는ToDto→JsonConvert, schema4만StructuredCatalogJson.Write. Deserialize는현재DTOparse후ReadRoot/1~4범위·FieldOriginsGuard→Guard→4Attach이다. LocalizedDto{koKr,enUs}가각record.name/description안에들어간다. 새5는DTOcoercion전rawroot표/metadata strict검증이필요하다. 구1~4의기존write 경로와bytes를보존한다.
- StructuredCatalogJson.ReadRoot는DateParseHandling.None/DuplicatePropertyNameHandling.Error/trailing거절. Guard는schema==4에서Metadata와recordarray타입을검사하고typed구조·extension을검증한다. Attach/Write는BaseStats·수치슬롯·구간·unique·확장을기존DTO에입힌다. 새5는이레이어를빠뜨리면안된다.
- FieldOriginFields.Required(record,int schemaVersion=3)는legacyname/description등논리pointer를만들고 schema>=4일때구조화leaf를더한다. 날짜같은원문을바꾸지않고빈문자열에가상origin을만들지않는다. CatalogValidator.ValidateFieldOrigins는지금>4거절/return이고 schema==4에서extension.SourceName와origin결합을검증한다.5허용만하고==4블록을빠뜨리면검증이약해진다.
- 실제양adapter의manifest현재4. CatalogMerger는현재값/이름/설명·Sources를공식우선으로선택하고최종schema최댓값·leaforigin을보존한다. schema4층은BaseStats9조합·전체목록·false우선·확장원문union이다. 새현지화기능이이선택을바꾸면안된다. source의unknown필드는inert extension이며임의내부표의입력이아니다.
- CatalogAssetWriter.VerifyPublishedHash는expectedIdentity인비legacy에서2/3/4만허용한다. PublishVerified는incoming>=3이고existingSchema<incoming이면AsSchema로구projection후contenthash를비교한다. 현재AsSchema는1~3만허용,네종류새구조를모두drop한clone,3이면기존logicalorigin만남긴다. 새4projection은전구조·전origin·Sources를보존해야한다. 실제sourceSHA/origin변화는충돌이정상이다. normalization/iconstaging은현재네종류전체구조를복사한다.
- Browser VM은schema>=4의구조화출처를기존4096상세에서제외하고전용20행페이지로읽는다. Name/Description은기존LocalizedTextAPI,영어대체는UsedEnglishFallback을표시한다.5reader의hydration후기존UI와CSV/정책은동일해야한다. storage표를읽었다고old4를5로보고하면안된다.
- 현재최종실행939/937P0F/hostsymlink2skip,UI227·관련113·게시16·8PNG/창/보호보존·CodeRabbit최신headSUCCESS/미해결0/PR23main병합. 이는기준기능의증거이지새schema5의검증이아니다. 실제API·실제증강등록0·실제n100·성급/장착미공급의미·KI-001·최종사용자확인은여전히잔여다.

도구/원본repo/Unity/네트워크/쓰기/재위임은검수범위밖이다. 이두문서만으로계약위험을검토하고숨은모델/추론/usage는추정하지않는다.
