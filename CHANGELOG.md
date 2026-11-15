# Changelog

## 1.1.2

- **Fix:** Manual renown MCM values (`Tier 0`–`Tier 50`) now save and load. The UI visibility filter was also applied during MCM json2 serialize/deserialize, so those properties were dropped on load (and JSON import was ignored) while `UseManualRenown` was still at its constructor default.
- **High-tier dynasty bonuses (on by default; turn off in MCM):** clan-wide logistics (speed / wages / food / healing), court (Influence / relation boost / companion recovery), reputation (loyalty / volunteers / militia), trade house (caravan / workshop income), and milestone perk picks at tiers 7 / 9 / 11 / 13. Scales with tiers above vanilla 6; no extra party caps or combat damage. War Sails–safe (active model only).
- **Dynasty bonus corrections:** relation % no longer forces a minimum +1 (and daily kin recovery stays +1); Influence / militia factors apply only to positive results; loyalty is towns-only; income scaling ignores negative base values; milestone effects use their own explanation label; dynasty master toggle refreshes MCM via `UIVersion`; ProductVersion set to 1.1.2.
- **Dynasty follow-up:** relation % uses floor + probabilistic remainder so small gains (e.g. +1/+5/+10) can still benefit; logistics healing skips prisoner/troop-prisoner passes; README unicode dashes normalized to ASCII.
- **Dynasty healing Harmony fix:** healing postfixes bind party/prisoner args by position (`__0`/`__1`) so vanilla and War Sails parameter renames cannot fail `PatchAll`.
- **CMT volunteer order:** dynasty volunteer postfix uses `Priority.Last` so Castle Market Towns castle recruit % multiplies first; CTE then applies its factor and clamps once at 1.0.
- **Dynasty party Owner NRE:** `ClanFromParty` / `ClanFromPartyBase` catch `PartyBase.get_Owner` NullReferenceException on broken/garrison parties so logistics healing postfixes cannot kill the map tick.

## 1.1.1

- SubModule dependencies updated for Bannerlord **1.5.7**.

## 1.1.0

- **Renown loss from defeats.** Optional MCM feature (default On) so player and AI noble clans can lose **Renown** and drop **clan tier** after meaningful military defeats. Vanilla `Clan.AddRenown` ignores negative values and only promotes tiers; CTE writes `Clan.Renown` directly and recalculates tier immediately through the active `ClanTierModel`.
- Battle coverage: field battles, siege assaults, siege-outside, sally-out, and War Sails blockade / naval variants via `CampaignEvents.MapEventEnded`. Excludes hideouts, raids, volunteer/supply forcing, tournaments/practice, bandits, and events without a winner.
- Per-clan aggregation of parties, casualty / odds / contribution factors, optional party-destruction and clan-leader-capture multipliers, group scales (your clan / your kingdom / other AI), minimum tier floor, and at-most-one-tier demotion per battle or correlated siege context.
- **Renown loss from lost fiefs.** Towns and castles only (`BySiege`, `ByRebellion` by default; optional `ByLeaveFaction`). Barter, gift, king decision, clan destruction, and villages are not counted. Siege battle + fief change share one capped loss ledger (primitive string save data).
- Optional **Renown loss from executions** (default Off). Existing companions, workshops, caravans, and parties are never deleted by this feature.
- **Low prestige ruler challenge.** If a kingdom’s ruling clan stays below tier 2 (configurable) and below that tier’s Renown mark for several days, the highest-Renown eligible clan proposes a real king-selection vote. Unlike vanilla death elections, the current ruler may contest and spend Influence; support Influence costs are higher for this vote only. Incumbent merit is reduced. Cooldown and MCM toggles included. No silent `ChangeRulingClanAction`.
- Safe tier setter reused from `ClanTierReconcileBehavior` for single-clan demotion; full reconciliation still runs on settings changes.
- **1.1.0 fixes:** ruler-challenge day counters persist (`KingdomState` class + dictionary write-back); battle capture multipliers use only `HeroPrisonerTaken` for that `MapEvent`; contribution denominator ignores MCM group disable; per-clan daily renown/tier caps added; execution adult-lord checks and single ledger-based daily accounting (no double count); removal docs require resolving prestige votes first.
- **1.1.0 follow-up:** cache `StrengthOfSide` on `MapEventStarted` (saved) for odds; ruler below-threshold timer tracks ruling clan id and resets on ruler change or feature disable; capture/strength cleanup on all `MapEventEnded` exits; MCM tooltips note daily cap and throne-challenge notifications.
- **High-tier curve fix:** formula and default manual thresholds are strictly increasing through tier 50 inside `int` range (geometric fair-share after the seed); `CalculateTier` no longer jumps through flat thresholds.
- **1.1.0 polish:** Tier MCM max allows `int.MaxValue`; manual/formula gaps are float-safe near 2e9; battle odds refresh on `OnPartyAddedToMapEventEvent`; stable map-event keys use `StringId` or exact `t:<time>:a:<idx>:d:<idx>` (no bare time-prefix matching); battle loss defers one–two ticks and uses `MobilePartyDestroyed` for the party-destroyed factor; ruler countdown clears on hourly tick when disabled.
- **1.1.0 save-edge fixes:** primary strength keys stay reachable after leader-change + reload via persisted `party:` / `id:` aliases; deferred battle-loss queue serializes across the MapEventEnded->destroy window; README odds wording matches reinforcement refresh.
- **1.1.0 production polish:** party-destruction multiplier uses only tracked destroyable mobiles (`MobilePartyDestroyed` + `StringId`); settlement parties no longer inflate that denominator; strength/alias caches use reference-aware orphan cleanup via `MapEventManager.MapEvents` instead of wiping at 64/128; pending v2 serialization preserves destroyable lists and fail-closes legacy pending rows.
- **1.1.0 test-prep:** manual thresholds that collide as floats near `int.MaxValue` are pulled backward to the last representable bands (or flattened when no band remains); pending StringIds escape `,` `:` and the other record separators so they round-trip; README diplomacy/War Sails line matches the actual patches.

## 1.0.2

- **AI diplomacy / kingdom scores.** War, peace, clan join/leave, mercenary, and story conspiracy clan-packing scores use vanilla-capped **party** limits (no Extra Parties; tier capped at 6 for that formula). Vanilla diplomacy does not read companion limits on those paths; companion limits are still capped during the same calls only for third-party scores that might. Real armies, actual party slots outside those scores, and Extra Parties for gameplay are unchanged. With Companion & Party Limits Expanded loaded, that mod's own shield still applies; CTE extras remain skipped.
- Missing diplomacy / conspiracy shield targets report a red in-game message instead of failing silently.

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
