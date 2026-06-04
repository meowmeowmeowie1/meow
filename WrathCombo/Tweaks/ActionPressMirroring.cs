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

    // The animation parameters from the most recent native pulse, reused when we
    // emit a cast-time pulse (which has no pulse of its own to copy them from).
    private static ulong _lastA3;
    private static int _lastA4;
    // The raw action of the last Performance-Mode press we suppressed, used as the
    // fallback target when the cast-time resolved action isn't on a visible bar.
    private static uint _lastPressedCommandId;

    private static bool MirrorPulse(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);
        var type = pressedSlot->CommandType;
        var commandId = pressedSlot->CommandId;

        // Performance Mode: the action that actually fires is only known when the
        // cast is committed. Combos can resolve differently at press time than at
        // GCD roll — while queueing during animation lock the press-time state is
        // mid-GCD and resolves to the BASE action, even though the real cast at the
        // end of the GCD resolves correctly. Re-resolving here therefore drifts the
        // highlight onto the base button the longer you play. So we DON'T pulse at
        // press time: suppress the game's press flash and let
        // ActionWatching.SendActionDetour drive the highlight via PulseResolved when
        // the action is actually sent (with the final, correct resolution).
        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            Service.Configuration.PerformanceMode)
        {
            _lastA3 = a3;
            _lastA4 = a4;
            _lastPressedCommandId = commandId;
            return true;
        }

        // Normal mode (icon hook on) or a non-action slot: the pressed button
        // already shows the resolved icon, so mirror immediately — match the lowest
        // visible slot by adjusted action, with a raw-CommandId fallback.
        var target = GameAdjusted(type, commandId);
        if (TryPulse(hotbarModule, type, a3, a4, byCommandId: false, target))
            return true;

        return TryPulse(hotbarModule, type, a3, a4, byCommandId: true, commandId);
    }

    /// <summary>
    ///     Pulse the button that holds the action that was actually cast. Called
    ///     from <c>ActionWatching.SendActionDetour</c> at the moment the action is
    ///     sent in Performance Mode, so the highlight reflects the final resolved
    ///     action (not the press-time guess). Falls back to the pressed button when
    ///     the cast action isn't on a visible bar.
    /// </summary>
    internal static void PulseResolved(uint resolvedActionId)
    {
        if (!Service.Configuration.DuplicateActionPresses
            || Service.Configuration.MasterDisabled
            || !Service.Configuration.PerformanceMode
            || _pulseHook is null)
            return;

        try
        {
            var hotbarModule = RaptureHotbarModule.Instance();
            const RaptureHotbarModule.HotbarSlotType action =
                RaptureHotbarModule.HotbarSlotType.Action;

            if (TryPulse(hotbarModule, action, _lastA3, _lastA4,
                    byCommandId: false, resolvedActionId))
                return;

            TryPulse(hotbarModule, action, _lastA3, _lastA4,
                byCommandId: true, _lastPressedCommandId);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MyTweak] ActionPressMirroring.PulseResolved failed");
        }
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
