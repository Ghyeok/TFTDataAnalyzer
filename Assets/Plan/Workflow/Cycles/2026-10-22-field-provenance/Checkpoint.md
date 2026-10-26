# 필드별 출처 기록 체크포인트

2026-10-22. 현재 단계: 사용자 승인 → 구현 → 독립 코드검수 → 최종 QA 통과, PR21 보완 검수 중. 기준 main f0058799355cd2754f193eff38d7dd415cd700e6, branch codex/field-provenance. [계획](refined/ImplementationPlan.md), [사용법·한계](refined/FinalDocumentation.md), [실행 기록](refined/DevelopmentReport.md).

승인: 기획 A=Antigravity pass, 기획 B=기존 GPT 담당의 완전성 P2 1건 보완 후 pass. 사용자가 call_d0d2d56f0c514f2eb73afc534b641d3a에 ‘이 계획대로 구현 진행 (권장)’으로 답했다. 이전 세 턴의 승인 대기/blocked는 이 승인으로 구현 재개의 근거가 생긴 과거 이력이다. 앱 목표 상태의 active 전환을 도구로 수행했다고 주장하지 않는다. 동일 승인은 다시 요청하지 않는다.

구현: 실제 공식/보완 파서·병합→아이콘staging·정규화→schema3 게시·검증된 loader→탐색 표시까지 FieldOrigins를 보존한다. 구schema1/2의 byte/hash·경로·전체 identity를 유지하며 같은 구 팩을 재사용하면 실제 저장된 구 catalog/schema/hash를 반환한다. 기본 네트워크 공급만3으로 올리고 metadata 없는 기존 provider에는 출처를 만들지 않는다. pointer는 기존 정규JSON의 /componentIds를 사용한다.

검증: 최종 동결19소스 전체796개 중794통과·실패0·호스트symlink2제외(83.32초), 신규31개 전부 통과. 기존 검수 담당의 locale 효과 키 P2는 실제 RED2/4→두 어댑터 GREEN을 확인해 해소했다. 독립 코드검수·QA 모두pass/필수0, 신규 에이전트 생성0·재위임0. 실제4PNG를 root와QA가 각각 확인했다. 정상·표시전후 예외 모두 기존9창/native1·포커스 보존, 소유창0. 전체검증 전후 Console오류0경고0, 보호1399·소스19 변경0.

화면 도구의 첫 합성 실행은 InvalidOperationException/캡처0이었고 정리는성공했다. 제한된 합성 상태 진단을 추가한 1회 재실행에서4PNG와정리통과. 최초원인은미확정이며 제품/KI-001의 원인해결로바꾸지않는다. 소유 ID와고유이름만정리하며 사용자Inspector·Chrome은보존했다.

PR20은 최신head c8e746a CodeRabbitSUCCESS·미해결0·CLEAN 후 UTC2026-10-21 13:58:53 mainf005879로병합했고전체트리일치확인. 현재 [PR21](https://github.com/Ghyeok/TFTDataAnalyzer/pull/21)의 최초head3619a5f에서CodeRabbit SUCCESS·미해결6을확인해보완했다. 보완전체797/795P0F/호스트2제외·동결20/보호1399불변·새4PNG·LF체크아웃증거는 [보완 기록](refined/PR21RevisionReport.md)에있다. 즉시사후명령은사용량한도로미실행이었고복구후동일PID의지연Console에는Pipeline전송오류2건이있다. 새head를push하면 최신head CodeRabbit·미해결의견·CI·실행증거를5분간격직접확인해조건충족시기존승인대로병합한다. Hermes예약은삭제상태다.

실제API설정/수집은사용자요청으로미룬다. 증강ID의Legend배제근거를확정하지못해분류등록0·동일자료AI재검수중단을유지한다. 실제n100사용성·실제데이터전과정·KI-001위험관리·최종사용자확인은전체서비스잔여범위다. 서비스전체완료로판정하지않는다. 사용자변경 Assets/DefaultVolumeProfile.asset 및 docs/riot-api-key.md는수정·스테이징하지않고키문서본문은읽지않았다.


최신 상태 갱신: PR21 최신54a29f3 CodeRabbit SUCCESS·미해결0·CLEAN/검토범위 최신head 확인 후 UTC2026-10-22 10:04:32 main5a67afb679bd9c2e91cf71f3bc83b2ce80622bcc로 병합. main과 해당head의 전체 트리가 같으며 테스트소스20 불변을 확인했다. 5분 직접조회 루프 종료, Hermes예약삭제 유지. 사용자 최종 결과 승인은 별도 대기다. 새 승인 기획은 다음 community-shape-compatibility 사이클에서 이어간다.
