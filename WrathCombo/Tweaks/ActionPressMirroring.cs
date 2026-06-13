#region

using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using WrathCombo.Core;
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

    // In-game "Hotbar 9" addon. Identified by NAME (not RaptureHotbarId) so we
    // can't accidentally silence a different bar. Slots on this bar never pulse:
    // not natively when pressed, and not as a mirror target for other bars.
    private const string SilentBar = "_ActionBar08";

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
        // Keep Hotbar 9 silent: swallow the pulse for any of its own slots so
        // they never flash (mits, anti-knockback, food/pots, etc.). Matched by
        // addon pointer so only that exact bar is affected.
        if (Svc.GameGui.GetAddonByName(SilentBar, 1) == (nint)ab)
            return;

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

    // Last resolved action per pressed button, refreshed on a throttle. Resolving
    // walks the whole combo set, and the game can fire PulseActionBarSlot many
    // times a second (per animation frame, and per visible copy of a slot), so
    // resolving on every pulse bogged the game down. Cache the result per button
    // and only re-resolve ~10x/sec — the flash still fires on every pulse, only the
    // (which-button) lookup is throttled, which is plenty to track the rotation.
    private static readonly Dictionary<uint, uint> _resolveCache = new();

    private static uint ResolveThroughCombosThrottled(uint commandId)
    {
        if (EzThrottler.Throttle("MyTweakMirrorResolve" + commandId, 100)
            || !_resolveCache.TryGetValue(commandId, out var resolved))
        {
            resolved = ResolveThroughCombos(commandId);
            _resolveCache[commandId] = resolved;
        }

        return resolved;
    }

    // Resolve an action through the current job's combos — the same pure call the
    // icon hook uses to display combos every frame (TryInvoke self-filters by job
    // and has no side effects). Returns the action the press would fire right now,
    // or the input unchanged if no combo applies.
    private static uint ResolveThroughCombos(uint actionId)
    {
        ActionReplacer.EnsureFilteredCombosCurrent();

        var combos = ActionReplacer.FilteredCombos;
        if (combos is null)
            return actionId;

        foreach (var combo in combos)
        {
            try
            {
                if (combo.TryInvoke(actionId, out var r) && r != 0)
                    return r;
            }
            catch
            {
                // Ignore a misbehaving combo and keep checking the rest.
            }
        }

        return actionId;
    }

    private static bool MirrorPulse(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);
        var type = pressedSlot->CommandType;
        var commandId = pressedSlot->CommandId;

        // Flash on EVERY press (responsive, like the vanilla game). Default target
        // is the pressed button's own adjusted action. In Performance Mode the icon
        // hook is off, so the pressed GCD shows its base icon; resolve the press
        // through the combos so the flash follows the action the rotation will fire
        // (a dance step, a proc, Saber Dance, ...) when that resolution is available.
        // This is best-effort: while queueing mid-GCD the combo can resolve to the
        // base action, in which case the base button flashes — that's the accepted
        // trade for an instant flash on every press.
        var target = GameAdjusted(type, commandId);
        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            Service.Configuration.PerformanceMode)
        {
            var resolved = ResolveThroughCombosThrottled(commandId);
            if (resolved != 0)
                target = resolved;
        }

        // Pass 1: flash the lowest visible slot whose adjusted action matches the
        // target. Pass 2 (fallback): the resolved action isn't on a visible bar, so
        // flash the raw button the user pressed — a press always mirrors somewhere.
        if (TryPulse(hotbarModule, type, a3, a4, byCommandId: false, target))
            return true;

        return TryPulse(hotbarModule, type, a3, a4, byCommandId: true, commandId);
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
            if (barName == SilentBar) // Hotbar 9 never pulses.
                continue;

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
