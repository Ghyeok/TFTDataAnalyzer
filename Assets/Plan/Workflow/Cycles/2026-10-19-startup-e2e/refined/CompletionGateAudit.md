# 전체 구현 계획 완료 조건 대응표

현재 상태(2026-10-20 PR14 비동기 진단 수정): **수동 E2E 간헐 실패의 원인 미확인으로 QA 검증 실패·병합 보류**. 두 검수 수정·비동기 진단 경로 제거와 최종620개/619통과/실패0/제외1은 [최신 수정 보고](PR14RevisionReport.md)를 따른다. 아래 이전 실행의 조건부 통과·수치는 당시 이력이며 최신 승인으로 적용하지 않는다.


2026-10-19. 상태: 진행 중. 이번 전체 실행과 수동 확인을 대조했다. 실제 새 EditorWindow 재생성 회귀도 별도 QA 지적 후 추가해1/1과 최종 suite로 확인했으며, 아래 통과를 사이클 완료로 확대하지 않는다. 사용자 최종 승인과 외부 Riot 등록 승인은 별도다.

| 조건 | 확인 대상 | 이번 실행 증거 |
|---|---|---|
| Phase1: 네 필드 identity, 원본 버전과 TFT 패치 구분 | CatalogIdentityTests, PublishedCatalogLoaderTests, 실제 E2E identity | 전체 suite: CatalogIdentity6/6·PublishedCatalogLoader11/11, 실제 E2E4/4 |
| 공식 데이터 우선, CommunityDragon 실행별 opt-in | CatalogMergerTests, CommunityDragonAdapterTests, 실제 가져오기 binding | CatalogMerger·CommunityDragonAdapter 기본 suite 및 실제 승인/opt-in E2E 통과 |
| 검증 후 게시, 실패 복구, immutable/idempotent 출력 | CatalogValidatorTests, CatalogAssetWriterTests, StaticCatalogPipelineTests, 실제 인덱스 E2E | StaticCatalogPipeline20/20·CatalogAssetWriter2/2·인덱스11/11, 실제 재가져오기 바이트 불변 E2E |
| 수동 Set18 가져오기 보고서, 원본 캐시 격리 | 실제 파이프라인 fabricated 수동 E2E, 소유 자산/Library 캐시 경로 | e2e-visual-final.json6 PNG 직접 확인, 주입 Library 캐시/실제 Set18 게시·인덱스 검증 |
| 공개 버전 네트워크 smoke | 유한 취소 기한의 DataDragonNetworkSmokeTests를 별도 명시 실행 | 명시 공개 versions smoke1/1,15초 취소 기한·Match API 호출 없음 |
| Phase2: 익명화, 중복, 요청 상한, 원본 식별자 미저장 | MatchAnonymizerTests, MatchCollectionServiceTests, ObservationStoreTests, E2E 전체 파일 검사 | MatchAnonymizer52/52·ObservationStore35/35·MatchCollectionService26/26, 실제 E2E 전체 파일 검사 |
| 패치/지역/큐/티어/기간/분모, byte-stable 집계 | AggregateBuilderTests, AggregateStoreTests, AnalysisServiceTests, E2E 재생성 | AggregateBuilder18/18·AggregateStore14/14·AnalysisService3/3 및 E2E 재생성 통과 |
| 기본 경로 미분류 증강 PolicyBlocked 유지 | AnalysisService·MetricPolicy 기본 경로, 실제 E2E의 명시적 차단 사유 | 실제 기본 E2E의 unclassified_augment 명시, 합성99/100에서도 기본 PolicyBlocked 확인 |
| 99/100 억제 사유, Wilson 95%, Legend 차단, CSV | 정책을 통과하는 행의 InsufficientSample, 신뢰 분류를 주입한 합성 표시 fixture, MetricPolicyTests·AnalysisTabTests·CSV | MetricPolicy31/31·AnalysisTab7/7·CSV5/5, 별도 신뢰 합성6 PNG에서 n100/Wilson 확인 |
| 실제 Riot API/개인 seed, 금지 실시간 endpoint 미사용 | 기본 테스트 가짜 전송, 제품 표면 검사, 수동 fixture 전송 기록 | 제품 표면18/18·가짜 전송 URI/익명 파일 검사 통과; 개인 Riot 요청 미실행 |
| Phase3: 버전→승인→게시→수집 승인→익명 저장→집계→실제 분석 화면 | 실제 파이프라인 E2E 및 940/500px 소유 창 수동 캡처 | 실제 E2E4/4·수동6 PNG, 창/native 잔류0. 실제 새 창 재생성1/1·추가HTTP0·구독 해제 |
| 시작 확인 24시간/두 필드/유휴/재로드/실패/테스트 배제 | StaticVersionCheckSchedulerTests, StaticVersionCheckBootstrapTests | scheduler20/20·bootstrap6/6,24시간/상태/실패/테스트 배제 단언 |
| 최신 성공 가져오기 기준 안내, 기존 데이터 비변경 | StartupCatalogMetadataTests, UI 안내, 보호 파일 해시 | metadata10/10·실제 import 안내 갱신/해제 및 보호1,386개 변경0. 새 창 재생성1/1 |
| 정책 고지와 미확인 Player build 차단 | CompliancePolicyTests, ComplianceUiTests, ComplianceBuildGuardTests | CompliancePolicy15/15·UI9/9·BuildGuard6/6; 실제 Player 빌드 실행/외부 등록 승인과 구분 |
| 최종 컴파일, 전체 기본 suite, 새 Console 오류, 사용자 창 보존 | 최종 코드 고정 후 root 실제 실행과 전후 ID/포커스 | 최종 컴파일 정상,전체616중615P/0F/호스트 링크1제외. Console 새2건은 Pipeline 통신 오류. 기존 창9/native1/focus 동일 |
| 독립 코드 검수·QA·문서·최신 PR head | 별도 검수/QA 결과 및 최신 head 검수/미해결/CI 각각 확인 | 독립 코드 검수 지적 종결. 새 창 회귀 보완/최종 실행 후 후속 QA 조건부 통과·blocker0. 문서/PR/사용자 최종 승인 남음 |

격리 E2E의 원본 정적 JSON은 소유 `Library/TftAnalyzerValidation/.../cache`에만 기록한다. 기본 제품 경로 `Library/TftAnalyzerCache`의 계약을 변경한 것으로 해석하지 않는다. 두 경기의 관측은 표본100 미만이므로 비율 억제를 검증하고, 표시 비율·구간은 별도 합성 snapshot으로 확인한다.

원본 실행 파일·환경별 제외·도구 오류·PNG 한계는 [개발 보고](DevelopmentReport.md)에 기록했다. 기본 전체 결과는 `Library/TftAnalyzerValidation/task6-final3-suite.json`이며 추가한 새 창 회귀도 포함한다.
