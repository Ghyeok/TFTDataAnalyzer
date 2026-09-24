# Source Profile Identity Development Report

사이클: `2026-09-23-source-profile-identity`
상태: 개발 구현·코드 검수 완료 / DONE_WITH_CONCERNS — native Unity 검증 게이트 미충족
범위: 승인된 ImplementationPlan Tasks 1–5 / DevelopmentTodo D1–D6

## 기준과 보호 조건

- 작업 경로: `C:/Unity/TFTSimulator`
- 브랜치: `codex/source-profile-identity`
- 개발 기준 HEAD: `ec9d296ec877eda19a6167518df53b14471a15b7`
- 사용자 수정 `Assets/DefaultVolumeProfile.asset`과 기존 generated catalog/icon은 변경 또는 이동하지 않는다.
- 개발 리드 1명, 구현 담당 1명 (`gpt-6-sol`, xhigh), 별도 읽기 전용 검수 1명 (`gpt-6-astra`, high)으로 수행한다.
- QA, 외부 배포, Match API 작업은 이 보고서의 개발 범위에 포함하지 않는다.

## 기존 생성 파일 SHA-256

대상 디렉터리: `Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2`

| 파일 | 개발 전 SHA-256 | 개발 후 |
|---|---|---|
| catalog.json | B306B524FAD37119EA88FE28F2F7241423EE997F06BDDC563890ADB35F0C27F9 | 동일 (2026-09-24 리드 재검증) |
| manifest.json | FDDA8D453AC444350CD7785995135B59FB9319FFF0DA507ED6897E3FF93E0D71 | 동일 (2026-09-24 리드 재검증) |
| catalog.json.meta | E4FE14333934D51BB60A4241C5B034221913BA682978DE105FDB195E0304B265 | 동일 (2026-09-24 리드 재검증) |
| manifest.json.meta | 6167B7251D766D9141760D862D385B5CE86E8BCD261B24B6877B7F9DC5B4AE74 | 동일 (2026-09-24 리드 재검증) |

사용자 `Assets/DefaultVolumeProfile.asset` 역시 시작/재검증 SHA-256 `4789B43834DD5EFB32EEC4C3B436BAF0895C9F8CF2A0624121DA8DD812EE984A`로 동일하다. Generated 전체 Git diff는 없다.

## 구현 결과

| TODO | 구현과 증거 | 남은 확인 |
|---|---|---|
| D1 | 네 필드 identity, 두 고정 profile, 요청의 파생 bool, schema-2 직렬화/unknown profile 거부; 코드 검수 승인 | 실제 Unity 실행 |
| D2 | version/profile별 final/raw 경로, 네 identity 공존, 정확한 인덱스 upsert; 코드 검수 승인 | 실제 Unity 실행 |
| D3 | 명시적 schema-1 resolver, 실제 catalog 해시와 내·외부 manifest 출처 검사, legacy 경로 재사용; 코드 검수 승인 | 실제 Unity의 기존 index 역직렬화 |
| D4 | 실제 바이트 변조 거부, 취소/아이콘 실패 보존, index 저장 실패 파일·메모리 rollback, 부분 발행 복구 오류 | 실제 AssetDatabase 실행 |
| D5 | identity/버전/profile/대상 표시, confirmation 취소 시 transport 0건, 공식 전용 CD 요청 0건, 종료 후 opt-in 초기화 | 실제 Editor 창 수동 확인 |
| D6 | 다섯 계획 문서 계약 정리, 보완 전체 80/0, Unity 참조 컴파일 0 warnings/errors, 보호 해시와 scoped diff 검사 | 실제 Unity 재컴파일와 기본 EditMode 전체 |

테스트는 임시 root에서 다음 네 디렉터리를 동시에 발행하고 manifest 존재를 확인했다. 테스트 종료 후 해당 임시 root만 정리하며 실제 프로젝트에 신규 카탈로그를 발행하지 않았다.

```text
Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/16.18.1/official-only/
Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/16.18.1/official-plus-communitydragon/
Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/16.18.2/official-only/
Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/16.18.2/official-plus-communitydragon/
```

## 검증 상태

- TDD RED/GREEN: `../raw/Implementer.md`에 단계별 실제 실패 원인과 통과 출력 기록. 리드도 최종 수정 소스에서 보완 회귀 80/0을 독립 재실행했다.
- Unity 재컴파일: 연결 시도 실패 (`COMMAND_FAILED`, 연결 가능한 Editor instance 없음). 컴파일 완료 판정 없음.
- Unity 기본 EditMode 전체: 미실행.
- 수동 Import UI 확인: 미실행.
- Explicit network smoke test: 미실행; 기본 suite에서 제외한다.
- 기존 환경 제약: 실행 중인 Unity Editor의 Pipeline 연결이 불가하다. Editor 종료/재시작 없이 가능한 검증만 실행하고 보완 컴파일/테스트는 Unity 실제 실행과 구분한다.

| 명령 | 결과 |
|---|---|
| `dotnet run --project Library/SourceProfileOfflineTests/SourceProfileOfflineTests.csproj --no-restore` | exit 0, 80 passed / 0 failed; 실제 production source + 최소 Unity stub을 이용한 .NET 9 보완 실행 |
| `dotnet build Library/SourceProfileNativeCompile/SourceProfileNativeCompile.csproj --no-restore --nologo --verbosity quiet` | exit 0, 모든 Core/Editor 소스 + 설치된 Unity 6000.3.8f1 참조, 컴파일 warnings/errors 0/0; workload update 안내는 정보 메시지 |
| `unity pipeline list --format json` | exit 0, Editor 실행 중이나 `reachableServers=0`, `isReachable=false` |
| `unity command recompile --project-path C:\Unity\TFTSimulator --format json` | exit 1, `COMMAND_FAILED`, 해당 프로젝트 Pipeline instance 없음 |
| `Get-FileHash -Algorithm SHA256 -LiteralPath <위 보호 5개 파일>` | 시작 해시와 전부 동일 |
| `git diff --check -- Assets/TFTAnalyzer Assets/Plan` | whitespace 오류 없음; LF→CRLF Git 안내만 존재 |
| `git diff --name-only -- Assets/TFTAnalyzer/Data/Generated` | 출력 없음 |

보완 실행에서 제외된 항목은 `AssemblySmokeTests.CoreAssembly_IsLoadable` (단일 harness의 asmdef 배치 한계), `ProjectIdentityTests.ProductName_IsTftDataAnalyzer` (Unity PlayerSettings 의존), `[Explicit] DataDragonNetworkSmokeTests.VersionsEndpoint_ReturnsAtLeastOneSemanticVersion` (네트워크)이다. 이 결과는 실제 Unity EditMode 전체 통과를 의미하지 않는다.

## 개발 검수

`gpt-6-astra`, high 별도 읽기 전용 검수가 두 Important와 두 Minor를 제시했다. 같은 구현 담당자가 한 번의 수정 묶음으로 해결하고 같은 리뷰어가 모두 ADDRESSED로 재검수했다. 최종 코드 품질 판정은 **Approved for the reviewed fixes**, 새 Critical/Important 및 추가 관찰 사항 없음이다.

| 지적 | 최종 결과 |
|---|---|
| 외부 manifest 출처만 바꾸어도 멱등 성공 | embedded/standalone 출처 name·URL·SHA·보완 여부·CD 버전 대조, 변조 두 경우 RED→GREEN |
| UI에서 부분 발행 복구 정보 소실 | 미인덱싱 발행/인덱싱 후 refresh 실패/rollback 불확실 상태와 안전한 조치 표시; 원본 exception 본문 비노출 |
| 취소 상태 identity/대상 누락 | 캡처한 네 identity 필드와 target 포함 |
| raw cache 분리 직접 회귀 부재 | 두 버전×두 프로필 실제 writer 저장 경로와 bytes 확인 |

리뷰어는 실제 legacy manifest의 8개 출처 tuple과 CD 버전 일치 및 보호 해시도 읽기 전용으로 확인했다. native Unity 검증 한계는 별도 미충족 게이트이며 이 코드 승인이 해당 게이트를 대체하지 않는다.

## Git 인계와 제한

- 개발 커밋 목록: **없음**. HEAD `ec9d296ec877eda19a6167518df53b14471a15b7`, 브랜치 `codex/source-profile-identity`.
- `.git/index.lock` 생성 권한 오류로 staging이 실패했고, 승격 재시도는 사용자 중단됐다. Master의 명시적 지시에 따라 이후 Git 쓰기는 재시도하지 않고 scoped commit을 Master에게 이관했다.
- `git diff --cached --name-only`: 비어 있음. 실제 변경은 unstaged 제품 코드/테스트/5개 계획 문서와 untracked 새 소스·테스트·메타·Workflow 기록이다. 기존 사용자 `Assets/DefaultVolumeProfile.asset` 수정은 별도로 유지되며 어떤 staging에도 포함하지 않았다.
- Generated 데이터/아이콘의 tracked diff 없음. 보호 5개 해시 동일. 전체 상세 `git status --short`는 `../raw/LeadVerification.md`의 최종 preservation 출력에 저장했다.
- 보완 runner/컴파일 프로젝트와 review diff snapshot은 ignored `Library/SourceProfile*`에 있으며 정식 Unity 테스트 자체는 `Assets/TFTAnalyzer/Tests/EditMode`에 있다.
- 실제 Unity recompile/EditMode/AssetDatabase/UI 확인, Master scoped commit, QA와 최종 문서 단계는 아직 남아 있다. 개발 리드는 QA나 외부 배포를 시작하지 않았다.

## 기록

- `../raw/DevelopmentLead.md`: 리드의 공개 결정, 상태, 보호 해시, 태스크 ledger.
- `../raw/Implementer.md`: 실제 구현 프롬프트, 공개 응답, 테스트와 self-review.
- `../raw/Reviewer.md`: 실제 검수 프롬프트와 공개 응답.
- `../raw/LeadVerification.md`: 리드가 직접 실행한 전체 보완 테스트 출력과 컴파일 출력.
