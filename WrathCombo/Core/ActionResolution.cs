using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using WrathCombo.API.Enum;
using WrathCombo.CustomComboNS.Functions;
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

        // Keep the filtered set in sync with the current job / PvP state. The icon
        // hook normally rebuilds it, but that hook is disabled in Performance Mode,
        // so without this the tracker would keep resolving the PREVIOUS job's
        // combos after a job change (showing another job's actions).
        ActionReplacer.EnsureFilteredCombosCurrent();

        // FilteredCombos is scoped to the current job + PvP status, but only by
        // ROLE: because every preset's JobInfo.Role is derived from its job,
        // FilteredCombos also contains the OTHER jobs that share the player's role
        // (e.g. Bard and Machinist while on Dancer). That's harmless for the icon
        // hook — those combos never match the player's presses — but the tracker
        // picks the first enabled combo, so we must restrict to the player's own
        // job or it shows another job's skill.
        var combos = ActionReplacer.FilteredCombos;
        if (combos is null) return false;

        var job = Player.Job.GetUpgradedJob();

        // The base action this target type starts from (the first enabled combo
        // for THIS job that declares one, e.g. Cascade for ST).
        uint baseAction = 0;
        foreach (var combo in combos)
        {
            var data = combo.Preset.Attributes();
            if (data is null) continue;
            if (data.JobInfo?.Job != job) continue;
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
            if (combo.Preset.Attributes()?.JobInfo?.Job != job) continue;
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
    internal static bool? IsBurstHeld() => IsBurstHeld(WrathCombo.BurstPresetMap);

    /// <summary>1-minute (odd-minute) subset — see Commands.Burst1PresetMap.</summary>
    internal static bool? IsBurst1Held() => IsBurstHeld(WrathCombo.Burst1PresetMap);

    /// <inheritdoc cref="IsBurstHeld()" />
    internal static bool? IsBurstHeld(
        System.Collections.Generic.Dictionary<ECommons.ExcelServices.Job, Preset[]> map)
    {
        if (Player.Object == null) return null;
        if (!map.TryGetValue(Player.Job, out var presets)
            || presets.Length == 0)
            return null;

        return !PresetStorage.IsEnabled(presets[0]);
    }

    /// <summary>
    ///     Toggle burst (hold ↔ resume) for the current job. Callable off the
    ///     command path (e.g. the Stream Deck bridge); MUST run on the framework
    ///     thread. <paramref name="state" /> is the resulting state ("ARMED" /
    ///     "HELD"). Returns false if no burst presets exist for the current job.
    /// </summary>
    internal static bool ToggleBurst(out string state) =>
        ToggleBurst(WrathCombo.BurstPresetMap, out state);

    /// <summary>1-minute (odd-minute) subset — see Commands.Burst1PresetMap.</summary>
    internal static bool ToggleBurst1(out string state) =>
        ToggleBurst(WrathCombo.Burst1PresetMap, out state);

    /// <inheritdoc cref="ToggleBurst(out string)" />
    internal static bool ToggleBurst(
        System.Collections.Generic.Dictionary<ECommons.ExcelServices.Job, Preset[]> map,
        out string state)
    {
        state = "—";
        if (Player.Object == null)
            return false;
        if (!map.TryGetValue(Player.Job, out var presets)
            || presets.Length == 0)
            return false;

        var enable = !PresetStorage.IsEnabled(presets[0]);

        // Cross-group guard: while the FULL burst is held, toggling the 1-min
        // subset must not silently re-arm it (the modern version of the old
        // "second macro turns it back on" double-toggle bug). Resume the full
        // burst first; that re-arms everything in one intentional action.
        if (enable
            && ReferenceEquals(map, WrathCombo.Burst1PresetMap)
            && IsBurstHeld() == true)
        {
            ECommons.Logging.DuoLog.Warning(
                "1-min burst NOT re-armed: the full burst is held. Resume it first.");
            state = "HELD";
            return true;
        }

        foreach (var preset in presets)
        {
            if (enable)
                PresetStorage.EnablePreset(preset, Configuration.ConfigChangeSource.Command);
            else
                PresetStorage.DisablePreset(preset, Configuration.ConfigChangeSource.Command);
        }

        state = enable ? "ARMED" : "HELD";
        return true;
    }
}
