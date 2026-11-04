# Clan Tier Expanded

A Mount & Blade II: Bannerlord mod that raises the clan tier ceiling above vanilla's tier 6, with formula or manual renown requirements in [Mod Configuration Menu (MCM)][mcm].

**Module id:** `ClanTierExpanded`

## About this mod

Inspired by **[Customizable Clan Tier](https://www.nexusmods.com/mountandblade2bannerlord/mods/4075)** by **[kz14365](https://www.nexusmods.com/profile/kz14365?gameId=3174)**. Credit for the idea only—this is a standalone mod for Bannerlord 1.4.8 with its own module id, MCM, and save-safe tier sync, not a continuation of that original.

### What this mod does differently

- Raises max clan tier (default 12, up to 50) with formula or manual renown curves.
- Recalculates saved clan tiers when you change settings or turn the mod off.
- Optional high-tier party bonuses that step aside when Companion & Party Limits Expanded is loaded.
- Optional soft support for Caravan Party Limit Increased Fixed (caravan size hard cap).

## Features

| Feature | Default |
|---|---|
| Max clan tier | 12 (6–50) |
| Renown | Formula, or per-tier manual values |
| Extra parties at high tiers | +1 at 7, 9, and 11 (off while CPLE is loaded) |
| Caravan size hard cap | Shown only with CaravanPartyLimitIncreasedFixed (default 500) |

## Mod options ([MCM][mcm])

**Options > Mod Options > Clan Tier Expanded**

Turn **Enabled** off and save before removing the mod so tiers return to vanilla 0–6. Other settings apply without a restart; tiers sync in the background.

## Optional support

- **[Caravan Party Limit Increased Fixed][cpi]** — hard cap for that mod's caravan sizes (MCM group appears only when it is enabled).
- **[Companion & Party Limits Expanded][cple]** — when loaded, this mod does not add extra high-tier party slots on top of CPLE's limits.
- **[Castle Market Towns][cmt]** — unrelated to tiers; optional caravan size scaling goes through CaravanPartyLimitIncreasedFixed.

Load **ClanTierExpanded** before CPLE and CPI Fixed when using those mods.

## Requirements

| Dependency | Tested |
|---|---|
| Mount & Blade II: Bannerlord | 1.4.8 |
| War Sails (optional) | 1.2.x |
| [Harmony][harmony] | 2.4.2 |
| [Mod Configuration Menu v5][mcm] | 5.13.1 |

MCM also needs [ButterLib][butterlib] and [UIExtenderEx][uiextenderex].

## Installation

1. Install Harmony and MCM (with ButterLib and UIExtenderEx).
2. Copy `ClanTierExpanded` into `Modules`.
3. Enable it after Harmony and MCM.

## Removing from a campaign

1. Turn **Enabled** off in MCM and wait a moment (or advance time).
2. Save.
3. Disable or remove the module.

## Building from source

```bat
copy ClanTierExpanded.user.props.example ClanTierExpanded.user.props
build.cmd /p:DeployToGame=true
```

## License

GNU GPL v3. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

[harmony]: https://www.nexusmods.com/mountandblade2bannerlord/mods/2006
[butterlib]: https://www.nexusmods.com/mountandblade2bannerlord/mods/2018
[uiextenderex]: https://www.nexusmods.com/mountandblade2bannerlord/mods/2102
[mcm]: https://www.nexusmods.com/mountandblade2bannerlord/mods/612
[cmt]: https://www.nexusmods.com/mountandblade2bannerlord/mods/14182
[cple]: https://www.nexusmods.com/mountandblade2bannerlord/mods/14288
[cpi]: https://github.com/AtanuDebbarma/CaravanPartyLimitIncreasedFixed
