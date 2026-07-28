# FFXIV patch-day downtime kit

After **every game patch**, Dalamud (and with it MyTweak / every plugin) and
ACT's FFXIV_ACT_Plugin stop working until their authors ship updates:

- **Dalamud** breaks because game memory structures and offsets change with the
  patch. goatcorp has to re-map them and publish a new Dalamud build. Until the
  release API reports the new game version as supported, XIVLauncher will
  (correctly) refuse to inject.
- **FFXIV_ACT_Plugin** breaks because network opcodes change. ravahn has to
  find the new opcodes and publish a release. Until then ACT parses nothing.

**Nothing installed locally can shortcut this.** The `MyTweak-OfflineKit.ps1`
in the repo root protects against *infrastructure* downtime (plugin repos or
the Dalamud CDN being unreachable) — it cannot make an old Dalamud work on a
new game version, and forcing it to inject anyway risks crashes or worse.
Patch day is a *waiting* problem, so this kit does two things instead: tells
you the instant the wait is over, and gives you plugin-free stand-ins in the
meantime.

## 1. `Watch-FFXIVPlugins.ps1` — know the moment plugins are back

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

## 2. `downtime-toolkit.html` — play without plugins in the meantime

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

## Patch-day routine

1. Patch drops → launch via XIVLauncher's **"Start w/o Dalamud"** (don't force-inject).
2. Start `.\Watch-FFXIVPlugins.ps1 -Watch` and open `downtime-toolkit.html`.
3. Play vanilla with the toolkit as your countdown/timer/notes surface.
4. Toast fires → restart the game through XIVLauncher normally; Dalamud and
   MyTweak load again. When ACT flips to READY, update FFXIV_ACT_Plugin via
   ACT's plugin listing and parsing resumes.
