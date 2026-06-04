#region

using System;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using WrathCombo.Services;

#endregion

namespace WrathCombo.Tweaks;

internal static unsafe class ActionPressMirroring
{
    private delegate void PulseActionBarSlotDelegate(
        AddonActionBarBase* addonActionBarBase, uint slotIndex, ulong a3, int a4);

    private static Hook<PulseActionBarSlotDelegate>? _pulseHook;

    private const string PulseSignature =
        "85 d2 78 ?? 48 89 5c 24 ?? 57 48 83 ec ?? 48 63 da 48 8b f9 48 8b 89 ?? ?? ?? ?? ba";

    private static readonly string[] AllActionBars =
    [
        "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03",
        "_ActionBar04", "_ActionBar05", "_ActionBar06", "_ActionBar07",
        "_ActionBar08", "_ActionBar09", "_ActionCross",
        "_ActionDoubleCrossL", "_ActionDoubleCrossR", "_ActionBarEx",
    ];

    internal static void Enable()
    {
        try
        {
            _pulseHook ??= Svc.Hook.HookFromSignature<PulseActionBarSlotDelegate>(
                PulseSignature, PulseActionBarSlotDetour);
            _pulseHook?.Enable();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MyTweak] ActionPressMirroring: failed to hook (signature?)");
        }
    }

    internal static void Dispose()
    {
        _pulseHook?.Disable();
        _pulseHook?.Dispose();
        _pulseHook = null;
    }

    private static void PulseActionBarSlotDetour(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        if (Service.Configuration.DuplicateActionPresses
            && !Service.Configuration.MasterDisabled)
        {
            try
            {
                if (MirrorPulse(ab, slotIndex, a3, a4))
                    return;
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "[MyTweak] ActionPressMirroring failed");
            }
        }

        _pulseHook!.Original(ab, slotIndex, a3, a4);
    }

    // The game's NATIVE adjustment for an action slot. In Performance Mode our
    // icon-swap hook is disabled, so this returns the game's own adjustment — for
    // a Dancer mid-dance that's the CURRENT dance step (Emboite/Entrechat/…), and
    // for upgrade tiers the upgraded action. Crucially it does NOT apply Wrath's
    // combo resolution, so it never reports a proc/combo-step (Reverse Cascade,
    // Fountain) that the user didn't visibly press. In normal mode the hook is on,
    // so this matches whatever icon the hotbar is actually displaying.
    private static uint GameAdjusted(RaptureHotbarModule.HotbarSlotType type, uint id) =>
        type == RaptureHotbarModule.HotbarSlotType.Action
            ? ActionManager.Instance()->GetAdjustedActionId(id)
            : id;

    private static bool MirrorPulse(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);
        var type = pressedSlot->CommandType;
        var commandId = pressedSlot->CommandId;

        // What action should light up? By default it's the pressed button's own
        // adjusted action. But in Performance Mode the icon hook is off, so the
        // hotbar shows base icons and the pressed GCD doesn't reveal what Wrath
        // actually cast. Use the resolution that ActionWatching.UseActionDetour
        // recorded for this slot's base action AT THE MOMENT OF THE REAL CAST, so
        // the pulse follows the action the rotation truly fired (next combo step,
        // proc, dance step, Saber Dance, ...) onto whatever button holds it. We do
        // NOT re-resolve the combo here: doing that evaluates the rotation against
        // the state at pulse time, which has already advanced past the action that
        // was just used, so the highlight drifts onto the wrong button. In normal
        // mode the icon hook already swaps the pressed button to the resolved
        // action, so its own adjusted action is already correct.
        var target = GameAdjusted(type, commandId);
        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            Service.Configuration.PerformanceMode &&
            Service.ActionReplacer is { } replacer &&
            replacer.PerfModeResolvedFor.TryGetValue(commandId, out var resolved) &&
            resolved != 0)
            target = resolved;

        // Pass 1: pulse the lowest visible slot whose adjusted action matches the
        // target action.
        if (TryPulse(hotbarModule, type, a3, a4,
                byCommandId: false, target))
            return true;

        // Pass 2 (fallback): the resolved action isn't on any visible bar, so pulse
        // the lowest visible copy of the raw button the user pressed — a press
        // always mirrors somewhere visible.
        return TryPulse(hotbarModule, type, a3, a4,
            byCommandId: true, commandId);
    }

    // Pulse the lowest-numbered VISIBLE bar slot that matches — by raw CommandId
    // (byCommandId: true) or by the game's native adjusted action. We DO NOT skip
    // the pressed slot; if it's the lowest match we pulse it and suppress the
    // game's default pulse, so there's exactly one highlight per press. Non-visible
    // bars (collapsed, off-screen, hidden by HUD layout) are skipped so the pulse
    // never lands somewhere the user can't see. Returns true when a pulse is issued.
    private static bool TryPulse(
        RaptureHotbarModule* hotbarModule,
        RaptureHotbarModule.HotbarSlotType type,
        ulong a3, int a4, bool byCommandId, uint target)
    {
        foreach (var barName in AllActionBars)
        {
            nint barPtr = Svc.GameGui.GetAddonByName(barName, 1);
            if (barPtr == nint.Zero)
                continue;

            var unitBase = (AtkUnitBase*)barPtr;
            if (unitBase->RootNode == null
                || (unitBase->RootNode->NodeFlags & NodeFlags.Visible) == 0)
                continue;

            var bar = (AddonActionBarBase*)barPtr;
            for (var i = 0U; i < bar->SlotCount; i++)
            {
                var barSlot = hotbarModule->GetSlotById(bar->RaptureHotbarId, i);
                if (barSlot->CommandType != type)
                    continue;

                var match = byCommandId
                    ? barSlot->CommandId == target
                    : GameAdjusted(type, barSlot->CommandId) == target;
                if (match)
                {
                    _pulseHook!.Original(bar, i, a3, a4);
                    return true;
                }
            }
        }

        return false;
    }
}
