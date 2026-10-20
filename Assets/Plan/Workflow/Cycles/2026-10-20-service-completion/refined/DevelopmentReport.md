# 카탈로그 탐색 개발 보고

2026-10-20. 상태: 구현·독립 코드검수·관련UI169/169·전체701개 중700통과/0실패/1환경제약 제외·실제 화면/소유창 정리·독립 QA 통과. 외부 PR검수·병합은 별도다.

승인 계획대로 기존 데이터 관리 탭에 한영이름/ID검색·50개페이지·읽기 전용 상세를 연결했다. 원래 도메인의 속성/효과/관계/출처를 보존하고 누락·교차종류·epoch시각을 구분한다. plain text·4096자필드/256자검색·strict기존아이콘경로·reparse전후검사·Texture2D/null/예외대체와수명주기정리를구현했다. 제품5파일·테스트3파일·캡처도구2파일이다.

실제 증거: 최초UXML RED1실패, 모델RED13중12F1P→GREEN13/13, 탭RED5중4F1P→GREEN5/5. 검수의언어양쪽누락P3도RED1실패→안내보완. 실제창재구성·loader예외를포함해관련UI169/169(실패/제외0)을실행했다. 기존코드검수담당을재사용했고필수수정0이다.

정상940/500px4PNG를직접확인해실제증강485개·기존아이콘·한영/출처/epoch안내·좁은폭줄바꿈·footer를확인했다. 표시전/후예외도Passed,기존창9/native1/포커스보존·추가/누락0·소유잔류0,후속fallbackVerified다. 소유창코드는기존안정된분석도구패턴을재사용했다.

도구입력실수: filter_type=class는실행전거부돼testName으로정정했고RED로세지않는다. 최초UIpatch의불완전경로와문서헤더불일치도atomic거부로파일변경0이었고정정했다. 계획초안의없는TryGetActiveSelection은기존검증행선택으로바로잡았다. 제품실패와도구실수를구분한다.

원문: Git제외Library/TftAnalyzerValidation/catalog-browser-*·catalog-visual-*·catalog-window-*.png. 이번계획전용ledger는.superpowers/sdd/2026-10-20-catalog-browser/progress.md다. root가구현/Unity/문서를맡고기존담당을재사용했으며신규에이전트0이다. 실제root모델/추론/개별비용은확인불가다. 실제API·실분류등록·새집계·외부배포·KI-001해결은이번완료로확대하지않는다. 전체서비스목표는계속활성이다.

## 동결 후 전체 실행

제품5/테스트3/검증도구2파일의 SHA-256을 고정한 뒤 전체EditMode701개 중700통과·실패0·심볼릭 링크 권한 제약1제외(83.7초)를 실행했다. 제외는 기존 ComplianceSettingsStoreTests.ExistingFileLinkIsRejectedWithoutReadingOrOverwritingDestination의 Host does not permit unprivileged symlinks다. 관련UI169/169는52.77초이며 서로 다른 실행 범위로 구분한다.

동결10파일과보호1,389개는변경0, 기존창9/native1/포커스 전후동일이다. Console cursor140→149/session동일/reset없음/dropped없음/새오류0을확인했다. 경고1은root가실행전에만든이문서를테스트생성미정리파일로보고했다. 초기mtime12:31:07Z<freeze12:31:34Z<경고12:33:02Z의SHA/시각증거를catalog-browser-final-evidence.json에보존했으며이후본문갱신시각과구분한다. 내부import/cleanup인식원인은미계측이다. 실제root문서를삭제하거나반복실행으로경고가해결됐다고주장하지않는다.

## 커밋 줄바꿈 검증 보완

staging과테스트원시바이트비교에서3파일의해시가달랐는데root가검사결과를분기하지않아로컬커밋을먼저실행했다. push는중단하고10파일전체를다시대조했다. 실제작업파일은모두테스트동결원시바이트와같고,커밋blob도CRLF→LF만정규화하면10파일전체가같다. 다른본문차이는없다. 근거는catalog-browser-commit-normalization-evidence.json이며테스트를새로실행한것으로표시하지않는다. 이후검증명령결과를확인한뒤에만종속커밋/push를실행한다.

## PR #17 첫 외부 검수 보완 — 2026-10-20

- c5ce192의 CodeRabbit 지적 3건을 검증해 수용했다. 과거 PR16 병합 명령의 공백과 현재 정본 계획의 승인 상태를 정정했다. 고정 검토 입력은 변경하지 않았다.
- 목록 행은 상위 Label 스타일이 white-space: normal을 적용하고 있었다. 500px 소유 테스트 창에 긴 한국어 이름/ID를 넣어 실제 resolvedStyle가 Normal인 RED 1건을 확인한 뒤, 결과 목록 하위 선택자의 우선순위와 nowrap을 지정했다. 관련 UI170/170 통과(52.29초), 실패/제외0이다. 처음 테스트에 사용한 IResolvedStyle.overflow는 지원되지 않아 CS1061이 발생했고 해당 단언을 제거한 뒤 정상 컴파일했다. 이를 제품 실패나 유효 RED로 세지 않는다.
- 수정 후 정상 940/500px 검색·상세 네 PNG를 다시 직접 확인했다. 실제 캡처는 기존 짧은 증강 행이며 긴 가짜 행의 픽셀 검증으로 확대하지 않는다. 긴 행은 실제 UI의 NoWrap/Ellipsis 스타일 회귀로 검증했다. 소유 창 잔류0, 기존 창9/native1/포커스0 보존, 수집/분석 호출0이다.
- 현재 컴파일/Console 오류0·경고0. 최초 전체701(700통과/기존 제외1)은 수정 전 증거이며 이번 수정 후 전체 재실행을 주장하지 않는다. 범위가 스타일·단일 UI회귀·문서 정정이므로 관련UI를 검증했다.
- 보호 원본1,389개 중1,388개 동일. Library/TftAnalyzerStore/version-check.json은 lastCheckedUtc=12:45:16Z, lastSeenVersion=16.20.1로 이전 동결 뒤 변경돼 있었다. 수정 검증 전 창 기록(12:52 이후)보다 이전이며 기존 버전 확인 상태와 이번 UI 실행을 구분한다. 카탈로그/아이콘/관측/사용자 Volume 변경0이다. 사용자 새 docs/riot-api-key.md도 커밋 범위에서 제외한다.
- 실행 원문: Library/TftAnalyzerValidation/pr17-revision-{red-status,red-status-2,red-test,green-status,ui-tests,evidence,windows-before,windows-after,console}.json 및 갱신된 catalog-visual-*.json/catalog-window-*.png. 기존 QA 담당에게 한정 독립 검수를 요청했다. 신규 에이전트0, 실제 모델/추론·개별 사용량 확인불가.

수정 검증 Console의 groundTruth는 현재 오류0·경고0이다. entries에는 이미 수정한 테스트 작성 CS1061(seq150) 이력1이 남아 있으므로 ‘새 오류가 전혀 없었다’고 기록하지 않는다. 기존 전체검증 뒤 생긴 이력과 현재 컴파일 상태를 구분했다.
