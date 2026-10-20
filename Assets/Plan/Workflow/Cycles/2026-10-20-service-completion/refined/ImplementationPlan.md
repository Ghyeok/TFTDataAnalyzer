# 로컬 정적 카탈로그 탐색 구현 계획

> 실행은 승인 후 superpowers:executing-plans를 적용하며 기존 코드검수·QA 담당을 재사용한다. 이 문서가 이번 사이클 TODO의 정본이다.

2026-10-20. 상태: 기획A/B 검토 의견 통합·보완 후 사용자 “이 계획대로 구현 진행 (권장)” 승인, 구현·전체 검증·QA 통과, 외부 PR검수/병합 별도. 고정 입력은 AntigravityCollab/tasks/2026-10-20-catalog-browser-planning/inputs/에 보존한다. A는pass+의견2건, B는revise+의견2건이며 동일한 관계/출처시각 모호성을 정본에 보완했다. 보완본의 추가 AI 재검토를 수행했다고 주장하지 않는다.

**Goal:** 데이터 관리 탭에서 검증된 활성 카탈로그의 기물·시너지·아이템·증강을 한국어/영어/안정 ID로 검색하고 상세 내용을 읽을 수 있게 한다.

**Architecture:** 기존 창의 SyncCollectionCatalog에서 DataManagerViewModel.Rows의 IsSelectable·ActiveIdentity 일치 행을 고르는 경로를 재사용해 새 CatalogBrowserViewModel에 같은 카탈로그를 전달한다. 고정 초안의 TryGetActiveSelection은 실제 코드에 없는 메서드명이어서 기존 연결 경로로 정정했다. 순수 투영/검색 모델과 disposable UI binding을 분리하고 기존 데이터 관리 탭에 접힌 상세 탐색 영역을 추가한다. 필터/선택은 메모리에서만 동작하며 원본/카탈로그/관측/집계를 수정하지 않는다.

**Tech Stack:** Unity6000.3.8f1, 기존 C#·UI Toolkit·NUnit. 새 패키지 없음.

**Spec:** Assets/Plan/TFTDataPipeline.md §2.1·6·12 및 ServiceCompletionAudit.md. 제품 범위는 기존4개 탭·개인 Editor 사용이다.

## 승인할 동작

- 현재 활성 카탈로그 아래에 ‘카탈로그 내용 탐색’ Foldout을 추가한다. 종류는 기물/시너지/아이템/증강, 검색은 한국어 이름·영어 이름·안정ID의 대소문자 무시 부분 일치다. 기본 기물/빈 검색, 필터 결과 건수와 이름 목록을 제공한다. 지역·통계·티어와 별개인 정적 원본 탐색임을 표시한다.
- 결과는 안정ID 순서·한 페이지50개이며 이전/다음 버튼으로 전부 접근한다. 새 검색/종류/카탈로그 전환은 페이지0·상세선택 해제다. 카탈로그가 없거나 검색 결과가 없으면 상태를 구분해 안내하고 이전 상세는 제거한다. 카탈로그 변경은 같은 이름/ID가 있어도 이전 선택을 재사용하지 않는다.
- 항목 선택 시 안정ID, 한국어/영어 이름·설명, 한국어 누락의 영어 대체 안내, 기존 레코드가 보유한 속성/효과/관계ID와 출처를 읽는다. 없는 비용/역할/등급/효과/관계는 ‘자료 없음’으로 구분하며0/임의 값/추론으로 보충하지 않는다. 관계ID는 해당 종류와 참조 이름을 함께 표기하되 새 탐색/네트워크를 자동 실행하지 않는다.
- 관계 이름은 현재 활성 카탈로그 안에서만 찾는다. 기물 TraitIds는 시너지, 아이템 ComponentIds는 아이템, 증강 RelatedIds는 해당 ID가 있는 종류들을 모두 조회한다. 대상 누락은 원시ID와 ‘카탈로그 내 미수록(이름 없음)’을 표시하고, 종류가 여러 개면 모두 종류를 붙여 표시하며 임의로 하나를 고르지 않는다.
- Sources의 조회UTC가epoch/미지정이면 ‘정규화 기본값 — 실제 수집 시각은 상단 로컬 카탈로그 참고’로 안내한다. 이를1970년에 실제로 수집한 것처럼 표시하지 않는다. 다른 출처 시각도 원본 메타데이터로만 표시하며 standalone manifest의 카탈로그 생성/수집시각과 구분한다. 정규화 본문을 고치지 않는다.
- 설명과 출처 등의 외부 문자열은 rich text/링크/명령으로 실행하지 않고 plain text로 표시한다. 필드별4096자 표시 상한과 생략 안내를 둔다. 검색 입력은256자, 단순 문자열 비교이며 정규식/HTML 렌더러를 추가하지 않는다.
- 아이콘은 기존 Assets/TFTAnalyzer/Data/Generated/Icons/ 아래의 정확한64자리 소문자 SHA-256.png 경로만 UI의 주입된 loader로 읽고, 파일/폴더 reparse 검사와 Texture2D 타입 확인 후 표시한다. URI/절대경로/상위경로·다른 자산은 거부하고 누락/오류는 ‘아이콘 없음’이다. 원본 다운로드/재저장/텍스처 생성/기존 자산 파괴는 하지 않는다. 검증은 로더 경계와 실제 누락 대체까지 포함한다.
- UI 재구성은 검색/종류 상태를 같은 VM에서 유지하되 현재 활성 카탈로그가 바뀌면 결과·선택을 재설정한다. 바인딩 Dispose는 자기 이벤트·일회성 callback만 해제하고 기존 창/Inspector/자산은 건드리지 않는다. 기존 가져오기/취소/패치 비교/수집/분석 동작을 보존한다.
- 같은 카탈로그 객체와 전체identity/setNumber의 반복 SetCatalog는 멱등이며 검색/종류/페이지/선택을 유지한다. 실제 다른 카탈로그 객체나identity로 바뀌면 이전 선택/페이지를 해제한다. 데이터 관리의 상태 알림이나 UI 재구성을 카탈로그 변경으로 오인해 검색을 초기화하지 않는다.

## 범위 밖

실제 증강의 정책 분류 승인/등록, 카탈로그 수록과 실제 해당 패치 등장 가능성의 인증, 새로운 통계/집계 그룹, 최소 표본 하한 변경, 실제 Riot API 호출, 새 다운로드/자동 수집/외부 배포는 추가하지 않는다. 증강 상세를 보여도 비율 차단은 유지한다. source16.18.1과TFT18.2·세트18·공식출처 전체 맥락을 표시하고 수록 여부가 현재 경기 등장 여부를 뜻하지 않는다고 안내한다.

## 파일과 인터페이스

- 새 Editor/UI/Data/CatalogBrowserViewModel.cs: CatalogRecordKind enum, 불변 표시 레코드, SetCatalog(StaticCatalog, CatalogIdentity), SetFilter(kind, search), Select(stableId), SetPage(int), 결과/총수/선택/Changed. 기존 AnalysisService.MatchesCatalog 검증을 재사용하거나 동등한 공용 기존 검증을 사용하고 legacy identity·세트 번호를 확인한다. Unity API/I/O 없음.
- 새 Editor/UI/Data/CatalogBrowserTab.cs: UXML controls와 VM 연결·아이콘 loader 주입·plain text detail·소유 callback 해제. 작업별 새로운 창/팝업 없음.
- 수정 TftDataAnalyzerWindow.cs: VM 보존·active selection 동기화·binding생성/Dispose 연결. 기존 경로/서비스를 대체하지 않는다.
- 수정 TftDataAnalyzerWindow.uxml/.uss: 기존 데이터 관리 탭의 탐색 Foldout·종류·검색·목록·paging·상세·아이콘/placeholder. 940px와500px에서 문장 줄바꿈/세로 스크롤로 접근 가능하다.
- 새 Tests/EditMode/UI/CatalogBrowserViewModelTests.cs, CatalogBrowserTabTests.cs 및 기존 TftDataAnalyzerWindowTests.cs의 필요한 통합 회귀. 새 파일에는.meta를 보존한다.
- 문서 README·구현 색인·이번 체크포인트/실행/QA/최종 문서. 사용자 DefaultVolumeProfile.asset·기존 카탈로그·관측은 수정/커밋 제외한다.

## TODO와 완료 조건

### Task1: 읽기 전용 투영·검색

- [x] RED:4종류 이름/ID검색, 한영fallback, 동일ID종류분리, legacy전체identity/setnumber검증, 잘못된 선택/검색0개/없음상태,51/101개paging와경계값, 검색전환선택해제, 관계대상누락/교차종류/다중종류와epoch출처시각안내, 원본컬렉션/정렬/값불변을 확인한다.
- [x] 최소 투영/VM 구현 후 같은 테스트 GREEN. 필드 누락을 가짜0·분류값으로 채우지 않고 원본 내용을 보존한다.
- [x] 기존 코드검수 담당이 요구사항·안전 경계·과구현·테스트 증거를 읽기 전용 검토한다.

### Task2: 기존 탭 연결과 수명주기

- [x] RED: 실제 UI events로 검색·종류·목록선택·page·카탈로그 전환, 같은선택재동기화멱등성/재구성상태유지, 카탈로그 없음/누락아이콘, richtext문자열, 경로거부/로더미호출, 로더오류fallback, dispose 후무응답/재구성구독중복0을 검증한다.
- [x] binding/UXML/USS/창연결 최소 구현 후 GREEN. 기물/효과/관계/출처를 확인하고 기존 수집/분석의 동일 활성선택 유지·가져오기/필터로추가HTTP0을 검증한다.
- [x] 독립 코드검수와 기존 QA 담당의 범위·회귀 대조. 지적은 같은 담당으로 수정/재검수한다.

### Task3: 통합 검증과 인수

- [x] root가 unity-cli 스킬과 docs/unity-editor-validation.md를 읽고 현재 Editor 연결·컴파일 상태를 확인한다. 관련UI/기존데이터관리 회귀 후 코드동결 전체EditMode를1회 실행한다. 오류나 새 변경이 있을 때만 반복한다.
- [x] 현재 소유창검증 패턴을 재사용해 새 탐색 영역의940/500px 실제PNG를 확인한다. 성공/예외 모두finally에서 정확한 소유 EditorWindow·native프레임·callback을 정리하고 기존 창/Inspector/포커스 보존을 확인한다. 기존 수집/분석 도구를 단순 실행한 결과로 새 탐색화면 검증을 대신하지 않는다.
- [x] 보호카탈로그/아이콘/관측/사용자파일 전후해시 불변과 새Console오류0을 확인한다. 실제 API/분류자료 미수행은 명시한다.
- [ ] 실행/코드검수/QA/문서 증거를 저장하고 변경파일만커밋·PR제출. 최신head CodeRabbit/review본문·미해결·CI를5분간격확인해 조건충족시main병합. 그뒤 실제분류자료검토와 남은과업으로진행하며 사용자최종결과승인은별도로기록한다.

## 기획 검수 포인트

기획A는 사용자 가치·상위설계 누락 보완 여부·정적탐색/통계허용 구분·현재패치로오인할위험·승인범위를, 기획B는 같은identity유지·참조/효과누락·UI재구성/이벤트/동결검증·외부문자열/아이콘경로검증을 독립 검토한다. 같은 고정입력만 사용하고 상대초안은 보지 않는다. 이 계획은 아직 사용자 승인 전이다.
