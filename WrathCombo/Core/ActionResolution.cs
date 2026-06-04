using ECommons.GameHelpers;
using ECommons.Throttlers;
using WrathCombo.API.Enum;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Services;

namespace WrathCombo.Core;

/// <summary>
///     Helpers for the <see cref="Window.NextActionTracker" /> window.<br />
///     Resolves the "next" action the current job's Single-Target and AoE
///     combos would produce right now (the same resolution the icon hook does,
///     but queried on demand), and reads the burst armed/held state.
/// </summary>
internal static class ActionResolution
{
    // Cached resolved ids, refreshed on a throttle so the per-frame Draw()
    // doesn't run TryInvoke every single frame.
    private static uint _stCached;
    private static uint _aoeCached;
    private static bool _stHas;
    private static bool _aoeHas;

    /// <summary>
    ///     Recomputes the cached ST/AoE next actions, throttled to ~100ms.
    ///     Call once per frame from the window; a no-op on throttled frames.
    /// </summary>
    internal static void Refresh()
    {
        if (!EzThrottler.Throttle("NextActionTracker", 100))
            return;

        _stHas = Resolve(ComboTargetTypeKeys.SingleTarget, out _stCached);
        _aoeHas = Resolve(ComboTargetTypeKeys.MultiTarget, out _aoeCached);
    }

    internal static bool TryGetSingleTarget(out uint action)
    {
        action = _stCached;
        return _stHas;
    }

    internal static bool TryGetAoE(out uint action)
    {
        action = _aoeCached;
        return _aoeHas;
    }

    /// <summary>
    ///     Finds the base action for the current job's combo of the given
    ///     <see cref="ComboTargetTypeKeys" /> (e.g. Cascade for ST, Windmill for
    ///     AoE), then resolves it through the full combo chain the same way a
    ///     button press does, and returns the action it would produce right now.
    /// </summary>
    private static bool Resolve(ComboTargetTypeKeys target, out uint resolved)
    {
        resolved = 0;
        if (Player.Object is null) return false;

        // FilteredCombos is already restricted to the current job + PvP status
        // and ordered by preset priority.
        var combos = ActionReplacer.FilteredCombos;
        if (combos is null) return false;

        // The base action this target type starts from (the first enabled combo
        // that declares one, e.g. Cascade for ST).
        uint baseAction = 0;
        foreach (var combo in combos)
        {
            var data = combo.Preset.Attributes();
            if (data is null) continue;
            if (data.TargetType != target) continue;
            if (data.ReplaceSkill is not { ActionIDs.Count: > 0 }) continue;
            if (!PresetStorage.IsEnabled(combo.Preset)) continue;

            baseAction = data.ReplaceSkill.ActionIDs[0];
            break;
        }
        if (baseAction == 0) return false;

        // Resolve it through the WHOLE combo chain exactly like a real press
        // (see ActionWatching.UseActionDetour's Performance-Mode block): the
        // first combo that actually changes the action wins. Invoking only the
        // single target-typed combo (as before) missed procs/combo-steps handled
        // by other presets, leaving the tracker stuck on the base action.
        resolved = baseAction;
        foreach (var combo in combos)
        {
            try
            {
                if (combo.TryInvoke(baseAction, out var r) && r != 0)
                {
                    resolved = r;
                    break;
                }
            }
            catch
            {
                // Ignore a misbehaving combo and keep checking the rest.
            }
        }

        return true;
    }

    internal static string ActionName(uint id) =>
        ActionWatching.ActionSheet.TryGetValue(id, out var a)
            ? a.Name.ToString()
            : id.ToString();

    internal static ushort ActionIcon(uint id) =>
        ActionWatching.ActionSheet.TryGetValue(id, out var a) ? a.Icon : (ushort)0;

    /// <summary>
    ///     Whether burst presets for the current job are currently HELD
    ///     (i.e. the first burst preset is disabled).<br />
    ///     <c>null</c> = no player, or no burst presets defined for the job.
    /// </summary>
    /// <remarks>
    ///     Read by <see cref="Window.NextActionTracker" /> to show burst state.
    /// </remarks>
    internal static bool? IsBurstHeld()
    {
        if (Player.Object == null) return null;
        if (!WrathCombo.BurstPresetMap.TryGetValue(Player.Job, out var presets)
            || presets.Length == 0)
            return null;

        return !PresetStorage.IsEnabled(presets[0]);
    }
}
