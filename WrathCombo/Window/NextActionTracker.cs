using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.GameHelpers;
using WrathCombo.Core;
using WrathCombo.Services;

namespace WrathCombo.Window;

/// <summary>
///     A small floating window that shows, live, the action the current job's
///     Single-Target and AoE combos would produce right now, plus the burst
///     armed/held state. Hidden by default; toggled with <c>/mytweak tracker</c>.
/// </summary>
internal sealed class NextActionTracker : Dalamud.Interface.Windowing.Window
{
    public NextActionTracker()
        : base("MyTweak##NextActionTracker",
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
        => !Service.Configuration.NextActionTrackerHidden
           && Player.Object != null
           && !Service.Configuration.MasterDisabled;

    public override void Draw()
    {
        ActionResolution.Refresh();

        DrawActionRow("ST", ActionResolution.TryGetSingleTarget(out var st), st);
        DrawActionRow("AoE", ActionResolution.TryGetAoE(out var aoe), aoe);
        DrawBurst();
    }

    private static void DrawActionRow(string label, bool has, uint actionId)
    {
        if (!has)
        {
            ImGui.TextDisabled($"{label}: —");
            return;
        }

        var icon = Icons.GetTextureFromIconId(ActionResolution.ActionIcon(actionId));
        if (icon != null)
        {
            var size = new Vector2(32f, 32f) * ImGui.GetIO().FontGlobalScale;
            ImGui.Image(icon.Handle, size);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{label}: {ActionResolution.ActionName(actionId)}");
        }
        else
        {
            ImGui.TextUnformatted($"{label}: {ActionResolution.ActionName(actionId)}");
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
