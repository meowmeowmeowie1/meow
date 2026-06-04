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
    ///     Finds the enabled combo for the current job whose
    ///     <see cref="ComboTargetTypeKeys" /> matches <paramref name="target" />,
    ///     resolves its base action through that combo, and returns the action
    ///     it would produce right now. Falls back to the base action when the
    ///     combo doesn't change it.
    /// </summary>
    private static bool Resolve(ComboTargetTypeKeys target, out uint resolved)
    {
        resolved = 0;
        if (Player.Object is null) return false;

        // FilteredCombos is already restricted to the current job + PvP status
        // and ordered by preset priority, so the first enabled match is the
        // job's active rotation combo.
        var combos = ActionReplacer.FilteredCombos;
        if (combos is null) return false;

        foreach (var combo in combos)
        {
            var data = combo.Preset.Attributes();
            if (data is null) continue;
            if (data.TargetType != target) continue;
            if (data.ReplaceSkill is not { ActionIDs.Count: > 0 }) continue;
            if (!PresetStorage.IsEnabled(combo.Preset)) continue;

            var baseAction = data.ReplaceSkill.ActionIDs[0];
            try
            {
                resolved = combo.TryInvoke(baseAction, out var r) && r != 0
                    ? r
                    : baseAction;
            }
            catch
            {
                resolved = baseAction;
            }

            return true;
        }

        return false;
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
