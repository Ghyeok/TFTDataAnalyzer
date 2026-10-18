세 가지 구체적인 검증 도구 문제가 있습니다. 제품 기능을 더 확장할 필요는 없습니다.

1. **P2 — 수동 검증 창의 소유권·종료 경로가 없습니다.**  
   `Library/TftAnalyzerValidation/collection-open.cs:18`에서 창을 생성하고 `:23`, `:33`에서 표시·포커스를 옮긴 뒤 그대로 반환합니다. `collection-capture.cs:14`는 그 창이 다음 호출까지 남아 있다고 가정하고, `:28`에서는 텍스처만 정리합니다. 열기나 캡처 실패·중단에도 창과 원래 포커스를 정리하는 공통 범위가 필요합니다.

2. **P2 — 렌더 대기 헬퍼가 Inspector를 새로 만들 수 있지만 소유권을 추적하지 않습니다.**  
   `CollectionUITestSupport.cs:53`, `DataManagerTabTests.cs:207`의 메뉴 호출은 Inspector가 없으면 생성합니다. 각각 `:65`, `:220`에서 이전 포커스만 복원하므로 새 Inspector는 남습니다. 기존 5초 제한과 `delayCall` 해제는 적절하지만 창 정리는 별개입니다.

3. **P2 — 과거 수동 헬퍼는 사용자 창을 재사용·변경할 수 있습니다.**  
   `data-manager-open.cs:12–16`, `data-manager-minimum.cs:12–15`, `ui-shell-open.cs:14–18`은 메뉴의 `GetWindow` 경로로 기존 사용자 창을 가져와 위치·크기·포커스를 바꿉니다. `ui-shell-open.cs:12`의 이름 기반 일괄 종료도 명확한 소유권 증거가 아닙니다. `ui-shell-diagnose-selection.cs:12–13` 역시 직접 생성한 창을 정리하지 않습니다.

UI 테스트 본체는 정상 TearDown에서 명시적으로 생성한 호스트를 파괴합니다(`CollectionTabTests.cs:37–42`, `CollectionWindowTests.cs:42–47`, `DataManagerTabTests.cs:46–51`, `TftDataAnalyzerWindowTests.cs:33–37`). 이를 모두 실제 누수라고 보고해서는 안 됩니다. 다만 창 표시 후 SetUp이 실패하거나 앞선 Dispose가 예외를 던질 경우를 보장하는 `try/finally`와 테스트 시작 전 포커스 복원은 없습니다.

최소 수정안은 작은 공통 `OwnedEditorWindowScope` 하나입니다.

- 생성 시 기존 창 ID와 원래 포커스를 기록합니다.
- `Create<T>()`는 **Show 전에** 정확한 객체를 소유 목록에 등록합니다. 이름·타입·전체 창 차이만으로 소유권을 추정하지 않습니다.
- Dispose는 자신이 생성한 객체만 닫고, 이미 닫힌 객체는 건너뜁니다. 마지막에 원래 창이 살아 있을 때만 포커스를 복원합니다. 여러 창 중 하나의 정리 실패가 나머지 정리를 막지 않게 합니다.
- 각 fixture SetUp에는 실패 시 scope 정리를 수행하는 catch 경로, TearDown에는 scope 정리를 보장하는 finally를 둡니다.
- Inspector가 필요하면 기존 인스턴스를 잠시 사용하고, 없을 때만 명시적으로 생성·소유합니다. 메뉴 실행을 통한 암묵적 생성은 제거합니다. 대기는 기존처럼 5초로 제한하고 finally에서 예약 콜백을 해제합니다.
- 수동 검증은 하나의 bounded 작업 안에서 `생성 → 실제 렌더 대기 → 두 크기 캡처 → finally 정리`를 끝냅니다. 이미 `async Task Main()` 검증 스크립트가 있으므로 서로 독립된 open/capture 호출보다 이 경로가 작습니다. 취소·시간 초과도 같은 finally로 합류시킵니다.
- 스크립트 성공·실패 증거에는 시작/종료 창 ID, 생성한 ID, 종료 확인, 원래 포커스 복원 여부를 기록합니다. 사용자 기존 창이 살아 있고 소유 창이 남지 않았는지를 확인하며 전체 창 개수만 비교하지 않습니다.

필요한 회귀 검증은 정상 종료, Show 직후 예외, 렌더 대기 시간 초과, 이중 Dispose/미리 닫힌 창, 기존 사용자 Analyzer·Inspector 보존, Inspector 부재 시 생성분 정리입니다. 테스트 실행 전체의 전후 inventory도 함께 남기면 사용자의 지적을 직접 검증할 수 있습니다.

현재 `collection-window-inventory.json`에는 표준 Unity 창 9개만 있고 Analyzer나 이름 없는 테스트 호스트는 없습니다. **누가 이전 창을 닫았는지는 이 자료로 확인할 수 없습니다.** `collection-final-full.json:13–15`의 455/455 통과는 확인했으며, 초기 8 RED와 회귀 2 RED는 보고서에 기록된 의도적 검증 단계입니다. 기존 draft/rebuild P2는 해결된 상태로 유지합니다. Task 4 분석·Task 5 정책·Task 6 스케줄러는 이번 정리에 포함할 이유가 없습니다.

읽기 전용 재검토만 수행했으며 파일·Unity·Git·환경변수를 변경하거나 실행하지 않았습니다. 요청 모델과 실제 런타임 모델·추론의 일치 여부는 독립적으로 확인할 수 없습니다.
