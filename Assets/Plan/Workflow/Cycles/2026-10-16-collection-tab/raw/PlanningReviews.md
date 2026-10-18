# 독립 기획 검토 기록

날짜: 2026-10-16. 요청 모델: 두 검토 모두 GPT-6.1 Sol / High. 실제 런타임 모델·추론과 사용량은 독립 확인 정보가 없다. 아래는 공개 가능한 전달 요청과 반환 결과다.

## 범위 검토 요청

Read-only independent product/privacy planning review for Phase 3 Task 3 Bounded Collection Tab in C:\Unity\TFTDataAnalyzer. Read AGENTS.md, README.md, Assets/Plan/TFTDataPipeline.md, only Task 3 block and global constraints in Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md, plus relevant current RiotApi and UI code. User explicitly said next task; Task2 PR10 passed review, root will merge after baseline tests. Scope already authorized Task3 only; do not request new approval or expand. Identify concrete completion criteria, privacy/confirmation/cancellation pitfalls, required fake tests. Root handles Unity/tests/git. No file edits, no Unity calls, no subagents. Return compact actionable findings and model identity limitations. Existing data/catalog and user DefaultVolumeProfile.asset must be preserved.

## 범위 검토 최종 반환

Task 3 completion criteria and review findings:

- Collection consumes the **validated active catalog** from Data Manager, retaining full `CatalogIdentity`. Show expected patch, source version/profile, platform/derived regional route, queue, tier label, maximum unique matches, and storage destination in confirmation. Start requires fresh confirmation each run.
- Expose only `ApiKeyConfigured` / `SeedConfigured` credential status. Do not provide secret text inputs, public seed collections, exception objects, raw response properties, or arbitrary exception messages.
- Validate **1–1,000** unique detail attempts, supported platform, queue, tier and selected catalog **before confirmation/service execution**. Queue/tier limits should reuse `MatchCollectionRequest`; tier is a caller label, not verified participant rank.
- Confirmation should explain that only supplied seeds are queried, with no participant expansion; attempts and stored counts differ. ID scanning remains capped at ten pages; completion must distinguish `ScanLimitReached`.
- Capture immutable catalog and request before confirmation. Prevent editing while awaiting confirmation/running/cancelling. Either lock Data Manager selection during collection or preserve and visibly label the captured catalog.
- Track requested/fetched/stored/duplicate/filtered/failed, cancellation, HTTP status, next retry time and scan cap using safe summaries. Preserve final counters on cancellation/failure. Stored counts survive cancellation.
- Existing service **stops on 401/403/429**; it does not resume automatically. Copy should say collection stopped and show the next eligible manual retry time. Reuse `RiotApiClient` / `RateLimitState` across reruns; rebuilding them resets cooldown protection.
- Closing/rebuilding the window must cancel and detach observers. Ignore delayed progress from old runs; restore editable controls on success, partial failure, denied confirmation, exceptions, cancellation and 429.
- Keep Task 4 analysis, aggregate regeneration, compliance/build gating and exports outside this change. Preserve catalog assets and `DefaultVolumeProfile.asset`.

Required offline fake coverage:

1. Caps: `1`, `1000` accepted; `0`, `1001` rejected without invoking service.
2. Missing/broken key or seed readers: safe boolean status, no collection, no fabricated secret in model/UI/exception.
3. Rejected confirmation invokes no collection; approved confirmation contains correct captured route/catalog/filter/cap and seed policy without seed values.
4. Concurrent start/edit attempts rejected; confirmation callback cannot mutate approved conditions.
5. Mid-run cancellation and close/dispose cancel token, preserve committed counters, release busy state and reject stale progress.
6. 401/403 and 429 produce safe actionable copy; 429 displays deadline and promises no automatic retry.
7. General fake exceptions containing key, PUUID, Riot ID and raw Match ID never reach UI/logs.
8. Success, `Failed > 0`, `ScanLimitReached`, cancellation and fault each have accurate terminal text and unlocked inputs.
9. Opening window/tab performs no collection; full identity/source profile reaches the service unchanged.

Read-only review only; no Unity execution, edits or tests performed. Runtime did not expose an independently verifiable model variant/reasoning setting, so I cannot substantiate an Astra/High selection.

## 기술 검토 요청

Read-only independent technical planning review Phase3 Task3 bounded collection UI, C:\Unity\TFTDataAnalyzer. Read AGENTS.md, only Task3 and global constraints in Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md; inspect existing Editor/RiotApi collection APIs, Persistence catalog loader/store, Editor/UI/Data and window + tests. Determine minimal adapter/test seams, catalog binding, cancellation and Editor UI lifetime/threading correctness, concrete fake tests. Task2 PR10 passed review root merges after fresh baseline. Task3 already authorized by user's next-task request. No files edited, Unity calls, git mutation, subagents or implementation. Return compact actionable interface proposal and risks. Preserve data & user's DefaultVolumeProfile.asset. Note explicitly requested model vs actual identity unavailable if not visible.

## 기술 검토 최종 반환

기존 수집·저장 코드는 재사용할 수 있습니다. Task 3에는 작은 서비스 어댑터와 disposable 탭 바인딩을 추가하는 구성이 적절합니다.

- `ICollectionService`: `ApiKeyConfigured`, `SeedConfigured`, `CollectAsync(StaticCatalog, MatchCollectionRequest, IProgress<CollectionProgress>, CancellationToken)`만 제공합니다. VM은 HTTP·환경변수·AssetDatabase·저장 경로를 직접 다루지 않습니다.
- `CollectionService`: 기존 `IRiotApiKeyProvider`, `EnvironmentSeedPuuidProvider`, `MatchCollectionService`, `ObservationStore`를 연결합니다. 설정 확인 중 발생한 임의 예외는 `false`로 처리하고 값은 보관하지 않습니다. **RiotApiClient와 RateLimitState는 창 수명 동안 재사용**하여 재실행으로 quota 기억이 초기화되지 않게 합니다.
- `CollectionViewModel`: `TrySetInput`, `SetActiveCatalog`, `StartAsync(confirm)`, `Cancel`, `Dispose`, `Changed`를 제공합니다. 진행 정보는 `CollectionProgressViewModel`의 숫자·불리언·허용된 상태 코드·UTC 시각만 노출합니다.
- `CollectionTab`: 기존 `DataManagerTab`처럼 이벤트를 연결하고 `EditorApplication.delayCall`로 렌더링하며, 재구성 시 기존 바인딩만 폐기하고 VM은 유지합니다.

카탈로그 연결은 DataManager의 `Rows` 중 `IsSelectable`이고 `Identity.Equals(ActiveIdentity)`인 행의 **검증된 Catalog와 전체 Identity를 함께** 전달하면 됩니다. 기존 `PublishedCatalogLoader`가 legacy resolver·hash·validation을 이미 수행하므로 별도 로더는 필요 없습니다. 예상 패치는 활성 카탈로그에서 파생한 읽기 전용 표시가 가장 작고 안전합니다. 별도 입력을 유지한다면 활성 `PatchId`와 정확히 일치할 때만 실행해야 합니다. 실행 시작 시 catalog/identity/request를 캡처하고 이후 선택 변경이 실행 대상에 영향을 주지 않도록 하세요. 실행 중 바인딩 변경을 거절한다면 종료 후 최신 DataManager 선택으로 다시 동기화해야 합니다.

상태는 `Idle → AwaitingApproval → Collecting → Succeeded/Failed/Cancelled`에 취소 요청 표시를 더하면 충분합니다. 취소는 토큰만 요청하고 작업이 실제 종료될 때까지 busy를 유지합니다. 최종 서비스 summary를 권위 있는 결과로 사용하여 늦은 취소가 완료 결과나 저장 개수를 덮어쓰지 않게 합니다. 확인창에는 route·전체 카탈로그 식별자·queue·tier bucket·상한과 “사용자가 제공한 시드만 조회” 문구를 표시합니다.

주의할 점:

- **429는 현재 자동 대기·재개가 아니라 즉시 중단**하고 `RetryAfterUtc`를 반환합니다. UI는 다음 재시도 가능 UTC와 수동 재시도 안내를 표시해야 합니다. 자동 재개처럼 보이는 문구나 별도 반복 작업은 추가하지 않는 편이 현재 구현과 일치합니다.
- 500/502/503/504는 기존 클라이언트가 최대 네 번 전송합니다. 정확한 재시도 횟수는 summary에 없으므로 가짜 실시간 횟수를 표시하지 마세요.
- tier bucket은 호출자가 지정한 기록용 분류이며 실제 선수 등급을 조회·검증하지 않습니다.
- `new Progress<T>`는 콜백이 큐에 남을 수 있습니다. 실행 ID와 disposed/busy 검사를 두어 종료 후 또는 다음 실행 중 도착한 이전 progress를 무시하고, 최종 summary를 덮어쓰지 않게 해야 합니다.
- Unity transport는 메인 스레드에서 실행해야 합니다. `Task.Run`으로 전체 수집을 감싸지 말고, UI에서 시작하며 Unity 동기화 컨텍스트를 유지하세요. 작업 스레드에서 `EditorApplication.delayCall` 등록을 직접 수행하는 경로도 피해야 합니다.
- `OnDisable`에서 바인딩 해제·예약된 렌더 취소·VM 취소를 수행하고 늦은 완료 알림을 억제합니다.

구체적인 fake 테스트는 다음이면 핵심 위험을 검증합니다.

1. 1/1000 허용, 0/1001 거절; invalid platform/queue/tier 또는 카탈로그 부재 시 확인창과 서비스 호출 없음.
2. 설정 상태와 모든 공개 UI 문자열·예외에 fabricated key/PUUID/Riot ID/Match ID가 없음. provider/service가 비밀 문자열을 담은 임의 예외를 던져도 안전한 고정 문구만 노출.
3. 확인 거절 시 호출 없음; 확인에는 route·patch·queue·상한과 전체 catalog identity 포함.
4. 취소 후 서비스가 미완료인 동안 입력과 Start가 계속 비활성; 종료 후 모든 terminal state에서 다시 활성.
5. 401/403 안내, 429 UTC와 재시도 안내, 일반 실패·부분 저장 결과의 안전한 문구.
6. 실행 A 종료 뒤 늦게 도착한 progress가 실행 B 상태를 변경하지 않음.
7. pending 작업 중 바인딩 재구성·Dispose·창 닫기에서 이전 컨트롤 콜백과 예약 렌더가 작동하지 않음.
8. 실제 UI 이벤트로 입력 잠금·취소·진행 카운터·재구성 시 상태 유지 검증.
9. 어댑터 offline integration 한 건: fake transport → 기존 anonymizer/store → 임시 저장소의 익명 결과와 전체 catalog scope 확인.

읽기 전용 검토만 수행했습니다. 파일·Git·Unity 상태를 변경하지 않았으며 `DefaultVolumeProfile.asset`도 건드리지 않았습니다. 요청된 모델명과 실제 런타임 모델·추론의 일치 여부는 제공된 정보로 확인할 수 없습니다.
