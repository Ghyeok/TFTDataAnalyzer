# 기존 계약과 제안

CatalogDiffer의 StableList는 ID 정렬 뒤 U+001F로 연결, Effects는 키 정렬 뒤 key=value를 같은 구분자로 연결한다. Common은 SourceName/Url 안정 정렬 뒤 SourceName·Url·ETag·Sha256·IsSupplemental을 U+001F로, 출처 간 U+001E로 연결한다. RetrievedUtc는 제외한다. 도메인 값에는 이 문자가 포함될 수 있다. 공개 FieldChange.Before/After는 문자열이고 DataManagerTab이 이를 화면에 쓴다.

후보: 공개 이전/이후 문자열은 유지하되 관계 목록의 정렬된 원소·개수, 효과의 정렬된 키와 기존 R/InvariantCulture 값, 출처의 기존 정렬 순서와 비교 대상 다섯 필드를 구조적으로 비교한다. 동일 StableId의 중복 그룹 첫 항목 사용, 관계 중복 개수, 동률 출처의 기존 순서·중복, source 조회 시각 제외를 유지한다. 충돌 사례에선 구 공개 문자열이 같아도 필드 변경을 유지한다.

UI는 양쪽 카탈로그의 원래 목록·사전·출처에서 적절한 가독성 표현을 만들어 실제 제어문자, 글자 그대로 역슬래시 시퀀스, 줄바꿈, 배열 경계를 구분한다. 비교 문자열을 Split하여 원래 구조로 추정하지 않는다. 원문 rich-text 해석 방지(별도 PR), 두 방향, 네 종류, 길이·작은 창 및 소유 창 정리를 검증한다.

합성 RED/GREEN: ID·효과·출처 세 충돌, 일반·빈 값·중복·순서·문화권·역방향·특수 문자, 기존 공개 문자열 및 카탈로그/해시 보존. 검증은 외부 데이터 다운로드나 실제 경기 호출이 없어야 한다.
