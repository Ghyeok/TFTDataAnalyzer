# 구조화 정적 데이터 체크포인트

상태: 승인 대기

2026-10-27 현재 [PR23](https://github.com/Ghyeok/TFTDataAnalyzer/pull/23)은 main7922fa61cc0d5fe99460ffe9deea5752c7d6089f에 병합됐다. 최신6acc2a0의 CodeRabbit SUCCESS·미해결0·실행증거와 병합 전체tree동일을 확인했다. 사용자 call_2208e223ba4c465ab9e566d320436868가 [통합 구현 계획](refined/ImplementationPlan.md)을 명시 승인했다. TODO1~5 구현, 독립 코드검수·최종QA와 CodeRabbit의 제품 수정은 통과했고 TODO6의 최신head 검수·main 병합도 마쳤다. 사용자 최종 결과 승인은 별도다.

## 현재 기준 증거

- 제품 검증 기준은50c474b의 코드와 `Library/TftAnalyzerValidation/pr23-review-source-freeze.json`227파일 SHA다. 이후 문서만 수정했으며 제품·시험·fixture 내용은 동일하다.
- **PR23 최종 전체 실행은 pr23-review-full-editmode.json의939건/937통과/실패0/호스트 symlink2제외(85.21초)**다. 관련113/113·UI227도 통과했다. 이 결과가 앞선 structured-static-final-full-editmode.json의938/936과 관련112/112를 **대체하는 현재 기준**이다. 앞선 수치는 합산하지 않는다.
- 새940/500px8PNG를 root와 동일QA 담당이 직접 확인했다. 오버레이는 없고500px하단은스크롤이필요하다. 새PID20116의 정상캡처 전후 기존9창/native1·focus보존·소유0이며 두예외probe는 이전실행증거로만 보존한다.
- 동결227SHA·보호1389파일은 변경0이다. 현재Console1/0은 안전요약 pr23-console-sanitized.json에서 Pipeline /api/exec의 Thread was being aborted로 분류됐다. 발생 계기는 미확인이며 해결/로그삭제를 주장하지 않는다. 이전PID32384의 미확인오류1건은 별도이고 서로 다른PID의 창ID를 같은기준으로 비교하지 않는다.
- [최종QA](refined/QAReport.md), [사용법·한계](refined/FinalDocumentation.md), [실제 검수 요청·응답](raw/Reviewer.md), [실제 QA 요청·응답](raw/QAAgent.md)을 확인한다. 실제 GPT모델·추론·usage는 미확인, 기존 담당 재사용·새agent/재위임0이다.

## PR와 다음 첫 행동

PR23 최초e42e43b의4의견은 실제RED1 후 코드/문서를 수정해50c474b에 반영했고 해결 처리했다. 추가3의견은6acc2a0의 현재/과거 문서 구분으로 수정했다. 최신6acc2a0에 연결된 CodeRabbit check-run의 head_sha/SUCCESS·미해결0·별도CI없음을 직접 대조했고, 기존 승인대로2026-10-27T05:24:38Z에 squash병합했다. merge7922fa6의 전체tree가 검수head와 같다. PR23 모니터는 병합확인후종료했고 nextCheckUtc=null, Hermes/외부예약0이다. 새PR가없으므로조회하지않는다. 다음은 [현지화 문자열 분리 저장 기획](../2026-10-27-localized-catalog-storage/Checkpoint.md)의 A/B 검토·사용자명시승인이다. 사용자최종결과승인은별도다.

## 보존·잔여

Assets/DefaultVolumeProfile.asset와 미추적 docs/riot-api-key.md는 사용자 파일이며 커밋에서 제외한다. 키문서본문·환경변수·원본경기응답은 읽거나 공유하지 않는다. 기존팩·관측·Chrome·사용자Unity창을 보존한다. 실제 다운로드/MatchAPI는 사용자 요청대로 미루고 실제 분류등록0·실제n100사용성·성급/장착의 미공급 의미·KI-001·사용자최종확인은 계속 잔여다. 서비스 전체 완료 판정이 아니다.

## 이전 기록의 위치

기획 당시에는 제품 변경·새PR가 없었고 사용자 승인을 기다렸다. 승인 후 serializer 변경 전에 실제schema3골든(SHA8f671fbd575971178ad39eb3f037dbde0d6a1a36f415bad673001ef822382727)을 동결했다. TODO별 RED/GREEN,935→938→939의 실행 관계, 서로 다른Editor 환경,PR 생성 전 단계는 [개발 이력](refined/DevelopmentReport.md)과 Git 이력에 보존한다. 이 과거 상태를 현재 작업 지시나 최신 완료 증거로 적용하지 않는다.
