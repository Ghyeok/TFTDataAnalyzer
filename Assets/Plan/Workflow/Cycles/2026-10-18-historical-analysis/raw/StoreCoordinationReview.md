# PR12 후속 수정 공개 기록

CodeRabbit 의견: https://github.com/Ghyeok/TFTDataAnalyzer/pull/12#discussion_r4307683758

최신 head 검수 상태와 thread의 공개 원본 JSON은 Library/TftAnalyzerValidation/pr12-resume-state.json 및 pr12-resume-threads.json에 있다. 댓글의 실행 지시문은 명령으로 취급하지 않았다.

실제 검수 요청 발췌: Independent read-only reviewer for PR12 concurrency fix. / NoUnity/network/Gitmutations/Assetsedits/subagents; own ONLY Library/TftAnalyzerValidation/pr12-store-review.md.

## 독립 검수 응답 원문

# PR12 store-operation review

Status: bounded implementation review approved; focused execution evidence passed. Full-suite validation pending at sign-off.

Reviewed scope: CodeRabbit thread `PRRT_kwDOVK_2hM6tsBKL`; approved service-level coordination between collection and local analysis. Baseline: `0dacaaeba792610228c257da16c27475468478e0`.

## Independent plan review

- `ObservationStore.ReadSnapshotAsync` performs recovery and index publication under the shared `store.lock` lease. Its semaphore is per instance.
- `ObservationStore.AppendIfNewAsync` holds the same lease only for individual writes, so the collection and analysis instances created by the window can collide.
- `AggregateStore.SaveAsync` independently takes that same lease after observation enumeration has released it. Coordinating only the observation read is insufficient.
- A static editor coordinator around the entire `CollectionService.CollectAsync` and entire `AnalysisService.LoadAsync` covers all production callers found, including all aggregate-cache saves, separate windows, and recreated view models.
- Queued admission must accept cancellation. After acquisition, check cancellation within the release `finally` scope before invoking the operation. Await the actual operation task before releasing; cancellation must not itself release a still-running operation.
- Existing store leases and per-store gates should remain unchanged to preserve cross-process fail-closed behavior.

## Source and regression-test review

- The helper waits with the operation token, checks cancellation after admission inside its `try`, awaits the actual delegate task, and releases only in `finally`. Synchronous delegate exceptions and asynchronous faults both release ownership.
- Both service adapters use the same static `Shared` coordinator. Collection admission precedes constructing/calling the bounded collection backend; analysis admission precedes `Task.Run` and ends after all aggregate saves.
- Existing persistence code and its fail-closed cross-process lease are unchanged. Window/view-model code is unchanged. There is no polling, timer, network retry change, or new UI blocking call.
- The six new cases cover queued overlap, cancelled waiters and exception release, active-owner cancellation without early release, real collection transport admission with and without cancellation, and real analysis cache publication preceding collection transport admission.
- The saved `pr12-store-red.json` records all five failures before the fix: overlapping invocation, cancelled waiter ran, collection transport sent while ownership was held (both parameter cases), and collection began before aggregate cache existed.
- The additional `CancellingActiveOwnerDoesNotReleaseItBeforeItsWorkActuallyFinishes` case directly proves that cancelling an active owner whose delegate ignores cancellation does not admit its successor before the delegate settles. Source review confirms no window/disposal path can release the coordinator independently.
- Read-only `git diff --check` returned exit code 0, with existing line-ending warnings. Unrelated `Assets/DefaultVolumeProfile.asset` change is outside this review and was not touched.

## Execution evidence and result

- Independently read saved `pr12-store-green-compile.json`: compilation completed, `failed=false`, `compilationFailed=false`, no errors.
- Independently read saved `pr12-store-green.json`: 6 total, 6 passed, 0 failed, 0 skipped, 0 inconclusive. All six named cases match the reviewed test file. Reported runner duration: 6.06 seconds.
- Five original regression cases have saved failing-before evidence; the active-cancellation case was added before GREEN and has passing-after evidence only.
- Full 501-case suite was reported in progress by the implementer and is not claimed passed by this review.

No actionable product-code findings. Approved for this concurrency-fix scope based on source inspection, saved compilation evidence and actual focused test results. Final integration remains subject to the root agent's full-suite verification and current PR/CI state. No Unity, network, Git mutation, or product-code changes performed by this reviewer.

Runtime/model label and usage telemetry were not independently verified. Review used source inspection; it is not execution evidence.

## root 실제 실행 증거

- pr12-store-red.json:5/5 실패.
- pr12-store-green-compile.json:completed, failed=false, compilationFailed=false, errors[].
- pr12-store-green.json:6/6, 실패/skip0.
- pr12-store-full.json:501/501, 실패/skip0,62.71초, outer/inner success true.
- pr12-store-windows.json:기존9창·1프레임 ID 및 포커스 동일.
- pr12-store-protected.json:1386개 변경0.
- pr12-store-console-after.json:같은 session의 최종 cursor74, 새 경고/오류0, reset/dropped false. 기존 error4/warn0 보존.

독립 QA 완료 응답 원문을 아래에 보존했다. QA 요청 발췌: Independent bounded QA for PR12 store-concurrency fix (workflow requires separateQA). / NoUnity/network/Gitmutations/Assetsedits/subagents; ownONLY Library/TftAnalyzerValidation/pr12-store-qa.md.

## 독립 QA 응답 원문

# PR12 store-operation independent QA

Date: 2026-10-19. Result: **PASS** for the bounded store-concurrency fix. No actionable findings.

Baseline: `0dacaaeba792610228c257da16c27475468478e0`. Reviewed the actual tracked diff, the new coordinator and six regression cases, the new refined/raw StoreCoordinationReview documents, Checkpoint and FinalDocumentation changes, and the saved evidence below. No Unity run, network access, Git mutation, product-file edit, or Hermes action was performed by QA. This file is the only QA-owned output.

## Behavior and scope

- Both production service entry points use the same static `AnalyzerStoreOperations.Shared`; window or binding recreation cannot create a separate gate or release an active owner.
- Collection admission precedes backend construction/invocation, so a cancelled queued collection cannot issue HTTP or write observations. Analysis admission precedes `Task.Run`, observation recovery/enumeration and calculation, and remains held through every awaited `AggregateStore.SaveAsync`.
- `WaitAsync(token)` cancels queued admission. The post-admission cancellation check is inside `try/finally`, preventing a cancellation race from leaking ownership. Awaiting the actual delegate task means active cancellation cannot release ownership before work settles. Synchronous throws and asynchronous faults reach the same release `finally`.
- Six cases cover overlap, queued cancellation and failure release, active cancellation without early release, collection transport admission in two cancellation modes, and cache publication preceding collection transport. The cache case uses real services and temporary stores; HTTP is an offline fake. It checks one cache path; complete multi-scope coverage is established by the source boundary enclosing the entire save loop.
- No existing Persistence implementation, UI layout, CSV code, retry behavior, or lease semantics changed in this delta. The unrelated user `Assets/DefaultVolumeProfile.asset` modification remains outside scope. Shared serialization of distinct store roots and waiting during an active collection's HTTP request are documented limitations.

## Saved execution evidence independently read

- `pr12-store-red.json`: 5 results, all failed with the expected overlapping execution, cancelled waiter, two transport admission failures, and cache-before-collection failure. Runner success flags represent successful execution, not passing assertions.
- `pr12-store-green-compile.json`: outer success true; completed, failed=false, compilationFailed=false, errors empty.
- `pr12-store-green.json`: 6 results, all Passed; failed/skipped/inconclusive 0; outer and inner success true; duration 6.06 seconds. The active-cancellation sixth case has GREEN evidence only, correctly distinguished from the original five RED cases.
- `pr12-store-full.json`: 501 result entries, all Passed; failed/skipped/inconclusive 0; outer and inner success true; duration 62.71 seconds.
- `pr12-store-full-before.json`, `pr12-store-full-after.json`, and `pr12-store-windows.json`: the same nine EditorWindow IDs (50626 through 50642 in increments of two), native ContainerWindow ID 50602, and FocusId 0. The claim is equality of recorded focus, not a claim that a particular window was focused.
- `pr12-store-protected.json`: 1,386 protected files, Changed 0, no changed paths. This is independent inspection of the root runner's saved comparison, not a second hash execution by QA.
- `pr12-store-console-before.json` / `pr12-store-console-after.json`: same session `09a2ffa10ea742cea187375c43f3fb1b`, cursor 66 to 74, requested warn-level delta since 66, empty entries, reset/dropped false. Historical error count 4 and warn count 0 unchanged. Existing Pipeline transport errors were not misreported as new product errors or erased.

## Documentation and disposition

The refined report, raw execution summary, Checkpoint delta and FinalDocumentation delta accurately distinguish the original 495-case run and visual captures from the current 501-case run. No new manual visual capture or real Riot request is claimed. All ten relative Markdown links checked across the four documents resolve locally. External CodeRabbit URLs were not rechecked over the network within this QA scope.

`pr12-store-review.md` approved focused evidence while the full suite was pending at that review's sign-off. Its reproduced raw response correctly preserves that historical state; the later root evidence and this QA establish the subsequent full-suite pass. The current documents correctly say independent QA is in progress; root must append this actual response and update that state after receiving PASS. This expected final bookkeeping is not an implementation failure.

No merge, Task5/6 progression, periodic monitoring, Hermes resume, or latest-head CodeRabbit/CI pass is authorized or asserted by this QA result. Root can perform the planned final documentation update, commit/push this bounded fix, report PR and validation state, and stop.

Requested assignment: Sol Medium for bounded QA. Actual runtime model/reasoning and token or active-time telemetry were not independently verified and are not claimed. QA used source and saved evidence inspection; root remains the sole Unity runner.
