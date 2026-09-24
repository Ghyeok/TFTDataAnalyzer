# Source Profile Identity Planning Review

사이클: `2026-09-23-source-profile-identity`
단계: 기획
상태: 승인됨 — 2026-09-23 Master 자동 승인
검토자: 기획 A, 기획 B, Master AI

## 1. 목표 이해

다음 개발 사이클의 목적은 같은 TFT 세트·패치라도 입력 출처 구성이 다른 정적 카탈로그를 서로 충돌하지 않는 불변 스냅샷으로 식별하고 보관하는 것이다.

이번 기획 승인은 정적 카탈로그 식별과 발행 안정성만 다룬다. Match API, 통계, 전체 Editor UI, 자동 업데이트, 외부 배포를 승인하지 않는다.

## 2. 확인된 현재 상태

- Phase 1 정적 데이터 파이프라인과 공식 전용 Set 18 / 18.2 카탈로그가 `ec9d296`에 커밋돼 있다.
- 기존 manifest는 Data Dragon 소스 8개, `communityDragonVersion` 빈 값, 콘텐츠 해시 `b306b524fad37119ea88fe28f2f7241423ee997f06bddc563890adb35f0c27f9`를 가진다.
- `StaticImportRequest.IncludeCommunityDragon`은 결과 내용을 바꾸지만, 최종 경로와 인덱스 키는 `setId + patchId`만 사용한다.
- CommunityDragon 포함 재수집은 기존 공식 전용 출력과 다른 콘텐츠를 같은 경로에 발행하려다 불변 보호 장치에 의해 차단됐다.
- 기존 출력은 손상되지 않았고 staging은 정리됐다.
- 작업 트리에는 이번 사이클과 무관한 `Assets/DefaultVolumeProfile.asset` 수정이 있으므로 건드리지 않는다.

## 3. 두 기획 검토의 교차 확인

### 공통 결론

- 소스 프로필은 요청뿐 아니라 manifest, 저장 경로, 인덱스, UI 선택 계약 전체에 포함돼야 한다.
- 경로만 분리하고 인덱스 키를 그대로 두면 한 프로필이 다른 프로필을 제거하므로 불완전하다.
- 기존 `Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2`는 이동하거나 다시 쓰지 않아야 한다.
- Phase 2는 정적 카탈로그 선택 계약이 안정된 후에 시작해야 한다.
- TODO별 테스트와 QA 증거가 필요하며 미실행 검증을 통과로 표시하면 안 된다.

### 의견 차이와 Master 판단

기획 A는 이번 사이클을 소스 프로필 분리에 집중하고 동일 패치 수정 빌드의 revision 보관은 미루는 안을 권했다. 기획 B는 현재 UI와 `PatchKey`가 Data Dragon 소스 버전을 이미 노출하므로 소스 버전도 식별자에 포함해야 다시 충돌하지 않는다고 지적했다.

Master AI 권고는 다음 복합 식별자다.

```text
CatalogIdentity = setId + patchId + sourceGameVersion + sourceProfile
```

이는 임의 revision이나 콘텐츠 해시 디렉터리를 새로 도입하지 않는다. 사용자가 이미 선택하는 `sourceGameVersion`을 정식 식별 차원으로 승격하는 범위다.

## 4. 검토한 접근 방식

### 접근 A — 복합 카탈로그 식별자 도입

```text
{setId}/{patchId}/{sourceGameVersion}/{sourceProfile}
```

권고안이다. 공식 전용과 CommunityDragon 보완판뿐 아니라 동일 TFT 패치의 서로 다른 Data Dragon 버전도 구분한다. 요청, manifest, 경로, 인덱스가 하나의 식별 계약을 공유해야 한다.

기존 `{setId}/{patchId}` 출력은 legacy 공식 전용 별칭으로 읽기만 하고 파일을 이동하거나 수정하지 않는다.

### 접근 B — 소스 프로필만 경로에 추가

변경량은 작지만 동일 TFT 패치에 다른 Data Dragon 버전을 선택하면 다시 충돌한다. 현재 Editor UI가 버전을 직접 입력받는 계약과 맞지 않아 채택하지 않는다.

### 접근 C — 콘텐츠 해시별 디렉터리

모든 변형을 보관할 수 있지만 사람이 선택하기 어려운 revision 모델과 활성 버전 정책이 추가된다. 현재 문제 해결 범위를 넘어가므로 채택하지 않는다.

## 5. 권고 설계

### 5.1 소스 프로필

초기 프로필은 두 개만 허용한다.

- `official-only`
- `official-plus-communitydragon`

자유 입력 문자열을 받지 않고 내부의 제한된 값에서 안전한 slug를 생성한다. CommunityDragon 사용 여부와 프로필이 서로 다른 두 상태로 존재하지 않게 하나의 진실 공급원으로 통합한다.

### 5.2 신규 출력 경로

신규 카탈로그는 다음 구조를 사용한다.

```text
Assets/TFTAnalyzer/Data/Generated/
  {setId}/
    {patchId}/
      {sourceGameVersion}/
        {sourceProfile}/
          catalog.json
          manifest.json
```

버전과 프로필을 포함한 모든 경로 조각에 기존 안전성 검사를 적용한다.

### 5.3 Legacy 호환

- 현재 `TFTSet18/18.2/catalog.json`과 `manifest.json` 및 `.meta` 파일은 바이트 단위로 보존한다.
- schema 1이고 CommunityDragon 흔적이 없는 기존 manifest만 legacy `official-only`로 해석한다.
- 출처를 안전하게 판별할 수 없는 legacy 출력은 추측하지 않고 명시적 오류를 반환한다.
- 기존 official-only 재실행은 legacy 출력을 중복 생성하지 않고 동일성 검사를 수행한다.
- 다른 소스 버전이나 보완 프로필은 신규 표준 경로에 발행한다.

### 5.4 인덱스

인덱스 고유 키는 전체 `CatalogIdentity`를 사용한다. 같은 세트·패치에 여러 버전과 프로필이 동시에 존재할 수 있어야 한다. 모든 항목은 실제 존재하는 catalog/manifest를 가리켜야 한다.

### 5.5 발행 무결성

- 기존 manifest의 해시 문자열만 신뢰하지 않고 실제 `catalog.json`의 해시도 확인한다.
- 동일 identity·동일 콘텐츠 재실행만 멱등 성공이다.
- 동일 identity·다른 콘텐츠는 기존 파일과 인덱스를 변경하지 않고 실패한다.
- index 갱신 실패 등 부분 발행 상태는 성공으로 보고하지 않고 복구 가능한 오류로 남긴다.
- 전역 content-addressed 아이콘은 기존 정책을 유지한다.

## 6. 이번 사이클 범위

포함:

- 복합 카탈로그 identity
- 두 개의 고정 source profile
- manifest·경로·인덱스·import UI의 identity 일치
- 기존 공식 전용 schema 1 출력의 읽기 호환
- 불변성, 멱등성, 변조 감지, 취소·실패 회귀 테스트
- 관련 계획 문서의 경로와 단계 상태 교정

제외:

- Match API와 API 키
- 익명화와 통계
- 전체 UI Toolkit 화면
- 자동 패치 확인과 자동 다운로드
- 기존 생성 데이터 삭제·이동·덮어쓰기
- 콘텐츠 해시 기반 revision 브라우저
- 필드별 provenance 모델 확장
- 외부 배포 또는 Riot 제품 등록 상태 변경
- 별도 전투 기술 연구 프로젝트

## 7. 후속 기획 백로그

다음 항목은 중요하지만 이번 사이클과 분리한다.

- 필드별 CommunityDragon 보완 출처의 영구 저장
- publication/index 트랜잭션의 별도 복구 명령
- Phase 2 `MetricScope`에 set 및 선택 카탈로그 계약 반영
- 티어 기준 시점과 검증 방식
- 동일 로비에 같은 조합이 여러 개 있을 때 상대 배치 우위율 분모 정의
- Phase 3 금지 표현 검사와 부정 설명문 허용 규칙 분리
- 외부 저장소·패키지 배포까지 포함한 compliance gate

## 8. 기획 승인 조건

사용자 승인은 다음을 의미한다.

1. 접근 A의 복합 identity를 채택한다.
2. 기존 18.2 생성 파일은 자동 이동하지 않고 legacy official-only로 읽는다.
3. 이번 사이클은 정적 카탈로그 identity와 무결성으로 제한한다.
4. `DevelopmentTodo.md`의 모든 완료 조건을 개발·검수·QA가 증명해야 한다.
5. Phase 2와 외부 배포는 별도 기획 승인 없이는 시작하지 않는다.

## 9. 승인 기록

사용자가 2026-09-23 외출 중 승인 대기 없이 계속 진행하도록 지시했다. Master AI는 이 지시를 이번 사이클의 단계 승인 위임으로 해석하고 위 권고안을 승인했다. 이 자동 승인은 외부 배포, 데이터 삭제, Match API 또는 비밀값 취급 권한으로 확대되지 않는다.
