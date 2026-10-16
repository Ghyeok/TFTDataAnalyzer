# 정적 데이터 탭 실행 검증

날짜: 2026-10-16
상태: 실제 실행 검증·최종 코드 검수·독립 QA 대조 통과. PR 검수·사용자 최종 확인은 별도다.

| 항목 | 실제 결과 | `Library/TftAnalyzerValidation/` 근거 |
|---|---|---|
| 기준 회귀 |376/376|`data-manager-baseline.json`|
| 최초 RED |신규타입 부재 CS0234/CS0246 11건|`data-manager-red-compile.json`|
| 목록실패 회귀 RED |1건 실패, Succeeded가 잘못 반환됨|`data-manager-reload-red.json`|
| 승인위치 회귀 RED |17중16통과, 경로변경 때 다운로드발생1건 실패|`data-manager-consent-red.json`|
| 최종 컴파일 |completed, failed=false, errors=[]|`data-manager-inspector-compile.json`|
| 최종 VM/서비스/UI |23/23|`data-manager-inspector-focused.json`|
| loader |11/11|`data-manager-loader-focused.json`|
| 최종 전체 |410/410, 실패0|`data-manager-verified-full.json`|
| 실제 창 |메뉴/입력/목록1행/optinfalse/고지/로드오류없음|`data-manager-open.json`|
| 실제 lifetime |VM·선택 유지, Close시 dispose, legacy UTC 정확|`data-manager-lifetime.json`|
| 화면 |어두운테마940×700·최소500×300, 스크롤·고지 확인|`data-manager-window.png`, `data-manager-minimum.png`|
| Console |현재error0/warning0, 최종smoke구간신규0|`data-manager-console-final.json`|
| 보존 |보호1386개 변경0|`data-manager-protected-check.json`|
| 최종 독립 검수 |Important2건·스케줄 보완 addressed, 추가지적없음|[최종검수](../raw/FinalReview.md)|
| 독립 QA |실제410개 결과·문서·예외·보존·미실행 범위 대조, 차단 없음|[QA 담당](../raw/QAAgent.md)|

초기 전체405/408와 중간 집중19/23은 통과로 처리하지 않았다. 실제 Inspector업데이트 조건을 갖춘 최종 결과가 이를 대체한다. 5초 timeout·기존 기대·제품스케줄은 유지했으며 실패 무시·제품용 테스트hook·전역delaydelegate 직접호출은 없다. 자세한 원인/재작업/공식근거는 [개발 보고](DevelopmentReport.md)와 [구현 raw](../raw/Implementer.md)에 남겼다.

## 재현

```powershell
unity command recompile --project-path 'C:\Unity\TFTDataAnalyzer' --format json
unity command recompile_status --project-path 'C:\Unity\TFTDataAnalyzer' --format json
unity command run_tests --project-path 'C:\Unity\TFTDataAnalyzer' --mode editor --filter_type testName --filter DataManager --timeout 300 --format json
unity command run_tests --project-path 'C:\Unity\TFTDataAnalyzer' --mode editor --filter_type testName --filter PublishedCatalogLoader --timeout 300 --format json
unity command run_tests --project-path 'C:\Unity\TFTDataAnalyzer' --mode editor --filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout 300 --format json
```

실제 실행은 설치된 CLI 절대 경로를 사용했다. 테스트외부 success값만으로 판단하지 않고 내부 total/results/failure를 대조했다. UI 테스트는 실제 Inspector업데이트 경계에서 NavigationSubmit/선택/변경 이벤트를 검증한다. loader는 임시 writer출력과 메모리index, 실제 version adapter는 fake HTTP를 사용한다.

실네트워크 import/version요청·native 확인창 직접클릭·실포인터·수동 전체domainreload·밝은테마는 미실행이다. 자동시작 네트워크·수집/통계/CSV·compliance는 범위밖이다. PR검수·사용자 최종확인은 이 QA를 대신하지 않는다.
