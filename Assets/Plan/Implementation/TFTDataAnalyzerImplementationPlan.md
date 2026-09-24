# TFT Data Analyzer Implementation Plan Index

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a policy-bounded Unity Editor tool that imports patch-versioned TFT static data, ingests Riot Match API responses without persisting identity-bearing payloads, and displays anonymous historical aggregate statistics.

**Architecture:** The implementation is split into three independently testable plans. Phase 1 produces a usable static-data catalog and patch-diff tool; Phase 2 adds memory-only match ingestion, anonymized local observations, and deterministic aggregates; Phase 3 adds the UI Toolkit Editor experience and compliance gates.

**Tech Stack:** Unity 6000.3.8f1, C#, Unity Test Framework 1.6.0, Newtonsoft Json 3.2.2, UnityWebRequest, UI Toolkit, Unity CLI/Pipeline 0.7.0-exp.1.

**Spec:** `Assets/Plan/TFTDataPipeline.md`

## Global Constraints

- This project never simulates TFT combat and never reports future win probability.
- Match API payloads containing PUUIDs or raw Match IDs never touch disk or logs.
- Static-data source priority is Riot Data Dragon first and CommunityDragon only for missing fields.
- CommunityDragon ingestion is opt-in for every import run.
- Statistics are segmented by patch, region, queue, and tier bucket.
- Ratio metrics are hidden below 100 observations and use a 95% Wilson confidence interval.
- Legends and Legend-based augment win-rate metrics are blocked at the policy boundary.
- API keys come only from `RIOT_API_KEY` or an OS credential provider; never from project files, `EditorPrefs`, URLs, or command-line arguments.
- No live-game, spectator, overlay, screen-capture, opponent-tracking, or recommendation feature is introduced.
- This workspace has a Git repository. The Phase 1 official-only Set 18 / 18.2 baseline was committed at `ec9d296`; preserve its generated catalog and manifest bytes.
- A static catalog is selected by `CatalogIdentity = (setId, patchId, sourceGameVersion, sourceProfile)`. Profiles are `official-only` or `official-plus-communitydragon`; schema-1 official-only output is read through the guarded legacy resolver.

---

## Plan Order

1. [Phase 1 — Static Data Pipeline](2026-09-17-tft-static-data-pipeline.md)
   - Baseline committed at `ec9d296`: official-only Set 18 / 18.2 catalog imported, normalized, validated, diffed, and indexed from the Unity Editor. The source-profile identity hardening cycle follows this baseline.
2. [Phase 2 — Anonymous Match Analytics](2026-09-17-tft-match-analytics.md)
   - Starts only after the four-field active catalog identity is stable. Bounded Match API collection produces only anonymous observations and aggregate metrics in `Library/TftAnalyzerStore`.
3. [Phase 3 — Editor UI and Compliance](2026-09-17-tft-analyzer-editor-ui.md)
   - Deliverable: the complete `TFT Data Analyzer` window exposes import, collection, and analysis workflows with policy and build gates.

Each phase must pass its own EditMode suite before the next phase begins. Network tests remain `[Explicit]`; the default suite uses fabricated fixtures and never requires a Riot API key.

## Standard Verification Commands

After adding or changing scripts:

```powershell
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command recompile --project-path 'C:\Unity\TFTSimulator' --format json
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command recompile_status --project-path 'C:\Unity\TFTSimulator' --format json
```

Run the full EditMode suite:

```powershell
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command run_tests --project-path 'C:\Unity\TFTSimulator' --mode editor --filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout 300 --format json
```

Success requires `success: true`, completed test status, zero failed tests, and no compiler errors in the fresh command output.
