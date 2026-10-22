# B P2 보완: schema3 필수 origin 조건

기준 f0058799355cd2754f193eff38d7dd415cd700e6. 기존 2개 동결 입력은 불변이다. 이 문서는 해당 P2 한 건의 결정과 근거만 추가하며 구현 완료 증거가 아니다.

## 현재 코드의 제한된 근거

root가 기준 코드에서 읽은 사실이다. B는 원본 파일을 새로 읽지 않고 제공된 사실의 계약 일관성을 검토한다.

- Core/Domain의 4개 record에서 직접 숫자 스칼라는 UnitRecord.Cost(int)다. 나머지 숫자는 Effects dictionary의 실제 존재 키다.
- `Assets/TFTAnalyzer/Core/Validation/CatalogValidator.cs:52–55`: `if (unit.Cost < 1 || unit.Cost > 9)`이면 `unit_cost_out_of_range` 오류, 문구 `Unit cost must be between 1 and 9.`
- `Editor/Ingestion/DataDragon/DataDragonAdapter.cs:356–367`: ReadInt는 alias 순서로 첫 Integer 토큰을 반환하며 실제 0도 즉시 반환한다. 토큰이 없으면 0을 반환한다.
- 같은 파일 :378–396: ReadNumericBag은 실제 JObject의 Integer/Float 속성만 dictionary에 넣는다. 실제 0은 키를 유지하고 absent bag/key는 키를 만들지 않는다. 뒤 bag의 같은 key는 덮어쓴다.
- record 문자열·locale·목록의 비어 있음은 모델에서 판별할 수 있다. 기존 이름/아이콘 등 값 유효성 검증은 별도이며 완화하지 않는다.

## 확정할 계약

validator는 origin 생산자가 준 목록을 필수 필드 목록으로 삼지 않고 고정 도메인 규칙으로 모델의 필수 pointer를 계산한다. adapter는 실제 선택 토큰/document로만 origin을 만든다. record.Sources나 기본 초기값으로 출처를 추측하지 않는다.

1. 모든 record `/stableId` 항상 필수. 빈 ID는 기존 검증도 거절한다. 사용자 지정 manifest set/patch identity, Sources/FieldOrigins metadata 자체는 record 값 출처 대상에서 제외한다.
2. unit `/cost` 항상 필수. 원본 없음→Cost0,origin없음→비용/출처 오류. 원본 실제0→Cost0,origin있음→비용 오류. 원본 유효1–9에서 origin 제거→출처 오류. 기존 1–9 규칙 유지, 0을 정상으로 바꾸지 않는다. cost0을 뒤 tier로 건너뛰지 않는다.
3. 모델에 존재하는 모든 `/effects/<escaped-key>` 필수. 실제0도 포함. 모델에 key가 없는 경우에는 요구하지 않는다. 실제 마지막 bag 선택과 문서에 연결한다.
4. name/description 각 locale, role/tier/iconPath는 모델 문자열이 공백이 아닐 때 필수. 빈 선택 문자열에는 origin을 만들지 않고 기존 별도 값 검증은 유지한다.
5. traitIds/components/relatedIds는 모델 목록이 비어 있지 않을 때 필수. 선택 목록 전체를 공급한 문서에 연결하며 원소별 출처를 주장하지 않는다. 빈 목록에는 요구하지 않는다.
6. 별도 presence bitset을 도입하지 않는다. 모델에 표현되지 않는 raw optional 필드의 원래 존재성까지 인증하지 않는다. 새 default scalar가 발견되면 묵시적으로 허용하지 않고 규칙부터 명시한다.

## 추가 완료 조건

adapter→serializer→validator 경로에서 비용 부재, 실제 비용0, 실제 유효 비용의 origin 제거를 각각 확인한다. 앞 두 경우 모두 게시 거부하되 실제0에서만 원본 origin이 남음을 확인한다. effects 키 부재, 실제0, 실제0의 origin 제거와 빈 선택 문자열/목록, nonempty 선택 필드의 origin 제거도 확인한다. 구 schema1/2의 JSON/hash 보존·metadata없음 표시는 기존 계획대로다.

이는 최초 계획의 출처 완전성 검증을 구체화하며 제품 범위·값 변환·기존 팩 변경 권한을 확장하지 않는다. A의 최초 범위 판정은 재실행하지 않는다. B는 이 계약으로 P2가 닫히는지만 판단하고 새 전체 분석이나 구현을 하지 않는다.
