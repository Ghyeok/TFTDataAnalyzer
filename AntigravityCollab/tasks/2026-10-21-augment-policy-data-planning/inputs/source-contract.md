# Source-inspected contracts and evidence (3484b13)

ReviewedAugmentPolicyProvider.Approved is currently empty. Its registration binds CatalogIdentity, catalog contentHash, policy document SHA256, reviewedUtc and HTTPS source URLs. Load(catalog, identity, token) rejects mismatched catalogs/hashes, linked paths, oversized documents and invalid strict schema. It reads only Assets/TFTAnalyzer/Data/Policy/<hash>.json. Schema1 fields: schemaVersion; catalog(setId,setNumber,patchId,sourceGameVersion,sourceProfile); contentHash; rules[{augmentId,kind}]. Known exact stable IDs only; no duplicate rules. Missing entries become Unknown in MetricPolicy. No production file exists yet. MetricPolicy minimum is100; all supplied augment IDs must be classified Standard for ratio visibility. Legend/LegendBased/Unknown still block at n100. Composition fingerprints include units/stars/items/active traits/all augments; exact board n100 usefulness is still unverified. Current MatchAnonymizer requires exact catalog IDs and quarantines unknown ones. No real API credentials yet; no raw payloads or identifiers are supplied.

The current immutable catalog has485 augments,119 duplicate English names. Baron's Lair maps to DA_BaronsLair and TFT_Augment_TheBaronsLair with differing scalar effects; Nesting Dolls maps to DA_NestingDollsPlus/PlusPlus. Names alone cannot establish IDs. Proposed four IDs below each have a unique matching name in the current catalog; inspect specific effects/context, not prefix alone.

## Primary sources checked by root on2026-10-21

- https://developer.riotgames.com/docs/tft — current policy prohibits Legends/Legend-based augment win rates; does not publish an ID classification table.
- https://teamfighttactics.leagueoflegends.com/en-us/news/game-updates/teamfight-tactics-patch-18-2/ — published2026-09-09; Enchanted Wilds. Ordinary Augments section lists Coven Acolyte/Dark Ritual as mutually exclusive Coven augments, discusses Dark Ritual Coven cashout AP, Consuming Flora emblem trait effectiveness, and Unrivaled Rengar/Kha'Zix interaction. This is specific gameplay context, not an explicit Standard enum. Root proposes non-Legend classification as an evidence-based inference, not authoritative approval. Evaluate whether these premises suffice and what essential evidence is missing.
- https://teamfighttactics.leagueoflegends.com/en-gb/news/game-updates/runeterra-reforged-mechanics-overview/ — historical Urf Legend table includes Big Grab Bag with different rewards from current DA_18_BigGrabBag. That current ID is intentionally excluded; historical names are not automatically classified.

## Exact local records (English description copied from preserved catalog)
[
  {
    "stableId": "DA_18_CovenTraitAugment",
    "name": "Coven Acolyte",
    "description": "When you choose to continue gathering Coven essence, heal 2 Tactician health. Gain a Camille, an Elise, and a Caitlyn."
  },
  {
    "stableId": "DA_18_CovenTraitAugment_LootToAP",
    "name": "Dark Ritual",
    "description": "Converting Coven Essence to rewards grants Coven champions Ability Power instead of loot. Gain a Camille, an Elise, and a Caitlyn."
  },
  {
    "stableId": "DA_18_FloraFatalisAugment",
    "name": "Consuming Flora",
    "description": "Gain a Flora Fatalis emblem. The champion holding the emblem gains 50% increased benefits from Flora Fatalis. Gain 3 gold."
  },
  {
    "stableId": "DA_18_RivalsAugment",
    "name": "Unrivaled",
    "description": "You can field Rengar and Kha'Zix together with no penalties, and they gain bonuses when the other casts. Gain a Rengar and a Kha'Zix."
  }
]
