using Dalamud.Configuration;
using System;

namespace MaskedCarnivale;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool enable { get; set; } = false;
    public bool showUI { get; set; } = true;
    public int xPosition { get; set; } = 0;
    public int yPosition { get; set; } = 0;
    public int renderIndex { get; set; } = 0;
    public int orderStatus { get; set; } = 1;

    // When true, `renderIndex` is used verbatim instead of the automatic
    // Show-UI -> gameWindowWithUI / gameWindowWithoutUI mapping. Lets the user
    // hunt for the correct render target after a game-patch offset shift.
    public bool manualIndex { get; set; } = false;

    // When true, capture the final swapchain backbuffer (which includes the game HUD) via a
    // dedicated present hook, instead of sampling a render-target index. Overlay-free only if
    // our present hook runs before Dalamud draws its ImGui overlays.
    public bool captureBackbuffer { get; set; } = false;

    public bool doUpdate { get; set; } = false;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
