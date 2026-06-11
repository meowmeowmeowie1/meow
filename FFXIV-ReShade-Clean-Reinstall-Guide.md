# FFXIV ReShade — Clean Reinstall Guide (Alive NEXT edition)

The fix for your "duplicate attribute" / "not completely initialized" errors is a
clean, **version-matched** install. The root cause was a shader graveyard plus
GPosingway feeding you an **older pinned** iMMERSE/METEOR base while you layered
**newer Patreon Pro** files on top — mismatched `.fxh` headers = those exact errors.

This guide swaps stale ipsuShade for **Alive NEXT** (actively maintained, uses your
Pro shaders, ships `XIV_ImmPad` for game-accurate normals/motion vectors) and pulls
iMMERSE/METEOR from source so **you** control the version instead of inheriting
GPosingway's pin.

The script `ffxiv-reshade-clean-reinstall.ps1` does steps 0–2 and 6 for you. The
rest is GUI/login-walled and has to be manual.

---

## 0. Run the helper script (backup + wipe + fetch)

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass   # if it complains
./ffxiv-reshade-clean-reinstall.ps1
```

It backs up your whole ReShade install to the Desktop, **deletes** `reshade-shaders`
and `reshade-presets` (no merging — that's what broke it), and downloads the free
iMMERSE/METEOR base. Add `-WhatIf` first if you want a dry run that deletes nothing.

> Do **not** copy anything from the backup back into `reshade-shaders` later. The
> backup is an escape hatch, not a source to merge from.

---

## 1. Install ReShade — fresh, add-on enabled

- Download **ReShade 6.7.3 or newer** (hard floor for Alive NEXT is 6.7.0).
- Run the installer → select `ffxiv_dx11.exe` → **DirectX 10/11/12**.
- **Tick "Install ReShade with full add-on support."** (Non-add-on builds break MXAO/RTGI/REST.)
- Skip the default shader collection prompt — Alive NEXT brings its own.

## 2. Install the REST add-on

Alive NEXT uses **ReshadeEffectShaderToggler (REST)** for UI masking. Drop the
`.addon64` into `\game\` (or let the ReShade installer add it) and confirm it's
**enabled in the Add-ons tab** in-game.

## 3. Install Alive NEXT

- Get the newest `Alive_NEXT_WIP_xx.zip` from the **Alive Reshade Preset** page on the FFXIV Nexus.
- Since you own **iMMERSE Pro**, use the **experimental / RTGI** build (needs iMMERSE **2506 or newer**).
- Extract the zip into your `\game\` folder and **overwrite**.

## 4. Drop in version-matched iMMERSE + METEOR (free base)

From the script's `_downloads` folder (or Marty's GitHub "Code → Download ZIP"),
copy the **Shaders\** and **Textures\** contents into `\game\reshade-shaders\`.
Keep iMMERSE and METEOR from the **same release window**.

## 5. Layer Patreon iMMERSE Pro LAST — re-download it

- **Re-download iMMERSE Pro fresh from Patreon.** Do **not** reuse the copy already
  on your drive — that stale copy is what threw the duplicate-attribute errors.
- Extract it **on top** of the base so Pro and base share identical headers.

## 6. Final in-game checks

- **`RESHADE_DEPTH_INPUT_IS_REVERSED = 1`** — the script sets this if `ReShade.ini`
  exists; otherwise set it in **Settings → Edit global preprocessor definitions**.
  Without it, RTGI/MXAO/DoF silently do nothing in Dawntrail.
- Put **Launchpad / XIV_ImmPad at the TOP** of the technique list so it feeds depth
  and motion vectors before everything else.
- Enable the Alive NEXT preset. Compile should be clean — zero errors.

---

## If something still errors

| Symptom | Cause | Fix |
|---|---|---|
| "duplicate attribute branch/flatten" | Pro ≠ base version | Re-download both, same release |
| "not completely initialized" | Old METEOR/iMMERSE include | Replace with current GitHub release |
| AO/RTGI do nothing | Depth not reversed | `RESHADE_DEPTH_INPUT_IS_REVERSED = 1` |
| Effects ignore UI / hit the HUD | REST not enabled | Enable REST add-on, Launchpad at top |

**One-line rule:** every iMMERSE file — free base, METEOR, and Pro — must come from
the **same release window**. Mix versions and you're back to square one.
