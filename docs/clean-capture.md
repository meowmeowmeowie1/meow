# Clean FFXIV capture for Discord & Medal (no plugin overlays)

Goal: record/stream FFXIV to **Discord** and **Medal** without your Dalamud overlays —
**WrathCombo** windows, **Splatoon** mechanic drawings, cactbot/ACT overlays, etc. — showing
up in the footage.

## Why this works (the one fact that matters)

Every Dalamud plugin overlay (WrathCombo's windows, Splatoon's on-screen drawings, and the
rest) is drawn as a **third-party overlay layer** on top of the game's frame. **OBS Game
Capture can exclude that layer.** Discord and Medal have **no** equivalent "hide overlays"
switch of their own, so the clean-up has to happen in **OBS**, and then you feed OBS's clean
output into Discord and Medal.

What this **hides**: WrathCombo, Splatoon, and any other overlay/ImGui plugin.
What it **cannot hide**: content mods that change the actual game render — **Penumbra**,
**Glamourer**, model/texture/dye swaps. Those are baked into the frame, not an overlay.

---

## Setup (once)

### 1. OBS Game Capture, overlays off
1. Install [OBS Studio](https://obsproject.com/).
2. Sources → **+** → **Game Capture**.
3. **Mode**: `Capture specific window` → select **`ffxiv_dx11.exe`**.
   (`Capture any fullscreen application` also works if you play fullscreen.)
4. **Uncheck** ✅→⬜ **"Capture third-party overlays (such as steam)"**.
   *This is the setting that removes WrathCombo + Splatoon from the capture.*
5. You should now see the game in OBS **without** your Dalamud overlays.

> **If overlays still show:** it's a launch-order/timing quirk (see Dalamud issue #2271).
> Fix: fully close OBS, make sure the **game is already running**, then reopen OBS and
> re-check the Game Capture source. It's reliable once set, but a Dalamud update can
> occasionally reintroduce the leak — worth a 10-second sanity check after big patches.

### 2. Into Discord
Two options — the Virtual Camera is the most reliable:

- **Virtual Camera (recommended):** In OBS click **Start Virtual Camera**. In Discord →
  User Settings → **Voice & Video** → **Camera** → pick **"OBS Virtual Camera"**. Now your
  Discord video / stream shows the clean OBS output.
- **Window share:** In OBS, right-click the preview → **Windowed Projector (Preview)**. In
  Discord, **Screen → Application Window →** share that **OBS projector window**. Sharing a
  specific *window* (not the whole screen) keeps everything else private.

### 3. Into Medal
Medal can't strip overlays on its own, so let OBS do the clip capturing:

- **OBS Replay Buffer (recommended — this is basically Medal-style clips):**
  OBS Settings → **Output → Replay Buffer** → enable, set the buffer length (e.g. 30–120s),
  bind a **Save Replay** hotkey. Click **Start Replay Buffer**. Press the hotkey any time to
  save the last N seconds — clean, no overlays.
- **Keep Medal in the loop:** point Medal's capture at the **OBS Windowed Projector** window
  (Medal can target a specific app/window), or just use OBS Replay Buffer as your clipper and
  keep Medal for sharing.

---

## Quick reference

| Destination | How | Overlays hidden? |
|---|---|---|
| Discord | OBS → Start Virtual Camera → Discord camera = "OBS Virtual Camera" | ✅ (WrathCombo, Splatoon) |
| Discord (window share) | OBS Windowed Projector → Discord shares that window | ✅ |
| Medal-style clips | OBS Replay Buffer + Save-Replay hotkey | ✅ |
| Medal app directly | Point Medal at the OBS projector window | ✅ |

**Reminder:** overlay plugins (WrathCombo, Splatoon) are hidden; content mods (Penumbra,
Glamourer) still appear. For a fully clean shot you can also hide the **Dalamud** button on
the ESC menu via Dalamud Settings.

---

*If OBS's overlay-exclusion ever proves flaky for you, the repo also has a longer-term plugin
route (a ported MaskedCarnivale that renders a separate overlay-free window you point Discord/
Medal at) — but this OBS workflow is the low-effort, no-maintenance option and does the same job.*
