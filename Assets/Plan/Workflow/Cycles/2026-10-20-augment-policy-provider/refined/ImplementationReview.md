# 현재 코드 조사와 범위 선택

기준534cfa5. Core/Analytics/MetricPolicy.cs는 제공된 MetricPolicyMetadata의 전체 identity/setNumber를 확인하고 누락은Unknown, Legend/LegendBased는 차단한다. 이름/설명에서 분류를 추론하지 않는다. 정책과 표본100의 방어선은 이미 있다.
Editor/UI/Analysis/AnalysisService.cs는 생성 시 policy??new MetricPolicy()를 고정해 카탈로그별 외부 분류를 읽는 경로가 없다. TftDataAnalyzerWindow.cs는 ObservationStore/AggregateStore만 전달한다. BuildSnapshot의 명시적 합성 policy 주입만 테스트에 쓰인다.
선택 비교: 실제 API 검증은 키/seed와 명시적 호출 범위가 필요하고, 집계 입도 변경은 지표 정의를 바꾼다. 우선 분류 공급 경로를 작게 분리하되 빈 기본목록으로 현재 차단을 유지한다. 추후 자료 확보 전에는 실사용 비율 지표가 계속 차단된다는 한계를 승인 판단의 핵심에 둔다.
공식 정책 확인: https://developer.riotgames.com/docs/tft (2026-10-20 열람)는 Legends/Legend-based Augments의 승률 표시를 금지한다. 이 페이지에서 특정 Set18 개별 증강을Standard로 인증하는 자료는 확보하지 않았다. 문서 열람을 분류 데이터 확보로 해석하지 않는다.
이번 기획 중 제품 수정/Unity/개인 API/PR조회는0. 이전 수집 준비 안내는 PR15 main병합 및 사용자최종승인완료. 사용자 Volume과 로컬 최종승인 문서 변경은 보존한다.
