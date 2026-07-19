#region

using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Logging;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using WrathCombo.API.Enum;
using WrathCombo.Attributes;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
using WrathCombo.Data;
using WrathCombo.Data.Conflicts;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Window;
using WrathCombo.Window.Tabs;
using static ECommons.ExcelServices.ExcelJobHelper;
using static WrathCombo.Core.Configuration;

#endregion

namespace WrathCombo;

public partial class WrathCombo
{
    private const string Command = "/mytweak";
    private const string OldCommand = "/qoltweaks";
    private const string LegacyCommand = "/ccombo";
    private const string LegacyCommand2 = "/customcombo";
    private const string TweakToggleCommand = "/tweak";

    // Per-job burst preset codes from user's existing job-titled macros.
    // /wrath toggle <code> targets these specific Preset enum values by id.
    internal static readonly Dictionary<Job, Preset[]> BurstPresetMap = new()
    {
        { Job.PLD, [(Preset)11003, (Preset)11016, (Preset)11010, (Preset)11019] },
        { Job.WAR, [(Preset)18003, (Preset)18019, (Preset)18007, (Preset)18018] },
        { Job.DRK, [(Preset)5015, (Preset)5054, (Preset)5016, (Preset)5055, (Preset)5018, (Preset)5057] },
        { Job.GNB, [(Preset)7008, (Preset)7201, (Preset)7011, (Preset)7204] },
        { Job.WHM, [(Preset)19008, (Preset)19195] },
        { Job.SCH, [(Preset)16003, (Preset)16054] },
        { Job.AST, [(Preset)1043, (Preset)1016] },
        { Job.SGE, [(Preset)14051, (Preset)14010, (Preset)14008, (Preset)14005] },
        { Job.DRG, [(Preset)6103, (Preset)6104, (Preset)6203, (Preset)6204, (Preset)6107, (Preset)6207, (Preset)6106, (Preset)6206] },
        { Job.MNK, [(Preset)9009, (Preset)9030, (Preset)9032, (Preset)9011] },
        { Job.NIN, [(Preset)10006, (Preset)10007, (Preset)10022, (Preset)10023] },
        { Job.SAM, [(Preset)15012, (Preset)15108, (Preset)15018, (Preset)15114] },
        { Job.RPR, [(Preset)12009, (Preset)12108, (Preset)12006, (Preset)12105] },
        { Job.VPR, [(Preset)30005, (Preset)30011, (Preset)30104, (Preset)30110, (Preset)30112] },
        { Job.BRD, [(Preset)3017, (Preset)3032] },
        // 8102/8112 were stale ids from an old enum revision (they no longer
        // exist); today's equivalents: Hypercharge = 8105, Stabilizer FMF = 8111.
        { Job.MCH, [(Preset)8110, (Preset)8108, (Preset)8107, (Preset)8103, (Preset)8105, (Preset)8111] },
        { Job.DNC, [(Preset)4018, (Preset)4045] },
        { Job.BLM, [(Preset)2103, (Preset)2202, (Preset)2102, (Preset)2201] },
        { Job.SMN, [(Preset)17053, (Preset)17017, (Preset)17020, (Preset)17061] },
        { Job.RDM, [(Preset)13010, (Preset)13207, (Preset)13011, (Preset)13208] },
        { Job.PCT, [(Preset)20021, (Preset)20054, (Preset)20027, (Preset)20060] },
    };

    // The ~60s-cooldown ("odd minute") subset of BurstPresetMap, so the 1-minute
    // burst can be held on its own while the 2-minute pieces keep flowing.
    // Jobs whose burst package is entirely 2-minute cooldowns are absent.
    internal static readonly Dictionary<Job, Preset[]> Burst1PresetMap = new()
    {
        { Job.PLD, [(Preset)11003, (Preset)11016, (Preset)11010, (Preset)11019] }, // FoF + Requiescat (all 60s)
        { Job.WAR, [(Preset)18003, (Preset)18019, (Preset)18007, (Preset)18018] }, // Inner Release + Infuriate (all 60s)
        { Job.DRK, [(Preset)5015, (Preset)5054, (Preset)5018, (Preset)5057] },     // Delirium + Shadowbringer
        { Job.GNB, [(Preset)7008, (Preset)7201] },                                 // No Mercy
        { Job.SGE, [(Preset)14008, (Preset)14051] },                               // Psyche
        { Job.DRG, [(Preset)6104, (Preset)6204, (Preset)6106, (Preset)6206] },     // Lance Charge + Life Surge
        { Job.MNK, [(Preset)9011, (Preset)9032] },                                 // Riddle of Fire
        { Job.NIN, [(Preset)10006, (Preset)10022] },                               // Trick Attack / Kunai's Bane
        { Job.SAM, [(Preset)15018, (Preset)15114] },                               // Meikyo Shisui
        { Job.RPR, [(Preset)12009, (Preset)12108] },                               // Gluttony
        { Job.VPR, [(Preset)30011, (Preset)30110, (Preset)30112] },                // Reawaken
        { Job.MCH, [(Preset)8103, (Preset)8107, (Preset)8105, (Preset)8303] },     // Reassemble + Queen + Hypercharge (ST/AoE)
        { Job.PCT, [(Preset)20027, (Preset)20060] },                               // Hammer combo
    };

    /// <summary>
    ///     Registers the base commands for the plugin.<br />
    ///     Also displays the biggest commands in Dalamud.
    /// </summary>
    private void RegisterCommands()
    {
        EzCmd.Add(Command, OnCommand,
            "Open the MyTweak settings window.\n" +
            $"{Command} disable | enable → Master kill-switch for combos + mirror.\n" +
            $"{Command} burst hold | resume → Hold/resume burst presets for current job.\n" +
            $"{Command} burst1 hold | resume → Same, but only the ~60s (odd-minute) subset.\n" +
            $"{Command} tracker show | hide → Toggle the next-action tracker window.\n" +
            $"{Command} tracker reset → Un-hide and re-center it if you lost it.\n" +
            $"{Command} debug → Dumps a debug log onto your desktop.\n" +
            $"{OldCommand} → Short alias, still works!");
        EzCmd.Add(OldCommand, OnCommand);
        EzCmd.Add(LegacyCommand, OnCommand);
        EzCmd.Add(LegacyCommand2, OnCommand);
        EzCmd.Add(TweakToggleCommand, OnTweakToggleCommand,
            "Toggle MyTweak's master kill-switch (silent).");
    }

    /// <summary>
    ///     Handles the command input, and calls the appropriate method.
    /// </summary>
    /// <param name="command">
    ///     Irrelevant, as we handle all commands the same.<br />
    ///     Required for the command handler.
    /// </param>
    /// <param name="arguments">
    ///     The arguments provided with the command.<br />
    ///     Generally treated as:<br />
    ///     The first argument is the command to execute, and the second is the
    ///     argument for the command.<br />
    ///     If the command is not recognized, the
    ///     <see cref="HandleOpenCommand">Open Command</see> is assumed, to handle
    ///     opening to a specific job.
    /// </param>
    private void OnCommand(string command, string arguments)
    {
        var argumentParts = arguments.ToLowerInvariant().Split();
        switch (argumentParts[0])
        {
            case "burst":
                HandleBurstControl(argumentParts, BurstPresetMap, "burst"); break;

            case "burst1":
                HandleBurstControl(argumentParts, Burst1PresetMap, "1-minute burst"); break;

            case "disable":
            case "enable":
            case "off":
            case "on":
                HandleMasterToggle(argumentParts); break;

            case "tracker":
                HandleNextTrackerToggle(argumentParts); break;

            case "unsetall":
            case "set":
            case "toggle":
            case "unset":
                HandleSetCommands(argumentParts); break;

            case "list":
            case "enabled":
            case "disabled": // unlisted
                HandleListCommands(argumentParts); break;

            case "combo":
                HandleComboCommands(argumentParts); break;

            case "ignore":
                HandleIgnoreCommand(); break;

            case "debug":
                HandleDebugCommands(argumentParts); break;

            case "settings":
            case "config": // unlisted
                HandleOpenCommand(tab: OpenWindow.Settings, forceOpen: true); break;

            case "pve":
                HandleOpenCommand(tab: OpenWindow.PvE, forceOpen: true); break;

            case "pvp":
                HandleOpenCommand(tab: OpenWindow.PvP, forceOpen: true); break;

            case "dbg": // unlisted
            case "debugtab": // unlisted
                HandleOpenCommand(tab: OpenWindow.Debug, forceOpen: true); break;

            // "IPromiseIWillDoMyJobQuestsLater" will be accepted
            // ReSharper disable once StringLiteralTypo
            case "ipromiseiwilldomyjobquestslater": // unlisted
                HandleJobStoneCheckCommand(); break;

            case "opener":
                OutputOpenerStatus(); break;
            default:
                HandleOpenCommand(argumentParts); break;
        }
    }

    private void OutputOpenerStatus()
    {
        Svc.Log.Debug($"{WrathOpener.CurrentOpener.Enabled}");
        if (WrathOpener.CurrentOpener is not null && WrathOpener.CurrentOpener != WrathOpener.Dummy && WrathOpener.CurrentOpener.Enabled)
        {
            var status = WrathOpener.OpenerStatus();
            DuoLog.Information($"Opener status: {status}");
        }
        else
            DuoLog.Warning("No valid opener active.");
    }

    /// <summary>
    ///     Handles the set command, which toggles, sets, or unsets presets.
    /// </summary>
    /// <value>
    ///     <c>&lt;blank&gt;</c> - list valid arguments<br />
    ///     <c>toggle</c> - toggle preset, requires another argument<br />
    ///     <c>set</c> - enable preset, requires another argument<br />
    ///     <c>unset</c> - disable preset, requires another argument<br />
    ///     <c>unsetall</c> - disable all presets
    /// </value>
    /// <param name="argument">
    ///     The action to take on the preset, then (if not "unset"), the preset to
    ///     act on. The preset can be provided as the internal name or the ID.
    /// </param>
    /// <remarks>
    ///     Will not allow the command to be used in combat.
    /// </remarks>
    private void HandleSetCommands(string[] argument)
    {
        #region Variable Setup

        const string toggle = "toggle";
        const string set = "set";
        const string unset = "unset";

        const string all = "all";

        string? action;
        string? target = null;

        Preset? preset = null;

        #endregion

        /*
        #if !DEBUG
        if (Player.Available && CustomComboFunctions.InCombat())
        {
            DuoLog.Error("Cannot use this command in combat");
            return;
        }
        #endif
        */

        // Parse the action
        switch (argument[0])
        {
            case "unsetall":
                action = unset;
                target = all;
                break;

            case "set":
                action = set;
                break;

            case "toggle":
                action = toggle;
                break;

            case "unset":
                action = unset;
                break;

            default:
                DuoLog.Error("Available set actions: toggle, set, unset, unsetall");
                return;
        }

        if (target is null && argument.Length < 2)
        {
            DuoLog.Error($"Please specify a feature to {action}");
            return;
        }

        // Parse the target feature
        target ??= argument[1];
        if (target != all)
        {
            var presetCanNumber = int.TryParse(target, out var targetNumber);
            try
            {
                preset = presetCanNumber
                    ? (Preset)targetNumber
                    : Enum.Parse<Preset>(target, true);
            }
            catch
            {
                DuoLog.Error($"Could not find preset '{target}'");
                return;
            }
        }

        // Give the correct method for the action
        Func<Preset, ConfigChangeSource?, bool> method = action switch
        {
            toggle => PresetStorage.TogglePreset,
            set => PresetStorage.EnablePreset,
            unset => PresetStorage.DisablePreset,
            _ => throw new ArgumentOutOfRangeException(nameof(argument), action,
                null),
        };

        // Execute the method
        if (target == all)
        {
            Service.Configuration.EnabledActions.Clear();
            DuoLog.Information("All unset");
        }
        else
        {
            var usablePreset = (Preset)preset!;
            method(usablePreset, ConfigChangeSource.Command);

            if (action == toggle)
                action =
                    Service.Configuration.EnabledActions
                        .TryGetValue(usablePreset, out _)
                        ? set
                        : unset;

            var ctrlText = P.UIHelper.PresetControlled(usablePreset) is not null
                ? " " + OptionControlledByIPC
                : "";

            if (!Service.Configuration.SuppressSetCommands && ctrlText == "")
                DuoLog.Information(
                    $"{usablePreset.Attributes().Name} {action} {ctrlText}");
        }
    }

    /// <summary>
    ///     Handles the list command, which lists all available presets.
    /// </summary>
    /// <value>
    ///     <c>&lt;blank&gt;</c> - list valid arguments<br />
    ///     <c>set</c> - enabled presets<br />
    ///     <c>enabled</c> - enabled presets<br />
    ///     <c>unset</c> - disabled presets<br />
    ///     <c>disabled</c> - disabled presets (unlisted command)<br />
    ///     <c>all</c> - all presets
    /// </value>
    /// <param name="argument">
    ///     The filter to apply to the list.<br />
    ///     If no argument is provided, all presets are listed.
    /// </param>
    private void HandleListCommands(string[] argument)
    {
        IEnumerable<Preset> presets = PresetStorage.AllPresets
            .Where(kvp => !kvp.Value.IsHidden)
            .Select(kvp => kvp.Key);
        const StringComparison lower = StringComparison.InvariantCultureIgnoreCase;
        var filter =
            argument.Length > 1 && argument[0].Trim().Equals("list", lower)
                ? argument[1]
                : argument[0];
        var job = argument.Length > 2
            ? argument[2]
            : argument.Length > 1 && !argument[0].Trim().Equals("list", lower)
                ? argument[1]
                : null;
        PluginLog.Debug($"Filter: {filter}, " +
                        $"Job: {job}");

        switch (filter)
        {
            case "enabled":
            case "set":
                presets = presets.Where(preset =>
                    IPC.GetComboState(preset.ToString())?.First().Value ?? false
                );

                presets = FilterPresetsToJob(presets, job);
                if (!presets.Any())
                    return;

                foreach (var preset in presets)
                {
                    var controlled =
                        P.UIHelper.PresetControlled(preset) is not null;
                    var ctrlText = controlled ? " " + OptionControlledByIPC : "";
                    DuoLog.Information($"{(int)preset} - {preset}{ctrlText}");
                }

                break;

            case "disabled":
            case "unset":
                presets = presets.Where(preset =>
                    !IPC.GetComboState(preset.ToString())!.First().Value);

                presets = FilterPresetsToJob(presets, job);
                if (!presets.Any())
                    return;

                foreach (var preset in presets)
                {
                    var controlled =
                        P.UIHelper.PresetControlled(preset) is not null;
                    var ctrlText = controlled ? " " + OptionControlledByIPC : "";
                    DuoLog.Information($"{(int)preset} - {preset}{ctrlText}");
                }

                break;

            case "all":
                presets = FilterPresetsToJob(presets, job);
                if (!presets.Any())
                    return;

                foreach (var preset in presets)
                {
                    var controlled =
                        P.UIHelper.PresetControlled(preset) is not null;
                    var ctrlText = controlled ? " " + OptionControlledByIPC : "";
                    DuoLog.Information($"{(int)preset} - {preset}{ctrlText}");
                }

                break;

            default:
                DuoLog.Error("Available list filters: set, unset, all");
                break;
        }

        return;

        Preset[] FilterPresetsToJob
            (IEnumerable<Preset> presetsList, string? jobShort)
        {
            if (jobShort is not null)
            {
                presetsList = presetsList.Where(preset =>
                    preset.Attributes().JobInfo.JobShorthand
                        .Equals(jobShort, lower));
            }

            presetsList = presetsList.ToArray();
            if (presetsList.Any()) return presetsList.ToArray();

            if (jobShort is not null)
                DuoLog.Error($"{jobShort} is not a correct job abbreviation," +
                             $" or has nothing to list.");
            else
                DuoLog.Error($"Nothing is disabled.");
            return [];

        }
    }

    /// <summary>
    ///     Handles the combo command, the replacing of actions.
    /// </summary>
    /// <value>
    ///     <c>&lt;blank&gt;</c> - toggle<br />
    ///     <c>on</c> - enable<br />
    ///     <c>off</c> - disable<br />
    ///     <c>toggle</c> - toggle
    /// </value>
    /// <param name="argument">
    ///     The way to change the combo setting.<br />
    ///     If no argument is provided, the setting is toggled.
    /// </param>
    private void HandleComboCommands(string[] argument)
    {
        if (argument.Length < 2)
        {
            Service.Configuration.SetActionChanging(
                !Service.Configuration.ActionChanging);
            DuoLog.Information(
                "Action Replacing set to "
                + (Service.Configuration.ActionChanging ? "ON" : "OFF"));
            return;
        }

        switch (argument[1])
        {
            case "on":
                Service.Configuration.SetActionChanging(true);
                break;

            case "off":
                Service.Configuration.SetActionChanging(false);
                break;

            case "toggle":
                Service.Configuration.SetActionChanging(
                    !Service.Configuration.ActionChanging);
                break;

            default:
                DuoLog.Error("Available combo options: on, off, toggle");
                return;
        }

        DuoLog.Information(
            "Action Replacing set to "
            + (Service.Configuration.ActionChanging ? "ON" : "OFF"));
    }


    /// <summary>
    ///     Handles the ignore command.
    /// </summary>
    /// <value>
    ///     <c>&lt;blank&gt;</c> - add target<br />
    /// </value>
    /// <remarks>
    ///     Requires a target to be selected, and the target to be hostile.
    /// </remarks>
    private void HandleIgnoreCommand()
    {
        var target = Svc.Targets.Target;

        if (target == null)
        {
            DuoLog.Error("No target selected");
            return;
        }

        if (!target.IsHostile())
        {
            DuoLog.Error("No valid target selected");
            return;
        }

        if (Service.Configuration.IgnoredNPCs.Any(x => x.Key == target.BaseId))
        {
            DuoLog.Error(
                $"{target.Name} (ID: {target.BaseId}) is already on the ignored list");
            return;
        }

        if (Service.Configuration.IgnoredNPCs.All(x => x.Key != target.BaseId))
        {
            Service.Configuration.IgnoredNPCs.Add(target.BaseId, target.GetNameId());
            Service.Configuration.Save();

            DuoLog.Information(
                $"Successfully added {target.Name} (ID: {target.BaseId}) to ignored list");
        }
    }

    /// <summary>
    ///     Handles the debug command, which calls
    ///     <see cref="DebugFile.MakeDebugFile" />.
    /// </summary>
    /// <value>
    ///     <c>&lt;blank&gt;</c> - current job<br />
    ///     <c>&lt;job abbr&gt;</c> - that job<br />
    ///     <c>all</c> - all jobs<br />
    ///     <c>path</c> - prints the path to the debug file<br />
    ///     <c>string</c> - puts the debug string on the clipboard<br />
    /// </value>
    /// <param name="argument">
    ///     The job abbreviation to provide the debug file for (or "all").<br />
    ///     If no argument is provided, the current job is used.
    /// </param>
    private void HandleDebugCommands(string[] argument)
    {
        try
        {
            ClassJob? job = null;

            // Handle an entered job abbreviation
            if (argument.Length > 1)
            {
                if (argument[1] == "path")
                {
                    DuoLog.Information(
                        $"MyTweakDebug.txt should have been created at:\n" +
                        $"{DebugFile.GetDebugFilePath()}");
                    return;
                }

                if (argument[1] == "string")
                {
                    GenericHelpers.Copy(
                        "```\n" + DebugFile.GetDebugCode() + "\n```");
                    DuoLog.Information(
                        "Debug string copied to clipboard. Paste this where requested.");
                    return;
                }

                if (argument[1].Length != 3)
                {
                    DuoLog.Error("Invalid job abbreviation");
                    throw new ArgumentException("Invalid job abbreviation");
                }

                if (argument[1] == "all")
                {
                    DebugFile.MakeDebugFile(allJobs: true);
                    return;
                }

                var jobAbbr = argument[1].ToUpperInvariant();
                try
                {
                    // Look up the entered job
                    if (TryGetJobByAbbreviation(jobAbbr, out ClassJob jobSearch))
                    {
                        //ClassJob -> enum,
                        //Check if Class and change to Job
                        //Retrieve final ClassJob
                        job = jobSearch.GetJob().GetUpgradedJob().GetData();

                        if (job.Value.RowId != Player.ClassJob.RowId)
                            DuoLog.Warning($"You are not on {job.Value.Name()}");
                    }
                }
                // the .first() failed
                catch (InvalidOperationException)
                {
                    DuoLog.Error($"Invalid job abbreviation, '{jobAbbr}'");
                    throw;
                }
                // unknown
                catch (Exception ex)
                {
                    DuoLog.Error($"Error looking up job abbreviation, '{jobAbbr}'");
                    Svc.Log.Error(ex, "Debug Log");
                    throw;
                }
            }

            // Request a debug file, with null, or the entered Job
            // (if converted successfully)
            Svc.Framework.RunOnTick(ConflictingPluginsChecks.ForceRunChecks)
                .ContinueWith(_ =>
                    Svc.Framework.RunOnTick(() =>
                        DebugFile.MakeDebugFile(job)));
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Debug Log");
            DuoLog.Error("Unable to write Debug log");
        }
    }

    /// <summary>
    ///     Handles the opening of the window, as well as the opening command.
    /// </summary>
    /// <value>
    ///     <c>&lt;blank&gt;</c> - toggle window<br />
    ///     <c>&lt;job abbr&gt;</c> - open window, to that job
    /// </value>
    /// <param name="argument">
    ///     Only should be provided if coming from
    ///     <see cref="OnCommand">OnCommand</see>.<br />
    ///     Job Abbreviation to open to (the PvE tab for).
    /// </param>
    /// <param name="tab">
    ///     Only should be provided if coming from <see cref="OnOpenMainUi" /> or
    ///     <see cref="OnOpenConfigUi" />, or the tab commands in
    ///     <see cref="OnCommand">OnCommand</see>.<br />
    ///     The tab of the UI window to open to.
    /// </param>
    /// <param name="forceOpen">
    ///     Only should be provided if coming from <see cref="OnOpenMainUi" /> or
    ///     <see cref="OnOpenConfigUi" />, or the tab commands in
    ///     <see cref="OnCommand">OnCommand</see>.<br />
    ///     If provided: the state the window should be forced to.
    /// </param>
    /// <remarks>
    ///     The order of operations is as follows:
    ///     <list type="number">
    ///         <item>Toggle the window state</item>
    ///         <item>
    ///             Force window state (UI buttons)
    ///             (if <paramref name="forceOpen" />)
    ///         </item>
    ///         <item>
    ///             Open to specific tab
    ///             (if <paramref name="tab" />)
    ///             (returns early)
    ///         </item>
    ///         <item>
    ///             Open to current job setting
    ///             (if <see cref="Configuration.OpenToCurrentJob" />)
    ///         </item>
    ///         <item>
    ///             Open to specified job
    ///             (if specified in <paramref name="argument" />, from
    ///             <see cref="OnCommand">OnCommand</see>)
    ///         </item>
    ///     </list>
    /// </remarks>
    internal void HandleOpenCommand
        (string[]? argument = null, OpenWindow? tab = null, bool? forceOpen = null)
    {
        argument ??= [""];

        ConfigWindow.ClearAnySearches();

        // Toggle the window state
        ConfigWindow.IsOpen = !ConfigWindow.IsOpen;

        // Force open (UI buttons)
        if (forceOpen is not null)
            ConfigWindow.IsOpen = forceOpen.Value;

        // Handle option to always open to the PvE tab
        var openingToPvP =
            ContentCheck.IsInPVPContent && Service.Configuration.OpenToPvP;
        if (ConfigWindow.IsOpen)
            if (openingToPvP)
                ConfigWindow.OpenWindow = OpenWindow.PvP;
            else if (Service.Configuration.OpenToPvE)
                ConfigWindow.OpenWindow = OpenWindow.PvE;

        // Open to specific tab
        if (tab is not null)
        {
            ConfigWindow.OpenWindow = tab.Value;
            return;
        }

        // If no arguments provided
        if (argument[0].Length <= 0)
        {
            // Handle the "Open to current job" setting
            if (ConfigWindow.IsOpen && !openingToPvP)
                PvEFeatures.OpenToCurrentJob(false);

            // Skip trying to process arguments
            return;
        }

        // Open to specified job
        var jobAbbrev = argument[0];

        if (TryGetJobByAbbreviation(jobAbbrev, out var job))
        {
            ConfigWindow.IsOpen = true;
            ConfigWindow.OpenWindow = OpenWindow.PvE;
            FeaturesWindow.OpenJob = job.GetJob();
        }
        else
        {
            DuoLog.Error($"{argument[0]} is not a correct job abbreviation.");
            return;
        }


    }

    /// <summary>
    ///     Disables job stone checking for the session.<br />
    ///     This is used to allow the user to play classes without being
    ///     prompted to use a job stone.<br />
    ///     Disables <see cref="ActionReplacer.ClassLocked" />.
    /// </summary>
    private void HandleJobStoneCheckCommand()
    {
        if (ActionReplacer.DisableJobCheck)
        {
            DuoLog.Information("Job Stone Checking is already disabled.");
            return;
        }

        ActionReplacer.DisableJobCheck = true;
        DuoLog.Information("Job Stone Checking has been disabled for this session.");
        DuoLog.Warning("Please do not play Classes with other people, " +
                       "it is objectively worse in every way, and you will lack " +
                       "a significant amount of functionality anyway.");
    }

    /// <summary>
    ///     Toggles the next-action tracker window (ST + AoE next action + burst
    ///     state). Subcommand: <c>show</c>/<c>on</c>, <c>hide</c>/<c>off</c>, or
    ///     blank to flip.
    /// </summary>
    private void HandleNextTrackerToggle(string[] argument)
    {
        var sub = argument.Length > 1 ? argument[1] : "";

        // Recovery: un-hide AND snap to the centre of the screen, in case it was
        // hidden or dragged off-screen and can't be found.
        if (sub is "reset" or "center" or "centre" or "find")
        {
            Service.Configuration.NextActionTrackerHidden = false;
            Service.Configuration.Save();
            NextActionTracker?.Recenter();
            DuoLog.Information(
                "Next-action tracker reset to the centre of your screen.");
            return;
        }

        var hide = sub switch
        {
            "hide" or "off" => true,
            "show" or "on" => false,
            _ => !Service.Configuration.NextActionTrackerHidden,
        };

        Service.Configuration.NextActionTrackerHidden = hide;
        Service.Configuration.Save();
    }

    private void HandleMasterToggle(string[] argument)
    {
        var verb = argument[0];
        var sub = argument.Length > 1 ? argument[1] : "";

        var disable = verb switch
        {
            "disable" or "off" => true,
            "enable" or "on" => false,
            _ => !Service.Configuration.MasterDisabled,
        };

        if (sub is "toggle")
            disable = !Service.Configuration.MasterDisabled;

        Service.Configuration.MasterDisabled = disable;
        Service.Configuration.Save();
        Service.ActionReplacer.UpdateFilteredCombos();
    }

    private void OnTweakToggleCommand(string command, string arguments)
    {
        Service.Configuration.MasterDisabled = !Service.Configuration.MasterDisabled;
        Service.Configuration.Save();
        Service.ActionReplacer.UpdateFilteredCombos();
    }

    private void HandleBurstControl(string[] argument,
        Dictionary<Job, Preset[]> map, string label)
    {
        if (!PresetStorage.AllPresets.Any(p => p.Value.JobInfo?.Job == Player.Job && p.Value.ComboType == ComboType.Advanced && PresetStorage.IsEnabled(p.Key)))
        {
            DuoLog.Error("This feature is for Advanced Mode Combos.");
            return;
        }

        if (!map.TryGetValue(Player.Job, out var presets))
        {
            DuoLog.Error($"No {label} presets defined for your current job.");
            return;
        }

        var sub = argument.Length > 1 ? argument[1] : "";
        var enable = sub switch
        {
            "hold" => false,
            "disable" => false,
            "resume" => true,
            "enable" => true,
            _ => !PresetStorage.IsEnabled(presets[0]),
        };

        foreach (var preset in presets)
        {
            if (enable)
                PresetStorage.EnablePreset(preset, ConfigChangeSource.Command);
            else
                PresetStorage.DisablePreset(preset, ConfigChangeSource.Command);
        }
    }
}
