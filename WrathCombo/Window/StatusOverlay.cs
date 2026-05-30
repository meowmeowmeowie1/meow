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
        // Row 1: master toggle. Whole line is a clickable Selectable that
        // toggles MasterDisabled when left-clicked.
        var disabled = Service.Configuration.MasterDisabled;
        var masterLabel = disabled ? "MyTweak: DISABLED" : "MyTweak: ON";
        var masterColor = disabled ? ImGuiColors.DalamudRed : ImGuiColors.HealerGreen;

        ImGui.PushStyleColor(ImGuiCol.Text, masterColor);
        if (ImGui.Selectable($"{masterLabel}##master", false,
                ImGuiSelectableFlags.None, new Vector2(0, 0)))
        {
            Service.Configuration.MasterDisabled = !disabled;
            Service.Configuration.Save();
            Service.ActionReplacer.UpdateFilteredCombos();
        }
        ImGui.PopStyleColor();

        // Row 2: burst hold. Click to flip every preset in the current job's
        // burst map. Inactive (no map for job) → grey, no click handler.
        var held = IsBurstHeld(out var jobLabel);
        if (held is null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
            ImGui.TextUnformatted($"Burst ({jobLabel}): n/a");
            ImGui.PopStyleColor();
        }
        else
        {
            var burstColor = held.Value
                ? ImGuiColors.DalamudYellow
                : ImGuiColors.HealerGreen;
            var burstLabel = $"Burst ({jobLabel}): {(held.Value ? "HELD" : "ARMED")}";

            ImGui.PushStyleColor(ImGuiCol.Text, burstColor);
            if (ImGui.Selectable($"{burstLabel}##burst", false,
                    ImGuiSelectableFlags.None, new Vector2(0, 0)))
            {
                ToggleBurstForCurrentJob(held.Value);
            }
            ImGui.PopStyleColor();
        }
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
