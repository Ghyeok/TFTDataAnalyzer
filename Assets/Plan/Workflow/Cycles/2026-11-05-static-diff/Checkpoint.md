# 정적 카탈로그 비교 검토 체크포인트

상태: 비교 화면 원문 표시 구현·실행 검증·독립 QA 완료, PR 준비. 기준 main ba50db8ad5ef8dc6ef58153cee57da0f8555588c, 브랜치 codex/static-diff-readability. 활성 목표의 2번 정적 데이터 기능을 살펴보며 기존 검색·패치 비교 구현을 중복 개발하지 않는다.

비교 화면은 외부 자료를 동적 Label/Foldout에 직접 전달하며 기존 탐색 창과 달리 리치 텍스트를 명시적으로 끄지 않는다. Unity 6000.3.8f1의 합성 Label에 대해 `enableRichText` 기본값 true를 직접 확인했다. 아직 기존 비교 화면의 실제 PNG에서 숨김을 재현하지 않았으므로 화면 재현은 미실행이다. 내장 Foldout Label probe 두 번은 각각 확장 메서드 범위·오버로드 모호성으로 컴파일 실패했다. 같은 관찰을 반복하지 않고 실제 합성 UI 검증 시 정확한 쿼리를 사용한다. 기존 창을 새로 만들지 않았다.

독립 기획 B는 외부 문자열의 원문 보존 및 비교 하위 트리에서 리치 텍스트 차단을 권고했다. 별도 발견: CatalogDiffer의 관계/효과/출처 연결에는 U+001F/U+001E가 쓰이며 도메인은 원문 안에 같은 제어문자를 금지하지 않는다. 두 다른 합성 효과 사전을 현재 문자열 연결 규칙으로 계산했을 때 동일해짐을 별도 Python 예제로 확인했다. 이는 실제 CatalogDiffer 실행 결과나 실제 카탈로그 발생 증거가 아니며, 별도 회귀로 실제 코드 재현·수정해야 한다. 단순 제어문자 치환으로 원래 목록 경계를 추정하지 않는다.

후속 독립 확인: `Library/TftAnalyzerValidation/StaticDiffCollisionProbe.cs`의 창작 두 카탈로그를 현재 Unity의 실제 `CatalogDiffer.Compare` 및 `CatalogValidator.Validate`에 전달했다. 효과 사전의 항목 수는 서로 다르지만 `ReportedChangedUnits=0`, 두 카탈로그의 검증 오류는 각각 0이었다. 실행 응답은 success=true, compile/execute 완료이며 실제 사용자 카탈로그·API·Unity 창은 사용하지 않았다. 이는 실제 비교기와 검증기의 합성 입력에서 재현한 정확성 결함이다. 실제 카탈로그의 발생 빈도는 미확인이고 제품 수정은 별도 검토·검증이 필요하다. 위 Python 결과는 최초 단순 예제 이력이다.

추가 읽기 전용 확인: 포함된 `Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/catalog.json` 한 개를 JSON으로 읽어 64기물·36특성·522아이템·485증강의 이름/설명에 Unity 리치텍스트 태그 후보가 있는지 검사했다. 아이템 이름 1건에서 태그가 확인됐고 해당 아이템의 한·영 이름 모두 태그를 포함했다. 이 수치는 필드별 정규식 검색의 결과이며 실제 비교 화면에서 숨겨진다는 증거는 아니다. 같은 카탈로그의 효과 키, 안정 ID·역할·등급과 관계 ID에는 U+001E/U+001F가 발견되지 않았다. 출처 필드 및 다른 카탈로그는 이 검사에 포함하지 않았다. 검사 전후 카탈로그 파일 SHA-256은 일치했고 원본을 수정하지 않았다.

합성 표시 경계 확인: `Library/TftAnalyzerValidation/StaticDiffRenderingProbe.cs`가 실제 `DataManagerTab.AddEntityDiff`를 리플렉션으로 호출해 임시 UI 요소만 구성했다. 필드 Before에 태그 원문이 남아 있지만 `enableRichText=true`였으며, 합성 ID를 포함한 Label 2개도 모두 true였다. 성공적으로 반환됐고 EditorWindow 생성이나 파일 게시·API 호출은 없었다. 태그가 렌더링된 실제 PNG나 글자가 숨겨지는 현상까지 확인한 것은 아니다.

구분자 충돌 범위 추가 확인: `Library/TftAnalyzerValidation/StaticDiffExtendedProbe.cs`에서 현재 `CatalogDiffer`와 `CatalogValidator`를 사용해 시너지 연결 ID 목록 및 문서 출처 각각의 서로 다른 창작 입력을 비교했다. 두 사례 모두 이전·이후 검증 오류는 각각 0건이었으나 변경 기물은 0개였다. 기존 효과 사전 사례와 함께 관계 목록·효과·출처 3종의 합성 정확성 결함이 실제 코드 경로에서 확인됐다. 원본에 제어문자가 없는 일반 입력이나 실제 카탈로그에서 발생했다는 증거는 없다. 제품 데이터와 EditorWindow·API를 건드리지 않았다.

Antigravity 기획 A 입력 두 문서는 `AntigravityCollab/tasks/2026-11-05-static-diff-planning/inputs/`에 고정했다. 사용자께서 새 자료 전송 허용을 표현했으나 자동 승인 검토가 이 문서·목적지에 대한 구체적 승인 부족을 이유로 해당 호출을 거절했다. 기획 A 실행·제품 코드 변경·실제 API 호출은 없다. 기존 사용자 파일, Chrome, Unity 창 및 불완전 경기 보관 계획은 보존한다.

2026-11-06 사용자께서 앞으로 Antigravity 승인을 자동으로 하라고 명시한 후 위 기획 A를 정상 실행했다. 결과는 revise이며 Foldout 내부 Label, 긴 자료·작은 창·소유 창 정리 및 제어문자 계산 문제의 별도 관리를 요구했다. `PlanningDraft.md`에 반영했다. 사용자의 지시는 Antigravity 협업 README에 기록했고 자동 승인 검토 자체의 결과를 강제할 수는 없다. 이 시점 제품 수정 및 실제 API 호출은 없다.

후속 구현: 비교 하위 Label의 rich text를 끄고 원문을 보존한다. 신규3 RED 후 DataManagerTabTests 10/10, 전체 1021건 중1019통과/0실패/2건너뜀을 확인했다. 최신 합성 940/500px 8PNG를 직접 확인하고 정상·표시 전/후 예외에서 기존 창9/native1·포커스 보존과 소유0을 확인했다. 독립 코드검수의 긴 문자열과 검증 경고 보완 후 재검수 통과, 최종 QA 진행 중. 자세한 한계는 [결과](DisplayFixResults.md). 비교 계산의 충돌은 미수정이며 별도로 해결한다.

독립 최종 QA는 최신 8PNG와 정상·예외 정리 증거를 직접 대조하여 필수 지적 없이 통과했다. 실행 테스트 수치는 root가 수행한 결과이고 QA가 직접 테스트를 실행했다고 주장하지 않는다. 다음은 이 표시 수정의 PR과 최신 head 검수다. 데이터 비교 계산의 구분자 충돌은 별도 작업으로 남는다.
