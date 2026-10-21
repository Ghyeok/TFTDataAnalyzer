# 수집 격리 보고서 연결 구현 계획

상태: 기획 A(Antigravity)/B(GPT) pass·필수0 후 사용자 “이 계획대로 구현 진행 (권장)” 명시 승인, 구현 중. 기준 main3484b13. 이전 실제 증강 분류 등록안은 근거 부족으로 반려 상태를 유지하며, 이 계획은 분류 허용을 추가하지 않는다.

**Goal:** 수집 중 알 수 없는 정적 ID로 제외한 경기의 안전한 진단을 로컬 보고서에 남기고 저장 실패·상한을 사용자에게 알린다.
**Architecture:** 기존 익명화기가 만든 종류+SHA256만 실행별로 합친다. 종료 시 한 번 원자적으로 기록하고 최종 summary에 저장 결과를 반영한다. 원본 응답·원문 ID·Match ID 해시·PUUID·키·참가자 단위 연결은 보고서에 포함하지 않는다.
**Tech Stack:** Unity6000.3.8f1, C#, 기존 Newtonsoft/AnalyzerStorePaths, NUnit, UI Toolkit.
**Spec:** Assets/Plan/TFTDataPipeline.md §10.2의 격리 보고서, Phase2 Task3의 UnknownStaticId 안전 해시 계약. root 실행(executing-plans), 기존 코드검수/QA 담당 재사용, 재위임/새 역할 생성 없음.

## 현재 결함과 범위

MatchAnonymizer.Read는 UnknownStaticIds를 포함한 AnonymizationResult를 반환하지만 MatchCollectionService.CollectOne은 !IsAccepted에서 Filtered만 늘려 해당 정보를 버린다. 현재 경기 수집 경로에는 저장 보고서가 없으므로 격리 원인을 나중에 대조할 수 없다. 새 보고서는 ID 이름 자동 복구나 카탈로그 수정·통계 승인 기능이 아니다.

- JSON schema1은 schemaVersion, requestContextHash, completedUtc, quarantinedMatches, truncated, entries[{kind,idHash}]만 허용한다. kind는 unit/item/trait/augment, idHash는 기존 검증된 소문자64hex. context는 전체 CatalogIdentity+catalog contentHash+요청region/queue/tier를 구조화하여 SHA256한 값이며 실제 수락된 경기 scope라는 뜻은 아니다.
- 원문 응답/키/seed/Match ID/Match hash/경기별 연결/유닛·증강 원문 이름은 저장·로그·협업 전달하지 않는다. 내보내기·네트워크·새 seed 탐색도 없다. 요약에 해시 목록을 넣지 않는다.
- 실행별 종류+해시를 중복 제거해 ordinal 정렬한다. 최대4,096개까지 보관한다. 초과는 truncated=true로 표시하고 해당 경기 격리 후 다음 요청 전에 수집을 종료한다. 마지막 처리 경기의 초과 해시는 보관하지 않았음을 명시하고 전체 격리 내역 보존 완료라고 하지 않는다. 이 상한은 root 제안이다.
- 해당 실행의 격리 경기가 하나 이상일 때만 종료 후 저장한다. 성공·취소·API 실패·상한 종료 모두 그전까지의 격리 정보를 기록한다. 격리0 실행은 기존 보고서를 덮어쓰지 않고 현재 UI는 격리0/이번 보고서 없음으로 표시한다. 파일은 해당 요청 context의 ‘마지막 격리 실행’이며 전체 이력 보관소가 아니다.
- 경로: AnalyzerStorePaths.RootDirectory/diagnostics/quarantine/<requestContextHash>.json. 기존 관측/처리index/집계/카탈로그/사용자 파일을 변경하지 않는다. 정상 수집이 승인 관측을 추가하는 기존 계약은 유지한다. 환경 변수 변경·실제 API 호출은 이번 기획 범위 밖이다.
- 기존 store와 같은 reparse-point 검사·정확한 소유 임시파일·CreateNew/Flush(true)/원자 교체/실패 시 기존 파일 보존/finally 임시 정리를 사용한다. 임의 경로/보고서 파일 열기 기능은 추가하지 않는다.
- 보고서 저장 실패는 기존 수집 건수·취소/HTTP 상태를 보존하며 QuarantineReportFailed=true와 일정한 안내만 반환한다. 원래 수집 성공을 숨기거나 추가 API 재시도를 하지 않는다. 정상 저장만 QuarantineReportSaved=true다.

## 파일과 인터페이스

아래 Editor/Tests 경로는 모두 Assets/TFTAnalyzer/를 기준으로 한다. 승인 전에는 제품 파일을 생성·변경하지 않는다.

- 신규 Editor/RiotApi/CollectionQuarantineReport.cs: 내부 sealed report/accumulator. 외부 raw 인수를 받지 않고 검증된 UnknownStaticId만 받는다. Add(IEnumerable<UnknownStaticId>)는 첫4,096쌍만 유지하고 초과 신호 반환. 보고서는 동결 복사본이며 JSON 직렬화는 명시 필드만 쓴다.
- 신규 Editor/Persistence/CollectionQuarantineReportStore.cs: 생성자 AnalyzerStorePaths, 내부 교체 함수 주입(실패 테스트용). internal void Save(CollectionQuarantineReport report). 고정 하위 경로에만 기록하고 오류는 일정한 quarantine_report_save_failed로 치환한다. 외부 serializer 타입 메타데이터·임의속성 역직렬화는 없다.
- AnalyzerStorePaths.cs: internal QuarantineReportPath(string requestContextHash). 64hex 검증 전 파일 접근0. 보고서 하위 경로 검사는 보고서 Save에서 전용으로 수행한다. 공용 EnsureSafe에 추가해 보고서 링크 문제가 정상 관측 저장까지 막게 하지 않는다. ObservationStore에는 internal Paths 읽기 전용 접근자를 추가해 동일 저장소 경로를 사용한다.
- MatchCollectionService.cs: 기본 운영 보고서 저장소를 동일 _store.Paths로 생성하고 테스트에서는 내부 저장 delegate를 주입한다. 기존 CollectAsync 공개 시그니처는 유지한다. 기존 루프를 private CollectCoreAsync로 옮겨 조기 return이 최종 snapshot을 미리 만들지 않도록 한다. 외부 CollectAsync의 try/catch 뒤 finally에서 제한된 보고서 저장/최종 Notify를 수행하고 finally 종료 후 단1회 Snapshot 반환. 수집 종료 후 저장은 취소 토큰으로 건너뛰지 않는다.
- MatchCollectionSummary.cs: 생성자 끝에 선택 매개변수 quarantined=0, quarantineReportSaved=false, quarantineReportFailed=false, quarantineReportTruncated=false와 읽기 전용 속성4개 추가. 기존 호출은 소스 호환. 격리는 Filtered의 부분집합이다. 저장 오류는 HTTP Failed 카운트를 가짜 증가시키지 않고 별도 flag로 표시한다.
- CollectionProgressViewModel.cs와 CollectionTab.cs: 숫자/flag를 복사해 기존 collection-scan-limit Label 뒤에 격리 건수와 ‘보고서 저장됨/저장 실패/상한 도달로 일부 누락’을 표시한다. 진행 중에는 저장 완료를 미리 표시하지 않는다. 파일 경로/해시 목록/버튼은 UI에 추가하지 않는다. 기존 지난 실행 기록·카탈로그·입력 상태를 준비 확인만으로 초기화하지 않는다.
- 테스트: 신규 Persistence/CollectionQuarantineReportStoreTests.cs, 기존 RiotApi/MatchCollectionServiceTests.cs, UI/CollectionViewModelTests.cs와 CollectionTabTests.cs 확장. 새 소유 창 도구 없이 기존 collection visual 도구의 합성 progress 시나리오만 최소 확장한다.

## Review Focus

1. 알 수 없는 ID가 악의적으로 키/PUUID/이름처럼 생겼어도 종류와 기존 해시 외 값이 디스크·summary·로그에 나오지 않는다.
2. 정상/취소/401/403/429/한도 종료의 마지막 격리 정보와 저장 실패 flag가 최종 반환·최종 progress에 반영된다.
3. 4,096 상한 초과의 누락을 숨기지 않고 다음 네트워크 요청 전에 종료한다.
4. 파일/부모 junction, 교체 실패, 임시 정리 오류가 기존 관측/보고서를 손상시키거나 원문 예외를 노출하지 않는다.
5. 격리0·새 실행·카탈로그 변경·준비 확인에서 과거 보고서/결과를 이번 보고서 성공으로 잘못 표시하지 않는다.

## Task1: 안전한 격리 보고서 저장

- [x] 실제 기존 source 누락 RED1F: 가짜 unknown4종에서 운영 수집 보고서 없음. 구현 후 실제JSON에종류+기대SHA만있는것을검증했다.
- [x] 저장소 RED10F→GREEN9P0F1호스트파일symlink제외. 허용필드/정렬/중복/범위/상한/교체실패/소유임시/기존바이트보존과원문marker0을확인했다. diagnostics junction과정상관측경로분리도실제RED/GREEN이다.
- [x] 모델/경로/저장 및 서비스 연결 GREEN, 기존독립검수Task1/최종15소스필수0. 제외된파일symlink는실행통과로표시하지않는다.

## Task2: 수집 종료·요약·화면 연결

- [x] 서비스 RED34개26P8F→GREEN34/34. 취소/429/상한/중복/격리0/저장실패/최종일치/다음요청0과기존회귀확인.
- [x] VM RED53개48P5F→GREEN53/53,실제UXML소유창RED10개9P1F→GREEN10/10. 준비확인/기준변경기록보존·새실행초기화·예외fallback보존확인.
- [x] 전체750개748P0F2호스트symlink제외(77.1초),같은실행Persistence102P1제외/RiotApi94P/UI196P. source15/보호1399변경0·기존9창/native1/포커스동일·Console새error0. 기존도구의940/500px6PNGroot직접확인·표시전후예외정리2회·소유잔류0. 실제API/Chrome조작0.
- [ ] 기존 코드검수/QA 재사용 후 문서·원문·TODO 갱신, 코드/문서 함께 PR. root가최신head검수5분확인·통과시승인범위대로병합. Hermes예약삭제유지. 실제분류자료/API검증/n100은별도미완료다.
