# 정적 데이터 탭 개발 조율

날짜: 2026-10-16

- 개발 단위: 기존 Phase3 Task2 하나, 기준 `cd1b67f`, 브랜치 `codex/static-data-management-tab`. 다른 제품 Task는 진행하지 않는다.
- 기획 A/B의 독립 조사 후 단일 요구사항 brief를 `Library/TftAnalyzerValidation/data-manager-task-brief.md`에 작성했다. VM/서비스/binding/loader 경계를 통해 기존 API를 재사용하고 정규화된 카탈로그와 실제 standalone 표시 메타데이터를 구분한다.
- 구현 담당 `implement_data_manager`에 `gpt-6.1-sol` / `high`를 명시 요청했다. 복수 모듈과 취소·복구 연결 때문에 high를 선택했고 실제 런타임 식별·사용량은 독립 확인 불가다.
- 1차 요청은 테스트만 작성하고 반환하도록 했다. 담당은 VM 상태/동의/오류/취소, actual service+fake transport versions-only, 임시 writer/loader 무결성·legacy·metadata, 실제 UXML binding 이벤트 테스트와 shared fixture 총5파일을 작성했다. 실제 generated를 수정할 수 있는 CatalogIndexWriter를 temp 테스트에서 사용하지 않도록 주의사항도 전달했다.
- 루트 실제 RED: `unity command recompile`, `recompile_status` 결과 completed/failed=true. Data namespace 및 신규 VM/Tab/서비스/DTO 부재 CS0234/CS0246 총11건. 원본 `Library/TftAnalyzerValidation/data-manager-red-compile.json`.
- RED 후 동일 담당에게 최소 제품 구현을 요청했다. 루트가 Unity·집중/전체·실제 화면·보호해시·검수·통합·문서를 담당한다. 담당의 Unity실행·커밋·문서변경·Hermes·추가에이전트는 금지했다.
- 현재 GREEN/집중/전체·실제 데이터 탭은 아직 검증하지 않았다. 기준 전체376/376은 신규 기능의 통과 증거가 아니다.
