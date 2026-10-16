# 통합 Editor 창 실행 검증

날짜: 2026-10-16
상태: 실제 실행 검증·독립 QA 대조 통과. PR 검수와 사용자 최종 확인은 별도다.

| 항목 | 실제 결과 | 근거 |
|---|---|---|
| 기준 회귀 |366/366, 실패0|`ui-shell-baseline.json`|
| 신규 기능 RED |신규 namespace/type 부재 CS0234/CS0246, 3건|`ui-shell-red-compile.json`|
| 최종 컴파일 |completed, failed=false, errors=[]|`ui-shell-final-compile.json`|
| 집중 UI |10/10, 실패0|`ui-shell-focused-final.json`|
| 전체 EditMode |376/376, 실패0|`ui-shell-full.json`|
| 실제 메뉴 |실행 성공, 창1개, footer 존재, 로드 오류 없음|`ui-shell-open.json`|
| 실제 화면 |어두운 테마720×430 콘텐츠와 전체 panel720×456 시각 확인|`ui-shell-capture.json`, `ui-shell-window.png`|
| Console |최종 구간 신규 error/warning0, 현재 error0/warning0|`ui-shell-console-final.json`|
| 보존 |1386개 보호 해시 변경0|`ui-shell-protected-check.json`|
| 독립 코드 검수 |Critical/Important/Minor 없음|[검수 보고](../raw/Reviewer.md)|
| 독립 QA |요구사항·예외·보존·문서와 실제 증거 대조, 차단 결함 없음|[QA 담당 보고](../raw/QAAgent.md)|

JSON과 PNG는 `Library/TftAnalyzerValidation/`에 있으며 Git 제외다. 변경 이유·도구 오용/fixture 수정과 미실행 항목은 [개발 보고서](DevelopmentReport.md)에 구분했다.

## 실행 명령

```powershell
unity command recompile --project-path 'C:\Unity\TFTDataAnalyzer' --format json
unity command recompile_status --project-path 'C:\Unity\TFTDataAnalyzer' --format json
unity command run_tests --project-path 'C:\Unity\TFTDataAnalyzer' --mode editor --filter_type testName --filter TFTAnalyzer.Tests.UI.TftDataAnalyzerWindowTests --timeout 300 --format json
unity command run_tests --project-path 'C:\Unity\TFTDataAnalyzer' --mode editor --filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout 300 --format json
```

실제로는 설치된 CLI 절대 경로를 사용했다. 외부 `success:true`만으로 판단하지 않고 내부 결과·총 실행 수·실패 수까지 확인했다. 테스트10개는 실제 AssetDatabase·EditorWindow·NavigationSubmitEvent 경로를 사용하며, 자산 누락은 없는 경로를 전달해 검증하므로 사용자 파일을 이동하지 않는다.

실제 포인터 입력·수동 domain reload·밝은 테마는 미실행. API 호출·키/seed 조회·실제 수집·내보내기·빌드 차단은 이번 범위가 아니다.

문서의 로컬 링크를 확인해 누락0, 코드·UXML·USS·Markdown의 diff 형식 검사를 통과했다. Unity가 생성한 meta의 빈 YAML 값 뒤 공백은 생성 형식을 유지했으며 제품 코드 오류로 취급하거나 기존 자산을 다시 쓰지 않았다.
