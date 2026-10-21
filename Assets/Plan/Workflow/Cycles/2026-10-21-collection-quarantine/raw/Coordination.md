# 승인 후 구현 기록

계획: ../refined/ImplementationPlan.md. 기준3484b13, 전용branch codex/collection-quarantine-report. 사용자 “이 계획대로 구현 진행 (권장)” 명시 승인. root가 구현·Unity를 맡고 기존 검수/QA 담당을 재사용한다. 같은 연결 checkout을 유지하는 기존 작업 방식을 따른다. 사용자 파일/Chrome을 보존한다.

Ruling: executing-plans의 별도 workspace/ledger는 프로젝트의 canonical 사이클 raw/Coordination과 Checkpoint를 사용한다. 이미 승인된 동일 연결 checkout에서 Unity를 검증하며 새 Editor/워크트리를 만들지 않는다. 실제 모델/추론은 노출되지 않아 전환을 주장하지 않는다.

첫 RED는 운영 MatchCollectionService가 unknown 진단 파일을 남기지 않는 실제 누락을 고정한다. 현재 컴파일 up_to_date/failedfalse/compilationFailedfalse를 확인했다. 제품 수정 전 가짜 응답 테스트부터 실행한다. 이전726/725P0F1환경제외 결과는 기준 증거이며 새 변경 검증을 대신하지 않는다.

실행: 최초새테스트의NUnitComparer오버로드모호함1건을정정후컴파일정상. 이컴파일오류를RED로세지않았다. 실제운영경로1/1실패(보고서없음),저장소미구현RED10/10실패후구현GREEN9P0F1호스트symlink제외를확인했다.

Ruling: 공용AnalyzerStorePaths.EnsureSafe에새diagnostics경로검사를추가하면진단폴더junction이정상관측저장까지차단한다. 새실제격리회귀1/1실패로확인했다. 보고서Save가target과모든부모를직접검사하므로공용검사추가를제거하고Save전용검사로한정한다. 보고서의경로차단은유지하며관측쓰기계약을보존한다. 이는새권한/기능확대가아닌승인된저장실패분리조건의구체화다.

이후저장소10개9P0F1동일호스트제외·기존코드검수Task1pass0필수. 수집서비스RED34개26P8F→GREEN34/34. VM RED53개48P5F,실제UXML/소유창RED10개9P1F를확인했다. 원문은Library/TftAnalyzerValidation/quarantine-{store-isolated-green,service-red,service-green,vm-red,ui-red}.json.

Ruling: CollectionViewModel의취소/HTTP예외fallback도기존Progress에서새격리카운트/flag를복사해야기록이사라지지않는다. 계획의기록보존조건에따라해당기존파일을수정한다. 별도기능이나재시도권한은추가하지않는다.

## PR19 문서 검수 후속 (2026-10-21)

190a579의 CodeRabbit 의견7건을 원본과 대조했다. Flora enUs는50% 증가, koKr는두배이므로 로컬 원문 부재라는 root 설명을 정정한다. 동결 전달 입력/AI 원문/카탈로그는 그대로 둔다. 반려 기획은 등록0과 미승인을 유지하며 미래 승인 목록 S와 N에 맞춰 테스트 기대값을 일치시킨다. 정제 문서에 워크플로§5 상태값을 명시한다. 최소 표본 FinalDocumentation의 완료 제안은 사용자 최종 결과 승인 증거가 없으므로 승인 대기로 표기한다.

고정 오류 대신 예외 유형을 추가 경고로 남기라는 nit은 현 승인 범위의 필수 수정으로 채택하지 않는다. 사용자 안내와 summary flag가 이미 실패를 구분하며 새 운영 로그 계약은 기획에 없고 제품 변경·추가 Console 경고를 요구한다. 경로/메시지/내부예외 비노출은 유지한다. 이번 수정은 문서만이며 소스15개 해시·동결기획 입력·링크를 검증하고 Unity/전체테스트를 반복하지 않는다. 기존 QA 담당만 같은 사이클에 재사용하며 새 에이전트/재위임은 없다. 실제 runtime 모델/추론/사용량은 미확인이다.

동일 QA 담당의 문서12개 재검수 결과: pass/필수0. 상태와 최종 승인 구분, S/N 기대값, 동결 원문과 Flora locale 차이, 기존 오류·UI 계약에 따른 로깅 비추가 판단, 로컬 링크/형식에 문제없음을 확인했다. 쓰기·Unity·재시험·네트워크·재위임은 수행하지 않았고 실제 모델/사용량은 미확인이다. root 문서 검사도 링크누락0·동결기획2건/제품소스15개 불변·사용자volume보존·키문서미열람을 확인했다.
