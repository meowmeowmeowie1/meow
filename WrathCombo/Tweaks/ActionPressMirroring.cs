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

    private static uint GameAdjusted(RaptureHotbarModule.HotbarSlotType type, uint id) =>
        type == RaptureHotbarModule.HotbarSlotType.Action
            ? ActionManager.Instance()->GetAdjustedActionId(id)
            : id;

    private static uint ResolveActionId(RaptureHotbarModule.HotbarSlotType type, uint id)
    {
        var resolved = GameAdjusted(type, id);

        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            Service.Configuration.PerformanceMode &&
            global::WrathCombo.Core.ActionReplacer.FilteredCombos is { } combos)
        {
            foreach (var combo in combos)
                if (combo.TryInvoke(resolved, out var comboResolved))
                {
                    resolved = comboResolved;
                    break;
                }
        }

        return resolved;
    }

    private static bool MirrorPulse(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);
        var type = pressedSlot->CommandType;
        var pressedCommandId = pressedSlot->CommandId;

        var pressedResolved = ResolveActionId(type, pressedCommandId);

        // Pass 1: pulse the lowest visible copy whose game-adjusted action
        // matches the pressed action's resolved id. This is the normal path and
        // the only one that hits while the icon hook is active.
        if (TryPulse(hotbarModule, type, a3, a4, byCommandId: false, pressedResolved))
            return true;

        // Pass 2 (fallback): under Performance Mode the icon hook is disabled, so
        // GameAdjusted returns the un-combo'd native id and a combo'd or
        // dance-step press never matches above. Pulse the lowest visible copy of
        // the *same button* instead, so every press still mirrors — including
        // Dancer steps, which live on the Cascade/Fountain buttons.
        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            TryPulse(hotbarModule, type, a3, a4, byCommandId: true, pressedCommandId))
            return true;

        return false;
    }

    // Pulse the lowest-numbered VISIBLE bar slot that matches — by raw CommandId
    // (byCommandId: true) or by game-adjusted action id. We DO NOT skip the
    // pressed slot; if it's the lowest match we pulse it and suppress the game's
    // default pulse, so there's exactly one highlight per press. Non-visible bars
    // (collapsed, off-screen, hidden by HUD layout) are skipped so the pulse
    // never lands somewhere the user can't see. Returns true when a pulse is
    // issued.
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
