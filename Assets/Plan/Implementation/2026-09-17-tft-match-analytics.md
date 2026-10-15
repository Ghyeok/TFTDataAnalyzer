# TFT Anonymous Match Analytics Implementation Plan

> 공통 모델·단계·기록 규칙은 [구현 계획 개요](TFTDataAnalyzerImplementationPlan.md)와 [워크플로](../Workflow/README.md)를 따른다. 아래 체크 항목은 이 단계의 구현·검증 상태를 관리한다.

**Goal:** Collect bounded TFT match history through the official Riot API, immediately remove identity fields in memory, persist only anonymous observations, and calculate policy-safe historical aggregate metrics.

**Architecture:** A key-safe HTTP client streams API responses into an anonymizer; only the anonymized domain model crosses into the local NDJSON store. Deterministic aggregation is rebuilt from anonymous observations and a separate policy layer suppresses forbidden or statistically weak metrics.

**Tech Stack:** Unity 6000.3.8f1, C#, Newtonsoft Json streaming reader, UnityWebRequest, SHA-256, Unity Test Framework 1.6.0.

**Spec:** `Assets/Plan/TFTDataPipeline.md`

## 구현 현황

2026-10-16: Task 1 관측 모델·조합 식별자를 구현했다. 집중 테스트 32/32, 실제 Unity EditMode 전체 114/114가 통과했다. Task 2–7은 미구현이다. 실행 기록과 현재 위치는 [Checkpoint](../Workflow/Cycles/2026-10-16-anonymous-observation-contracts/Checkpoint.md)를 확인한다.

## Global Constraints

- Execute only after Phase 1 and the source-profile identity cycle pass. Every observation and aggregate scope must carry the active `CatalogIdentity` with set ID, TFT patch, Data Dragon source version, and source profile; patch ID alone cannot select or join a catalog.
- Read `RIOT_API_KEY` and `RIOT_TFT_SEED_PUUIDS` from environment memory; never persist or log either value.
- The first implementation collects match IDs only for user-supplied seed PUUIDs; it never crawls other participants' PUUIDs.
- Raw Match API bodies remain in memory streams only and are disposed after anonymization.
- Persist hashed Match IDs, final placement, last round, final units/items/traits/augments, patch, queue, region, and tier bucket only.
- Hide ratio metrics below 100 observations and block policy-forbidden metrics before presentation/export.

## Locked File Structure

```text
Assets/TFTAnalyzer/Core/Analytics/
  {AnonymousMatchObservation,AnonymousParticipant,AnonymousUnit,AnonymousTrait}.cs
  {CompositionFingerprint,MetricScope,AggregateMetric,PairwiseMetric}.cs
  {AnonymizationResult,MatchAnonymizer,CompositionFingerprintBuilder,WilsonInterval,AggregateBuilder,MetricPolicy}.cs
Assets/TFTAnalyzer/Editor/RiotApi/
  {IRiotApiKeyProvider,EnvironmentRiotApiKeyProvider,RiotRouting,RiotApiClient}.cs
  {RateLimitState,RiotApiException,MatchCollectionRequest,MatchCollectionSummary,MatchCollectionService}.cs
Assets/TFTAnalyzer/Editor/Persistence/
  {ObservationStore,ProcessedMatchIndex,AggregateStore}.cs
Assets/TFTAnalyzer/Tests/EditMode/Analytics/
Assets/TFTAnalyzer/Tests/EditMode/RiotApi/
Assets/TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json
```

---

### Task 1: Define Anonymous Observation Contracts

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Analytics/AnonymousMatchObservation.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/AnonymousParticipant.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/AnonymousUnit.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/AnonymousTrait.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/CompositionFingerprint.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/CompositionFingerprintBuilder.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/MetricScope.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Analytics/AnonymousObservationTests.cs`

**Interfaces:**
- Produces: `AnonymousMatchObservation(string matchHash, MetricScope scope, DateTimeOffset playedAt, IReadOnlyList<AnonymousParticipant> participants)`.
- Produces: `MetricScope(CatalogIdentity activeCatalog, string region, string queueId, string tierBucket)`. Persist the full active catalog identity with observations and aggregates; never infer it from a patch-only key.
- Produces: `CompositionFingerprintBuilder.Build(AnonymousParticipant participant)`.

- [x] **Step 1: Write failing constructor and determinism tests**

```csharp
[Test]
public void Fingerprint_IsStableAcrossUnitAndItemOrder()
{
    var a = ParticipantFixture.Create(unitOrder: new[] { "TEST_B", "TEST_A" });
    var b = ParticipantFixture.Create(unitOrder: new[] { "TEST_A", "TEST_B" });
    Assert.That(CompositionFingerprintBuilder.Build(a),
        Is.EqualTo(CompositionFingerprintBuilder.Build(b)));
}
```

- [x] **Step 2: Confirm failure and implement identity-free classes**

No class in `Core/Analytics` may define properties named `Puuid`, `SummonerId`, `RiotId`, `GameName`, or `TagLine`. Constructors reject blank match hashes, invalid placement outside 1–8, and blank scope fields.

The implementation additionally requires a 64-character SHA-256 hexadecimal match hash; raw Match IDs are rejected without including the input in errors. Full match admission and catalog membership checks belong to Task 3.

- [x] **Step 3: Implement a canonical fingerprint**

Build SHA-256 from ordinally sorted unit IDs with tier and sorted item IDs, then sorted active trait IDs and augment IDs. Prefix the canonical string with `fingerprint-v1|` so later schema changes do not collide.

Canonical encoding is compact JSON `[units, activeTraitIds, augmentIds]`, where each unit is `[stableId, tier, sortedItemIds]`, after the prefix. Unit ties sort by tier then item-array JSON. Duplicate units/items remain significant; placement/last round and inactive traits do not affect the composition key.

- [x] **Step 4: Run focused and full suites**

Expected: deterministic fingerprints and all invariant tests pass.

2026-10-16: actual Unity focused 32/32 and full default EditMode 114/114 passed; independent review found no Critical/Important/Minor issues. Implementation checkpoint: `5ffc910`.

- [x] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Core/Analytics Assets/TFTAnalyzer/Tests/EditMode/Analytics
git commit -m "feat: define anonymous TFT match observations"
```

---

### Task 2: Implement API-Key Safety, Routing, and Rate-Limit Handling

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/IRiotApiKeyProvider.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/EnvironmentRiotApiKeyProvider.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/RiotRouting.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/RateLimitState.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/RiotApiException.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/RiotApiClient.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/RiotApi/RiotApiClientTests.cs`

**Interfaces:**
- Produces: `string IRiotApiKeyProvider.GetRequiredKey()`.
- Produces: `Task<Stream> RiotApiClient.GetMatchStreamAsync(string regionalRoute, string matchId, CancellationToken token)`.
- Produces: `Task<IReadOnlyList<string>> GetMatchIdsAsync(string regionalRoute, string seedPuuid, int start, int count, CancellationToken token)`.

- [ ] **Step 1: Write failing secret-redaction and 429 tests**

```csharp
[Test]
public void ExceptionMessage_NeverContainsApiKey()
{
    const string secret = "RGAPI-test-secret";
    var ex = RiotApiFixture.CreateUnauthorized(secret);
    Assert.That(ex.ToString(), Does.Not.Contain(secret));
}
```

Assert a 429 response blocks the route until the parsed `Retry-After` UTC and makes no extra transport call during that interval.

- [ ] **Step 2: Confirm focused failures**

Run with `--filter RiotApiClientTests`.

- [ ] **Step 3: Implement environment-only key retrieval**

`EnvironmentRiotApiKeyProvider` reads `RIOT_API_KEY`, trims it, rejects blank values, and exposes only `IsConfigured` plus `GetRequiredKey`. It has no `ToString` override containing the value.

- [ ] **Step 4: Implement regional routes and endpoints**

Map `KR`, `JP1`, `PH2`, `SG2`, `TH2`, `TW2`, and `VN2` to `asia`; `NA1`, `BR1`, `LA1`, `LA2`, and `OC1` to `americas`; European platforms to `europe`. Use:

```text
https://{region}.api.riotgames.com/tft/match/v1/matches/by-puuid/{puuid}/ids?start={start}&count={count}
https://{region}.api.riotgames.com/tft/match/v1/matches/{matchId}
```

Send the key only in the `X-Riot-Token` header. Restrict `count` to 1–100 and percent-encode path values.

- [ ] **Step 5: Parse rate-limit headers and retry policy**

Track app/method limit headers by regional route. For 429, honor `Retry-After` exactly; for 500/502/503/504, retry at 1, 2, and 4 seconds; do not retry 400/401/403/404. Return response streams without logging bodies.

- [ ] **Step 6: Run focused and full suites, then commit**

```powershell
git add Assets/TFTAnalyzer/Editor/RiotApi Assets/TFTAnalyzer/Tests/EditMode/RiotApi
git commit -m "feat: add key-safe Riot match API client"
```

---

### Task 3: Stream and Anonymize Match DTOs Before Persistence

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Analytics/AnonymizationResult.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/MatchAnonymizer.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/RiotMatchDtos.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Analytics/MatchAnonymizerTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/Fixtures/Matches/fabricated-match-v2.json`

**Interfaces:**
- Produces: `AnonymizationResult MatchAnonymizer.Read(Stream utf8Json, string region, string tierBucket)`.
- Consumes the stream once and never returns a Riot DTO.

- [ ] **Step 1: Add a fabricated identity-bearing match fixture and failing redaction test**

The fixture includes fake `puuid`, `riotIdGameName`, `riotIdTagline`, Match ID, placements, units, items, traits, augments, queue, patch, and timestamp.

```csharp
[Test]
public void Read_RemovesEveryIdentityFieldFromSerializedObservation()
{
    var result = _anonymizer.Read(FixtureLoader.MatchV2(), "asia", "MASTER+");
    var json = JsonConvert.SerializeObject(result.Observation);
    Assert.That(json, Does.Not.Contain("FAKE-PUUID"));
    Assert.That(json, Does.Not.Contain("FakePlayer"));
    Assert.That(json, Does.Not.Contain("KR1_123456"));
}
```

- [ ] **Step 2: Confirm the test fails**

Run with `--filter MatchAnonymizerTests`.

- [ ] **Step 3: Implement streaming parse and immediate projection**

Use `JsonTextReader` over the provided stream. Deserialize into internal Editor-only DTOs, immediately compute `SHA256("match-v1|" + rawMatchId)`, project participants to anonymous records, clear DTO collection references in `finally`, and dispose the reader without closing a caller-owned stream.

- [ ] **Step 4: Validate and quarantine unknown static IDs**

Return an `AnonymizationResult` containing the observation plus unknown stable IDs. Reject matches with missing patch, queue, participant collection, placement, or unit IDs. Never include raw JSON or identity values in errors.

Admission must also validate complete-lobby participant count and placement consistency, retain safe match-version metadata, and verify static IDs against the active catalog. These payload-aware checks are not implied by Task 1 constructors, which only validate individual values and nonempty collections.

- [ ] **Step 5: Run memory-safety and malformed-input tests**

Assert malformed JSON produces a typed code such as `invalid_match_payload`; exception messages contain neither body fragments nor API values. Run full suite.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Core/Analytics Assets/TFTAnalyzer/Editor/RiotApi/RiotMatchDtos.cs Assets/TFTAnalyzer/Tests
git commit -m "feat: anonymize TFT matches before persistence"
```

---

### Task 4: Persist Anonymous Observations and Deduplication Index

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Persistence/ObservationStore.cs`
- Create: `Assets/TFTAnalyzer/Editor/Persistence/ProcessedMatchIndex.cs`
- Create: `Assets/TFTAnalyzer/Editor/Persistence/AnalyzerStorePaths.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Persistence/ObservationStoreTests.cs`

**Interfaces:**
- Produces: `Task<bool> ObservationStore.AppendIfNewAsync(AnonymousMatchObservation observation, CancellationToken token)`.
- Produces: `IAsyncEnumerable<AnonymousMatchObservation> ObservationStore.ReadAllAsync(CancellationToken token)`.
- Stores NDJSON under `Library/TftAnalyzerStore/observations/{scopeHash}.ndjson` and hashed IDs under `processed-match-hashes.json`.

- [ ] **Step 1: Write failing idempotency and leak-scan tests**

Append the same observation twice and assert one line. Recursively scan the temporary store and assert fake PUUID/name/raw Match ID strings are absent.

- [ ] **Step 2: Confirm failure and implement safe paths**

Resolve the store root from `Path.GetFullPath(Path.Combine(projectRoot, "Library", "TftAnalyzerStore"))`; reject any override that resolves inside `Assets`, `Packages`, or `ProjectSettings`.

- [ ] **Step 3: Implement atomic index and NDJSON append**

Lock per store instance, check the hash index, append one canonical JSON line, flush, then atomically replace the index via temporary file. On cancellation before flush, append nothing; on index failure, rebuild hashes from observations at next open.

- [ ] **Step 4: Test recovery and concurrent duplicate attempts**

Run two append tasks for the same hash and assert exactly one success. Corrupt the index and assert rebuilding recovers all hashes. Run full suite.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/Persistence Assets/TFTAnalyzer/Tests/EditMode/Persistence
git commit -m "feat: persist deduplicated anonymous observations"
```

---

### Task 5: Calculate Aggregate and Pairwise Placement Metrics

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Analytics/AggregateMetric.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/PairwiseMetric.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/WilsonInterval.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/AggregateBuilder.cs`
- Create: `Assets/TFTAnalyzer/Editor/Persistence/AggregateStore.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Analytics/AggregateBuilderTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Analytics/WilsonIntervalTests.cs`

**Interfaces:**
- Produces: `AggregateSnapshot AggregateBuilder.Build(IEnumerable<AnonymousMatchObservation> observations)`.
- Produces: count, placement sum, average placement, top-four numerator, first-place numerator, and pairwise higher-placement numerators.
- Produces: `ConfidenceInterval WilsonInterval.Calculate(int successes, int total, double z = 1.959963984540054)`.

- [ ] **Step 1: Write failing known-vector tests**

For 56 successes in 100 observations, assert the 95% Wilson interval rounded to four decimals is approximately `[0.4623, 0.6533]`. Add eight fabricated participants and assert 28 unordered pairwise comparisons.

- [ ] **Step 2: Confirm focused failures**

Run filters `WilsonIntervalTests` and `AggregateBuilderTests`.

- [ ] **Step 3: Implement deterministic aggregate building**

Group by the complete `MetricScope` and `CompositionFingerprint`. Compute pairwise higher-placement counts only within one match and one scope. Sort outputs by scope then fingerprint before serialization.

- [ ] **Step 4: Implement aggregate persistence**

Write one canonical snapshot to `Library/TftAnalyzerStore/aggregates/{schemaVersion}/{scopeHash}.json` using temporary-file replacement. Store numerator/denominator, not only rendered percentages.

- [ ] **Step 5: Run exact-count, empty-input, and rebuild tests**

Expected: rebuilding from the same observations is byte-identical; different patches never merge. Run full suite.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Core/Analytics Assets/TFTAnalyzer/Editor/Persistence/AggregateStore.cs Assets/TFTAnalyzer/Tests/EditMode/Analytics
git commit -m "feat: aggregate historical TFT placement metrics"
```

---

### Task 6: Enforce Metric Policy and Minimum Samples

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Analytics/MetricPolicy.cs`
- Create: `Assets/TFTAnalyzer/Core/Analytics/MetricPresentation.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Analytics/MetricPolicyTests.cs`

**Interfaces:**
- Produces: `MetricPresentation MetricPolicy.Evaluate(AggregateMetric metric, StaticCatalog catalog)`.
- Produces states `Visible`, `InsufficientSample`, and `PolicyBlocked` with reason codes.

- [ ] **Step 1: Write failing policy tests**

```csharp
[TestCase(99, "insufficient_sample")]
[TestCase(100, null)]
public void Evaluate_EnforcesOneHundredSampleFloor(int count, string expectedReason)
{
    var result = _policy.Evaluate(MetricFixture.WithCount(count), CatalogFixture.Safe());
    Assert.That(result.ReasonCode, Is.EqualTo(expectedReason));
}
```

Add fabricated augment metadata tagged `LegendBased` and assert related win-rate metrics are `PolicyBlocked` regardless of sample size.

- [ ] **Step 2: Confirm failure and implement centralized rules**

The minimum is a compile-time floor of 100; user settings may only increase it. Allow average placement with its sample count, but block ratio presentation under the floor. Use neutral copy keys: `historical_observation`, `relative_placement_advantage`, never prediction language.

- [ ] **Step 3: Add an export-safe DTO**

`MetricPresentation` exposes only approved names, scope, counts, rates, and confidence interval; it cannot expose source PUUIDs, raw IDs, or real-time fields.

- [ ] **Step 4: Run focused and full suites, then commit**

```powershell
git add Assets/TFTAnalyzer/Core/Analytics Assets/TFTAnalyzer/Tests/EditMode/Analytics/MetricPolicyTests.cs
git commit -m "feat: enforce TFT analytics policy boundaries"
```

---

### Task 7: Orchestrate Bounded Collection From User-Supplied Seeds

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/MatchCollectionRequest.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/MatchCollectionSummary.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/EnvironmentSeedPuuidProvider.cs`
- Create: `Assets/TFTAnalyzer/Editor/RiotApi/MatchCollectionService.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/RiotApi/MatchCollectionServiceTests.cs`

**Interfaces:**
- Produces: `Task<MatchCollectionSummary> MatchCollectionService.CollectAsync(MatchCollectionRequest request, IProgress<CollectionProgress> progress, CancellationToken token)`.
- Reads comma-separated seed PUUIDs from `RIOT_TFT_SEED_PUUIDS` for the current process only.
- Request includes platform, expected patch, queue, tier bucket, and maximum unique matches capped at 1,000 per run.

- [ ] **Step 1: Write a failing bounded-collection test**

Use two fake seeds returning overlapping Match IDs. Assert each unique match is fetched once, mismatched patch/queue observations are excluded, no participant PUUID becomes a new seed, and the maximum-match cap is honored.

- [ ] **Step 2: Confirm failure and implement the service**

For each supplied seed, request Match IDs in pages of at most 100, de-duplicate IDs in memory, stop at the cap, fetch each stream, anonymize, filter scope, append, and report counts. Progress contains numeric counts and status codes only; never seed or Match ID values.

- [ ] **Step 3: Implement cancellation and safe summaries**

Cancellation stops before the next request, disposes an active stream, retains already committed anonymous observations, and returns no raw identifiers. Summary fields: requested, fetched, stored, duplicate, filtered, failed, cancelled, and retry-after UTC.

- [ ] **Step 4: Run full tests and an explicit API-key absence test**

Default tests use fakes. Add an `[Explicit]` test that only verifies a configured key can call one user-authorized seed; skip with a clear reason when either environment variable is absent.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/RiotApi Assets/TFTAnalyzer/Tests/EditMode/RiotApi
git commit -m "feat: collect bounded anonymous TFT match samples"
```

## Phase 2 Completion Gate

Run the full default EditMode suite. Then scan `Library/TftAnalyzerStore` using fabricated secret values from tests; completion requires zero occurrences of PUUID, Riot ID, API key, or raw Match ID fixtures, byte-stable aggregate rebuilds, policy suppression below 100 samples, and no calls to spectator or live-game endpoints.
