# Changelog

## 1.0.1

- Reconcile saved `Clan.Tier` on load and when tier-affecting MCM settings change (vanilla only raises tier on renown gain), including eliminated clans.
- Turn **Enabled** off and save before removing the mod so tiers return to vanilla 0–6.
- Clamp vanilla `GetRequiredRenownForTier` when disabled so leftover high tiers cannot index out of range.
- Prevent int overflow in `CalculateInitialRenown` near `int.MaxValue` renown caps (including saturated lower==upper).
- Extra high-tier party slots are skipped whenever Companion & Party Limits Expanded is loaded, so that mod's party limits stay exact for every clan.
- MCM visibility filter no longer swallows unrelated MCM discovery exceptions.

## 1.0.0

First release of **Clan Tier Expanded** (module id `ClanTierExpanded`).

- Configurable max clan tier (6–50, default 12) with formula or manual renown requirements.
- Optional extra party slots at high tiers (skipped entirely while Companion & Party Limits Expanded is loaded).
- Optional hard caravan party-size cap when CaravanPartyLimitIncreasedFixed is active (shown under that mod-support group).
