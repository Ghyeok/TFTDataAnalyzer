# Source Profile Identity Development TODO

사이클: `2026-09-23-source-profile-identity`
상태: 개발 구현·코드 검수 완료 — native Unity 검증 게이트 미충족
개발 시작 조건: 충족 (`PlanningReview.md` Master 자동 승인)

## 완료 조건 교차 확인표

| ID | 개발 TODO | 객관적 완료 조건 | 개발 검수 조건 | QA 필수 증거 |
|---|---|---|---|---|
| D1 | 카탈로그 identity와 프로필 계약 | 두 프로필이 요청·manifest 직렬화에서 왕복하고 알 수 없는 값은 거부됨 | 프로필과 CommunityDragon 사용 여부가 단일 계약에서 파생되는지 확인 | 집중 테스트 결과와 직렬화 예시 |
| D2 | 경로·raw cache·인덱스 복합 키 | 같은 set/patch/version의 두 프로필과 같은 set/patch의 두 버전이 별도 경로·인덱스 항목으로 공존 | `setId+patchId`만으로 제거하는 코드가 남지 않음 | 4개 identity 공존 테스트와 실제 경로 목록 |
| D3 | legacy official-only 호환 | 기존 18.2 catalog/manifest/.meta 바이트가 변하지 않고 official-only로 검색·재검증 가능 | 판별 불가능한 legacy를 추측하지 않음 | 작업 전후 SHA-256과 legacy fixture 테스트 |
| D4 | 불변 발행·무결성 강화 | 실제 catalog 변조를 감지하고 같은 identity의 다른 콘텐츠가 기존 파일·인덱스를 바꾸지 않음 | manifest 해시 문자열만 신뢰하지 않음 | 변조·충돌·취소·실패 테스트 결과 |
| D5 | Import UI identity 확인 | 확인 대화상자와 결과 상태가 version/profile/대상 경로를 표시하며 취소 시 네트워크 요청 0건 | CommunityDragon 선택이 매 실행 명시적이며 기본 false | fake transport UI/서비스 테스트와 수동 확인 기록 |
| D6 | 전체 회귀와 계획 정합성 | 컴파일 성공, 기본 EditMode 전체 통과, 네트워크 없는 기본 테스트, 관련 계획의 낡은 경로·Git 문구 교정 | 범위 밖 Phase 2 코드 변경 없음 | 명령, 종료 상태, 테스트 수, git diff 요약 |

## D1. 카탈로그 identity와 프로필 계약

예상 변경 대상:

- `Assets/TFTAnalyzer/Core/Domain/`
- `Assets/TFTAnalyzer/Editor/Ingestion/StaticImportRequest.cs`
- `Assets/TFTAnalyzer/Core/Domain/PatchManifest.cs`
- `Assets/TFTAnalyzer/Core/Serialization/CanonicalCatalogSerializer.cs`
- 관련 Domain/Serialization/Ingestion 테스트

작업 순서:

- [x] 두 프로필의 안정 ID와 경로 slug를 정의하는 실패 테스트 작성
- [x] `CatalogIdentity`가 set, patch, source game version, source profile을 모두 비교하는 실패 테스트 작성
- [x] 알 수 없는 profile 역직렬화를 거부하는 테스트 작성
- [x] 최소 구현 후 집중 테스트 통과
- [x] 기존 schema 1 역직렬화 회귀 확인

완료 기준:

- profile과 `IncludeCommunityDragon`이 서로 독립적으로 불일치할 수 없다.
- 신규 manifest가 identity를 명시한다.
- 정규화 직렬화는 같은 identity와 콘텐츠에 대해 결정적이다.

## D2. 경로, raw cache, 인덱스 복합 키

예상 변경 대상:

- `Assets/TFTAnalyzer/Editor/Persistence/StaticCatalogPaths.cs`
- `Assets/TFTAnalyzer/Editor/Persistence/CatalogIndex.cs`
- `Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs`
- `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs`
- 신규 Persistence 테스트

작업 순서:

- [x] 두 프로필이 별도 최종 경로를 얻는 실패 테스트 작성
- [x] 서로 다른 source game version이 별도 경로를 얻는 실패 테스트 작성
- [x] 인덱스에 두 프로필·두 버전이 함께 유지되는 실패 테스트 작성
- [x] version/profile의 위험한 경로 조각 거부 테스트 작성
- [x] 복합 identity를 사용하는 최소 구현

완료 기준:

- 신규 경로는 `{setId}/{patchId}/{sourceGameVersion}/{sourceProfile}`이다.
- `CatalogIndex.Upsert`는 전체 identity가 같은 항목만 교체한다.
- 인덱스 정렬은 결정적이다.
- raw cache도 입력 identity를 구분하거나, 동일 content-addressed 원본을 안전하게 공유한다는 테스트된 계약을 가진다.

## D3. Legacy official-only 호환

예상 변경 대상:

- `Assets/TFTAnalyzer/Editor/Persistence/StaticCatalogPaths.cs`
- `Assets/TFTAnalyzer/Editor/Persistence/CatalogAssetWriter.cs`
- legacy fixture와 Persistence/Ingestion 테스트

작업 순서:

- [x] 현재 schema 1 official-only 구조를 복제한 fixture 작성
- [x] legacy 검색과 profile 추론 실패 테스트 작성
- [x] 기존 official-only 동일 재실행이 신규 중복 폴더를 만들지 않는 테스트 작성
- [x] legacy 파일 바이트 불변 테스트 작성
- [x] 명시적 legacy resolver 최소 구현

완료 기준:

- 현재 커밋된 18.2 catalog, manifest, `.meta` 파일은 이동·수정되지 않는다.
- CommunityDragon 흔적이 있거나 출처를 판별할 수 없는 legacy는 자동으로 official-only로 간주하지 않는다.
- legacy 인덱스 항목은 복합 identity를 갖되 기존 asset path를 유지한다.

## D4. 불변 발행과 무결성 강화

예상 변경 대상:

- `Assets/TFTAnalyzer/Editor/Persistence/CatalogAssetWriter.cs`
- `Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs`
- `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs`
- 신규 Persistence 테스트

작업 순서:

- [x] manifest는 같지만 catalog bytes가 변조된 경우 실패하는 테스트 작성
- [x] 같은 identity·다른 콘텐츠가 기존 출력과 인덱스를 보존하는 테스트 작성
- [x] 취소와 icon 실패가 staging을 비우는 테스트 작성
- [x] index 갱신 실패 시 성공을 반환하지 않는 테스트 작성
- [x] 실제 catalog hash 확인과 복구 가능한 오류 상태 최소 구현

완료 기준:

- `Idempotent=true`는 실제 catalog bytes가 기대 해시와 일치할 때만 반환된다.
- 모든 실패 경로에서 기존 immutable output이 바뀌지 않는다.
- 부분 발행이 발생하면 오류가 경로와 복구 상태를 식별하지만 원본 응답 body를 노출하지 않는다.

## D5. Import UI identity와 동의 표시

예상 변경 대상:

- `Assets/TFTAnalyzer/Editor/Menu/StaticDataImportMenu.cs`
- 관련 Editor 테스트

작업 순서:

- [x] 확인 전 취소 시 transport 요청 0건 테스트 작성
- [x] confirmation 모델이 profile/version/final path를 포함하는 테스트 작성
- [x] official-only 실행에서 CommunityDragon 호스트 요청 0건 테스트 작성
- [x] 완료·취소·실패 후 CommunityDragon 선택이 기본 false로 돌아가는 테스트 작성
- [x] UI와 상태 문자열 최소 구현

완료 기준:

- 사용자는 다운로드 전에 정확한 identity와 대상 경로를 볼 수 있다.
- 보완 소스는 매 실행 명시적 선택이며 묵시적으로 유지되지 않는다.
- 오류는 identity를 포함하되 응답 body나 비밀값을 포함하지 않는다.

## D6. 회귀 검증과 계획 정합성

예상 변경 대상:

- `Assets/Plan/TFTDataPipeline.md`
- `Assets/Plan/Implementation/TFTDataAnalyzerImplementationPlan.md`
- `Assets/Plan/Implementation/2026-09-17-tft-static-data-pipeline.md`
- `Assets/Plan/Implementation/2026-09-17-tft-match-analytics.md`
- `Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md`

작업 순서:

- [x] 기존 계획의 `{setId}/{patchId}` 경로를 승인된 identity 계약과 일치시킴
- [x] 이미 존재하는 Git 저장소와 Phase 1 완료 상태를 정확히 기록
- [x] Phase 2에 set/catalog selection 계약이 선행 조건임을 기록
- [x] Phase 3 source profile/version 선택과 부정 설명문 검사 예외를 기록
- [ ] Unity 재컴파일과 전체 기본 EditMode 테스트 실행
- [x] 새 코드에 대한 네트워크 없는 테스트 확인
- [x] 기존 18.2 파일 해시와 git diff 확인

완료 기준:

- 코드, 테스트, 기획 문서가 동일한 identity 계약을 사용한다.
- 전체 기본 EditMode suite가 0 failures로 완료된다.
- 실행하지 않은 explicit network test는 `미실행` 또는 `skip`으로 정확히 보고된다.
- `Assets/DefaultVolumeProfile.asset`은 이번 사이클 diff에 포함하지 않는다.

## 개발 완료 게이트

- [ ] D1–D6의 구현 에이전트 체크 완료
- [ ] D1–D6의 읽기 전용 검수 에이전트 승인
- [x] 기존 18.2 파일 바이트 불변 증거
- [ ] compiler clean 증거
- [ ] 전체 기본 EditMode 0 failures 증거
- [x] 두 profile·두 source version 공존 증거
- [x] 실패·취소·변조 회귀 증거
- [x] 범위 밖 변경 없음
- [x] 개발 보고서와 raw 기록 저장

> 2026-09-24 개발 인계: 구현 및 같은 독립 리뷰어의 수정 검수 승인 완료. 보완 .NET 회귀 80/0, Unity 참조 컴파일 0 warnings/errors. 실제 Unity 재컴파일/EditMode/AssetDatabase/UI 검증은 Pipeline 연결 불가로 미완료이며 관련 게이트는 체크하지 않았다. 전체 근거와 Git 커밋 이관은 `DevelopmentReport.md`, raw 기록 참조. QA는 시작하지 않았다.
