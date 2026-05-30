using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.GameHelpers;
using WrathCombo.Core;
using WrathCombo.Services;

namespace WrathCombo.Window;

internal sealed class StatusOverlay : Dalamud.Interface.Windowing.Window
{
    public StatusOverlay()
        : base("MyTweak##StatusOverlay",
               ImGuiWindowFlags.NoTitleBar
               | ImGuiWindowFlags.NoResize
               | ImGuiWindowFlags.NoScrollbar
               | ImGuiWindowFlags.AlwaysAutoResize
               | ImGuiWindowFlags.NoFocusOnAppearing
               | ImGuiWindowFlags.NoNav)
    {
        DisableWindowSounds = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    public override bool DrawConditions()
        => !Service.Configuration.StatusOverlayHidden;

    public override void Draw()
    {
        var disabled = Service.Configuration.MasterDisabled;
        ImGui.TextUnformatted("MyTweak:");
        ImGui.SameLine();
        ImGui.TextColored(
            disabled ? ImGuiColors.DalamudRed : ImGuiColors.HealerGreen,
            disabled ? "DISABLED" : "ON");

        var held = IsBurstHeld(out var jobLabel);
        ImGui.TextUnformatted($"Burst ({jobLabel}):");
        ImGui.SameLine();
        if (held is null)
            ImGui.TextColored(ImGuiColors.DalamudGrey, "n/a");
        else
            ImGui.TextColored(
                held.Value ? ImGuiColors.DalamudYellow : ImGuiColors.HealerGreen,
                held.Value ? "HELD" : "ARMED");
    }

    private static bool? IsBurstHeld(out string jobLabel)
    {
        jobLabel = "—";
        if (Player.Object == null) return null;

        jobLabel = Player.Job.ToString();
        if (!WrathCombo.BurstPresetMap.TryGetValue(Player.Job, out var presets)
            || presets.Length == 0)
            return null;

        return !PresetStorage.IsEnabled(presets[0]);
    }
}
