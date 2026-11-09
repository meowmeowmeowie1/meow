using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Dsr.P6WrothFlames;

public enum WrothRole { Spread, Stack, None }

public enum DiveLane { West, Centre, East }

// User-forced rolls for DsrP6WrothFlamesState; null leaves a field randomized.
public sealed class DsrP6WrothFlamesStateOverrides
{
    public DiveLane? HraesvelgrLane { get; set; }
    public bool? HraesvelgrFromNorth { get; set; }

    // The intercardinal the second orb set spawns at; the third is opposite.
    public Direction? SecondOrbSet { get; set; }
    public bool? HotWing { get; set; }

    public PerRoleSetting<WrothRole> Role { get; set; } = new();
    public PerRoleSetting<bool> AkhMornTarget { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        conflicts.AtMost(4, PerRole.All.Where(r => Role[r] == WrothRole.Spread).ToList(), "Spreading Flames");
        conflicts.AtMost(2, PerRole.All.Where(r => Role[r] == WrothRole.Stack).ToList(), "Entangled Flames");
        conflicts.AtMost(2, PerRole.All.Where(r => Role[r] == WrothRole.None).ToList(), "no debuff");
        conflicts.AtMost(1, PerRole.All.Where(r => AkhMornTarget[r] == true).ToList(), "the Akh Morn target");
        conflicts.AtLeast(1, PerRole.All.Where(r => AkhMornTarget[r] == false).ToList(), 8, "the Akh Morn target");
        return conflicts;
    }
}
