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

        // Pass 1: pulse the lowest visible slot whose ADJUSTED action matches the
        // pressed slot's adjusted action. This is what makes each Dancer dance
        // step light up its own button: the step button's native adjustment is the
        // current step, so the highlight follows the dance. It does NOT chase
        // combo/proc resolutions, so a Cascade press never jumps to a separate
        // Fountain/Reverse Cascade slot.
        var pressedResolved = GameAdjusted(type, pressedSlot->CommandId);
        if (TryPulse(hotbarModule, type, a3, a4,
                byCommandId: false, pressedResolved))
            return true;

        // Pass 2 (fallback): if nothing matched by adjusted action, pulse the
        // lowest visible copy of the same raw button, so a press always mirrors.
        return TryPulse(hotbarModule, type, a3, a4,
            byCommandId: true, pressedSlot->CommandId);
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
