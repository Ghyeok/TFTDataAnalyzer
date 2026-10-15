# Source Profile Identity QA — 2026-10-16

상태: 오프라인 QA 통과. 최종 사용자 완료 승인은 별도이며, 사용자는 다음 구현 진행을 지시했다.
기준: [통합 실행 계획](ImplementationPlan.md) D1–D6와 `3715893` 구현.

## 실제 실행 결과

| 확인 | 결과 |
|---|---|
| `unity pipeline list --format json` | 해당 Editor 연결 가능, Pipeline 0.7.0-exp.1 |
| `unity command recompile` 및 `recompile_status` | 오류 없음, `failed=false`, `compilationFailed=false` |
| 기본 EditMode 어셈블리 `TFTAnalyzer.Tests.EditMode` | 기존 82/82 통과. 후속 관측 모델 포함 전체는 114/114 통과 |
| 기존 CatalogIndex/legacy | 실제 AssetDatabase에서 기존 entry 1개 역직렬화, 검증된 legacy identity 해결, catalog TextAsset 로드 성공 |
| 네이티브 인덱스 직렬화 | 별도 임시 asset에 두 버전×두 프로필 4개 저장, `SaveAssetIfDirty`·`ImportAsset` 후 네 identity 유지 |
| 실제 Import 창 | 기본 입력·Import 버튼·Ready 상태 표시를 이미지로 확인. opt-in false·미실행 상태 확인 후 창 닫음 |
| 보호 파일 SHA-256 | 기존 catalog/manifest 및 두 메타, 사용자 volume profile 모두 기존 보호 해시와 동일 |

기본 명령은 구현 계획 개요의 표준 검증 명령을 사용했다. Explicit 네트워크 테스트는 제외했다.

## D1–D6 판정과 범위

- D1–D2: 네 필드 계약, 요청의 파생 opt-in, schema 직렬화, 네 identity 공존을 기존 회귀 테스트와 실제 인덱스 저장으로 확인했다.
- D3: 실제 기존 인덱스와 카탈로그 로드, strict legacy resolver와 보호 바이트 불변을 확인했다.
- D4: 발행·변조·충돌·취소·실패·rollback은 실제 production 파일 처리 코드를 호출하는 오프라인 회귀 테스트로 확인했다. native 확인은 인덱스 직렬화 범위다.
- D5: 실제 창의 기본화면과 기본 false를 관찰했고, identity 확인·동의 전 취소·오류·opt-in 초기화는 presentation/fake transport 회귀로 확인했다.
- D6: 새 실제 Unity 컴파일·전체 테스트 결과를 확보했다. [독립 QA 기록](../raw/QAAgent.md)에서 새 Critical/Important 문제 없음으로 판정했다.

실제 네트워크 import, 네이티브 모달의 Import/Cancel 클릭, production `CatalogIndexWriter.Upsert` 전체 AssetDatabase 발행 실행은 **미실행**이다. 기존 데이터와 사용자 변경을 보존하기 위해 위 오프라인 증거를 결합해 다음 로컬 구현의 선행 QA로 승인했다. 위험은 해당 종단 간 동작의 검증 공백이며, 이를 전체 발행 실행 완료로 해석하지 않는다.

## 근거 보관

로컬 재현 스크립트는 `Library/TftAnalyzerValidation/Phase1Checks.cs`와 `ImportWindowCapture.cs`, 실제 창 이미지는 같은 폴더의 `import-window.png`다. 임시 인덱스 asset과 검증 창은 정리했다. 실행 로그와 이미지는 로컬 임시 자료이며 Git에는 포함하지 않는다. 과거 개발 보고서는 변경하지 않고 이력을 보존한다.
