# 현지화 문자열 분리 저장 기획 통합

상태: 승인 대기

기준main7922fa61cc0d5fe99460ffe9deea5752c7d6089f. [승인 대상 계획](ImplementationPlan.md), [동결 전달/실제 결과](../../../../../../AntigravityCollab/tasks/2026-10-27-localization-planning/result.md), [실제 요청·응답 연결](../raw/Planning.md)을 따른다.

현재 한영 표시가 이미 동작한다는 사실과 상위§6.6의 저장 분리 요구를 구분했다. 새 schema5의 record 본문에서 이름/설명을 빼고 안정kind/ID/field별 표로 저장하며 strict key/type·영어대체flag일치를 검사한다. read는 기존immutableAPI에 연결한다. 구1~4bytes/hash·논리fieldOrigins·문서Sources·구조화자료를 보존한다. 구4projection에서 구조화값/출처를 지워 재사용에 성공시키지 않는다. Domain/UI언어기능·추정번역·API·새분류·통계변경은 범위밖이다.

| 검토 | 실제 판정 | 한계 |
|---|---|---|
| A Antigravity | pass/필수0 | 제공2문서만검토, 새schema5구현/실행은미검증 |
| B GPT | pass/필수0 | 같은2문서만검토, 실제모델·추론·usage미확인 |

기존 /root/analysis_code_review를 B로 재사용했고 A는 설치된agyCLI로 호출했다. 신규agent/재위임0. 복잡한 저장/복구와 데이터무결성에 해당해 High/Astra우선고려 기준으로 검수범위를 정했지만 GPT 실제모델/추론의 전환을 증명하지 못해 미확인으로 기록한다. 역할표 때문에 새에이전트를 만들지 않았다.

A 실제실행: agy1.4.7, requested/init gemini-4-argon-medium 일치, requested effort medium, SUCCESS/exit0/전체39.046초(응답30.988초), input11507/output2453/total13960tokens. thinking2242는별도계측이므로total에다시더하지않는다. toolAttempts0은강제OS/도구격리증거가아니다. A 응답의모델메타미검증표시는제공문서만의한계이며 root는실제runtime파일을별도대조했다.

최초A호출은새payload/목적지에대한구체적전송승인부족으로자동승인검토가실행전에거절했다. 사용자call_2c96c309ff154cf7b589f4f4727f2285의‘이 두 문서 전송·기획 검수 승인’후동일2문서/digest/require_escalated경로로실행했다. 우회·추가자료전송0이다. 이승인은외부전송·기획검수범위이며제품구현승인으로확대하지않는다.

입력16094bytes/요청17056bytes, digest eadd8516c28e796367d36944565fcc7f5e227caf38854779f9a7f408af732012. 준비단계dry-run이Windows CRLF와문자열LF로계산한SHA차이를감지했고송신전에명시LF/원본사본/실제파일SHA로수정해통과했다. 검토가시작된후frozen inputs는변경하지않았다. 최종계획은상태와timeout 공백오타만정정했고계약은동일하다.

현재는 기획 통과·사용자 구현 승인 대기다. 새제품/새시험/Unity실행0이다. 기준939/937P0F·UI227·PR23병합증거를schema5완료로쓰지않는다. 실제API·분류등록0·n100사용성·성급/장착미공급·KI-001·사용자최종결과승인의잔여는유지한다.
