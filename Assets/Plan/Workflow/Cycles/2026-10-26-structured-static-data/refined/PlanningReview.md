# 구조화 정적 데이터 기획 통합

상태: 승인 대기

기준 main f9974726021bb35edfdb3b5392904db42d1c270f. [구현 계획](ImplementationPlan.md)의 마지막 통합 절까지 사용자 승인 대상이다. 원본 snapshot과 최초 기획 verdict를 덮어쓰지 않는다. [동결 자료와 실제 결과](../../../../../../AntigravityCollab/tasks/2026-10-26-structured-static-planning/result.md), [실제 요청·응답](../raw/Planning.md).

## 결정

기물 능력치·스킬 수치 배열, 시너지 구간, item.unique와 비실행 원본 확장을 schema4에 보존하고 실제 어댑터·공식 우선 병합·leaf 출처·불변 게시·재로딩·패치 비교·페이지 탐색까지 연결한다. 서로 연결된 계약을 한 사이클에서 검증하며 UI에 원문만 추가하는 것으로 완료 처리하지 않는다. 기존 schema1/2/3의 바이트와 의미를 보존한다. 원본7슬롯을 성급으로 추정하거나 unique를 실제 장착 수로 확대하지 않는다.

## 독립 검토와 처리

| 검토 | 실제 결과 | 통합 처리 |
|---|---|---|
| A Antigravity | revise: P1 하나/P2 세 개 | null 구간·BaseStats9조합·양locale known-field 제외를 수용. Sources pruning 처방은 실제 기존 코드와 충돌해 불채택하고 기존 Sources 보존과 구팩 재사용/실제문서변경 충돌 증거 조건을 강화 |
| B GPT 최초 | revise: P2 하나 | nullable 구간 key누락과 명시null의 출처 구분을 요구. 세 scalar key필수/null허용/항상출처필수로 보완 |
| B 자기 지적 delta | pass, 잔여 필수0 | 명시null/누락/출처제거의 adapter→serialize→loader 검증 조건 확인 |
| B 최종 통합 delta | pass, 잔여 필수0 | A 처리의 코드 발췌·구형식 불변·9조합·exact pointer·unknown 하위 격리 계약 확인 |

A의 P1이 우려한 구 projection 출처 검증 필요성은 수용했다. 하지만 현재 PairSources/manifest는 읽은 문서를 모두 보존하고 MergeSources는 official/supplement 전체 합집합이다. 같은 원본을 새로 읽는 schema4에서 이를 선택 leaf만으로 줄이면 오히려 구schema 의미를 바꾼다. 원본 SHA가 달라진 경우는 충돌이 정상이다. [근거와 판정](../../../../../../AntigravityCollab/tasks/2026-10-26-structured-static-planning/outputs/root-resolution.md)을 남겼으며 A 최초 revise를 최종 pass라고 표현하지 않는다. A 전체 검토 재실행은 하지 않았다.

## 역할·실행·비용

root가 기획 통합·구현, 기존 /root/analysis_code_review를 GPT 기획 B로 재사용했고 이후 같은 담당을 코드검수에 사용한다. 기존 /root/analysis_final_qa는 개발 후 QA에 재사용한다. 기존 기술기획 담당이 현재 live 목록에 없어 읽기 전용 담당을 재사용했다. 신규 생성0·재위임0이며 B의3turn을 에이전트3개로 집계하지 않는다.

복잡한 개발/구팩 무결성에 해당해 High 기준과 중요 데이터 설계의 Astra 우선 고려를 적용하되, 현재 재사용 GPT의 실제 모델·추론·사용량은 확인할 수 없어 미확인으로 기록한다. 모델 전환을 실행했다고 주장하지 않는다.

A는 agy1.4.7, requested/init model gemini-4-argon-medium 일치, requested effort medium, SUCCESS·exit0·duration144.241초. runtime input14262/output10928/total25190 tokens, 별도 thinking9309 계측은 total에 다시 더하지 않는다. toolAttempts0이나 강제 접근 제한은 검증되지 않았다. 제공한 비밀 없는 문서 두 개만 읽는 계획 프로필이며 원본 코드/Unity 실행 증거가 아니다.

최초 A 명령은 auto-review가 구체적 외부 전송 승인 부족으로 실행 전에 거절했다. 사용자가 같은 두 문서 전송·검수를 명시 승인한 후 같은 경로로 재시도해 성공했다. 사용량 한도/제품 실패/정책 우회로 해석하지 않는다.

## 완료 조건과 잔여

계획의6개 TODO가 구현·독립 검수·실제 RED/GREEN·전체 회귀·940/500px 화면/창 정리·PR 최신head 검수를 모두 충족해야 개발/통합을 완료한다. 현재 제품 변경·신규 테스트·Unity 실행은 미실행이다. 별도 워크플로 §3.1.6 ‘사용자의 명시적 승인을 기다린다.’에 따라 승인을 요청한다.

실제 자료의 성급/장착 의미 확보, 실제 static 다운로드/Match API, 실제 증강 분류등록0·n100 입도, KI-001 기록, 사용자 최종 확인을 서비스 잔여에서 지우지 않는다. 이번 기획 검토는 전체 서비스 완료나 사용자 최종 승인 증거가 아니다.
