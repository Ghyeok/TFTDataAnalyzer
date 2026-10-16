# 통합 Editor 창과 탭 전환

날짜: 2026-10-16
상태: 구현·집중10/10·전체376/376·독립 코드 검수 통과. PR 검수와 사용자 최종 확인은 별도다.

## 변경

`TFT Analyzer > Open Data Analyzer` 메뉴에 UI Toolkit 창을 추가했다. 데이터 관리·경기 수집·과거 집계 분석·정책 안내 중 하나만 표시하며, 선택은 UI 재생성 및 직렬화 복원 후 유지된다. 과거 관측 고지는 탭 밖에 항상 표시한다.

고정 UXML/USS가 각각 또는 함께 없으면 해당 경로를 설명하는 기본 오류 화면과 고지를 표시한다. 자산을 복구하고 다시 UI를 만들면 오류 화면 대신 기존 선택 탭이 열린다. 기존 import 창·메뉴·카탈로그와 사용자 Volume 변경은 보존했다.

이번 범위는 창과 탭 전환이다. 실제 데이터 관리·수집·통계·CSV·정책 설정·빌드 차단·자동 버전 확인은 후속 작업이며, 창을 여는 동작은 네트워크·키/seed 조회·관측 저장을 시작하지 않는다.

## 실행 증거와 재작업

- 기준 `0b1eab9`: PR #8 최신 `e3b35f1` CodeRabbit SUCCESS·미해결0, 재개 시 실제366/366 통과 후 병합했다.
- RED: 새 테스트만 추가한 뒤 실제 Unity 컴파일이 UI namespace 부재 CS0234 및 창 타입 부재 CS0246 총3건으로 실패했다.
- 구현 후 컴파일 completed/failed=false/errors=[]를 확인했다.
- 입력 이벤트 조사에서 직접 ClickEvent는 Button.clicked의 실제 활성화 경로가 아님을 확인했다. 기본 Button.clicked를 유지하고 테스트는 NavigationSubmitEvent로 활성화한다. 공개 Unity 소스 근거는 [구현 담당 보고](../raw/Implementer.md)에 있다.
- 첫 집중 실행은 필터 형식 `class` 미지원으로 실행0건이었다. 내부 `success:false`와 `testName/assembly/category` 계약을 읽고 실행 구문만 수정했다. 0건을 통과로 계산하지 않는다.
- 올바른 집중 실행은9/10. 실패 테스트의 부분 JSON이 EditorWindow의 실제 MonoBehaviour 래퍼 형식과 달라 잘못된 값을 설정하지 못했다. 한 번의 실제 진단에서 값이 analysis-tab에 그대로 남음을 확인했다. 제품 변경 없이 SerializedObject로 invalid 값을 주입·확인하도록 fixture를 수정했다.
- 최종 컴파일 오류0, 집중10/10, 전체 EditMode376/376(실패0). 기본 실행은 Explicit 네트워크 테스트를 포함하지 않는다.
- 실제 메뉴 실행 성공·창1개·로드 오류 없음. 720×430 콘텐츠(제목 포함720×456) 어두운 테마 화면을 캡처해 네 탭·텍스트·footer를 시각 확인했다. 첫 캡처는 제목 높이를 빠뜨려 footer 아래쪽이 잘렸고, 전체 panel 높이를 사용해 캡처를 바로잡았다. 제품 레이아웃 변경은 없었다.
- 이전 Pipeline 연결 종료2건과 의도한 RED 오류는 버퍼 이력이다. 최종 실행 구간 cursor17 이후 신규 warning/error0, 현재 Console error0/warning0 및 compilationFailed=false를 확인했다.
- 보호 해시1386개(사용자 Volume 및 추적된 generated 파일) 변경0.

실행 파일은 Git 제외 `Library/TftAnalyzerValidation/ui-shell-{baseline,red-compile,final-compile,focused-final,full,open,capture,console-final,protected-check}.json`과 `ui-shell-window.png`다. 재현 명령·요약은 [QA 보고서](QAReport.md)에 남겼다.

## 역할·모델·한계

루트가 조율·Unity 실행·문서·통합을 수행하고 기획2명, 구현1명, 읽기 전용 검수1명을 분리했다. 각 하위 호출은 `gpt-6.1-sol` / `medium`을 명시했다. 역할별 고정 고가 모델 대신 명확한 작은 UI 작업의 프로젝트 기준을 따랐다. 하위 보고의 런타임 식별 불가는 유지하며 요청값과 실제 식별을 혼동하지 않는다. 루트 런타임 상세·토큰·전체 소요시간은 확인 불가라 추정하지 않았다.

사용자 별도 승인으로 Unity CLI beta.13 업데이트와 안내 스킬 refresh를 수행했으며 실제 version 출력을 확인했다. Unity Editor 6000.3.8f1과 프로젝트 패키지는 변경하지 않았다.

실제 포인터 클릭·수동 domain reload·밝은 테마 시각 검수는 미실행이다. 실제 메뉴·NavigationSubmit·EditorJsonUtility 상태 복원 및 어두운 테마 확인과 구분한다. 전체 Phase 3 완료를 의미하지 않는다.
