# FFXIV patch-day downtime kit

After **every game patch**, Dalamud (and with it MyTweak / every plugin) and
ACT's FFXIV_ACT_Plugin stop working until their authors ship updates:

- **Dalamud** is disabled because XIVLauncher's version gate won't inject an
  un-whitelisted build onto the new game version. Memory offsets can shift with
  a patch, so goatcorp gate it until they publish a matching build.
- **FFXIV_ACT_Plugin** breaks because network opcodes change. ravahn has to
  find the new opcodes and publish a release. Until then ACT parses nothing —
  no local trick fixes this; it's a genuine wait.

The important nuance: **the Dalamud version gate lives only in XIVLauncher.**
Dalamud itself and `Dalamud.Injector.exe` don't check the game version, so the
*previous* Dalamud can often be force-loaded onto the new patch. It usually
survives small/hotfix patches and often crashes on major ones — you're trading
"no plugins" for "maybe plugins, maybe a crash to desktop." This kit gives you
that option, plus a way to know when the wait is truly over, plus plugin-free
stand-ins if you'd rather not risk it.

## 1. `Force-InjectDalamud.ps1` — use MyTweak now, before Dalamud is whitelisted

Loads the Dalamud runtime that `MyTweak-OfflineKit.ps1` snapshotted before
downtime (or the live `Hooks\dev`) into the running game, bypassing the gate.

**Prerequisite:** run `..\MyTweak-OfflineKit.ps1` *before* the patch so a
snapshot and the MyTweak devPlugin exist. (If your `Hooks\dev` still holds the
last working Dalamud, that's used automatically as a fallback.)

Recommended patch-day flow:

1. Patch drops → launch via XIVLauncher's **"Start w/o Dalamud"**.
2. Log in and reach **character select** (safest injection point).
3. First pass with third-party plugins off (MyTweak still loads):
   ```powershell
   .\Force-InjectDalamud.ps1 -SafeMode
   ```
4. If that's stable, **restart the game** (don't re-inject the same session)
   and load everything:
   ```powershell
   .\Force-InjectDalamud.ps1
   ```

Troubleshooting ladder if injection fails:

| Try | Command |
|---|---|
| Access/ACL error | `.\Force-InjectDalamud.ps1 -FixAcl` |
| Still can't open the process | *(elevated PowerShell)* `.\Force-InjectDalamud.ps1 -FixAcl -SeDebugPrivilege` |
| Suspect a bad 3rd-party plugin | `.\Force-InjectDalamud.ps1 -SafeMode` |
| Watch the load live | `.\Force-InjectDalamud.ps1 -DalamudConsole` |
| Is the core even surviving? | `.\Force-InjectDalamud.ps1 -BareMode` (no plugins) |

Logs land in `downtime\logs\`. `-Runtime <path>` picks a specific snapshot;
`-Language` overrides the client language; `-SkipPreflight` forces even when the
official Dalamud already supports your version.

**The lazy route:** `.\Force-InjectDalamud.ps1 -Launch` starts XIVLauncher with
`--dalamud-runner-override` so it injects the snapshot at launch. It's hands-off
but injects at the entrypoint (the crashier moment), so Inject mode above is
preferred. Close any running XIVLauncher first.

> **Honest caveat:** this runs an old Dalamud against a new game binary. goatcorp's
> position is that if you bypass the post-patch disable, you're on your own. Worst
> case is a crash to desktop — nothing is permanently damaged, and your snapshots
> are untouched. Forcing does **nothing** for ACT: FFXIV_ACT_Plugin's broken
> opcodes are a real wait (see below).

## 2. `Watch-FFXIVPlugins.ps1` — know the moment plugins are officially back

Run this in parallel so you know when to stop forcing and return to the normal
XIVLauncher flow (and when ACT is updatable).

One-shot status check:

```powershell
.\Watch-FFXIVPlugins.ps1
```

Watch mode — polls every 10 minutes and fires a Windows toast + beeps the
moment each component is updated for your installed game version, then exits:

```powershell
.\Watch-FFXIVPlugins.ps1 -Watch
.\Watch-FFXIVPlugins.ps1 -Watch -IntervalMinutes 5
```

How it decides:

- Reads your installed game version from `game\ffxivgame.ver` (auto-detected
  via XIVLauncher's config, or pass `-GamePath`).
- **Dalamud**: exact check — the official release API's `supportedGameVer`
  must equal your installed version.
- **FFXIV_ACT_Plugin**: date check — the latest GitHub release must be
  published on/after the patch date encoded in your game version.

## 3. `downtime-toolkit.html` — play without plugins if you'd rather not force

Open it in any browser (double-click), ideally on a second monitor or snapped
next to the game in borderless windowed. No install, no dependencies,
everything runs locally in the page:

| Stand-in for | You get |
|---|---|
| Countdown plugins | Pull countdown with 3-2-1-PULL beeps (`Space`) |
| ACT encounter timer | Fight stopwatch with phase splits (`S` / `Enter` / `R`) |
| Food/pot reminders | 30-min Well Fed timer with 5-min warning, 270 s potion cooldown, custom timer |
| Cactbot timelines | Notes pad for mechanic timings and mit assignments (auto-saved) |
| Repo status checking | Live Dalamud + FFXIV_ACT_Plugin status with READY/PENDING pills (set the patch day once) |

There is intentionally no DPS meter stand-in: parsing damage requires reading
the game's network data, which is exactly what's broken until ravahn updates.
For post-fight numbers on patch day, [FFLogs](https://www.fflogs.com/) uploads
from other regions/players whose parser already updated are the only source.

## When to stop

`Watch-FFXIVPlugins.ps1` fires READY for Dalamud → close the game, launch
XIVLauncher normally (it now injects the official build), and run
`..\MyTweak-OfflineKit.ps1 -Cleanup` to remove the snapshots. When ACT flips to
READY, update FFXIV_ACT_Plugin via ACT's plugin listing and parsing resumes.
