# 소스 계약과 확인된 증거

기준 git HEAD 6a6f787 (2026-11-02 프로젝트 상태). 현재 AGENTS/모델정책·README·기존 체크포인트/known-issues와 사용자 Volume 및 키 문서는 기존 미커밋 변경으로 보존한다. 키 문서 본문 열람 금지.

MatchCollectionService.CollectOne은 상세 응답 취득 후 Fetched++, MatchAnonymizer.Read에서 catalog_mismatch면 Filtered++ 나머지 MatchPayloadException은 Failed++ 후 계속, 일반 저장/정리 예외는 Failed++ 후 중단한다. 따라서 최종 5 fetched/5 failed가 반복 일반 저장 catch와 같다고 단정할 수 없다. 상세 코드들은 화면에 각각 노출되지 않는다.

MatchAnonymizer.Read는 전체 RiotMatchDto를 먼저 Populate하며 필수 DTO 필드와 타입 실패 등을 invalid_match_payload로 통합한다. Project에서 data_version 2 외는 unsupported, 경기 set/릴리스가 카탈로그와 다르면 catalog_mismatch다. 파싱이 먼저 실패하면 버전/세트 불일치 판정에 도달하지 않는다. RiotMatchDtos는 participant.augments array를 필수로 요구한다. 이미 한 경기 단일 진단은 invalid_match_payload였다.

안전 진단에서 요청 시점의 최신 경기 한 건에 대한 제한된 Boolean 결과(진단 간 동일 경기 여부는 미확인): MetadataTypes=true, InfoTypes=true, EightPlayers=true, Placement/LastRound/Units/Traits=true, Augments=false, DataVersion1=false, DataVersion2=false, DataVersionOther=true. 데이터 버전 구체값과 경기 set/version/mode 및 augments의 부재/null/타입은 출력하거나 기록하지 않았다. 값 오류·타입 오류·모드 차이·미지원 데이터 버전은 가설로만 남긴다. 데이터/개인정보를 외부 입력에 포함하지 않는다.

기존 schema1~5 카탈로그/파일·관측을 보존하고 사용자 창·Chrome을 건드리지 않는다. 정적 카탈로그 18.2가 실제 경기의 버전과 맞는지 미확인. 수집 확인 조건과 제한, 저장 시 익명화, 401/403/429 중단을 유지한다. 수동 API 가동과 실제 경기 자료의 전송·저장 범위는 별도로 명시한다.
