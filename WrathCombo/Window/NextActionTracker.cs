using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.GameHelpers;
using WrathCombo.Core;
using WrathCombo.Services;

namespace WrathCombo.Window;

/// <summary>
///     A small movable window that shows, live, the action the current job's
///     Single-Target and AoE combos would produce right now, plus the burst
///     armed/held state. Toggle with <c>/mytweak tracker</c>.
/// </summary>
internal sealed class NextActionTracker : Dalamud.Interface.Windowing.Window
{
    public NextActionTracker()
        : base("MyTweak — Next Action##NextActionTracker",
               ImGuiWindowFlags.NoResize
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
        => !Service.Configuration.NextActionTrackerHidden
           && Player.Object != null
           && !Service.Configuration.MasterDisabled;

    public override void Draw()
    {
        ActionResolution.Refresh();

        DrawActionRow("ST", ActionResolution.TryGetSingleTarget(out var st), st);
        DrawActionRow("AoE", ActionResolution.TryGetAoE(out var aoe), aoe);
        ImGui.Separator();
        DrawBurst();
    }

    private static void DrawActionRow(string label, bool has, uint actionId)
    {
        // Label column.
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(48f * ImGui.GetIO().FontGlobalScale);

        if (!has)
        {
            ImGui.TextDisabled("—  (no combo enabled)");
            return;
        }

        var size = new Vector2(32f, 32f) * ImGui.GetIO().FontGlobalScale;
        var icon = Icons.GetTextureFromIconId(ActionResolution.ActionIcon(actionId));
        var name = ActionResolution.ActionName(actionId);

        if (icon != null)
        {
            ImGui.Image(icon.Handle, size);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(name);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(name);
        }
        else
        {
            ImGui.TextUnformatted(name);
        }
    }

    private static void DrawBurst()
    {
        var held = ActionResolution.IsBurstHeld();
        var (text, color) = held switch
        {
            true => ("Burst: HELD", ImGuiColors.DalamudYellow),
            false => ("Burst: ARMED", ImGuiColors.ParsedBlue),
            null => ("Burst: —", ImGuiColors.DalamudGrey),
        };

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }
}
