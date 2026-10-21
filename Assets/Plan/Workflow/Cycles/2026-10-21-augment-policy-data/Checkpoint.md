# 실제 증강 분류 자료 첫 등록 체크포인트

2026-10-21. 기준 main3484b13(PR18 병합), 기획 당시 branch codex/augment-policy-initial-pack. 현재 **기획 A/B 반려, 추가 공식 자료 필요**, 분류 등록 관련 제품 코드·승인 배열·정책 파일 변경0. 현재 문서는 codex/collection-quarantine-report에서 별도 승인된 격리 보고서 작업과 함께 보존한다. 정확한4개ID와일반효과만으로Legend배제근거가부족해등록0을유지한다. [통합검토](refined/PlanningReview.md). 같은자료로반복AI호출/재분류하지않고새근거가있을때재개한다.

현재 카탈로그485개 중 정확한 Set18 고유4개를 첫 Standard 후보로 제안한다. 공식 문맥과 개별 효과를 대조한 판단이며 공식 enum/인간 검수는 아니다. 나머지481개·Legend 역사와 동명 ID의 불확실성은 차단 유지한다. [계획](refined/ImplementationPlan.md)의 범위·객관적 완료조건을 따른다.

PR18 head ea890d6은 CodeRabbit SUCCESS/미해결0/별도CI0/병합가능CLEAN을 확인해 UTC04:16:16 main3484b13으로 squash 병합했다. 전체 트리가 검증 head와 같음을 확인했다. 실제 API 키/seed는 현재 미설정이며 사용자가 설정 방법을 물어 Windows 사용자 환경 변수·공식 PUUID 조회·Unity/Hub 정상 재실행 방법을 안내했다. 값은 받거나 읽지 않는다.

사용자 변경 Assets/DefaultVolumeProfile.asset 및 docs/riot-api-key.md는 보존/커밋 제외. 현재 Chrome 창·탭을 절대 닫지 않고 Windows 재시작/로그오프/Chrome 종료를 하지 않는다. 실제 API 호출과 설정값 변경은 이 기획 범위 밖이다.
