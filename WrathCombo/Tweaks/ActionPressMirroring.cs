#region

using System;
using Dalamud.Hooking;
using ECommons.DalamudServices;
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

    private static bool MirrorPulse(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);

        // Mirror the button the user actually pressed by matching its raw
        // CommandId. We deliberately do NOT match by the resolved/adjusted action:
        // doing so jumps the pulse to whichever slot happens to hold the resolved
        // action (a separate Reverse Cascade button, or the Standard Step button
        // when a dance step resolves onto Cascade/Fountain), landing the highlight
        // on a slot the user never touched. CommandId is stable across combos,
        // procs, and dance-step swaps in BOTH Performance and normal mode, so the
        // pulse always lands on a visible copy of the pressed button.
        return TryPulse(hotbarModule, pressedSlot->CommandType, a3, a4,
            pressedSlot->CommandId);
    }

    // Pulse the lowest-numbered VISIBLE bar slot that holds the same button
    // (matched by CommandType + raw CommandId). We DO NOT skip the pressed slot;
    // if it's the lowest match we pulse it and suppress the game's default pulse,
    // so there's exactly one highlight per press. Non-visible bars (collapsed,
    // off-screen, hidden by HUD layout) are skipped so the pulse never lands
    // somewhere the user can't see. Returns true when a pulse is issued.
    private static bool TryPulse(
        RaptureHotbarModule* hotbarModule,
        RaptureHotbarModule.HotbarSlotType type,
        ulong a3, int a4, uint target)
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

                if (barSlot->CommandId == target)
                {
                    _pulseHook!.Original(bar, i, a3, a4);
                    return true;
                }
            }
        }

        return false;
    }
}
