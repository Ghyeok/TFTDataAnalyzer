# 현지화 문자열 분리 저장 체크포인트

상태: 진행 중

사용자 call_d5412b46c27542e99a05dffe990ea1c4가 [구현 계획](refined/ImplementationPlan.md)을 승인했다. 기준 main 7922fa6, 기획 커밋 265bdf1, 당시 브랜치 codex/localized-catalog-storage다. TODO 1·2 구현 및 회귀, 독립 코드검수 수정과 재검수, 독립 최종 QA를 통과했다. [PR24](https://github.com/Ghyeok/TFTDataAnalyzer/pull/24)는 최신 head 1cdebea의 CodeRabbit 통과와 미해결 의견 0을 확인한 뒤 main 6a6f787에 병합했고 검수 head와 병합 트리가 동일하다. 사용자 최종 결과 승인은 별도다.

schema4 원본 serializer로 제품 수정 전에 생성한 창작 golden의 SHA는 6152c706c5e36c459c81cb08bbd4d384c18141f0f69240d9ee734ebc7021cd7e다. 구 1~3 golden을 재생성하지 않았다. schema5 manifest 별칭 문제는 실제 2건 RED 후 수정하여 관련 60/60 GREEN과 동일 코드검수 담당의 delta pass를 받았다.

최종 소스 232개를 동결한 뒤 직렬화 86/86, 전체 EditMode 999건 중 997 통과·실패 0·호스트 심볼릭 링크 제한으로 2건 건너뜀을 확인했다. 기존 창 9개·native 프레임 1개·포커스가 유지됐다. 보호 대상 1389개 중 version-check.json 1개가 변경됐고 나머지 1388개는 동일했다. 기존 스케줄러가 원인일 가능성은 있으나 확정하지 못했다. Console 오류는 1→3이며 새 2건은 Unity.Pipeline /api/commands 전송 오류이고 원인은 미확정이다. UI 레이아웃 변경은 없고 실제 BrowserVM과 기존 UI 테스트를 검증했다. 상세 내용은 [개발 기록](refined/DevelopmentReport.md)에 있다.

기획 A/B는 동일 공통 입력으로 pass, 필수 수정 0이었다. A의 외부 전송 최초 자동 승인 거절 후 사용자가 두 문서의 전송을 승인해 실행했다. 실제 모델·사용량은 기획 검토 기록에 구분되어 있다. 제품의 실제 경기 API 호출과 증강 분류 등록은 하지 않았다.

사용자 Volume 변경, 키 문서, 기존 카탈로그·관측, Chrome 및 사용자 Unity 창을 보존한다. 키 문서 본문은 읽지 않는다. 후속 확인에서 실행 중인 Unity가 API 키와 수집 대상 1개의 설정을 인식했다. 설정 인식은 키의 유효성이나 실제 수집 성공을 뜻하지 않는다. 실제 경기 데이터·증강 분류·n100 검증, KI-001과 사용자 최종 확인은 서비스의 잔여 범위다. Hermes 예약은 삭제 상태다.
