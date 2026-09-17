# TFT Static Data Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import Set 18 patch data from Riot Data Dragon with optional CommunityDragon supplementation, then normalize, validate, diff, and publish immutable Unity catalog assets.

**Architecture:** Pure domain and validation code lives in `TFTAnalyzer.Core`; all HTTP, cache, AssetDatabase, and menu integration lives in the Editor-only assembly. Imports stage under `Library/TftAnalyzerCache`, and only a fully validated catalog is promoted into `Assets/TFTAnalyzer/Data/Generated`.

**Tech Stack:** Unity 6000.3.8f1, C#, Newtonsoft Json 3.2.2, UnityWebRequest, Unity Test Framework 1.6.0.

**Spec:** `Assets/Plan/TFTDataPipeline.md`

## Global Constraints

- Preserve immutable patch snapshots; never overwrite an existing `{setId}/{patchId}` output.
- Fetch `ko_KR` and `en_US`; stable IDs, never display names, join localized records.
- Data Dragon wins every non-empty conflict; CommunityDragon only fills missing values and is explicitly enabled per request.
- Keep downloaded source JSON under `Library/TftAnalyzerCache`; never place raw source archives under `Assets`.
- Store URL, ETag, retrieved UTC, SHA-256, policy-review date, and policy URLs in every manifest.
- Tests use fabricated `TEST18_*` fixtures, not copied Riot records or images.

## Locked File Structure

```text
Assets/TFTAnalyzer/
  Core/
    TFTAnalyzer.Core.asmdef
    Domain/{PatchKey,LocalizedText,SourceStamp,PatchManifest}.cs
    Domain/{StaticCatalog,UnitRecord,TraitRecord,ItemRecord,AugmentRecord}.cs
    Serialization/{CanonicalCatalogSerializer,ContentHasher}.cs
    Validation/{ValidationIssue,ValidationReport,CatalogValidator}.cs
    Diff/{CatalogDiff,CatalogDiffer}.cs
  Editor/
    TFTAnalyzer.Editor.asmdef
    Ingestion/{IHttpTransport,HttpResponseData,UnityWebRequestTransport}.cs
    Ingestion/DataDragon/{DataDragonEndpoints,DataDragonDtos,DataDragonAdapter}.cs
    Ingestion/CommunityDragon/{CommunityDragonEndpoints,CommunityDragonDtos,CommunityDragonAdapter}.cs
    Ingestion/{CatalogMerger,StaticImportRequest,StaticImportResult,StaticCatalogPipeline}.cs
    Persistence/{StaticCatalogPaths,CatalogAssetWriter,CatalogIndex}.cs
    Menu/StaticDataImportMenu.cs
  Tests/EditMode/
    TFTAnalyzer.Tests.EditMode.asmdef
    Domain/StaticCatalogTests.cs
    Serialization/CanonicalCatalogSerializerTests.cs
    Validation/CatalogValidatorTests.cs
    Diff/CatalogDifferTests.cs
    Ingestion/{DataDragonAdapterTests,CommunityDragonAdapterTests,CatalogMergerTests,StaticCatalogPipelineTests}.cs
  Tests/Fixtures/Static/{ddragon-en_US,ddragon-ko_KR,cdragon-en_us,cdragon-ko_kr}.json
  Data/Generated/
```

---

### Task 1: Establish Assemblies and Test Harness

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/TFTAnalyzer/Core/TFTAnalyzer.Core.asmdef`
- Create: `Assets/TFTAnalyzer/Editor/TFTAnalyzer.Editor.asmdef`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/TFTAnalyzer.Tests.EditMode.asmdef`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/AssemblySmokeTests.cs`

**Interfaces:**
- Produces: assemblies `TFTAnalyzer.Core`, `TFTAnalyzer.Editor`, and `TFTAnalyzer.Tests.EditMode`.
- Produces: direct package dependency `com.unity.nuget.newtonsoft-json` version `3.2.2`.

- [ ] **Step 1: Add a failing assembly smoke test**

```csharp
using NUnit.Framework;

namespace TFTAnalyzer.Tests
{
    public sealed class AssemblySmokeTests
    {
        [Test]
        public void CoreAssembly_IsLoadable()
        {
            Assert.That(typeof(TFTAnalyzer.Domain.StaticCatalog).Assembly.GetName().Name,
                Is.EqualTo("TFTAnalyzer.Core"));
        }
    }
}
```

- [ ] **Step 2: Run recompile and confirm the test fails**

Run the standard recompile commands from the index, then the full EditMode command. Expected: compile failure because `TFTAnalyzer.Domain.StaticCatalog` does not exist.

- [ ] **Step 3: Add the direct JSON dependency and asmdefs**

Add this manifest entry without changing unrelated dependencies:

```json
"com.unity.nuget.newtonsoft-json": "3.2.2"
```

Use these assembly boundaries:

```json
{
  "name": "TFTAnalyzer.Core",
  "rootNamespace": "TFTAnalyzer",
  "references": ["Unity.Newtonsoft.Json"],
  "autoReferenced": true
}
```

```json
{
  "name": "TFTAnalyzer.Editor",
  "rootNamespace": "TFTAnalyzer.Editor",
  "references": ["TFTAnalyzer.Core", "Unity.Newtonsoft.Json"],
  "includePlatforms": ["Editor"],
  "autoReferenced": true
}
```

```json
{
  "name": "TFTAnalyzer.Tests.EditMode",
  "rootNamespace": "TFTAnalyzer.Tests",
  "references": ["TFTAnalyzer.Core", "TFTAnalyzer.Editor", "Unity.Newtonsoft.Json"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

- [ ] **Step 4: Add the minimal domain type and verify green**

Create `StaticCatalog.cs` with an empty sealed class in `TFTAnalyzer.Domain`, recompile, and run the full EditMode suite. Expected: one passing smoke test.

- [ ] **Step 5: Commit checkpoint after VCS is configured**

```powershell
git add Packages/manifest.json Assets/TFTAnalyzer
git commit -m "chore: establish TFT analyzer assemblies"
```

---

### Task 2: Define the Patch-Versioned Domain Model

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Domain/PatchKey.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/LocalizedText.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/SourceStamp.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/PatchManifest.cs`
- Modify: `Assets/TFTAnalyzer/Core/Domain/StaticCatalog.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/UnitRecord.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/TraitRecord.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/ItemRecord.cs`
- Create: `Assets/TFTAnalyzer/Core/Domain/AugmentRecord.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Domain/StaticCatalogTests.cs`

**Interfaces:**
- Produces: `PatchKey(string setId, int setNumber, string patchId, string sourceGameVersion)`.
- Produces: `LocalizedText(string koKr, string enUs)` with `DisplayValue` preferring Korean.
- Produces: `StaticCatalog` lists keyed by each record's `StableId`.
- Produces: `PatchManifest` source stamps and validation counts.

- [ ] **Step 1: Write failing domain invariants**

```csharp
[Test]
public void LocalizedText_UsesEnglishWhenKoreanIsBlank()
{
    var text = new LocalizedText("", "Test Unit");
    Assert.That(text.DisplayValue, Is.EqualTo("Test Unit"));
}

[Test]
public void PatchKey_RejectsBlankStableParts()
{
    Assert.Throws<ArgumentException>(() => new PatchKey("", 18, "18.2", "16.18.1"));
}
```

- [ ] **Step 2: Run the focused tests and confirm failure**

Run `run_tests` with `--filter StaticCatalogTests`. Expected: compile failure for the missing domain types.

- [ ] **Step 3: Implement immutable constructor-validated model classes**

All lists are exposed as `IReadOnlyList<T>`, numeric effect bags as `IReadOnlyDictionary<string, double>`, and source provenance as `IReadOnlyList<SourceStamp>`. Use explicit constructors and reject blank `StableId` values. The core signatures are:

```csharp
public sealed class StaticCatalog
{
    public PatchManifest Manifest { get; }
    public IReadOnlyList<UnitRecord> Units { get; }
    public IReadOnlyList<TraitRecord> Traits { get; }
    public IReadOnlyList<ItemRecord> Items { get; }
    public IReadOnlyList<AugmentRecord> Augments { get; }
}
```

```csharp
public sealed class UnitRecord
{
    public string StableId { get; }
    public LocalizedText Name { get; }
    public int Cost { get; }
    public IReadOnlyList<string> TraitIds { get; }
    public string IconPath { get; }
    public IReadOnlyList<SourceStamp> Sources { get; }
}
```

Trait, item, and augment records follow the same stable-ID/localization/provenance shape and add only fields required by the approved spec.

- [ ] **Step 4: Test Korean preference, English fallback, null collections, and invalid IDs**

Add explicit assertions for every constructor invariant, run the focused tests, then run the full EditMode suite. Expected: all pass.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Core/Domain Assets/TFTAnalyzer/Tests/EditMode/Domain
git commit -m "feat: define patch-versioned TFT catalog model"
```

---

### Task 3: Add Canonical Serialization and Content Hashing

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Serialization/CanonicalCatalogSerializer.cs`
- Create: `Assets/TFTAnalyzer/Core/Serialization/ContentHasher.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Serialization/CanonicalCatalogSerializerTests.cs`

**Interfaces:**
- Produces: `string CanonicalCatalogSerializer.Serialize(StaticCatalog catalog)`.
- Produces: `StaticCatalog CanonicalCatalogSerializer.Deserialize(string json)`.
- Produces: `string ContentHasher.Sha256Hex(ReadOnlySpan<byte> bytes)` and `Sha256Hex(string utf8Text)`.

- [ ] **Step 1: Write a failing determinism test**

```csharp
[Test]
public void Serialize_SortsRecordsAndEffectKeys()
{
    var first = CatalogFixture.WithUnitOrder("TEST18_B", "TEST18_A");
    var second = CatalogFixture.WithUnitOrder("TEST18_A", "TEST18_B");
    Assert.That(CanonicalCatalogSerializer.Serialize(first),
        Is.EqualTo(CanonicalCatalogSerializer.Serialize(second)));
}
```

- [ ] **Step 2: Confirm the focused test fails**

Run with `--filter CanonicalCatalogSerializerTests`. Expected: missing serializer.

- [ ] **Step 3: Implement canonical projection before JSON serialization**

Project records into DTOs sorted ordinally by `StableId`; sort referenced ID lists and dictionary keys with `StringComparer.Ordinal`. Serialize with invariant culture, UTF-8, indented formatting, and no type metadata. Hash the exact UTF-8 bytes written to disk.

```csharp
private static IEnumerable<T> Stable<T>(IEnumerable<T> values, Func<T, string> key) =>
    values.OrderBy(key, StringComparer.Ordinal);
```

- [ ] **Step 4: Test round-trip and known SHA-256 vector**

Use the standard vector `SHA256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad`. Run focused and full suites; expected: all pass.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Core/Serialization Assets/TFTAnalyzer/Tests/EditMode/Serialization
git commit -m "feat: serialize catalogs deterministically"
```

---

### Task 4: Implement the Riot Data Dragon Adapter

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/IHttpTransport.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/HttpResponseData.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/UnityWebRequestTransport.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/DataDragon/DataDragonEndpoints.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/DataDragon/DataDragonDtos.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/DataDragon/DataDragonAdapter.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/DataDragonAdapterTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/Fixtures/Static/ddragon-en_US.json`
- Create: `Assets/TFTAnalyzer/Tests/Fixtures/Static/ddragon-ko_KR.json`

**Interfaces:**
- Consumes: `PatchKey`, `StaticCatalog`, `SourceStamp`.
- Produces: `Task<DataDragonSnapshot> DataDragonAdapter.FetchAsync(DataDragonRequest request, CancellationToken token)`.
- Produces: `StaticCatalog DataDragonAdapter.Parse(DataDragonSnapshot snapshot, PatchKey patch)`.

- [ ] **Step 1: Add fabricated Data Dragon fixtures and failing mapping tests**

The fixtures use top-level `data` objects and IDs `TEST18_Unit_A`, `TEST18_Trait_A`, `TEST18_Item_A`, and `TEST18_Augment_A`. Assert Korean/English names join by ID and unrelated `TEST17_*` records are excluded.

```csharp
[Test]
public void Parse_JoinsLocalesAndFiltersRequestedSetPrefix()
{
    var catalog = _adapter.Parse(FixtureLoader.DataDragonPair(), PatchFixture.Set18);
    Assert.That(catalog.Units.Single().Name.KoKr, Is.EqualTo("시험 기물"));
    Assert.That(catalog.Units.Single().Name.EnUs, Is.EqualTo("Test Unit"));
    Assert.That(catalog.Units, Has.None.Matches<UnitRecord>(x => x.StableId.StartsWith("TEST17_")));
}
```

- [ ] **Step 2: Confirm the adapter tests fail**

Run with `--filter DataDragonAdapterTests`. Expected: missing adapter and transport contracts.

- [ ] **Step 3: Implement endpoint construction and transport**

Use these official URL templates:

```text
https://ddragon.leagueoflegends.com/api/versions.json
https://ddragon.leagueoflegends.com/cdn/{version}/data/{locale}/tft-champion.json
https://ddragon.leagueoflegends.com/cdn/{version}/data/{locale}/tft-trait.json
https://ddragon.leagueoflegends.com/cdn/{version}/data/{locale}/tft-item.json
https://ddragon.leagueoflegends.com/cdn/{version}/data/{locale}/tft-augments.json
```

`UnityWebRequestTransport` returns status, body bytes, ETag, content type, and retrieval UTC; it throws a typed `HttpTransportException` without including response bodies.

- [ ] **Step 4: Implement official-field mapping**

Map only fields present in Data Dragon: ID, localized name, tier/cost when present, description when present, and image metadata. Do not invent stats or trait links when absent. Record a Riot `SourceStamp` for each mapped record.

- [ ] **Step 5: Test HTTP errors, missing locale entries, and stable filtering**

Use a fake `IHttpTransport`; do not contact the network. Run focused and full suites. Expected: all pass.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/Ingestion Assets/TFTAnalyzer/Tests/EditMode/Ingestion Assets/TFTAnalyzer/Tests/Fixtures/Static
git commit -m "feat: import official TFT Data Dragon records"
```

---

### Task 5: Validate and Diff Catalogs

**Files:**
- Create: `Assets/TFTAnalyzer/Core/Validation/ValidationIssue.cs`
- Create: `Assets/TFTAnalyzer/Core/Validation/ValidationReport.cs`
- Create: `Assets/TFTAnalyzer/Core/Validation/CatalogValidator.cs`
- Create: `Assets/TFTAnalyzer/Core/Diff/CatalogDiff.cs`
- Create: `Assets/TFTAnalyzer/Core/Diff/CatalogDiffer.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Validation/CatalogValidatorTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Diff/CatalogDifferTests.cs`

**Interfaces:**
- Produces: `ValidationReport CatalogValidator.Validate(StaticCatalog catalog)`.
- Produces: `CatalogDiff CatalogDiffer.Compare(StaticCatalog previous, StaticCatalog current)`.
- `ValidationReport.CanPublish` is true only when error count is zero.

- [ ] **Step 1: Write failing duplicate-ID and broken-reference tests**

```csharp
[Test]
public void Validate_BlocksDuplicateIdsAndMissingTraits()
{
    var report = new CatalogValidator().Validate(CatalogFixture.WithDuplicateUnitAndMissingTrait());
    Assert.That(report.CanPublish, Is.False);
    Assert.That(report.Issues.Select(x => x.Code),
        Is.EquivalentTo(new[] { "duplicate_unit_id", "missing_trait_reference" }));
}
```

- [ ] **Step 2: Confirm failure, then implement validation rules**

Validate duplicate IDs per record kind, blank localization in both languages, costs outside 1–9, trait/item relationships, invalid icon metadata, and patch identity. Sort issues by severity, code, and record ID.

- [ ] **Step 3: Implement semantic patch diffing**

Return added, removed, and changed stable IDs per entity type. A change contains field-level before/after values but excludes retrieval timestamps so identical content fetched later does not appear changed.

- [ ] **Step 4: Run focused and full suites**

Expected: invalid catalogs cannot publish; deterministic diffs report exact changed fields.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Core/Validation Assets/TFTAnalyzer/Core/Diff Assets/TFTAnalyzer/Tests/EditMode/Validation Assets/TFTAnalyzer/Tests/EditMode/Diff
git commit -m "feat: validate and diff TFT catalogs"
```

---

### Task 6: Add Opt-In CommunityDragon Supplementation

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon/CommunityDragonEndpoints.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon/CommunityDragonDtos.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon/CommunityDragonAdapter.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/CatalogMerger.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/CommunityDragonAdapterTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/CatalogMergerTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/Fixtures/Static/cdragon-en_us.json`
- Create: `Assets/TFTAnalyzer/Tests/Fixtures/Static/cdragon-ko_kr.json`

**Interfaces:**
- Produces: `StaticCatalog CommunityDragonAdapter.Parse(string koJson, string enJson, PatchKey patch)`.
- Produces: `MergeResult CatalogMerger.Merge(StaticCatalog official, StaticCatalog supplement)`.
- `MergeResult` contains `Catalog` and a deterministic `IReadOnlyList<MergeConflict>`.

- [ ] **Step 1: Write failing Set 18 selection and precedence tests**

Use fabricated `setData` number 18 and 17. Assert set 18 is selected by both set number and stable-ID prefix. Assert an official non-empty name wins while an absent official role/effect is filled from the supplement.

- [ ] **Step 2: Confirm focused failures**

Run with `--filter CommunityDragonAdapterTests`, then `--filter CatalogMergerTests`.

- [ ] **Step 3: Implement patch-pinned CommunityDragon endpoints**

```text
https://raw.communitydragon.org/{major.minor}/cdragon/tft/ko_kr.json
https://raw.communitydragon.org/{major.minor}/cdragon/tft/en_us.json
```

Derive `{major.minor}` deterministically from the Data Dragon source version (`16.18.1` becomes `16.18`) and store both values in the manifest. Never use `/latest/` for an immutable patch import. Refuse a request when `IncludeCommunityDragon` is false before constructing or calling either URL.

- [ ] **Step 4: Implement field-level official-first merging**

Only fill null, blank, or empty official fields. Record every supplement fill and every conflicting non-empty value. Preserve both `SourceStamp` entries on a supplemented record.

- [ ] **Step 5: Test opt-in, conflicts, and deterministic output**

Run focused and full suites. Expected: no CommunityDragon request when disabled; official values always win conflicts.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon Assets/TFTAnalyzer/Editor/Ingestion/CatalogMerger.cs Assets/TFTAnalyzer/Tests
git commit -m "feat: supplement missing TFT fields from CommunityDragon"
```

---

### Task 7: Stage, Publish, and Index Immutable Catalogs

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/StaticImportRequest.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/StaticImportResult.cs`
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs`
- Create: `Assets/TFTAnalyzer/Editor/Persistence/StaticCatalogPaths.cs`
- Create: `Assets/TFTAnalyzer/Editor/Persistence/CatalogAssetWriter.cs`
- Create: `Assets/TFTAnalyzer/Editor/Persistence/CatalogIndex.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs`

**Interfaces:**
- Produces: `Task<StaticImportResult> StaticCatalogPipeline.RunAsync(StaticImportRequest request, CancellationToken token)`.
- Produces immutable output at `Assets/TFTAnalyzer/Data/Generated/{setId}/{patchId}/catalog.json` and `manifest.json`.
- Produces/updates `Assets/TFTAnalyzer/Data/Generated/CatalogIndex.asset` only after validation succeeds.

- [ ] **Step 1: Write a failing atomic-publish test**

```csharp
[Test]
public async Task RunAsync_DoesNotPublishWhenValidationFails()
{
    var result = await PipelineFixture.Invalid().RunAsync(PipelineFixture.Request, CancellationToken.None);
    Assert.That(result.Published, Is.False);
    Assert.That(Directory.Exists(result.FinalDirectory), Is.False);
}
```

- [ ] **Step 2: Confirm failure and implement path policy**

Stage under `Library/TftAnalyzerCache/staging/{guid}`. Reject path segments containing separators or `..`. Refuse to overwrite an existing final patch directory whose manifest content hash differs.

- [ ] **Step 3: Implement the orchestration sequence**

Execute: fetch official → optional supplement → merge → canonicalize → validate → hash → write staged catalog/manifest → atomically move into final directory → refresh AssetDatabase → update `CatalogIndex.asset`.

- [ ] **Step 4: Add image acquisition with content-addressed filenames**

Download only image URLs referenced by normalized records. Validate successful HTTP status and PNG signature, hash bytes, then write `Assets/TFTAnalyzer/Data/Generated/Icons/{sha256}.png`. Reuse an existing identical hash. Record each record-to-hash mapping in the catalog; never use source filenames as identity.

- [ ] **Step 5: Test cancellation and existing-patch behavior**

Assert cancellation removes staging, existing identical hash is idempotent, conflicting immutable output fails, and validation failure leaves the index unchanged. Run full suite.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor Assets/TFTAnalyzer/Tests Assets/TFTAnalyzer/Data
git commit -m "feat: publish immutable TFT patch catalogs"
```

---

### Task 8: Add a Minimal Editor Entry Point and Explicit Network Smoke Test

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Menu/StaticDataImportMenu.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Ingestion/DataDragonNetworkSmokeTests.cs`
- Modify: `ProjectSettings/ProjectSettings.asset`

**Interfaces:**
- Produces menu `TFT Analyzer/Data/Import Set 18 Patch...` that opens a confirmation dialog and runs `StaticCatalogPipeline`.
- Produces `[Explicit]` network test that fetches only `versions.json` and verifies a non-empty semantic version.

- [ ] **Step 1: Add a failing project identity test**

```csharp
[Test]
public void ProductName_IsTftDataAnalyzer()
{
    Assert.That(UnityEditor.PlayerSettings.productName, Is.EqualTo("TFT Data Analyzer"));
}
```

- [ ] **Step 2: Change only `productName` and add the menu command**

The dialog requires Set ID, set number, TFT patch ID, Data Dragon source version, and a CommunityDragon opt-in checkbox defaulted off. Disable the action while an import runs and expose cancellation.

- [ ] **Step 3: Add the explicit versions endpoint smoke test**

```csharp
[Test, Explicit("Uses the public Riot Data Dragon endpoint")]
public async Task VersionsEndpoint_ReturnsAtLeastOneVersion()
{
    var versions = await _adapter.FetchVersionsAsync(CancellationToken.None);
    Assert.That(versions, Is.Not.Empty);
}
```

- [ ] **Step 4: Run default tests, then explicitly run the network test once**

Default suite must pass without network. Run the explicit test only with `--include_explicit true --filter DataDragonNetworkSmokeTests`; expected: one pass and no project files written.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add ProjectSettings/ProjectSettings.asset Assets/TFTAnalyzer/Editor/Menu Assets/TFTAnalyzer/Tests/EditMode/Ingestion
git commit -m "feat: expose static catalog import in the editor"
```

## Phase 1 Completion Gate

Run the full default EditMode suite and inspect a manual Set 18 import report. Completion requires: compiler clean, all non-explicit tests pass, raw JSON exists only under `Library/TftAnalyzerCache`, published catalogs are immutable, and a second identical import is idempotent.
