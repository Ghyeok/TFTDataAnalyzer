# 정적 가져오기 검증·병합 보고서 구현 계획

상태: 완료. 사용자 “이 계획대로 구현 진행 (권장)” 명시 승인 후 구현·관련 검증 완료, 전체 검증·QA 및 PR20 최신head 검수 통과 후 mainf005879 병합. 구현 계획의 실행 완료이며 사용자 최종 결과 승인은 별도다. 기획 A(Antigravity)/B(GPT) 모두 pass·필수0. 기준main1a900596c6d876ee49299ba220cd4b834bfbb7fb, branch codex/static-import-report. 실제 API 환경 변수 설정과 실데이터 수집은 사용자 요청으로 미룬다. 기획 당시 승인 전 제품 변경0이었으며 현재 승인 범위만 구현한다.

> 구현은 executing-plans로 root가 맡고 기존 코드검수·QA 담당을 같은 사이클에 재사용한다. 새 에이전트 생성·재위임은 하지 않는다.

**Goal:** 데이터 관리 탭에서 최근 정적 가져오기의 검증 오류·경고와 공식/보완 병합 결정을 요청 조건과 함께 읽을 수 있게 한다.
**Architecture:** 기존 StaticImportResult의 Validation과 MergeConflicts를 제한된 불변 표시 snapshot으로 변환한다. 가져오기·게시·병합 알고리즘은 그대로 두고 기존 VM과 UI에 읽기 전용 보고서를 연결한다.
**Tech Stack:** Unity6000.3.8f1, C#, UI Toolkit UXML/USS, NUnit EditMode, 창작 fixture.
**Spec:** [상위 설계](../../../../TFTDataPipeline.md) §7.3·11·12.1. 원본 충돌 양쪽 값과 결정은 이미 MergeConflict에 있지만 현재 DataManagerViewModel이 결과를 화면에 전달하지 않는다.

## 범위와 사용자 흐름

- 기존 데이터 관리 탭에 접힌 ‘최근 가져오기 보고서’를 추가한다. 요청의 전체 카탈로그 식별자와 시작 UTC, 게시 여부/동일 내용 재사용 여부, 결과 유무를 표시한다. 카탈로그를 선택만 한 경우 해당 카탈로그의 과거 보고서가 존재한다고 주장하지 않는다.
- 검증 절: 오류/경고 총수와 Severity·Code·RecordId·Message. Validation=null이면 ‘검증 상세 없음’이며 오류0/검증통과로 바꾸지 않는다.
- 병합 절: OfficialWins(공식 값 유지), SupplementFill(빈 필드 보완)의 건수를 구분하고 RecordType·StableId·FieldName·OfficialValue·SupplementValue·Resolution을 보여준다. 충돌이 없으면0건을 명시하며 보완을 허용하지 않은 실행을 보완 실행처럼 표시하지 않는다.
- 검증 상세와 병합 상세 각각 최대200행을 snapshot에 복사하고20행씩 넘긴다. 전체 건수/보유 건수/일부 상세 누락을 구분한다. 필드 문자열은 최대512자로 표시하며 잘린 경우 명시한다. 정적 ID나 원문 설명은 일반 텍스트로 표시하고 rich text·링크 실행·아이콘 다운로드·외부 명령으로 해석하지 않는다.
- 자동 실행·다운로드·새 API 호출·정책/증강 허용·카탈로그 수정·새 저장 파일·CSV/보고서 내보내기는 추가하지 않는다. 보고서는 이 창의 현재 세션 메모리에만 보유하며 창을 닫으면 사라진다는 한계를 안내한다.

## 기록과 작업 생명주기

- 버전 확인·입력 변경·로컬 목록 갱신·활성 카탈로그 선택·승인 거절·실제 서비스 호출 전 입력/대상 검증 실패는 이전 보고서를 보존한다. 이전 요청의 기록임을 전체 식별자/UTC로 알린다.
- 승인과 두 번째 저장 위치 확인을 통과해 실제 ImportAsync를 시작할 때만 새 요청 기록으로 교체한다. 결과가 오기 전에는 상세 없음/진행 중이며 이전 결과를 새 성공으로 표시하지 않는다.
- 정상 StaticImportResult가 반환되면 Published=false인 검증 거절도 상세를 보존한다. Published=true 이후 로컬 목록 갱신이 실패해도 반환된 보고서는 보존한다. 늦은 취소가 이미 완료한 게시를 취소로 바꾸지 않는 기존 규칙을 유지한다.
- 취소·일반 예외·StaticImportRecoveryException처럼 결과가 없는 종료는 새 요청의 ‘상세 보고서 없음’으로 남긴다. 결과 없이 게시 여부를 미게시로 추정하지 않고 ‘게시 여부 미확인’과 기존 복구 상태 안내를 유지한다. 예외 메시지/stack/inner exception/HTTP 원문을 보고서로 복사하지 않는다. 닫힌 VM에 도착한 결과는 무시한다.
- snapshot은 전체 결과/카탈로그/RawResponses나 가변 리스트를 참조하지 않고 허용된 표시 필드만 복사한다. UI는 현재 보고서가 바뀔 때 페이지를 초기화한다. 페이지 이동이나 재그리기는 수집/가져오기를 실행하지 않는다.

## 변경 파일과 인터페이스

- 생성 `Assets/TFTAnalyzer/Editor/UI/Data/StaticImportReportViewModel.cs`: `FromResult(CatalogIdentity requested, bool includeSupplement, DateTimeOffset startedUtc, StaticImportResult result)` 형태의 표시 snapshot. 결과 없음과0건을 별도로 표현한다. 상세 배열과 counts를 고정하고 표시용 잘림 상태를 제공한다.
- 수정 `DataManagerViewModel.cs`: `LastImportReport` 및 요청 시작 snapshot을 추가하고 위 생명주기에 연결. 필요 시 기본 생성자 호환을 유지한 UTC clock 주입으로 시험 가능하게 한다.
- 수정 `DataManagerTab.cs`, `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml` 및 필요한 USS만: 보고서 요약·두 절·각 이전/다음 버튼·페이지 안내. 구독과 예약 render는 기존 Dispose 계약을 따른다.
- 생성/수정 시험 `StaticImportReportViewModelTests.cs`, `DataManagerViewModelTests.cs`, `DataManagerTabTests.cs` 및 필요한 기존 fixture. production pipeline/source/serialization/보호 카탈로그는 수정하지 않는다.
- 생성 검증 도구 `AgentScripts/TftAnalyzerImportReportVisualValidation.cs`, `AgentScripts/Invoke-TftAnalyzerImportReportVisualValidation.ps1`: 기존 소유 창 검증 패턴을 사용하며 UI를 가짜 서비스로만 구동한다. 별도 창을 열기 전에 docs/unity-editor-validation.md를 읽고 기존 instance/native IDs·포커스를 기록한다. 테스트 소유 창과 콜백만 finally에서 정리하고 실제 PNG와 창 잔류를 각각 확인한다.

## Review Focus

1. 검증 거절의 상세와 정상 게시 후 목록 복구 오류가 기존 성공/실패 의미를 바꾸지 않는가.
2. 버전 확인·승인 거절과 실제 실행 취소를 구분하며 이전 보고서를 새 요청 결과로 오인하지 않는가.
3. 결과 없음/검증 정보 없음/0건/상세 잘림을 서로 구분하는가.
4. 원문에 UI 태그·매우 긴 문자열이 있어도 일반 텍스트/상한을 지키며 외부 동작을 일으키지 않는가.
5. 사용자 카탈로그·관측·파일·Chrome·기존 Unity 창을 보존하고 API나 실제 네트워크 없이 검증 가능한가.

## Task1: 제한된 표시 snapshot과 VM 연결

- [x] 실제 운영 VM에 가짜 StaticImportResult를 전달해 검증 오류/병합 결정이 사라지는 현재 RED를 확인한다. 컴파일 실패는 기능 RED로 세지 않는다.
- [x] 결과의 오류/경고·공식유지/보완 건수, null 결과/Validation, 복사 불변성,200행/512자 상한과 누락 안내를 시험하고 최소 구현으로 GREEN을 확인한다.
- [x] VM 정상/검증거절/동일 재사용/목록 로드 실패, 사전 거절·버전 확인·입력 변경 보존, 승인된 새 실행·취소·예외·닫힘 결과 무시를 회귀 검증한다. production 저장·네트워크 경로 변경0.
- [x] 기존 코드검수 담당이 해당 diff와 실제 결과를 검토한다.

## Task2: 보고서 UI·가시성·QA

- [x] 실제 UXML의 보고서·페이지 이동·텍스트 태그 비해석·0건/결과없음/누락 상태 시험에서 RED→GREEN을 확인한다. Dispose 이후 콜백·재요청0과 기존 데이터 관리 기능을 회귀 검증한다.
- [x] 합성 검증거절 및 병합결정 보고서를940/500px에서 캡처해4PNG를 직접 확인한다. 표시 전/후 예외 정리도 검증하며 소유 창·빈 프레임0/기존 창·포커스 보존을 확인한다. 새 검증 도구의 소유 HWND가 하나로 확정될 때만 가시성을 바꾼다.
- [x] 관련 UI 회귀 후 소스 동결→전체 EditMode1회→기존 QA 담당의 실행/실제PNG/보호 파일 대조. 새로운 수정이나 실패가 없으면 전체 테스트/AI 검수를 반복하지 않는다.
- [ ] 새 소스·폴더 meta, 로컬 문서 링크/상태, 비밀값 비포함을 확인하고 코드·문서를 함께 커밋/PR한다. 최신head CodeRabbit/미해결/CI를5분마다 직접 확인하고 통과하면 기존 승인대로 병합한다. 사용자 최종 결과 승인은 별도다.

## 제외·진행 경계

실제 수집 키 설정/Match API/n100 실사용/증강 분류 근거 확보는 이번 완료로 대체하지 않는다. KI-001 원인 조사나 같은 자료의 AI 재검토를 재개하지 않는다. 사용자 `Assets/DefaultVolumeProfile.asset` 및 `docs/riot-api-key.md`는 보존·커밋 제외하고 키 문서는 읽지 않는다. Chrome 창/탭·Windows 로그인·Unity 사용자 창을 닫거나 재시작하지 않는다.
