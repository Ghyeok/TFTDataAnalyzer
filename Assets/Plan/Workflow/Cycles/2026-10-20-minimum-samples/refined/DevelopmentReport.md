# 최소 표본 수 설정 개발 보고

갱신2026-10-21. 상태: Task1/2·동결후전체검증·화면·독립QA통과, 문서통합/PR제출단계. 제품 전체 완료 아님. 아래 중단 이력과 후속 복구 실행을 구분한다. 승인 계획과PR17 병합기준0c6c1d5에서root가구현했다.

Task1은정책복사/요청별기준/주입정책하한/metadata보존/snapshot실제기준을추가했다. 테스트실행용임시no-op스켈레톤에서정책35개중4F31P·서비스12개중5F7P를실제로확인한뒤최소구현으로대체했다. 최종정책35/35(8.09초),서비스12/12(51.73초),독립코드검수필수수정0이다. regex형식의testName필터는0건실행해RED로세지않고실제클래스별명령으로확인했다.

당시 중단 시점의 Task2 VM은100이상입력/99·0·음수보존·차단/자동조회없음/결과초기화/진행중취소·세대확인/낮은기준snapshot거부를구현했다. RED22개중9F13P→GREEN22/22다. UI새5테스트는정수입력부재RED를확인한뒤UXML/탭구독·해제를연결했다. 연결전12개중5F7P이며연결후GREEN/전체회귀는미실행이다.

Unity컴파일도중bee_backend가-1073741819로종료하고BuildFinishedMessage가누락됐다. Bee0확인후1회재컴파일요청은이전실패상태때문에새실행을거부당했다. 추가실행을중단하고사용자Editor재시작을요청했다. 원인은미확정이며C#오류가없다고컴파일성공으로간주하지않는다. KI-001과별도KI-002다.

당시 중단 시점에는Task2정적검수의CSV대화상자중100→200→100 이벤트지연가설(P2)을실제테스트로확인해야했다. 기존회귀테스트를먼저실행해재현되면수정한다. 분석소유창도구의-MinimumSamples 소스/PS구문검사는준비됐으나실행·PNG·창정리검증/전체QA는아직없다. 사용자Volume/새docs/riot-api-key.md를보존한다.

검수담당은사용량제한으로1회실패했고안내시각이실제로지난후같은담당을1회재시도해Task1검수를받았다. 기존담당만재사용하고새에이전트0이다. 실제root/GPT모델·추론·개별사용량확인불가이며A기획의실측은PlanningReview를따른다.

원문:Library/TftAnalyzerValidation/minimum-samples-{bootstrap-red,task1-policy-red,task1-service-red,task1-policy-green,task1-service-green,task2-vm-red,task2-vm-green,task2-ui-red,task2-ui-green-status,task2-ui-recovery-compile,task2-ui-recovery-status,pending-restart}.json및bee-failure로그/traceevents.

## 복구 후 실제 검증 — 2026-10-21

새정상Editor PID15640을정확한프로젝트/생성시각으로확인해연결을복구했다. Chrome/Windows재시작조작은0이다. 실제원인이해결됐다고일반화하지않는다. 컴파일정상후기존UI12개에서CSV대화상자중100→200→100 입력의write1을재현했다. 경로선택후기존상태즉시검사와일회성Editor update의최종검사,정확한delegate소유권/Dispose취소로보완해UI13/13이통과했다. 새재구성시예약쓰기취소/새binding정상저장도검증했다. 기존담당재검수필수수정0이다.

-MinimumSamples의940/500px top/rows/invalid6PNG를root가직접확인했다. 기준200적용/비율없는표본부족/99그대로보존/갱신·CSV차단/줄바꿈·스크롤/footer가보인다. 화면은합성자료이며실제표본확보를뜻하지않는다. Load3/수집0/소유창잔류0,기존창/native/포커스보존. 표시전/후예외정리도Passed/fallbackVerified다. 원문minimum-samples-reopened-ui-tests.json,minimum-samples-export-fix-ui-tests.json,analysis-minimum-visual-final.json,analysis-visual-*cleanup.json및analysis-window-*-minimum-*.png를보존한다.

## 최종 동결·전체 검증·QA

소스14파일동결후전체726개중725통과/0실패/기존symlink제약1제외(129.4초). 같은실행Analytics153/UI190통과. source14/보호1389변경0,창9/native1/focus0동일. Console같은session/cursor12→20/resetfalse/droppedfalse/새오류0/현재오류·경고0.원문minimum-samples-final-source-freeze.json·minimum-samples-full-tests.json·minimum-samples-final-evidence.json이다.기존analysis_final_qa가소스/실행/6PNG를직접대조해필수수정0으로수용했다.

QA의문서조건은중단당시미실행문구를과거시점으로명시하고최신결과/Task3상태를통합하는것이다.500px이미지에서CSV비활성은viewport밖이라helper/UI테스트증거와구분했다.같은source에추가제품수정이나반복full실행을하지않았다.최신PR검수/병합은별도다.
