# 현재 구현·경계 요약

기준 main 6a6f787. EnvironmentRiotApiKeyProvider는 현재 process 환경 변수에서 매번 읽고 control 문자를 거절하며 trim한다. CollectionService는 주입된 IRiotApiKeyProvider로 생성한 RiotApiClient 하나를 재사용해 rate limit 상태를 유지한다. CollectionViewModel은 현재 카탈로그/입력/API 키/seed가 준비되었을 때만 시작하고 사용자 확인 후 실행, 401/403/429를 중단한다. CollectionTab과 TftDataAnalyzerWindow는 UI 재구성과 OnDisable에서 바인딩/VM을 정리한다. 현재 키 문자열 입력 UI/세션 provider는 없다.

상위 설계 §8.3은 환경 변수/OS 자격 증명만 허용하고 키의 프로젝트 파일/EditorPrefs/로그/명령줄 저장을 금지한다. 사용자 2026-11-04 신규 선택은 도구 창 메모리 입력을 요청하므로 기존 상위 출처 계약을 명시적으로 조정할 필요가 있다. 메모리 임시 보유와 런타임 문자열 보안 삭제 보장은 다르다. 사용자 창·Chrome, 키 문서와 기완료 데이터·미커밋 변경을 보호한다.

KI-003 실제 경기 자료의 누락/미지원/불명확 버전은 별도의 미해결 문제다. 이 작업에 데이터 채택 조건 완화·실제 API 호출·자동 수집·계정 관리·OS 영구 저장을 포함하지 않는다. 키·식별자·경기 원문은 본 문서에 없다.
