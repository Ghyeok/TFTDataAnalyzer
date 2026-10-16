# TFT Data Analyzer Implementation Plan Index

> 모델 선택은 [AGENTS.md](../../../AGENTS.md), 단계·역할·기록은 [개발 워크플로](../Workflow/README.md)를 따른다. 단계별 계획은 구현 절차이며 현재 진행 상태와 과거 실행 근거를 구분한다.

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
   - 기준 카탈로그는 `ec9d296`, source-profile identity 강화는 `3715893`에 반영됐다. 기능 구현과 실제 Unity 검증 완료를 구분하며, 남은 검증·QA는 [현재 체크포인트](../Workflow/Cycles/2026-09-23-source-profile-identity/Checkpoint.md)를 따른다.
2. [Phase 2 — Anonymous Match Analytics](2026-09-17-tft-match-analytics.md)
   - Starts only after the four-field active catalog identity is stable. Bounded Match API collection produces only anonymous observations and aggregate metrics in `Library/TftAnalyzerStore`.
3. [Phase 3 — Editor UI and Compliance](2026-09-17-tft-analyzer-editor-ui.md)
   - Deliverable: the complete `TFT Data Analyzer` window exposes import, collection, and analysis workflows with policy and build gates.

Each phase must pass its own EditMode suite before the next phase begins. Network tests remain `[Explicit]`; the default suite uses fabricated fixtures and never requires a Riot API key.

## Standard Verification Commands

After adding or changing scripts:

```powershell
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command recompile --project-path 'C:\Unity\TFTDataAnalyzer' --format json
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command recompile_status --project-path 'C:\Unity\TFTDataAnalyzer' --format json
```

Run the full EditMode suite:

```powershell
& 'C:\Users\gulww\AppData\Local\Unity\bin\unity.exe' command run_tests --project-path 'C:\Unity\TFTDataAnalyzer' --mode editor --filter_type assembly --filter TFTAnalyzer.Tests.EditMode --timeout 300 --format json
```

Success requires `success: true`, completed test status, zero failed tests, and no compiler errors in the fresh command output.
