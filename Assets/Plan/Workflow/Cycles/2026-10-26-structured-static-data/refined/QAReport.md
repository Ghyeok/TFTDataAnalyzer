# 구조화 정적 데이터 QA

상태: 진행 중

진행 결과: QA 통과 — PR 통합과 사용자 최종 결과 승인 별도

pass — 승인 범위의 필수 결함 0건.

- 동결227파일 SHA256을 직접 대조해 변경0을 확인했습니다.
- 최종938건: 936통과·실패0·호스트 symlink 제외2건(158.72초). 관련112건·UI226건·게시 경계16건도 통과 기록을 확인했습니다.
- schema3 골든 SHA가 고정값과 일치하며, 구1/2/3 바이트·문서 출처·기존 leaf 출처 보존과 실제 양 어댑터→게시→loader/browser 경로를 확인했습니다.
- null·0·false, 슬롯·구간 순서, 확장 상한·날짜 문자열 보존, 공식 우선 병합, 불변 충돌과 미분류 증강 차단이 시험에 포함됩니다.
- PNG8장을 직접 확인했습니다. 작은 창은 일부 하단 스크롤이 필요하고 좌측 외부 오버레이가 있다는 제한을 유지합니다.
- 정상·표시 전후 예외는 소유 창0, 기존9창/native1·포커스 보존입니다. 보호1389변경·추가0은 제공된 대조 증거와 일치합니다.

환경 한계: PID32384의 Console 오류1건은 원인 미확인입니다. PID20116의 현재0/0·컴파일 실패false를 이전 오류 해결 증거로 보지 않았으며, 서로 다른 PID의 창 ID를 같은 기준으로 비교하지 않았습니다.

쓰기·Unity·네트워크·재위임·금지 자료 열람은 하지 않았습니다. 실제 모델·추론·사용량은 미확인입니다. 실제 API·분류 등록·n100 실사용·성급/장착 의미 확보·KI-001·사용자 최종 승인은 이 판정 밖입니다.

원본 결과: Git 제외 Library/TftAnalyzerValidation/structured-static-final-full-editmode.json, structured-static-review-all-green.json, structured-static-ui-regression-green.json, structured-static-final-source-freeze.json, structured-static-final-integrity.json, structured-catalog-visual-*.json. 공개 응답은 [raw](../raw/QAAgent.md).


## PR23 CodeRabbit 보완 결과

최초 e42e43b 검수4건을 현재상태문서2곳·실제공개prompt raw·정제문서허용상태·구상세출처잘림으로 분류해 모두 수용했다. 500개 구조화출처 때문에 기존 /cost와 /name/enUs가4096자에서 사라지는 실제RED1(pr23-field-sources-red.json)을 확보했다. schema4에서 구조화포인터만 구상세에서 제외하고 전용20행 출처 페이지는 유지했다. 기존 actualpipeline 시험은 구상세 /cost와 구조화행 /baseStats/hp의 공식출처를 각각 확인한다. 새 자료를 숨기거나 구1~3출처를 바꾸지 않았다.

관련113/113, 최종 pr23-review-full-editmode.json **939개/937통과/실패0/호스트2제외(85.21초)** 및 UI227통과다. pr23-review-source-freeze.json227SHA·보호1389변경0. 새8PNG를 root와 동일QA 담당이 직접 확인했고 좌측overlay는 없으며500px하단은스크롤이필요하다. 새PID20116 정상캡처는 기존9창/native1·focus보존·소유0이다. 이전표시전/후예외probe는 변경없는도구의 이전실행증거로만 보존한다.

현재Console1/0은 pr23-console-sanitized.json에서 `Unity.Pipeline`의 `/api/exec` 요청 `Thread was being aborted` 오류로 분류했고 제품/컴파일 언급은 false다. 도구 요청 중단의 발생 계기는 미확인이며 해결/로그삭제를 주장하지 않는다. 이전PID32384의 미확인오류1건과 같은오류로단정하지않는다. 독립코드검수delta와최종QA가모두pass/필수0을확인했다. 실제모델·추론·usage미확인,새agent0. 실제API·분류등록0·실제n100·성급/장착의미·KI-001·사용자최종승인은계속잔여다.
