using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.GameHelpers;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
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
               | ImGuiWindowFlags.NoNav
               | ImGuiWindowFlags.NoBackground)
    {
        DisableWindowSounds = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    public override bool DrawConditions()
        => !Service.Configuration.StatusOverlayHidden;

    public override void Draw()
    {
        // Two stacked symbols, no labels, no background. Click to toggle.
        var disabled = Service.Configuration.MasterDisabled;
        DrawSymbol(
            disabled ? "X" : "O",
            disabled ? ImGuiColors.DalamudRed : ImGuiColors.HealerGreen,
            "##master",
            () =>
            {
                Service.Configuration.MasterDisabled = !disabled;
                Service.Configuration.Save();
                Service.ActionReplacer.UpdateFilteredCombos();
            });

        var held = ActionResolution.IsBurstHeld();
        if (held is null)
        {
            DrawSymbol("-", ImGuiColors.DalamudGrey, "##burstNA", null);
        }
        else
        {
            DrawSymbol(
                held.Value ? "X" : "O",
                held.Value ? ImGuiColors.DalamudYellow : ImGuiColors.ParsedBlue,
                "##burst",
                () => ToggleBurstForCurrentJob(held.Value));
        }
    }

    private static void DrawSymbol(string glyph, Vector4 color, string id, System.Action? onClick)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1f, 1f, 1f, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(1f, 1f, 1f, 0.20f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0, 0, 0, 0));

        var size = ImGui.CalcTextSize(glyph);
        if (ImGui.Selectable($"{glyph}{id}", false,
                onClick is null ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None,
                size))
        {
            onClick?.Invoke();
        }
        ImGui.PopStyleColor(4);
    }

    private static void ToggleBurstForCurrentJob(bool currentlyHeld)
    {
        if (!WrathCombo.BurstPresetMap.TryGetValue(Player.Job, out var presets))
            return;

        // Held → resume (enable). Armed → hold (disable).
        var enable = currentlyHeld;
        foreach (var preset in presets)
        {
            if (enable)
                PresetStorage.EnablePreset(preset, Configuration.ConfigChangeSource.Command);
            else
                PresetStorage.DisablePreset(preset, Configuration.ConfigChangeSource.Command);
        }
    }
}
