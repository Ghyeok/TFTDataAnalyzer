# 검토된 증강 정책 분류 공급 경로 구현 계획

2026-10-20. 상태: 진행 중. 사용자 기획 승인 후 구현·코드검수·QA·실행 완료, 통합 제출 준비. A/B 독립 검토의 P3 보완2건을 반영했다. base main534cfa50ffbc55500585e874412b11c2246c2fe7(PR15). 사용자가 “이 계획대로 구현 진행 (권장)”을 선택해 본 범위를 승인했다.

**목표:** 검토된 카탈로그별 증강 분류 자료를 명시적 로컬 분석 새로고침 때 읽어 기존 MetricPolicy에 전달한다. 이번 단계는 공급 경로 구축이며 실제 Set18 분류 자료 확보·검토 및 실데이터 비율 표시 완료와 구분한다.
**구조:** 고정 승인 목록→해시로 고정한 JSON→전체 카탈로그/콘텐츠 검증→MetricPolicyMetadata→기존 정책/표시/CSV. 임의 파일 배치나 reviewed=true 같은 자기 선언은 승인 근거로 삼지 않는다. 기본 승인 목록은 비워 두며 실제 자료 추가는 근거와 함께 별도 검수/승인을 거친다.
**기술:** 기존 Unity6000.3.8f1/C#/Newtonsoft.Json/EditMode. 새 패키지·사용자 입력 UI·설정 저장·네트워크는 추가하지 않는다.
**설계:** Assets/Plan/TFTDataPipeline.md §2/4/5, 기존 MetricPolicy의 fail-closed 계약. 읽기 전용 기획 검토 후 사용자 승인을 받고 root가 실행한다.

## 범위·계약

1. Editor/Compliance에 공급 인터페이스/결과와 JSON reader, 승인 목록을 둔다. 파일명은 승인 SHA-256으로 고정해 Assets/TFTAnalyzer/Data/Policy/<sha256>.json만 읽는다. 등록 목록은 코드 검수 대상인 불변 값이며 사용자 편집 파일을 자동 신뢰하지 않는다. 실제 배포 목록은 이번에0개다.
2. 승인 항목은 CatalogIdentity의 setId/setNumber/patchId/sourceGameVersion/sourceProfile, catalog.Manifest.ContentHash, 자료 SHA-256, 검토일/근거 출처를 고정한다. JSON은 필수 필드 schemaVersion(정수1), catalog(객체: setId/patchId/sourceGameVersion/sourceProfile은 문자열, setNumber는 양의 Int32), contentHash(64자리 소문자 SHA-256 문자열), rules(배열: 각 객체는 augmentId 문자열과 kind 문자열)만 가진다. kind는 Unknown/Standard/LegendBased/Legend의 대소문자까지 정확히 일치하는 이름이다. 필수값 누락/null, 잘못된 JSON 타입과 숫자↔문자열 암묵 변환, 주석/후행 쉼표, 최상위 객체 뒤의 추가 값은 거부한다. JSON 전체를 끝까지 소비하며 숫자 필드를 문자열로 해석하지 않는다. hash 검증과 검토 근거의 충분성은 다른 문제이며 해시만으로 사람의 검토나 Riot 승인을 증명했다고 하지 않는다.
3. 미등록 카탈로그는 파일을 열지 않는다. 경로 조상과 파일의 링크/접합점은 기존 AnalyzerStorePaths.RejectReparsePoints로 거부한다. 최대256KiB/깊이8/rules4096, 원시 바이트 해시 후 strict UTF-8/JSON, 중복 속성·알 수 없는 필드/enum·중복 정규화 ID·카탈로그에 없는 ID·identity/contentHash 불일치를 거부한다. 전체 파일 유효성 확인 전 부분 Metadata를 반환하지 않는다. 불완전한 목록은 허용하되 없는 증강은 계속 Unknown이다.
4. 결과는 Missing/Loaded/Invalid와 nullable Metadata인 불변 객체다. Missing/Invalid는 기존 미분류 차단을 유지한다. 취소는 OperationCanceledException으로 전파하고 I/O/파싱 오류는 Invalid로 바꾸되 원문 예외/경로/자료 문자열을 UI·로그에 노출하지 않는다. 읽기·검증 중 기존 카탈로그/관측/집계 파일을 수정하지 않는다.
5. AnalysisService의 기존 생성자 인수/명시적 MetricPolicy 주입은 호환 보존한다. 네 번째 선택적 인수 IAugmentPolicyMetadataProvider provider=null을 추가한다. policy와 provider 모두 없으면 기존 기본 정책을 유지한다. TftDataAnalyzerWindow의 생성 지점은 이미 가진 paths.ProjectRoot로 빈 승인 목록의 ReviewedAugmentPolicyProvider를 만들어 명시적으로 전달한다. 작업자 스레드에서 Unity API나 현재 작업 디렉터리로 프로젝트 경로를 추정하지 않는다. 명시적 policy와 provider를 동시에 주면 구성 오류로 거부한다. 매 LoadAsync에서 선택 identity snapshot에 해당하는 공급 결과를 한 번 얻어 그 실행의 MetricPolicy를 만든다. 카탈로그 전환·취소 시 이전 분류를 재사용하지 않는다. provider Missing/Invalid는 기존 new MetricPolicy()와 같은 차단 동작이다. BuildSnapshot의 순수 가짜 policy 주입 API는 바꾸지 않는다.
6. UI는 기존 행의 정책 사유를 유지한다. 파일 선택기·검토 완료 체크박스·차단 해제 버튼은 만들지 않는다. 일반 Missing/Invalid의 세부 구분은 공급 결과와 테스트/개발 문서에서 확인한다. 기본 목록이 비었으므로 실사용 화면의 비율 차단은 그대로이며 README에 이 한계를 명시한다.

## 한 작업의 파일과 완료 기준

제품4파일: 신규 Editor/Compliance/ReviewedAugmentPolicyProvider.cs(인터페이스/불변 결과·등록값·불변 빈 승인목록·공급), 신규 Editor/Compliance/AugmentPolicyDocumentReader.cs(바이트/strict JSON 검증), 수정 Editor/UI/Analysis/AnalysisService.cs(작업별 주입), 수정 Editor/UI/TftDataAnalyzerWindow.cs(기존 paths.ProjectRoot에서 provider 조립). 소비 인터페이스는 AugmentPolicyLoadResult IAugmentPolicyMetadataProvider.Load(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token)이며 기존 LoadAsync의 소유 Task.Run 안에서 호출한다. Core/Analytics/MetricPolicy.cs·집계/관측 스키마·UI/UXML/USS는 변경하지 않는다.
테스트: 신규 Tests/EditMode/Compliance/ReviewedAugmentPolicyProviderTests.cs, 기존 Tests/EditMode/UI/AnalysisServiceTests.cs. 문서: README.md에 빈 기본목록/실분류자료 미확보/실사용 비율 차단 유지 한계를 명시하고, 구현 계획 색인과 이번 refined/raw/Checkpoint, 이전 사이클의 승인 기록을 코드와 함께 보존한다. 사용자 자산과 기존 Generated는 보존한다.

- [x] RED: 가짜 임시 파일/등록 항목으로 정상 Standard+표본100의 허용,99의 억제,Legend/LegendBased/Unknown 및 누락분류의 차단을 먼저 검증한다. 실카탈로그를 가짜로 Standard 분류하지 않는다.
- [x] RED: 미등록은 I/O0, 필수 필드 누락/null·타입 혼용·주석/후행 쉼표·후행 추가 객체, 잘못된 해시/identity5차원/콘텐츠/중복속성·ID/enum/크기/깊이/링크·부분손상은 Invalid+Metadata null, 취소는 전파, 원문 비밀표식 미표시를 확인한다. 링크 생성 미지원은 실제 제약으로 구분한다.
- [x] 최소 구현→관련 GREEN: 실제 ObservationStore 가짜 관측→provider→서비스→정책 presentation→CSV를 확인한다. 카탈로그A→B 분리/동일 작업 snapshot/명시적 기존 policy 주입 호환/추가 HTTP0·보호 파일 변경0을 확인한다. 표본을 만들려고 비동일 보드를 임의 합산하지 않는다.
- [x] 기존 코드검수·QA 담당 재사용, root만 Unity 실행. 소스 고정 후 전체 EditMode1회. 창 테스트가 필요하면 기존 소유 창 절차/전후 frame·focus를 지키며 수동 PNG 여부를 구분한다.
- [ ] README에 빈 기본목록과 실사용 차단 유지 한계를 확인하고, 사용자 Volume·PR15 최종 승인 문서 변경을 보존해 코드/문서 함께 커밋/PR. 최신 head 검수와 사용자 최종 승인 전 완료 처리하지 않는다. push 후 자동 대기/추가 AI 호출·Hermes 예약은 하지 않는다.

## 실사용 한계와 후속

현재 Set18의 신뢰 가능한 개별 증강 분류 근거는 아직 확보/검수하지 않았다. 이번 합성 정상 자료 테스트는 실제 자료 승인이나 기본 비율 표시의 증거가 아니다. 실제 분류 자료 제작/등록은 다음 별도 데이터 검토 과업이다. 전체 보드가 일치해야 하는 표본 입도, 실제 Match API 검증, KI-001은 이번 작업 밖이다. 최소 표본100·95%Wilson·Legend 차단·지역/큐/티어/identity 분리·429수동재시도·Player빌드전용gate는 유지한다.

## 독립 기획 검토 통합과 승인 요청

동일 입력9600bytes/inputDigest534a92e24dc592c10994fff294b435ec00fa5ae5d3f4748a4890dad576ff002e의 A/B 모두 pass다. A의 README 파일/완료조건 명시(P3), B의 필수필드·타입·문서 전체 소비 거부 테스트 명시(P3)를 이 정본에 반영했다. 고정 inputs는 당시 초안으로 보존하고 유료 재검토는 하지 않았다.

우선순위는 root의 권고였으며 공급경로 완료 뒤에도 실제 자료 등록 전에는 비율 차단이 유지된다는 점까지 포함해 사용자가 승인했다. 그 승인 후 본 범위만 구현했다.

## 실행 기록

root가 직접 TDD/Unity/통합을 맡고 기존 코드검수·QA 담당을 재사용한다. 연결된 Unity 프로젝트를 그대로 검증하기 위해 새 worktree 대신 merged main의 codex/augment-policy-provider 브랜치를 사용한다. 사용자 Volume·이전 최종승인 문서 변경을 보존한다. 실제 root 모델/추론·개별 사용량은 확인하지 못해 추정하지 않는다.

## 현재 완료 증거

공급45/45·서비스7/7, 동결 뒤 전체680개/679P/0F/1호스트권한 제외(89.23초), 코드검수/QA 잔여결함0. 창/native/focus·보호1,389개 유지·새오류0/문서경고1을 구분했다. [개발 보고](DevelopmentReport.md)·[QA](QAReport.md)에 원문 연결과 한계를 보존했다. 마지막 제출 TODO는 로컬 준비를 마쳤으며 커밋/PR 최신 head 검수·사용자 최종 승인은 별도다.
