# 수집 격리 보고서 개발 결과

2026-10-21. 상태: 승인 대기. 현재 상태: 승인 구현·관련 회귀·코드검수·6PNG/예외 정리·전체 EditMode·최종QA 통과, PR 단계. 기준3484b13, branch codex/collection-quarantine-report. 실제 API 호출/환경값 반환/Chrome 조작0.

## 변경과 이유

기존 익명화기가 반환하던 UnknownStaticIds를 수집 서비스가 버리던 누락을 수정했다. 제한된 accumulator가 종류와 SHA만 합쳐 원문 ID/PUUID/키/Match ID/Match hash/경기별 연결을 보존하지 않는 JSON을 실행 종료 때1회 저장한다. 전체 요청 context는 해시이며 실제 수락 scope라는 뜻은 아니다.

저장 위치는 같은 ObservationStore의 validated RootDirectory 아래 diagnostics/quarantine/<requestContextHash>.json이다. 이 파일은 해당 context의 마지막 격리 실행이다. 격리0 실행은 덮어쓰지 않고 이번 보고서 성공으로 표시하지 않는다. 임시 파일은 정확한 소유 경로에서 CreateNew/Flush(true)/원자 교체/finally 정리를 사용한다. 보고서 대상/부모 링크를 거부하고 이전 파일을 보존한다.

고유 종류+해시는4,096개까지만 유지한다. 초과 경기의 일부 진단을 기록하지 못했음을 표시하고 다음 요청 전에 종료한다. 취소·API실패·한도종료에서도 이전에 얻은 진단을 기록한다. 저장 실패는 기존 수집·HTTP결과·관측을 보존하고 별도flag로 알린다. finally 뒤에만 최종snapshot을 생성해 저장 결과가 반환값과 마지막progress에 모두 반영된다.

UI는 기존 라벨에 격리 건수·이번 보고서 상태·일부 누락/중단을 추가한다. 준비 확인·입력/카탈로그 변경은 이전 실행 결과를 지우지 않으며 새 수집을 시작하면 초기화한다. VM 예외fallback에서도 이미 받은 격리상태를 보존한다. 기존 집계/증강 분류/키 읽기/네트워크/CSV 형식은 변경하지 않는다.

## 실제 RED/GREEN

| 실행 | 결과 |
|---|---|
| 운영 수집 누락 최초재현 |1개0P1F,실제보고서없음|
| 새저장소미구현 |10개0P10F|
| 저장소구현 |10개9P0F1호스트symlink제외|
| diagnostics분리추가회귀 |1개1F→저장소10개9P0F1동일제외|
| 수집서비스 |34개26P8F→34/34|
| 화면모델 |53개48P5F→53/53|
| 실제UXML/소유창 |10개9P1F→10/10|

원문은Library/TftAnalyzerValidation/quarantine-{bootstrap-red,store-red,store-isolated-green,report-isolation-red,service-red,service-green,vm-red,vm-green,ui-red,ui-green}.json이다. 최초 테스트의 NUnit comparer 오버로드 컴파일 오류는 정정했고 기능RED로 세지 않았다.

## 소유창 검증

수집 도구의 -QuarantineReport 모드에서 합성 저장 성공/실패/상한 상태를940/500px로6회 캡처했고 root가 실제6PNG를 열어 대상 화면/줄바꿈/상태/고지를 확인했다. 500px은 새 소유창의 높이700(panel726)에서 모든 진단 문구가 보였다. 합성서비스 호출6, 실제API0, 디스크 관측 쓰기0이다.

collection-quarantine-visual-final.json 및 표시 전/후 예외 probe2개가Passed, 소유잔류0/기존9창·native1·포커스보존을확인했다. UI회귀의전후inventory도동일하다. 표시한창은Close()를사용하며검증창/nativeID를다음정리대상으로재사용하지않는다. Chrome창/탭은닫거나변경하지않았다.

코드검수는 기존analysis_code_review가Task1과최종15소스를각각재사용해필수0으로통과했다. [원문1](../raw/Task1CodeReview.md)·[원문2](../raw/Task2CodeReview.md). 새에이전트생성/재위임0, root/검수런타임모델·추론·사용량은미확인이다. 동결은quarantine-final-source-freeze.json15파일이다. 기획A실제사용량은PlanningReview/협업runtime에기록했으며실행증거를대체하지않는다.

전체회귀는750개중748통과·실패0·호스트파일symlink2제외(77.1초)다. 같은실행에서Persistence102P1제외/RiotApi94P/UI196P를분리집계했다. source15/보호1,399개변경0,기존창9/native1/포커스50768→50768동일,Console동일session에서새error0/현재error0warning1을확인했다. 원문은quarantine-{full-tests,final-evidence}.json이다. 이후 기존analysis_final_qa가실제6PNG·소스15SHA·실행·경고원문을대조해필수0으로통과했다. docs/riot-api-key.md는사용자파일이며내용읽기/커밋대상에서제외한다. 실제분류자료등록0/API실사용/n100달성은별도미완료다.

QA 대조에서 root가 잘못 쓴 warning0/focus0 기록을 정정했다. 경고1은UnityTestRunner가문서3개를새자산으로발견한것이다. 세문서의물리적생성UTC08:38:19는전체실행전Console기준UTC08:39:14보다빠르다. 설치된test-framework의CleanupVerificationTask는FileCleanupVerifierTaskBase.GetAllFilesInAssetsDirectory(AssetDatabase.GetAllAssetPaths)목록차이를경고한다. root는문서자산등록지연으로판단하며제품보고서/창정리실패로확대하지않는다. 경고원문은quarantine-full-console-warnings-sanitized.json에보존했고콘솔삭제/문서삭제/전체시험반복은하지않았다.
