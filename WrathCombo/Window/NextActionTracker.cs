using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.Core;
using WrathCombo.Services;

namespace WrathCombo.Window;

/// <summary>
///     A small movable window that shows, live, the action the current job's
///     Single-Target and AoE combos would produce right now, plus the burst
///     armed/held state. Toggle with <c>/mytweak tracker</c>.
/// </summary>
/// <remarks>
///     Drag this window OUT of the game onto a second monitor (requires
///     borderless + multi-monitor, which enables Dalamud's ImGui viewports) and
///     it becomes its own OS window — so game-capture recorders (Medal, Discord)
///     won't record it. When detached we additionally set
///     <c>WDA_EXCLUDEFROMCAPTURE</c> on that OS window so screen/display capture
///     skips it too. We never touch the main (in-game) viewport's window.
/// </remarks>
internal sealed class NextActionTracker : Dalamud.Interface.Windowing.Window
{
    // Set by /mytweak tracker show to snap the window back to the centre of the
    // screen on the next frame, so it can never be lost off-screen.
    private bool _recenter;

    public void Recenter() => _recenter = true;

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
        if (_recenter)
        {
            _recenter = false;
            var vp = ImGui.GetMainViewport();
            ImGui.SetWindowPos(
                vp.WorkPos + (vp.WorkSize - ImGui.GetWindowSize()) * 0.5f);
        }

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
