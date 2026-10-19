# 전체 구현 계획 완료 조건 대응표

2026-10-19. 상태: 진행 중. 아래 테스트 이름은 검증 대상이며, 이번 Task6 최종 코드의 실행 결과가 나오기 전에는 통과 증거가 아니다. 사용자 최종 승인과 외부 Riot 등록 승인은 별도다.

| 조건 | 확인 대상 | 이번 실행 증거 |
|---|---|---|
| Phase1: 네 필드 identity, 원본 버전과 TFT 패치 구분 | CatalogIdentityTests, PublishedCatalogLoaderTests, 실제 E2E identity | 대기 |
| 공식 데이터 우선, CommunityDragon 실행별 opt-in | CatalogMergerTests, CommunityDragonAdapterTests, 실제 가져오기 binding | 대기 |
| 검증 후 게시, 실패 복구, immutable/idempotent 출력 | CatalogValidatorTests, CatalogAssetWriterTests, StaticCatalogPipelineTests, 실제 인덱스 E2E | 대기 |
| 수동 Set18 가져오기 보고서, 원본 캐시 격리 | 실제 파이프라인 fabricated 수동 E2E, 소유 자산/Library 캐시 경로 | 대기 |
| 공개 버전 네트워크 smoke | 유한 취소 기한의 DataDragonNetworkSmokeTests를 별도 명시 실행 | 대기 |
| Phase2: 익명화, 중복, 요청 상한, 원본 식별자 미저장 | MatchAnonymizerTests, MatchCollectionServiceTests, ObservationStoreTests, E2E 전체 파일 검사 | 대기 |
| 패치/지역/큐/티어/기간/분모, byte-stable 집계 | AggregateBuilderTests, AggregateStoreTests, AnalysisServiceTests, E2E 재생성 | 대기 |
| 99/100 억제, Wilson 95%, Legend 차단, CSV | MetricPolicyTests, AnalysisTabTests, AnalysisCsvExporterTests, 별도 합성 화면 | 대기 |
| 실제 Riot API/개인 seed, 금지 실시간 endpoint 미사용 | 기본 테스트 가짜 전송, 제품 표면 검사, 수동 fixture 전송 기록 | 대기; 개인 Riot 요청은 미실행 |
| Phase3: 버전→승인→게시→수집 승인→익명 저장→집계→실제 분석 화면 | 실제 파이프라인 E2E 및 940/500px 소유 창 수동 캡처 | 대기 |
| 시작 확인 24시간/두 필드/유휴/재로드/실패/테스트 배제 | StaticVersionCheckSchedulerTests, StaticVersionCheckBootstrapTests | 대기 |
| 최신 성공 가져오기 기준 안내, 기존 데이터 비변경 | StartupCatalogMetadataTests, UI 안내, 보호 파일 해시 | 대기 |
| 정책 고지와 미확인 Player build 차단 | CompliancePolicyTests, ComplianceUiTests, ComplianceBuildGuardTests | 대기 |
| 최종 컴파일, 전체 기본 suite, 새 Console 오류, 사용자 창 보존 | 최종 코드 고정 후 root 실제 실행과 전후 ID/포커스 | 대기 |
| 독립 코드 검수·QA·문서·최신 PR head | 별도 검수/QA 결과 및 최신 head 검수/미해결/CI 각각 확인 | 대기 |

격리 E2E의 원본 정적 JSON은 소유 `Library/TftAnalyzerValidation/.../cache`에만 기록한다. 기본 제품 경로 `Library/TftAnalyzerCache`의 계약을 변경한 것으로 해석하지 않는다. 두 경기의 관측은 표본100 미만이므로 비율 억제를 검증하고, 표시 비율·구간은 별도 합성 snapshot으로 확인한다.
