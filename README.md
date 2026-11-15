# Clan Tier Expanded

A Mount & Blade II: Bannerlord mod that raises the clan tier ceiling above vanilla's tier 6, with formula or manual renown requirements in [Mod Configuration Menu (MCM)][mcm], plus optional **Renown** loss after defeats and lost fiefs.

**Download:** [Clan Tier Expanded on Nexus Mods][nexus]

**Module id:** `ClanTierExpanded`

## About this mod

Inspired by **[Customizable Clan Tier](https://www.nexusmods.com/mountandblade2bannerlord/mods/4075)** by **[kz14365](https://www.nexusmods.com/profile/kz14365?gameId=3174)**. Credit for the idea only - this is a standalone mod for Bannerlord 1.5.7 with its own module id, MCM, and save-safe tier sync, not a continuation of that original.

### What this mod does differently

- Raises max clan tier (default 12, up to 50) with formula or manual renown curves.
- Recalculates saved clan tiers when you change settings or turn the mod off.
- Optional high-tier party bonuses that step aside when Companion & Party Limits Expanded is loaded.
- **High-tier dynasty bonuses** (on by default; turn off in MCM): small clan-wide logistics, court, reputation, trade, and milestone perks for tiers above vanilla 6 - not extra party caps or combat damage.
- AI war, peace, join/leave, and related scores use vanilla-capped **party** limits so Extra Parties and high tiers do not inflate those decisions; real armies still count in full.
- Optional **Renown loss from defeats** / lost fiefs so clans can fall in tier (vanilla cannot subtract Renown).
- Optional soft support for Caravan Party Limit Increased Fixed (caravan size hard cap).

## Features

| Feature | Default |
|---|---|
| Max clan tier | 12 (6-50) |
| Renown | Formula, or per-tier manual values |
| Extra parties at high tiers | +1 at 7, 9, and 11 (off while CPLE is loaded) |
| High-tier dynasty bonuses | On by default (MCM off switch): logistics, court, reputation, trade, milestones |
| AI diplomacy scores | Vanilla-capped party limits during those scores |
| Renown loss from defeats | On (see below) |
| Renown loss from lost fiefs | On |
| Renown loss from executions | Off |
| Low prestige ruler challenge | On (ruling clan below tier 2 + low Renown) |
| Caravan size hard cap | Shown only with CaravanPartyLimitIncreasedFixed (default 500) |

## Renown loss from defeats

Bannerlord stores clan prestige as **Renown** and **clan tier**. Vanilla `Clan.AddRenown` ignores negative values and only promotes tiers, so clans never lose Renown from defeats. CTE can reduce `Clan.Renown` directly, then recalculate tier through the active `ClanTierModel` immediately (no settings-reconcile wait).

### Eligible battles

Processed on `CampaignEvents.MapEventEnded` for player and simulated AI fights (land and War Sails naval):

- Field battles
- Siege assaults
- Siege-outside battles
- Sally-out battles
- Blockade / blockade sally-out (naval) battles
- Surrenders that still leave a clear winning / defeated side on the `MapEvent`

### Excluded

- Tournaments and practice fights
- Hideouts
- Raids and forcing volunteers/supplies
- Events without a winner / defeated side
- Bandit clans and eliminated clans
- Caravan, villager, and militia parties without an eligible noble clan

`Clan.PlayerClan` is always eligible for classification even if early-campaign `IsNoble` differs. Mercenary / minor nobles may be affected; bandit factions never are.

### Formula

Loss uses the width of the clan’s **current tier Renown band**, not total accumulated Renown:

```
currentThreshold = GetRequiredRenownForTier(currentTier)
previousThreshold = GetRequiredRenownForTier(currentTier - 1)
tierBand = max(1, currentThreshold - previousThreshold)

lossPercent =
    BaseLossPercent
    x casualty/severity factor (~0.5-1.5)
    x relative-odds factor (~0.5-1.5; stronger losers pay more)
    x clan-contribution factor (share of the defeated side)
    x group scale (your clan / your kingdom / other AI)
    x optional party-destruction (~1.25) and clan-leader-capture multipliers

renownLoss = max(1, round(tierBand x lossPercent))
```

Defaults: base **2%** of the tier band, max **10%** per battle/siege context **and** per clan per campaign day. Odds use `StrengthOfSide` cached when the battle starts and refreshed when additional parties join, rather than post-casualty values at battle end (saved across load). The optional party-destruction multiplier is confirmed through `MobilePartyDestroyed` and counts only tracked destroyable mobile parties (`MobileParty.StringId`); settlement `PartyBase` objects still affect troop/casualty contribution but never enlarge the destruction denominator. Strength and alias caches use reference-aware orphan cleanup against live `MapEventManager.MapEvents` and pending losses (they are not wiped merely for exceeding 64/128 entries). Deferred battle losses serialize across the short MapEventEnded-to-destroy save window. Captures counted for a battle come only from `HeroPrisonerTaken` during that `MapEvent` (not older prisoners). Contribution uses the full defeated-side eligible troop count even if some clan groups have loss disabled. Renown is clamped at 0, never below the configured minimum tier's threshold, and a battle/siege context or a campaign day may demote **at most one tier**.

Turning the feature (or CTE) off stops future losses immediately. Existing reduced Renown values remain ordinary saved `Clan.Renown`.

**High max tiers (13-50):** the formula (and default manual values) keep strictly increasing Renown thresholds through tier 50 inside `int` range, ending at `int.MaxValue` (2,147,483,647). Growth after tier 12 is capped so later tiers never collapse early. Effective thresholds stay **float-distinguishable** (`Clan.Renown` is float; near 2e9 consecutive ints are not real bands). If a manual edit sits so close to `int.MaxValue` that the next tier has no distinct float, earlier effective thresholds are pulled backward until each band is real. Tiers that still cannot fit share the last representable threshold and are not separate bands. Saved MCM numbers are left as entered.

### Clan groups

Classification is evaluated when the battle/fief event ends:

| Group | Rule | Default scale |
|---|---|---|
| Your clan | `Clan.PlayerClan` | 100% |
| Your kingdom | Same `Kingdom` as the player (none if you have no kingdom) | 100% |
| Other AI | Everyone else eligible | 100% |

Scale **0** disables that group. Player clan is never classified twice.

### Lost fiefs

Listens to `CampaignEvents.OnSettlementOwnerChangedEvent` for **towns and castles** only (villages are not counted separately).

| Detail | Counted? |
|---|---|
| `BySiege` | Yes |
| `ByRebellion` | Yes (toggle, default On; separate multiplier) |
| `ByLeaveFaction` | Optional, default Off |
| `ByBarter`, `ByGift`, `ByKingDecision`, `ByClanDestruction`, `Default` | Never |

`ByKingDecision` stays excluded so post-conquest fief distribution does not falsely punish a provisional owner. A successful siege may apply battle loss and fief loss; they share a per-clan / settlement / day ledger so the combined result stays under the max percent and one-tier cap. Other defending clans that do not own the settlement can still take battle loss.

Losing a final fief may cost Renown but does **not** destroy the clan, remove it from its kingdom, start a ruler election, or reassign settlements. Low tier/renown alone does not replace a ruling clan.

### Executions

Optional MCM toggle (default **Off**). Vanilla execution / succession flows are unchanged. If enabled, a surviving clan can lose a small band percentage for an executed adult lord (larger if the victim was clan leader), still under the same combined max. Eliminated / bandit clans are skipped.

### Low prestige ruler challenge

When a kingdom’s **ruling clan** stays below the configured tier threshold (default **below tier 2**) **and** below that tier’s Renown requirement for several consecutive days (default 3), CTE starts a real kingdom king-selection vote proposed by the **highest-Renown** eligible non-ruling clan.

- Uses a CTE decision based on vanilla `KingSelectionKingdomDecision` (candidates by Renown; incumbent kept on the shortlist).
- **`IsKingsVoteAllowed` is true** for this vote only, so the current ruler can contest and **spend Influence** (vanilla death/succession king elections still deny that).
- Support Influence costs are multiplied for this prestige vote only (default x1.5).
- Incumbent council merit is reduced (default x0.45) so a low-prestige ruler is harder to keep.
- Per-kingdom cooldown (default 60 days). No silent `ChangeRulingClanAction`.

### What tier decline does *not* do

- Does not delete companions, workshops, caravans, or clan parties.
- Does not award extra Renown to captors as part of this feature.

### Compatibility

| Loadout | Behavior |
|---|---|
| **CPLE** loaded | CPLE keeps companion / party-count limits; CTE extra party slots stay off. Lower tier still affects workshops, party troop size, policies, and CPI caravan scaling. |
| **CPI Fixed** loaded | Caravan party-size math follows the new (possibly lower) clan tier. |
| **CMT** loaded | Town / tier-scaled castle workshop ownership follows the reduced tier; fixed/unlimited castle workshop settings stay as configured. |
| **No CPLE** | Companion limits and CTE party slots follow the reduced tier; excess companions/parties are not removed. |
| **Diplomacy / War Sails** | Compatible: active diplomacy scores use vanilla-capped party limits; War Sails naval and blockade defeats participate in optional Renown loss. |

## Mod options ([MCM][mcm])

**Options > Mod Options > Clan Tier Expanded**

Turn **Enabled** off and save before removing the mod so tiers return to vanilla 0-6. Renown-decline settings live under **Enabled / Renown / Renown decline**. Other settings apply without a restart; tier sync for curve changes still runs in the background.

**High-tier dynasty bonuses** (under **Enabled / High-tier dynasty bonuses**, on by default):

| Track | Effect |
|---|---|
| Logistics | Party speed, wage discount, food saving, healing (scales with tiers above 6) |
| Court | Influence income (gains only), positive relation %, companion/kin +1 recovery |
| Reputation | Owned **town** loyalty, volunteer production, militia growth (gains only) |
| Trade house | Caravan and workshop owner income (positive income only) |
| Milestones | Flat perk picks at tiers 7 / 9 / 11 / 13 (`0` or `1` each) |

## Optional support

- **[Caravan Party Limit Increased Fixed][cpi]** - hard cap for that mod's caravan sizes (MCM group appears only when it is enabled).
- **[Companion & Party Limits Expanded][cple]** - when loaded, this mod does not add extra high-tier party slots on top of CPLE's limits.
- **[Castle Market Towns][cmt]** - unrelated to tiers; optional caravan size scaling goes through CaravanPartyLimitIncreasedFixed.

Load **ClanTierExpanded** before CPLE and CPI Fixed when using those mods.

## Requirements

| Dependency | Tested |
|---|---|
| Mount & Blade II: Bannerlord | 1.5.7 |
| War Sails (optional) | 1.3.7 |
| [Harmony][harmony] | 2.4.2 |
| [Mod Configuration Menu v5][mcm] | 5.13.1 |

MCM also needs [ButterLib][butterlib] and [UIExtenderEx][uiextenderex].

## Installation

1. Install Harmony and MCM (with ButterLib and UIExtenderEx).
2. Copy `ClanTierExpanded` into `Modules`.
3. Enable it after Harmony and MCM.

## Removing from a campaign

1. Resolve or let finish any open **low-prestige ruler** kingdom vote (CTE saves that custom decision type).
2. Turn **Enabled** off in MCM and wait a moment (or advance time) so tiers reconcile toward vanilla 0-6.
3. Save.
4. Disable or remove the module.

## Building from source

```bat
copy ClanTierExpanded.user.props.example ClanTierExpanded.user.props
build.cmd /p:DeployToGame=true
```

## License

GNU GPL v3. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

[nexus]: https://www.nexusmods.com/mountandblade2bannerlord/mods/14503
[harmony]: https://www.nexusmods.com/mountandblade2bannerlord/mods/2006
[butterlib]: https://www.nexusmods.com/mountandblade2bannerlord/mods/2018
[uiextenderex]: https://www.nexusmods.com/mountandblade2bannerlord/mods/2102
[mcm]: https://www.nexusmods.com/mountandblade2bannerlord/mods/612
[cmt]: https://www.nexusmods.com/mountandblade2bannerlord/mods/14182
[cple]: https://www.nexusmods.com/mountandblade2bannerlord/mods/14288
[cpi]: https://www.nexusmods.com/mountandblade2bannerlord/mods/14505
