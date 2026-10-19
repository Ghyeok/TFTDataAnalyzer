# 경기 수집 탭 체크포인트

갱신일: 2026-10-18
상태: 코드 검수·실행 검증·독립 QA·문서 대조·CodeRabbit 재검수 통과 및 PR #11 병합 완료. 사용자 최종 사이클 승인은 별도.

- 승인: 기존 Phase3 Task3, 사용자 요청의 계획 재검토·검증 창 정리·지침 등록. 최신 요청은 “다음 작업 진행하고 문서 변경과 한번에 커밋하자”다. 분석 등 Task4 이후 제품 기능은 이번에 시작하지 않는다.
- 기준 HEAD: bc42f6527ae8e062bc953363692a10dfa2421303. 브랜치 codex/bounded-match-collection-tab. PR #10은 최신 검수·410/410 확인 후 병합한 이력이며 새 수집 탭 PR과 구분한다.
- 병합 PR: [#11](https://github.com/Ghyeok/TFTDataAnalyzer/pull/11). 문서 지적은 c6156c1에서 해결됐다. 재개 시 최신 head c6156c1276b0c6d896a6b8ee8ddba0603ccbcd9c의 CodeRabbit SUCCESS·미해결0과 코드 변경 없는 문서 수정임을 확인한 뒤 병합했다. 병합 커밋은9d87f5d3d0c9d181ceab587f915d496e79fd3d0e이다.
- 현재 diff: 수집 service/VM/binding·창 연결, 입력/legacy identity 회귀, one-shot update·테스트 소유 창 scope·추적 가능한 수동 검증 도구, 계획/보고서/README/프로젝트 지침. 기존 Assets/DefaultVolumeProfile.asset 사용자 변경과 Generated는 stage하지 않는다.
- 최신 실행: 컴파일 up_to_date/failed=false/compilationFailed=false/errors[]; 전체 EditMode467/467(그중 UI90). 전체 실행 전후 사용자 창9개·native 프레임1개 ID 동일, 추가·누락0·포커스 복원. 458/458은 legacy 수정 전 이력이다.
- 수동 검증: 실제 포함 schema1 카탈로그의 검증된 전체 identity를 사용했다. 현재940px·500px PNG를 직접 열어 대상 UI·identity·고지와 작은 창 스크롤을 확인했다. 정상·표시 전/후 실패 작업 및 셸 fallback 모두 소유 창0, 기존 창·프레임·포커스 보존. 과거500px 캡처 불일치 원인 해결이나 모든 timeout/연결 끊김 검증을 주장하지 않는다.
- 보호: Volume 및 생성 데이터1,386개 SHA256 변경0. 기준은 Library/TftAnalyzerValidation/collection-protected-baseline.json이다.
- Console: 컴파일 실패false·경고0. 현재 검증 전에 발생한 com.unity.pipeline GET /api/commands 통신 오류2건은 남아 있으며 최종 cursor 이후 새 경고·오류0. Console 전체 오류0으로 보고하지 않는다.
- 독립 검수: 입력 초안/재생성 P2 수정 후 승인, cleanup/scheduling·legacy identity 코드 검수 승인. 최종 독립 QA는 제품·실행·문서 대조 PASS다. 비차단 P3: Idle 기본 안내가 선택된 카탈로그와 별개로 선택을 요청할 수 있음.
- 주요 보고서: [개발](refined/DevelopmentReport.md), [QA](refined/QAReport.md), [사용 안내](refined/FinalDocumentation.md), [계획 재검토](refined/PlanReReview.md). 원래 Phase3 계획이 유일한 TODO 원본이다.
- 미실행: 실제 Riot 네트워크·실제 키/seed·네이티브 확인창 클릭·Hermes 자동 실행. 분석·CSV·정책/빌드 gate·startup scheduler는 후속이다.
- 커밋: 수집 탭과 지침·문서를 한 커밋으로 기록했다. 제목은 feat: add bounded collection UI and scoped Editor validation이며 최종 식별자는 브랜치 Git 로그를 따른다. 사용자 Volume·Generated는 커밋에서 제외했다. 사이클의 사용자 최종 완료 승인은 별도다.
- 다음: 완료된 PR #11 검수 대기를 다시 시작하지 않는다. 후속 사용자 요청의 CodeRabbit5분 확인은 Hermes 작업 c5f808787506으로 등록하고 실제 예약 실행을 확인했다. 운영 상태와 알림 한계는 [검수 모니터 지침](../../../../../docs/coderabbit-monitoring.md)을 따른다. 기존 Task4 분석 화면은 아직 구현하지 않았으며 사용자 최종 사이클 승인과 구분한다.
