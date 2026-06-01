#region

using System;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
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

            // DIAGNOSTIC: confirm the hook actually resolved + enabled, so we
            // can tell "signature didn't match this game version" apart from
            // "hook is fine but never fires for controller input".
            Svc.Log.Information(
                $"[MyTweak][xhb-diag] PulseActionBarSlot hook installed={_pulseHook != null} " +
                $"enabled={_pulseHook?.IsEnabled} addr=0x{(_pulseHook?.Address ?? 0):X}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MyTweak][xhb-diag] ActionPressMirroring: FAILED to hook (signature?)");
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
        // DIAGNOSTIC (temporary): logs on every pulse, BEFORE the config gate,
        // so we can see whether the hook fires for controller presses, the gate
        // state, and which cross-hotbar addons are visible + their set ids.
        // Read via /xllog and filter for "xhb-diag". Removed once tuned.
        DumpCrossDiag(ab, slotIndex);

        if (Service.Configuration.DuplicateActionPresses
            && !Service.Configuration.MasterDisabled)
        {
            try
            {
                var mirrored = MirrorPulse(ab, slotIndex, a3, a4);

                // Additionally replicate the controller cross-hotbar SELECTION
                // onto a WXHB display surface (independent of the single pulse).
                MirrorCrossSetSelection(ab, slotIndex, a3, a4);

                if (mirrored)
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

    // Dance-step actions: Standard Step (15997), Technical Step (15998),
    // and the four dance steps shared by both finishers (15999-16002:
    // Emboite, Entrechat, Jete, Pirouette). Checked as both the raw
    // CommandId (button's base action) and the resolved id (after
    // GameAdjusted + WrathCombo combos).
    private static readonly uint[] DanceStepActions =
    [
        15997, 15998, 15999, 16000, 16001, 16002,
    ];

    // Status effect IDs for the player-side "currently dancing" buffs.
    // While either is active the mirror short-circuits entirely — pulse
    // stays on the pressed button, regardless of what the resolved action
    // looks like (WrathCombo's ActionReplacer can rewrite the resolved id
    // back to Cascade or the ST follow-up, so checking action ids alone
    // misses cases mid-dance).
    private const uint StandardStepStatus = 1818;
    private const uint TechnicalStepStatus = 1819;

    private static bool IsDancing()
    {
        var player = Player.Object;
        if (player == null) return false;
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == StandardStepStatus
                || status.StatusId == TechnicalStepStatus)
                return true;
        }
        return false;
    }

    // Find the lowest-numbered VISIBLE bar that holds a copy of the
    // resolved action. We DO NOT skip the pressed slot — if the pressed bar
    // is itself the lowest match, we pulse it manually and suppress the
    // game's default pulse so there's exactly one highlight per press.
    //
    // Non-visible bars (collapsed, off-screen, hidden by HUD layout) are
    // skipped so the pulse never lands somewhere the user can't see.
    //
    // Returns true if a pulse was issued, suppressing the default game
    // pulse on the pressed button.
    private static bool MirrorPulse(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);
        var type = pressedSlot->CommandType;

        var pressedResolved = ResolveActionId(type, pressedSlot->CommandId);

        if (IsDancing()) return false;
        if (type == RaptureHotbarModule.HotbarSlotType.Action
            && (Array.IndexOf(DanceStepActions, pressedSlot->CommandId) >= 0
                || Array.IndexOf(DanceStepActions, pressedResolved) >= 0))
            return false;

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

                if (GameAdjusted(type, barSlot->CommandId) == pressedResolved)
                {
                    _pulseHook!.Original(bar, i, a3, a4);
                    return true;
                }
            }
        }

        return false;
    }

    // --- Controller cross-hotbar (WXHB) selection mirror ---------------------
    //
    // On a controller, pressing an action SELECTS the cross-hotbar set that
    // holds it (a visible UI change) before the pulse. We replicate that
    // selection onto the expanded cross hotbar (WXHB: _ActionDoubleCrossL / R),
    // used as a display surface: point it at the set that contains the pressed
    // action so it shows + highlights that set, then pulse the matching slot.
    // The live single cross bar (_ActionCross) is intentionally never modified.
    //
    // The cross-hotbar sets occupy RaptureHotbar IDs 10..17 (8 sets of 16
    // buttons; the on-screen set number is id - 9). This is confirmed via the
    // game's own AddonActionCross logic (GetBarTarget returns 0xA..0x13, and the
    // active set is stored as an sbyte at AddonActionCross+0x254).
    //
    // PATCH NOTE: the WXHB derives its displayed set from controller input every
    // frame (AddonActionDoubleCrossBase.OnRequestedUpdate/Update), so a plain
    // RaptureHotbarId write may be reverted on some configs. If the set switch
    // doesn't visually stick in-game, this is the spot to add an explicit
    // OnRequestedUpdate/Update re-assert (offsets are build-specific; re-verify
    // each major patch). Everything here is guarded so a layout change degrades
    // to "no selection mirror" rather than a crash.
    private static readonly string[] WxhbBars =
    [
        "_ActionDoubleCrossL", "_ActionDoubleCrossR",
    ];

    private const uint FirstCrossSet = 10;
    private const uint LastCrossSet = 17;
    private const uint CrossSetSlotCount = 16;

    private static void MirrorCrossSetSelection(
        AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        if (hotbarModule == null) return;

        var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);
        if (pressedSlot == null) return;

        var type = pressedSlot->CommandType;
        var pressedResolved = ResolveActionId(type, pressedSlot->CommandId);

        // Same short-circuits as MirrorPulse: never mirror mid-dance / dance steps.
        if (IsDancing()) return;
        if (type == RaptureHotbarModule.HotbarSlotType.Action
            && (Array.IndexOf(DanceStepActions, pressedSlot->CommandId) >= 0
                || Array.IndexOf(DanceStepActions, pressedResolved) >= 0))
            return;

        if (!TryFindCrossSet(hotbarModule, type, pressedResolved,
                out var targetSet, out var targetSlot))
            return;

        foreach (var barName in WxhbBars)
        {
            nint barPtr = Svc.GameGui.GetAddonByName(barName, 1);
            if (barPtr == nint.Zero)
                continue;

            var unitBase = (AtkUnitBase*)barPtr;
            if (unitBase->RootNode == null
                || (unitBase->RootNode->NodeFlags & NodeFlags.Visible) == 0)
                continue;

            var bar = (AddonActionBarBase*)barPtr;

            // Point the WXHB display at the set containing the pressed action,
            // then pulse the matching slot. The game's per-frame UpdateHotbarSlot
            // repaints the slots from RaptureHotbarId, so the set visibly swaps.
            // RaptureHotbarId is a byte; targetSet is a cross set id (10..17).
            bar->RaptureHotbarId = (byte)targetSet;
            _pulseHook!.Original(bar, targetSlot, a3, a4);
        }
    }

    // DIAGNOSTIC (temporary): one throttled dump per press of the gate state,
    // the pressed slot, whether the action lives on a cross set, and the
    // present/visible/set-id of every known action-bar addon. Lets us discover
    // which addons host the user's visible cross-hotbar sets.
    private static void DumpCrossDiag(AddonActionBarBase* ab, uint slotIndex)
    {
        if (!EzThrottler.Throttle("xhbDiag", 750))
            return;

        try
        {
            var hotbarModule = RaptureHotbarModule.Instance();
            if (hotbarModule == null) return;

            var pressedSlot = hotbarModule->GetSlotById(ab->RaptureHotbarId, slotIndex);
            var type = pressedSlot == null
                ? default
                : pressedSlot->CommandType;
            var cmdId = pressedSlot == null ? 0u : pressedSlot->CommandId;
            var resolved = pressedSlot == null ? 0u : ResolveActionId(type, cmdId);

            Svc.Log.Information(
                $"[MyTweak][xhb-diag] PRESS dup={Service.Configuration.DuplicateActionPresses} " +
                $"master={Service.Configuration.MasterDisabled} pressRHID={ab->RaptureHotbarId} " +
                $"slot={slotIndex} type={type} cmdId={cmdId} resolved={resolved}");

            if (pressedSlot != null
                && TryFindCrossSet(hotbarModule, type, resolved, out var fset, out var fslot))
                Svc.Log.Information(
                    $"[MyTweak][xhb-diag] resolved action on cross set {fset} slot {fslot}");
            else
                Svc.Log.Information(
                    "[MyTweak][xhb-diag] resolved action NOT found on cross sets 10..17");

            foreach (var name in AllActionBars)
            {
                nint p = Svc.GameGui.GetAddonByName(name, 1);
                if (p == nint.Zero)
                {
                    Svc.Log.Information($"[MyTweak][xhb-diag]   {name}: absent");
                    continue;
                }

                var ub = (AtkUnitBase*)p;
                var vis = ub->RootNode != null
                          && (ub->RootNode->NodeFlags & NodeFlags.Visible) != 0;
                var b = (AddonActionBarBase*)p;
                Svc.Log.Information(
                    $"[MyTweak][xhb-diag]   {name}: visible={vis} RHID={b->RaptureHotbarId} slots={b->SlotCount}");
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MyTweak][xhb-diag] failed");
        }
    }

    // Scan the 8 cross-hotbar sets (RaptureHotbar IDs 10..17) for the slot that
    // holds the resolved action; returns the set + slot index within it.
    private static bool TryFindCrossSet(
        RaptureHotbarModule* hotbarModule,
        RaptureHotbarModule.HotbarSlotType type,
        uint resolved,
        out uint set, out uint slot)
    {
        set = 0;
        slot = 0;

        for (var s = FirstCrossSet; s <= LastCrossSet; s++)
        {
            for (var i = 0U; i < CrossSetSlotCount; i++)
            {
                var barSlot = hotbarModule->GetSlotById(s, i);
                if (barSlot == null)
                    continue;
                if (barSlot->CommandType != type)
                    continue;
                if (GameAdjusted(type, barSlot->CommandId) != resolved)
                    continue;

                set = s;
                slot = i;
                return true;
            }
        }

        return false;
    }
}
