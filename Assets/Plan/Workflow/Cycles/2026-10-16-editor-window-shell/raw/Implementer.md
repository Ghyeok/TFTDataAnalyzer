# 통합 Editor 창 구현 담당 보고

## 1차: RED 준비

- 범위: Phase 3 Task 1. 제품 코드는 작성하지 않았으며 테스트 파일만 추가했다.
- 변경: `Assets/TFTAnalyzer/Tests/EditMode/UI/TftDataAnalyzerWindowTests.cs`.
- 테스트: 실제 EditorWindow와 AssetDatabase 자산, toolbar, 단일 탭 표시, ClickEvent 전환, 지속 고지, 중복 재생성, 선택 유지, EditorJsonUtility 직렬화 복원, 잘못된 선택 복구, UXML/USS 각각 및 동시 누락, 정상 로드 복구, 최소크기 wrapping을 확인하도록 작성했다.
- 최소 구현 계약: `TFTAnalyzer.Editor.UI.TftDataAnalyzerWindow`, `public CreateGUI()`, `internal RebuildUI(string uxmlPath, string ussPath)`, 직렬화되는 문자열 `selectedTab`에 탭 루트 이름 저장. 공개 로더나 서비스는 요구하지 않는다.
- 예상 RED: 신규 namespace/type 부재로 컴파일 실패. 루트가 실제 Unity 실행 결과를 확인한 후 구현을 지시해야 한다.
- Unity: 실행하지 않음. 현재 RED/GREEN 결과는 아직 없다. 직렬화 테스트는 상태 복원 검증이며 전체 domain reload의 실행 증거가 아니다.
- 자체 검토: 기존 사용자 변경, 기존 importer 및 카탈로그를 수정하지 않았다. 테스트는 자산 이동·삭제, 네트워크·환경 변수·키·seed 접근을 하지 않는다. 테스트 종료 때 창 인스턴스를 파괴한다.
- 모델·추론: 프로젝트 일반 개발 권장값 GPT-6.1 Sol / Medium. 이 하위 실행에서 실제 런타임 모델 및 추론 설정 식별은 확인 불가. 사용량·소요시간 수치는 도구로 제공되지 않아 추정하지 않았다.
- 다음 행동: 루트 단독 Unity RED 실행, 결과 전달 후 최소 제품 구현.

## 2차: 제품 구현 / 루트 검증 대기

- 실제 RED 근거: `Library/TftAnalyzerValidation/ui-shell-red-compile.json`을 읽었다. `status: completed`, `failed: true`, UI namespace 부재 CS0234 및 창 타입 부재 CS0246 두 건의 예상 실패다.
- 추가 파일: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`, `.uxml`, `.uss`.
- 구현: 고정 경로 자산 로딩, 메뉴, 네 toolbar 버튼, 단일 탭 표시, 선택 직렬화/잘못된 값 기본 복구, 별도 TemplateContainer 교체를 통한 중복 방지, UXML 한 곳에서 USS 연결, 누락 경로별 기본 오류 화면 및 지속 고지. 콘텐츠는 한국어 목적·후속 연결 안내만 제공한다.
- 누락 시에는 stylesheet 자체가 없을 수 있어 두 기본 Label에 wrapping과 footer 보존에 필요한 최소 inline 레이아웃만 적용했다. 정상 UI 스타일은 USS로 정의한다. 테마 기본 글자색/배경을 사용한다.
- 이벤트 수정 이유: 처음 작성한 직접 `ClickEvent` 전송은 Unity Button.clicked의 실제 입력 경로가 아니었다. Unity 6000.3 [Button 소스](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3/Modules/UIElements/Core/Controls/Button.cs)와 [Clickable 소스](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3/Modules/UIElements/Core/Clickable.cs)를 확인했다. Button은 NavigationSubmit을 받아 Clickable.SimulateSingleClick을 실행하며 Clickable은 pointer down/up을 처리한다. 제품은 기본 `Button.clicked`를 사용하고, 테스트는 focus 후 실제 `NavigationSubmitEvent`를 보내도록 수정했다. 제품 핸들러를 직접 부르거나 mock하지 않는다. 실제 포인터 클릭은 루트 화면 검증에서 확인해야 한다.
- 자체 검토: Clear 후 새로운 clone만 추가하며 USS를 C#에서 추가하지 않는다. 성공 재생성은 선택을 복구하고 오류 화면과 오래된 이벤트 대상은 트리에서 제거한다. footer는 네 콘텐츠 루트 밖에 있다. 서비스·ViewModel·Update·HTTP·환경 변수·키/seed·저장·CSV·컴플라이언스 기능은 추가하지 않았다.
- 검증 상태: Unity 실행 및 GREEN 결과 확인은 담당 범위 밖이므로 미실행. UXML 기본 XML 형식과 소스/스코프를 확인했지만 UXML/USS Unity 파싱·실제 레이아웃·집중/전체 테스트는 루트 검증 대기다. `.meta` 생성은 Unity에 맡겼다.
- 모델·추론: 별도 모델·추론 요청값은 이 작업 요청에 없으며 프로젝트 기본 권장값만 위에 기록했다. 실제 식별과 사용량·소요시간은 확인할 수 없다.

## 3차: 잘못된 직렬화 선택 테스트 입력 수정

- 루트 실제 집중 결과 `Library/TftAnalyzerValidation/ui-shell-focused.json`: 10개 중 9개 통과, `InvalidSerializedSelection_FallsBackToData` 1개 실패. 제품 컴파일 성공은 루트가 확인했다.
- 원인 근거 `Library/TftAnalyzerValidation/ui-shell-selection-diagnostic.json`: 실제 EditorJsonUtility JSON은 `MonoBehaviour` wrapper를 포함한다. 테스트의 wrapper 없는 부분 JSON 적용 뒤에도 선택은 `analysis-tab`에 머물렀고, 재생성도 해당 유효 선택을 정상 유지했다. 제품 fallback 문제가 아니라 잘못된 테스트 fixture였다.
- 수정: 해당 테스트에서 `SerializedObject.FindProperty("selectedTab")`로 `unknown-tab`을 설정하고 `ApplyModifiedPropertiesWithoutUndo()` 후 값을 확인한다. 기존 Data fallback·footer 기대는 유지했다. 제품 파일과 대기 횟수는 변경하지 않았다.
- 수정 후 Unity 실행은 미실행이며 루트가 실제 집중/전체 검증을 이어간다.
