using System.Collections.Frozen;
using System.Collections.Generic;

namespace WrathCombo.Core;

/// <summary>
///     Action IDs of every job's gap closer / dash, used by the universal
///     "Exclude Gap Closers" filter (<see cref="Configuration.ExcludeGapClosers" />)
///     applied at the single combo chokepoint in
///     <see cref="WrathCombo.CustomComboNS.CustomCombo.TryInvoke" />.
///
///     Per the user's request this intentionally includes DAMAGE dashes (DRG
///     Dragonfire Dive / Stardiver, SMN Crimson Cyclone, ...): "exclude gap
///     closers even if it does damage". Excluding those creates real DPS / GCD
///     holes, so the set is grouped by job with each id on its own line — pull
///     any entry back out to stop excluding it.
///
///     IDs verified against each job's *_Helper.cs constants. Where a skill was
///     renamed/upgraded (DRK Plunge -> Shadowstride, GNB Rough Divide ->
///     Trajectory) the fork's combos emit the current id, which is the one here.
/// </summary>
internal static class GapCloserData
{
    internal static readonly FrozenSet<uint> GapClosers = new HashSet<uint>
    {
        // Tanks
        16461, // PLD Intervene
        7386,  // WAR Onslaught
        36926, // DRK Shadowstride (formerly Plunge)
        36934, // GNB Trajectory (formerly Rough Divide)

        // Melee
        25762, // MNK Thunderclap
        96,    // DRG Dragonfire Dive (deals damage)
        16480, // DRG Stardiver (deals damage)
        2262,  // NIN Shukuchi
        7492,  // SAM Gyoten
        24401, // RPR Hell's Ingress
        24402, // RPR Hell's Egress
        34646, // VPR Slither

        // Physical ranged / casters / healers
        16010, // DNC En Avant
        7506,  // RDM Corps-a-corps
        7515,  // RDM Displacement
        24295, // SGE Icarus
        25835, // SMN Crimson Cyclone (deals damage)
    }.ToFrozenSet();
}
