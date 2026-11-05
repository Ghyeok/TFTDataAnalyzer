# 현행 계약과 검토할 위험

MatchAnonymizer.Read는 엄격한 스트리밍 JSON 검사 후 Required.Always DTO를 파싱하며 예외를 제한된 MatchPayloadException으로 바꾼다. 그 뒤 Project에서 data_version 2, 카탈로그에 맞는 숫자 게임 릴리스 및 참가자 데이터 등을 검사한다. DTO 필수 조건이 실패하면 data_version과 게임 버전 검증까지 도달하지 않을 수 있다. 무작정 예외 문자열이나 경로를 표시하면 원문 노출이 가능하다.

MatchCollectionService는 상세 조회·익명화·저장 상태를 집계한다. catalog_mismatch는 필터, 다른 MatchPayloadException은 실패로 센다. RiotApiException은 정해진 상태만 노출하고 인증/제한에서는 중단한다. 저장/정리 오류는 중단한다. MatchCollectionSummary와 CollectionProgress를 거쳐 CollectionTab에 표시한다. 식별자와 원문은 저장하지 않는다.

과제: 엄격 검사·용량 상한·중복 필드 거절·stream 정리·기존 정상 결과를 보존하면서 신뢰할 수 있는 고정 실패 범주를 전달한다. 파싱이 조기에 끝나 진단할 수 없는 다른 원인을 있다고 단정하지 않는다. 고정 범주별 집계의 합산·취소·새 실행 초기화·기존 결과 보존·실패 표시의 상태와 문구·누출 방지 및 테스트 방법을 검토한다. 실제 경기 요청 없이 합성 입력, 서비스, UI 회귀와 개인정보 sentinel 검증을 사용한다.
