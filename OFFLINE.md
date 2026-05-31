# MyTweak Offline Kit

`MyTweak-OfflineKit.ps1` keeps Dalamud + MyTweak running through downtime windows when XIVLauncher can't fetch a fresh Dalamud (e.g. scheduled maintenance, FFXIV patch day).

## Modes at a glance

| When | Command | What it does |
|---|---|---|
| Before downtime | `.\MyTweak-OfflineKit.ps1` | Snapshots your working Dalamud + stages MyTweak into devPlugins. |
| During downtime, if Dalamud broke | `.\MyTweak-OfflineKit.ps1 -Restore` | Rolls the snapshot back over the broken `Hooks\dev\`. |
| Any time | `.\MyTweak-OfflineKit.ps1 -Status` | Shows what's installed, snapshotted, broken. |
| After Dalamud is back | `.\MyTweak-OfflineKit.ps1 -Cleanup` | Removes snapshots + broken-runtime folders. Keeps MyTweak in devPlugins. |
| Full uninstall | `.\MyTweak-OfflineKit.ps1 -Cleanup -RemoveMyTweak` | Above, plus removes the MyTweak devPlugin so you can reinstall via pluginmaster. |

Add `-Force` to any of the cleanup modes to skip confirmation prompts.

## Pre-downtime one-liner

Paste this into a PowerShell window (no admin needed):

```powershell
iex (irm https://raw.githubusercontent.com/meowmeowmeowie1/meow/main/MyTweak-OfflineKit.ps1)
```

That downloads and runs the script in install mode. Run it at least once before downtime starts so your snapshot exists.

## During downtime (if Dalamud breaks)

Download the script once and run with `-Restore`:

```powershell
irm https://raw.githubusercontent.com/meowmeowmeowie1/meow/main/MyTweak-OfflineKit.ps1 -OutFile MyTweak-OfflineKit.ps1
.\MyTweak-OfflineKit.ps1 -Restore
```

Then relaunch FFXIV via XIVLauncher.

**Pro-tip**: in XIVLauncher → Settings → Dalamud Settings, set the Dalamud update behavior to "Manually only" before downtime, so XL doesn't try to overwrite the runtime mid-outage.

## After Dalamud is back

```powershell
.\MyTweak-OfflineKit.ps1 -Cleanup
```

The script confirms `Hooks\dev\` has a valid Dalamud.dll before deleting anything, so you can't accidentally erase the only working copy.

## What gets touched

- **Snapshot location**: `%APPDATA%\XIVLauncher\addon\Hooks\dev-mytweak-snapshot-{date}\` — a full copy of your working Dalamud runtime.
- **Plugin location**: `%APPDATA%\XIVLauncher\devPlugins\MyTweak\` — Dalamud loads dev plugins from here on every launch, no pluginmaster needed.
- **Set-aside broken runtimes**: `%APPDATA%\XIVLauncher\addon\Hooks\dev-broken-{date}\` — if `-Restore` finds a clobbered `dev\`, it moves it here instead of deleting it.
- **Download cache**: `%TEMP%\MyTweak.zip` — the release zip, kept around in case `-Restore` needs to redownload.

Nothing outside these paths is modified. The kit never touches your FFXIV install or your XIVLauncher account.

## Why this works during downtime

- MyTweak's release zip lives on **GitHub Releases**, which is a different CDN from Dalamud's update server. GitHub Releases stays up unless GitHub itself is down.
- Once Dalamud is in `Hooks\dev\` as a snapshot, XIVLauncher can't lose it. Even if XL re-downloads a broken build, `-Restore` rolls it back.
- MyTweak in `devPlugins\` loads as a **dev plugin** — Dalamud loads it directly from the folder, no plugin master poll required.
