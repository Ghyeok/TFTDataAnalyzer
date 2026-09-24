# TFT Data Analyzer Editor UI and Compliance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a complete Unity UI Toolkit Editor workflow for static imports, bounded match collection, historical metric exploration, policy disclosure, and deployment blocking until compliance is acknowledged.

**Architecture:** A single EditorWindow hosts four lazily bound tabs backed by small view-models. View-models depend on Phase 1/2 interfaces and contain no direct HTTP or AssetDatabase logic; compliance rules are shared by UI, export, and build preprocessing.

**Tech Stack:** Unity 6000.3.8f1, UI Toolkit/UXML/USS, Unity Editor APIs, Unity Test Framework 1.6.0.

**Spec:** `Assets/Plan/TFTDataPipeline.md`

## Global Constraints

- Execute only after Phase 1 and Phase 2 completion gates pass.
- UI copy says historical observation and relative placement advantage, never combat win rate or prediction.
- Never render or log API key, PUUID, Riot ID, or raw Match ID.
- CommunityDragon remains an unchecked opt-in on every static import.
- Catalog rows and the active selection carry the complete `(setId, patchId, sourceGameVersion, sourceProfile)` identity. A schema-1 legacy row is selectable only after the official-only resolver validates it.
- Player builds are blocked until a local compliance setting confirms current Riot registration/review.
- The Editor tool remains usable while player builds are blocked.

## Locked File Structure

```text
Assets/TFTAnalyzer/Editor/UI/
  TftDataAnalyzerWindow.cs
  TftDataAnalyzerWindow.uxml
  TftDataAnalyzerWindow.uss
  Data/{DataManagerViewModel,PatchRowViewModel}.cs
  Collection/{CollectionViewModel,CollectionProgressViewModel}.cs
  Analysis/{AnalysisViewModel,MetricRowViewModel}.cs
  About/AboutViewModel.cs
Assets/TFTAnalyzer/Editor/Compliance/
  {ComplianceSettings,ComplianceSettingsProvider,ComplianceBuildGuard,PolicyCopy}.cs
Assets/TFTAnalyzer/Tests/EditMode/UI/
Assets/TFTAnalyzer/Tests/EditMode/Compliance/
```

---

### Task 1: Build the Editor Window Shell

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Create: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/TftDataAnalyzerWindowTests.cs`

**Interfaces:**
- Produces menu item `TFT Analyzer/Open Data Analyzer`.
- Produces named tab roots `data-tab`, `collection-tab`, `analysis-tab`, and `about-tab`.

- [ ] **Step 1: Write a failing UI structure test**

```csharp
[Test]
public void CreateGUI_ContainsAllPolicyBoundedTabs()
{
    var window = ScriptableObject.CreateInstance<TftDataAnalyzerWindow>();
    window.CreateGUI();
    Assert.That(window.rootVisualElement.Q("data-tab"), Is.Not.Null);
    Assert.That(window.rootVisualElement.Q("collection-tab"), Is.Not.Null);
    Assert.That(window.rootVisualElement.Q("analysis-tab"), Is.Not.Null);
    Assert.That(window.rootVisualElement.Q("about-tab"), Is.Not.Null);
}
```

- [ ] **Step 2: Confirm failure and implement UXML shell**

Use a toolbar for tab buttons, one content root per tab, and a persistent footer label named `historical-data-disclaimer`. No `Update()` polling; view-model events schedule UI updates through `EditorApplication.delayCall`.

- [ ] **Step 3: Add resilient asset loading**

Load UXML/USS through fixed asset paths and show a plain IMGUI-style error label if either asset is missing, so a broken stylesheet does not make the tool unusable.

- [ ] **Step 4: Run focused and full suites, then commit**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI
git commit -m "feat: add TFT analyzer editor window shell"
```

---

### Task 2: Implement the Static Data Management Tab

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/Data/DataManagerViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/Data/PatchRowViewModel.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerViewModelTests.cs`

**Interfaces:**
- Consumes: `StaticCatalogPipeline`, `CatalogIndex`, `CatalogDiffer`.
- Produces: `CheckForUpdatesAsync`, `ImportAsync`, `CancelImport`, `SelectActiveCatalog(CatalogIdentity)`, and read-only catalog rows.

- [ ] **Step 1: Write failing explicit-consent tests**

Assert checking versions performs no catalog or image download. Assert `IncludeCommunityDragon` defaults false and is reset false after every completed/cancelled import.

- [ ] **Step 2: Implement the view-model state machine**

States are `Idle`, `Checking`, `AwaitingApproval`, `Importing`, `Succeeded`, `Failed`, and `Cancelled`. Commands reject invalid transitions and expose user-facing error codes without response bodies.

- [ ] **Step 3: Bind controls and patch table**

Show Set, TFT patch, source game version, stable source-profile ID, source badges, retrieved UTC, validation status, content hash prefix, resolved target path, and active catalog selection. Require a modal confirmation with the full identity and target before `ImportAsync`.

- [ ] **Step 4: Add patch-diff presentation**

Show added/removed/changed counts per entity and an expandable field-level list. Retrieval timestamps never appear as content changes.

- [ ] **Step 5: Run tests and commit**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI/DataManagerViewModelTests.cs
git commit -m "feat: add static data management workflow"
```

---

### Task 3: Implement the Bounded Collection Tab

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/Collection/CollectionViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/Collection/CollectionProgressViewModel.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/CollectionViewModelTests.cs`

**Interfaces:**
- Consumes: `IRiotApiKeyProvider`, `EnvironmentSeedPuuidProvider`, `MatchCollectionService`.
- Produces: platform, expected patch, queue, tier bucket, capped maximum matches, `StartAsync`, and `Cancel`.

- [ ] **Step 1: Write failing secret-visibility and cap tests**

Assert the model exposes only `ApiKeyConfigured`/`SeedConfigured` booleans, never values. Assert match limit accepts 1–1,000 and rejects 0 or 1,001.

- [ ] **Step 2: Implement validated input and confirmation**

Before collection, show route, patch, queue, maximum matches, and the statement that only user-supplied seeds are queried. Do not show the seeds themselves.

- [ ] **Step 3: Bind progress and cancellation**

Display requested, fetched, stored, duplicate, filtered, failed, rate-limit wait, and cancellation state. Disable all editable inputs during a run and re-enable them on every terminal state.

- [ ] **Step 4: Test cancellation, 401, 429, and retry copy**

Use fake services and assert no exception or UI text contains fabricated secrets. Run full suite.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI/CollectionViewModelTests.cs
git commit -m "feat: add bounded match collection workflow"
```

---

### Task 4: Implement Historical Analysis Views

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/Analysis/AnalysisViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/UI/Analysis/MetricRowViewModel.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/AnalysisViewModelTests.cs`

**Interfaces:**
- Consumes: `AggregateStore`, `MetricPolicy`, and active `StaticCatalog`.
- Produces filters for patch, region, queue, tier bucket, unit, trait, item, and augment.
- Produces metric rows containing sample count, average placement, top-four, first-place, Wilson interval, and policy state.

- [ ] **Step 1: Write failing copy and suppression tests**

```csharp
[Test]
public void Rows_DoNotUsePredictionLanguage()
{
    var allCopy = string.Join(" ", _viewModel.Load(Fixture.Aggregates).Select(x => x.DisplayText));
    Assert.That(_viewModel.PredictionClaims(Fixture.Aggregates), Is.Empty);
}
```

The scan should reject positive prediction or combat-win-rate claims while allowing the explanatory negation `같은 로비의 최종 순위 비교이며 실제 전투 승률이 아닙니다.` Assert samples 99 show `표본 부족` and no percentage; 100 show rate plus confidence interval.

- [ ] **Step 2: Implement filter intersection**

Every selection narrows the immutable snapshot in memory; no filter triggers an API request. Display an explicit empty state with scope and collection period.

- [ ] **Step 3: Implement aggregate and pairwise tables**

Label pairwise data `상대 배치 우위율` and include the tooltip `같은 로비의 최종 순위 비교이며 실제 전투 승률이 아닙니다.` Show numerator/denominator and 95% interval.

- [ ] **Step 4: Add CSV export of approved presentation DTOs only**

Export UTF-8 CSV from `MetricPresentation`; refuse raw observations. Include scope, sample count, retrieval period, metric name, numerator, denominator, estimate, lower, upper, and policy note.

- [ ] **Step 5: Run tests and commit**

```powershell
git add Assets/TFTAnalyzer/Editor/UI Assets/TFTAnalyzer/Tests/EditMode/UI/AnalysisViewModelTests.cs
git commit -m "feat: present policy-safe historical TFT metrics"
```

---

### Task 5: Add About, Policy Metadata, and Compliance Build Gate

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/UI/About/AboutViewModel.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/PolicyCopy.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/ComplianceSettings.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/ComplianceSettingsProvider.cs`
- Create: `Assets/TFTAnalyzer/Editor/Compliance/ComplianceBuildGuard.cs`
- Modify: `Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Compliance/ComplianceBuildGuardTests.cs`

**Interfaces:**
- Produces Project Settings page `Project/TFT Data Analyzer/Compliance`.
- Produces local settings under `Library/TftAnalyzerStore/compliance.json` with review date, policy URLs, product registration status, and acknowledgement timestamp.
- Implements `IPreprocessBuildWithReport` and blocks Player builds when registration status is not `ApprovedOrAcknowledged`.

- [ ] **Step 1: Write a failing unregistered-build test**

```csharp
[Test]
public void OnPreprocessBuild_BlocksWhenRegistrationIsNotAcknowledged()
{
    var guard = ComplianceFixture.UnregisteredGuard();
    Assert.Throws<BuildFailedException>(() => guard.ValidateForBuild());
}
```

- [ ] **Step 2: Implement local compliance settings**

Settings are intentionally outside `Assets` and version control. Required fields are `policyReviewedOn`, the three Riot policy URLs from the spec, `registrationStatus`, and `acknowledgedAt`. Never infer approval from API-key presence.

- [ ] **Step 3: Implement build and export gates**

Block Player build and public-style export while unregistered. Do not block Editor data exploration or local aggregate CSV export labeled for personal research.

- [ ] **Step 4: Bind the About tab**

Show data sources, policy-review date, registration state, required unofficial-project disclaimer source link, privacy summary, metric definitions, and excluded features.

- [ ] **Step 5: Run tests and commit**

```powershell
git add Assets/TFTAnalyzer/Editor/Compliance Assets/TFTAnalyzer/Editor/UI/About Assets/TFTAnalyzer/Tests/EditMode/Compliance
git commit -m "feat: add policy disclosure and build compliance gate"
```

---

### Task 6: Add Startup Version Check and End-to-End Editor Tests

**Files:**
- Create: `Assets/TFTAnalyzer/Editor/Ingestion/StaticVersionCheckScheduler.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/UI/TftDataAnalyzerEndToEndTests.cs`
- Create: `Assets/TFTAnalyzer/Tests/EditMode/Compliance/ForbiddenFeatureSurfaceTests.cs`

**Interfaces:**
- Produces a once-per-24-hours non-blocking Data Dragon version check.
- Persists only last-check UTC and last-seen version under `Library/TftAnalyzerStore`.

- [ ] **Step 1: Write failing scheduler tests**

Assert two Editor loads within 24 hours perform one version request, the check never downloads catalogs or icons, and network failure does not block domain reload.

- [ ] **Step 2: Implement `[InitializeOnLoad]` scheduling**

Schedule after the Editor is idle using `EditorApplication.delayCall`; skip batch mode and play mode; use a cancellation token tied to assembly reload. Display a passive notification only when the latest source version differs from the last imported version.

- [ ] **Step 3: Add an end-to-end fabricated workflow test**

With fake transports: check version → approve import → validate/publish a fabricated Set 18 catalog → collect two fabricated matches → persist anonymous observations → aggregate → show analysis rows. Assert no network, no secrets on disk, and the relative-placement label is correct.

- [ ] **Step 4: Add forbidden-surface reflection and text scans**

Fail if project assemblies expose types or menu items containing `Spectator`, `LiveGame`, `Overlay`, `CombatSimulator`, or `WinPrediction`; scan UI text for prohibited Korean/English prediction wording while allowing the documented negation disclaimer.

- [ ] **Step 5: Run full verification**

Recompile, run the entire EditMode assembly, and inspect Unity Console errors. Expected: zero compile errors, zero failed tests, explicit network tests skipped by default.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add Assets/TFTAnalyzer/Editor/Ingestion/StaticVersionCheckScheduler.cs Assets/TFTAnalyzer/Tests
git commit -m "test: verify TFT analyzer workflow and policy boundary"
```

## Phase 3 Completion Gate

Run the full EditMode suite, manually open `TFT Analyzer/Open Data Analyzer`, and execute the fabricated end-to-end workflow. Confirm the tool never displays identities or secrets, all ratio metrics show sample count and interval, CommunityDragon remains opt-in, startup checks do not mutate data, and Player builds are blocked until compliance status is acknowledged.
