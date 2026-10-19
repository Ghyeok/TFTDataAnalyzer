# Task 5 정책 안내와 빌드 차단 구현 계획

상태: 구현·독립 코드 검수·별도 QA 통과. PR 검수와 사용자 최종 승인은 별도다. 작성일: 2026-10-19. 사용자 “워크플로대로 진행해줘”로 이 계획의 구현을 승인받았다.

> 실행자는 승인 후 기존 개발 워크플로와 `superpowers:executing-plans` 또는 `superpowers:subagent-driven-development`를 따른다. 실행·완료 조건은 이 문서에서 관리한다.

**Goal:** 정책 출처와 사용자가 확인한 등록 상태를 표시하고, 확인되지 않은 상태에서 이 프로젝트의 Player 빌드를 차단한다.

**Architecture:** 로컬 설정 저장소, 부작용 없는 허용 판단, Unity 빌드 콜백, Settings/About 표시를 분리한다. 설정이 없거나 읽기에 실패하면 배포를 허용하지 않으며, 기존 Editor 탐색·개인 연구용 로컬 CSV 경로에는 등록 상태 조건을 추가하지 않는다.

**Tech Stack:** 기존 Unity 6000.3.8f1, C#, UI Toolkit, Newtonsoft.Json, EditMode 테스트. 패키지는 추가하지 않는다.

**Spec:** [Phase 3 Task 5](../../../../Implementation/2026-09-17-tft-analyzer-editor-ui.md), [제품 설계 §4](../../../../TFTDataPipeline.md), [개발 워크플로](../../../README.md).

## 기준과 범위

- 기준: PR [#12](https://github.com/Ghyeok/TFTDataAnalyzer/pull/12) 병합 커밋 `6a9027c3ae9426c195dbc70ab4d16fbc46374e59`. Task 4의 마지막 실행 기록은 전체 501/501이며 이번 문서 작업에서 다시 실행하지 않았다.
- 포함: 정책 안내 탭, Project Settings, 로컬 확인 메타데이터, Player 빌드 전 차단, 공개 제공 허용 판단의 단위 테스트. 파일 symlink 생성 사례 1건은 호스트 권한으로 건너뛰었고 접합점 차단은 실제 실행했다.
- 제외: 실제 제품 등록/승인 요청, 승인 상태 온라인 조회, 정책 자동 판독, 공개 내보내기 기능 신설, 네트워크 수집 변경, Task 6 버전 예약·E2E.
- 제품 설계의 외부 데이터 갱신 중단은 이번 범위 밖이다. Task 5 완료 보고는 배포 차단과 고지만 다루며 정책 경계 전체의 완성을 주장하지 않는다.
- `ApprovedOrAcknowledged`는 사용자가 Riot Portal 상태를 확인해 기록한 값이다. API 키 유무·저장 버튼·정책 링크 열기를 승인 증거로 사용하지 않는다.
- 정책 검토일은 사용자가 기록한다. 화면을 열었다는 이유로 오늘 날짜·확인 시각을 저장하지 않는다. 등록 미확인 상태가 기본값이다.
- 사용자 `Assets/DefaultVolumeProfile.asset`과 기존 Generated 카탈로그를 보존한다. 설정은 `Library/TftAnalyzerStore/compliance.json`에만 저장하고 Git에 포함하지 않는다. 키·seed·개인 식별 필드는 추가하지 않는다.

## 공식 출처와 결정

2026-10-19 계획 조사에서 아래 원문을 열어 등록·고지 항목을 확인했다. 이는 제품의 등록 완료나 사용자의 정책 확인을 대신하지 않는다.

- [TFT Developer Policy](https://developer.riotgames.com/docs/tft): 이용자에게 제공하는 제품의 등록과 TFT 제한을 확인한다.
- [General Policies](https://support-developer.riotgames.com/hc/en-us/articles/22698591841939-General-Policies): 등록, 변경 심사, 눈에 띄는 비공식 프로젝트 고지의 출처다. 구현 시 제품명을 적용한 원문 고지를 제공하고 링크를 함께 표시한다.
- [Legal Jibber Jabber](https://www.riotgames.com/en/legal): IP 이용과 팬 프로젝트 고지의 별도 출처다. API 일반 정책 고지와 혼동하지 않고 필요한 안내를 함께 제공한다.
- 이 프로젝트의 Player 빌드 차단은 기존 설계의 내부 배포 통제다. 로컬 확인 값만으로 Riot 승인을 검증했다고 표시하지 않는다.
- 구현 선택: 기존 UI Toolkit/JSON 경로를 유지한다. ProjectSettings 에셋·EditorPrefs·자동 기본 파일 생성은 사용하지 않는다. 세 정책 URL은 고정 출처이며 사용자 임의 URL 실행은 허용하지 않는다.

## 검수 초점

1. 설정 없음·손상·알 수 없는 상태에서 배포 허용으로 넘어가지 않는다.
2. 저장 실패·화면 재생성·어셈블리 재로드가 확인 상태나 시각을 만들어내지 않는다.
3. 등록 확인 여부와 관계없이 기존 로컬 CSV의 억제 필드·개인 연구 표시가 유지된다.
4. Project Settings와 About이 같은 저장 결과를 표시하며 오류에 파일 내용·비밀값을 노출하지 않는다.
5. 창 검증은 소유 창만 닫고 기존 EditorWindow/native 프레임 ID·포커스를 보존한다.

## 구현 순서와 완료 조건

각 작업은 필요한 실패 테스트 → 실제 실패 확인 → 최소 구현 → 집중 테스트 순으로 실행한다. 아래 시그니처는 계획에서 정한 연결 계약이다.

### 1. 확인 설정과 허용 판단

**Files:** `Editor/Compliance/ComplianceSettings.cs`, `ComplianceSettingsStore.cs`, `CompliancePolicy.cs`, `PolicyCopy.cs`; 대응 테스트는 `Tests/EditMode/Compliance/` 아래. 경로 접두사는 `Assets/TFTAnalyzer/`이다.

**Interfaces:**
- `ComplianceSettings`는 `policyReviewedOn`, `policyUrls`, `registrationStatus`, `acknowledgedAt`만 저장한다. JSON 필드 이름은 이 계약을 따른다.
- `ComplianceSettingsStore(string projectRoot)`의 `ComplianceSettingsReadResult Load()`는 설정과 읽기 상태(Missing/Valid/Invalid/Unavailable)를 함께 반환하고, `void Save(ComplianceSettings settings)`는 검증 성공 후 명시적 저장만 수행한다. 결과 타입은 같은 파일에서 정의하며 테스트에서는 임시 프로젝트 루트를 사용한다. 경로 객체 생성과 파일 접근은 `Load()` 안에서 처리하고 접근 실패를 Unavailable로 반환한다.
- `CompliancePolicy.CanPublish(ComplianceSettings settings, DateTimeOffset utcNow)`는 유효한 필수 메타데이터와 `ApprovedOrAcknowledged`를 모두 요구한다. null/손상/알 수 없는 값은 false다.
- `policyReviewedOn`은 `yyyy-MM-dd`, `acknowledgedAt`은 명시적 확인 시 UTC 시각이다. 확인되지 않은 상태에는 확인 시각을 생성하지 않는다. 검토일 또는 확인 시각이 미래이거나 필수 세 정책 URL과 다르면 공개 제공을 허용하지 않는다. 임의의 만료 주기는 추가하지 않는다.

- [x] 설정 없음·잘못된 JSON·알 수 없는 상태·누락된 URL/날짜·확인 시각 없음·미래 날짜를 거부하는 테스트를 먼저 실패시킨다. 현재 시각은 테스트에 주입한다.
- [x] 허용 상태와 완전한 메타데이터의 저장/재읽기, 미확인 상태의 저장, 명시적 상태 철회가 반영되는 테스트를 추가한다.
- [x] 기존 `AnalyzerStorePaths.RejectReparsePoints`를 설정 파일·임시 파일·교체 직전 대상에 직접 적용한다. 부모만 확인하는 `EnsureSafe()`에 의존하지 않는다. 같은 디렉터리 임시 파일과 교체로 저장하며 실패 시 기존 파일을 보존하고 임시 파일을 정리한다. 테스트로 교체 실패·링크 경로 거부를 확인한다.
- [x] 읽기는 디렉터리/파일을 생성하지 않고 상태·고정 오류 코드만 반환한다. 손상 원문이나 예외의 임의 메시지는 UI·로그에 복사하지 않는다.

### 2. 빌드 전 차단

**Files:** `Editor/Compliance/ComplianceBuildGuard.cs`; `Tests/EditMode/Compliance/ComplianceBuildGuardTests.cs`.

**Interfaces:** `ComplianceBuildGuard : IPreprocessBuildWithReport`; `ValidateForBuild()`가 현재 설정을 다시 읽고 `ReadResult.Valid && CanPublish(...)`일 때만 허용한다. 그 외는 `BuildFailedException`을 던진다. Unity 기본 생성자의 실제 콜백과 저장소/시계를 주입한 테스트 모두 같은 메서드를 호출한다. 테스트는 실제 Player 빌드를 시작하지 않는다.

- [x] 미확인·철회·손상·읽기 실패에서 예외를, 확인된 유효 설정에서 허용을 확인한다. 오류에는 Project Settings 위치와 조치만 담는다.
- [x] 테스트에서 빌드 콜백이 같은 판단을 사용하는지 확인한다. 빌드 직전에 파일을 바꾸면 최신 상태를 반영해야 한다.
- [x] 공개 내보내기는 현재 존재하지 않으므로 새 화면이나 명령을 만들지 않는다. 동일한 공개 제공 판단을 단위 테스트하되 실제 공개 내보내기 경로를 검증했다고 기록하지 않는다.

### 3. Project Settings와 정책 안내

**Files:** `Editor/Compliance/ComplianceSettingsProvider.cs`, `Editor/UI/About/AboutViewModel.cs`, `AboutTab.cs`; 기존 `TftDataAnalyzerWindow.cs`, `.uxml`, 필요 시 `.uss`; 대응 Compliance/UI 테스트.

**Interfaces:** Settings 경로는 `Project/TFT Data Analyzer/Compliance`. About은 읽기 전용 표시이며 설정 페이지 열기와 고정 정책 링크 열기만 제공한다. `AboutTab.Refresh()`가 저장소를 다시 읽고 표시하며 `SelectTab("about-tab")`에서 호출한다. UI 구독과 해제는 기존 탭의 `Dispose()` 패턴을 따르고 `RebuildUI`와 `OnDisable` 모두에서 해제한다.

- [x] Settings는 정책 검토일·사용자가 확인한 등록 상태·명시적 확인 및 저장을 제공한다. 오류/성공은 저장 결과에 따라 표시하고 실패한 값을 저장된 상태로 표시하지 않는다.
- [x] About은 출처(Data Dragon, opt-in CommunityDragon, Riot Match API), 정책 링크와 확인 상태, 비공식 고지, 익명화·로컬 저장 요약, 지표 정의·최소 표본 100·95% Wilson 구간·상대 배치 우위율, 제외 기능을 표시한다. 기존 하단의 과거 관측 고지를 유지한다.
- [x] 재생성·닫기·설정 변경 후 About 재진입에서 저장된 상태를 다시 읽으며, 탭 열기/변경 자체는 파일 저장·네트워크 요청을 시작하지 않는다. 실제 탭 전환으로 재조회와 중복 구독 0을 테스트한다. 자동 갱신 예약 콜백은 추가하지 않는다.
- [x] 500px 창에서 안내와 링크를 스크롤로 읽을 수 있는지, 키·등록 비밀값을 요구하거나 표시하지 않는지 확인한다.

### 4. 회귀 검증과 기록

- [x] 미확인/확인/철회 상태 각각에서 기존 분석의 허용 DTO → 개인 연구용 CSV 저장이 가능하고 억제된 비율 필드가 빈 값임을 확인한다. 등록 설정 때문에 수집/분석 작업 조정자를 변경하지 않는다.
- [x] 승인 후 Unity 검증 전에 [창 검증 절차](../../../../../../docs/unity-editor-validation.md)를 읽는다. 소유 Settings/About 테스트 호스트를 한 짧은 작업에서 생성·검증·finally 정리한다. 정상·예외에서 기존 창/프레임 ID·포커스 동일, 소유 잔류 0을 확인한다. 실제 PNG를 직접 확인한다.
- [x] 컴파일, 신규 집중 테스트, 전체 EditMode 어셈블리를 실행하고 새 Console 오류·보호 파일 해시를 확인한다. 실패/미실행을 통과와 구분한다. 변경 없이 전체 테스트를 반복하지 않는다.
- [x] 독립 코드 검수와 별도 QA 후 변경·사용법·검증 제한을 짧게 기록한다. 제품 코드와 문서를 함께 커밋하고 PR 검수를 요청한다. push 뒤 반복 대기하지 않는다.

## 승인과 다음 단계

Task 5 기획은 2026-10-19 사용자 “워크플로대로 진행해줘”로 승인됐다. Task 4 구현의 PR 병합과 사이클 최종 승인, Task 5 기획 승인, Riot 제품 승인은 각각 별개다. 이 순서로 개발하며 Task 6은 별도 계획으로 남긴다.
