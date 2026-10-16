# 정적 데이터 탭 기획 공개 기록

날짜: 2026-10-16. 읽기 전용 검토이며 Unity 실행 증거를 대신하지 않는다.

## 기획 A

요청: `data_plan_scope`, 명시 요청 `gpt-6.1-sol` / `high`. 기존 Task2·워크플로·StaticImportPresentation·pipeline·legacy resolver를 읽고 consent/불변데이터/안전한오류/fullidentity/검증legacy 경계를 검토하도록 했다. 명시적 version-only, 승인 import·optin reset·로컬선택·diff만 범위로 주었다. 파일수정·Unity·Hermes·추가에이전트는 금지했다.

공개 결과: 외부 의존성 차단 없음. 버전확인은 versions.json만 호출, TFT패치 추정 금지. immutable 요청과 resolved path 확인, 모든 종료에서 optin false. 중복작업·입력변경·늦은완료를 차단하고 예외본문은 표시하지 않는다. 전체identity로 목록/선택, invalid 행은 정상행과 분리, legacy는 resolver 검증 없이 official로 취급하지 않는다. 실제 hash/index/identity/provenance 확인 후 읽기만 한다. 두 validated catalog의 방향·counts·field diff를 표시한다. 활성선택 영속화는 미정이므로 창내메모리로 판단 가능. 실제 모델식별은 확인 불가라고 보고했다.

## 기획 B

요청: `data_plan_tech`, 같은 모델/추론을 명시했다. 기존 serializer/index/hashverification/DataDragon version/differ/window를 읽고 가장 작은 service·loader·binding 경계와 취소/rebuild를 제안하도록 했다. 파일수정·Unity·Hermes·추가에이전트 금지.

공개 결과: IDataManagerService 뒤에 HTTP/AssetDatabase를 두고 기존 FetchVersionsAsync와 pipeline RunAsync를 연결한다. StaticCatalogAsset은 없으므로 새 SO를 만들지 않는다. VerifyPublishedHash+기존 Deserialize+index/resolver/path검증으로 로드한다. catalog.json은 Epoch/validation0으로 정규화되어 표시 메타데이터는 standalone manifest가 필요하다. 취소요청만으로 terminal전환 금지, publish 후 성공은 늦은 취소로 변경 금지, recovery3개를 구분한다. binding Dispose/delayCall구독 해제, 재생성시 중복실행을 막는다. 기존 CatalogDiffer는 retrieval timestamp를 이미 무시한다. 영속active복원 제안은 이번 범위에서는 채택하지 않았고 창내메모리만 유지한다. 실제 모델식별·Unity실행은 확인하지 않았다고 보고했다.

## 통합/개발 인계

새 기능 대신 기존 Task2 연결을 구체화했다. 모델·추론 요청은 확인되지만 런타임 식별을 확인했다고 주장하지 않는다. 사용량·전체시간 추정 없음. 구현 담당 `implement_data_manager`에도 `gpt-6.1-sol` / `high`를 요청했다. 단일 brief `Library/TftAnalyzerValidation/data-manager-task-brief.md`를 먼저 읽고 테스트만 작성·반환하도록 했으며, 루트의 실제 RED 뒤 동일 담당이 제품 코드를 구현한다. 루트가 Unity실행·문서·통합·PR을 담당한다.
