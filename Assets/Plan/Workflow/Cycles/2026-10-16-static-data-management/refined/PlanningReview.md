# 정적 데이터 관리 탭 기획 검토

날짜: 2026-10-16
상태: 진행 중 — 사용자 재개 승인에 따른 기존 Phase 3 Task 2 실행.

| 기능 | 완료·검수 조건 | 필요한 증거 |
|---|---|---|
| 버전 확인 | 명시적 버튼으로 versions.json만 조회, TFT 패치 자동추정 없음 | 실제 adapter + fake transport 요청 |
| 가져오기 | immutable fullidentity·resolved path 확인 후 그 요청만 실행 | 승인/거절·재진입·동시작업 거부 |
| 동의/취소 | CommunityDragon 기본false·모든 import 종료false, 취소 후 종료까지 busy | 상태·cancellation·late completion |
| 오류 | validation 실패 및 게시 후 복구상태 구분, 예외본문 비노출 | 고정코드·오류에 넣은 창작 민감문구 비노출 |
| 로컬 목록 | 전체identity/출처/실제 수집UTC/validation/hash/path 표시 | 실제 loader/standalone metadata |
| 무결성 | canonical 또는 verified legacy만 사용, hash/index/identity/provenance 검증 | 임시파일 변조/legacy 거부·정상 행 보존 |
| 선택/diff | 전체identity로 활성선택, 이전→이후 counts·field세부 | profile/version 공존·timestamp-only 차이0 |
| UI lifecycle | 반복 CreateGUI는 VM 유지·binding 교체, close는 취소·구독해제 | 실제 UI 이벤트·늦은 callback |
| 회귀 | 기존 메뉴·고지·나머지 탭·카탈로그·사용자 변경 보존 | 전체 EditMode·화면·보호 해시 |

기획 A는 동의와 불변 저장·전체identity·검증된 legacy·안전한 오류를 검토했고 범위를 막는 외부 의존성은 없다고 보고했다. 기획 B는 실제 API 연결과 lifetime·정규화 메타데이터 차이를 조사했다. 제안한 구조와 경계는 원래 Task 2의 구체화이며 새 제품 기능을 추가하지 않는다.

## 구현 판단

- VM은 HTTP/AssetDatabase를 직접 호출하지 않는다. 작은 서비스 어댑터와 disposable tab binding, persistence loader로 기존 API를 연결한다. 새 framework나 catalog SO 타입은 만들지 않는다.
- CatalogAssetWriter.VerifyPublishedHash와 CanonicalCatalogSerializer.Deserialize를 재사용한다. sourceProfile이 있는 index entry도 canonical/검증된 legacy 경로인지 별도로 확인한다.
- catalog.json은 정규화돼 시간·검증수가 실제 게시 메타데이터와 다르다. 표시용 수집UTC와 validation은 standalone manifest.json에서 읽는다. 새 domain 변환기를 복제하지 않는다.
- 열린 창에서 활성identity를 기억하고 CreateGUI 재생성에 유지한다. 종료/리로드 뒤 영속선택을 추가하지 않는다. 승인된 요청은 immutable로 고정하며 입력 변경과 중복실행을 차단한다.
- 취소 요청만으로 새 작업을 허용하지 않는다. publish 후 파이프라인이 성공으로 반환한 결과를 늦은 취소로 실패/취소라고 바꾸지 않는다. 게시 후 복구상태3개는 안전한 개별코드를 유지한다.
- 실제 네트워크·native 확인창 클릭은 이번 검증에서 실행하지 않는다. transport/delegate 경계로 실제 연결을 확인하고 미실행 항목을 구분한다.

실행 단계는 원래 [Task 2](../../../../Implementation/2026-09-17-tft-analyzer-editor-ui.md#task-2-implement-the-static-data-management-tab)가 단일 계획이다. 현재 진행은 [체크포인트](../Checkpoint.md)를 따른다.
