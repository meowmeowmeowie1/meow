# MaskedCarnivale (unofficial API-15 port)

This folder is an **unofficial port** of **MaskedCarnivale** by **Marulu** and **StreetRat**
(ProjectMimer), updated to build and load on the current Dalamud (API level 15).

- Upstream (original): https://github.com/ProjectMimer/MaskedCarnivale
- Original authors: Marulu, StreetRat (ProjectMimer)

## What it does
Spawns a separate, overlay-free window that mirrors the game render *before* Dalamud draws
its plugin overlays — so you can point OBS/Discord/Medal at that clean window for
streaming/recording without WrathCombo, Splatoon, etc. showing. Opened with `/carnivale`.
Only hides *overlay* plugins; content mods (Penumbra/Glamourer) still appear.

## Port notes
- Only the managed plugin was rebuilt (`net10.0-windows`, Dalamud API 15, `ImGui.NET` →
  `Dalamud.Bindings.ImGui`, DalamudPackager 14.0.1).
- `outputwindow.exe` is the **original prebuilt** native capture window (a generic D3D11
  shared-texture renderer with no game coupling), shipped unmodified beside the DLL.
- The capture path uses a hardcoded signature and render-target offsets that drift with game
  patches and may need re-tuning; see the plugin's log if the mirror is blank.

## Attribution / license
The upstream repository ships **no license**. This port is hosted here for personal use, with
full credit to the original authors. If you are an original author and want this removed,
open an issue on the host repo.
