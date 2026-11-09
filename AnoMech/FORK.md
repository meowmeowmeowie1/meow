# AnoMech fork notes

This folder is a vendored copy of [anomek/AnoMech](https://github.com/anomek/AnoMech)
(upstream commit `1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade`) plus the Dragonsong's Reprise
(Ultimate) scenarios in `AnoMech/Scenarios/Dsr/`.

## What was added

- `Core/Map/SimArenaBoundary.cs`, `Core/SimObjects/SimWorld.cs`: a square arena fence
  (`EnforceSquareArenaBoundary`) for the dragons' platform.
- `Scenarios/Dsr/`: zone, constants, actions, duty-init data, voidzone helper.
- `Scenarios/Dsr/P5DeathOfTheHeavens/`: scenario, state, overrides, settings, `LPDU` AI.
- `Scenarios/Dsr/P6WrothFlames/`: scenario, state, overrides, settings, `LPDU` AI.
- `tests/AnoMech.Tests/Scenarios/Dsr/`: negative tests (each mechanic kills when stood in).

## What could not be verified without a replay or the game

No packet capture of this duty was available, so these were taken from sheets, BossMod/cactbot
data and community scripts rather than observed:

- the knockback row Faith Unmoving sends (its 16y distance is known),
- the Akh Morn stack icon (the generic stack marker is used),
- the entrance-barrier layout ids, the eye map-effect "hide" flags, and whether the duty's
  voidzone EObjs (twister, Akh Morn puddle, cleanse puddle) render without replay layout data.
  The mechanics kill regardless of the visuals.
- Orb-set geometry for Wroth Flames follows the Dragonsong-Sim layout (three orbs per set,
  diagonal line or zigzag), scaled to 6y-wide Flame Blast crosses.

The LPDU priority orders (west-to-east line-up and spread order) are the usual
MT > OT > H1 > H2 > M1 > M2 > R1 > R2; adjust `Priority` in the two `*LpduAi.cs` files if your
group's toolbox differs.

## Building here

CI: `.github/workflows/anomech.yml` builds and tests on Windows and publishes
`anomech-v<version>/AnoMech.zip`; `anomech/pluginmaster.json` points at it.

Locally on Linux: `dotnet build tests/AnoMech.Tests -c Release -p:EnableWindowsTargeting=true
-p:GenerateSupportedOSPlatformAttribute=false` with `DALAMUD_HOME` set, then `dotnet test
--no-build`.
