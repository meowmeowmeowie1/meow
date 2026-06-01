#region

using System;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using WrathCombo.Data;
using WrathCombo.Services;

#endregion

namespace WrathCombo.Tweaks;

/// <summary>
///     "Duplicate Action Presses Between Hotbars": when the player commits an
///     action, flash the button-press pulse on every visible hotbar copy of
///     that action.
/// </summary>
/// <remarks>
///     Driven off <see cref="ActionWatching.OnActionSend" /> (the SendAction
///     path), which fires for ALL input devices — keyboard/mouse, controller
///     cross hotbar, and macros alike. An earlier version hooked
///     <c>PulseActionBarSlot</c> directly, but that hook never fires for
///     controller input, so the feature did nothing for controller users.
///     Pulsing is done via the FFXIVClientStructs
///     <see cref="AddonActionBarBase.PulseActionBarSlot(int)" /> helper, so no
///     hook of our own is required.
/// </remarks>
internal static unsafe class ActionPressMirroring
{
    private static readonly string[] AllActionBars =
    [
        "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03",
        "_ActionBar04", "_ActionBar05", "_ActionBar06", "_ActionBar07",
        "_ActionBar08", "_ActionBar09", "_ActionCross",
        "_ActionDoubleCrossL", "_ActionDoubleCrossR", "_ActionBarEx",
    ];

    internal static void Enable()
    {
        ActionWatching.OnActionSend += OnActionSent;
    }

    internal static void Dispose()
    {
        ActionWatching.OnActionSend -= OnActionSent;
    }

    private static uint GameAdjusted(uint id) =>
        ActionManager.Instance()->GetAdjustedActionId(id);

    // Fires whenever the player commits an action (any input device). We flash
    // that action on every visible hotbar copy of it.
    private static void OnActionSent()
    {
        if (!Service.Configuration.DuplicateActionPresses
            || Service.Configuration.MasterDisabled)
            return;

        try
        {
            var actionId = ActionWatching.LastSentActionId;
            if (actionId == 0)
                return;

            MirrorToAllBars(actionId);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MyTweak] ActionPressMirroring failed");
        }
    }

    // Pulse every visible hotbar slot (across all bars, including the controller
    // cross hotbars) whose game-adjusted action matches the action just used.
    private static void MirrorToAllBars(uint sentActionId)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        if (hotbarModule == null)
            return;

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
                var slot = hotbarModule->GetSlotById(bar->RaptureHotbarId, i);
                if (slot == null
                    || slot->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
                    continue;

                if (GameAdjusted(slot->CommandId) != sentActionId)
                    continue;

                bar->PulseActionBarSlot((int)i);
            }
        }
    }
}
