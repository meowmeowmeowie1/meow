using System;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Dsr.P5DeathOfTheHeavens;

// Same order as the firechain lockon ids. Both circles go to Doom players and both crosses to
// players without one; triangle and square each pair one Doom with one non-Doom.
public enum PlaystationSymbol { Circle, Triangle, Square, Cross }

// User-forced rolls for DsrP5DeathOfTheHeavensState; null leaves a field randomized.
public sealed class DsrP5DeathOfTheHeavensStateOverrides
{
    // Where Ser Guerrique lands: relative north for the whole mechanic.
    public Direction? GuerriqueDirection { get; set; }

    // The Dragon's Eye on the rim; Thordan perches opposite it, up to 45° off.
    public Direction? EyeDirection { get; set; }

    public PerRoleSetting<bool> Doom { get; set; } = new();
    public PerRoleSetting<PlaystationSymbol> Symbol { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        var dooms = PerRole.All.Where(r => Doom[r] == true).ToList();
        conflicts.AtMost(4, dooms, "Doom");
        conflicts.AtLeast(4, PerRole.All.Where(r => Doom[r] == false).ToList(), 8, "Doom");
        foreach (var symbol in Enum.GetValues<PlaystationSymbol>())
            conflicts.AtMost(2, PerRole.All.Where(r => Symbol[r] == symbol).ToList(), $"the {symbol} symbol");
        conflicts.Forbidden(PerRole.All.Where(r => Symbol[r] == PlaystationSymbol.Circle && Doom[r] == false).ToList(), "take a circle without Doom");
        conflicts.Forbidden(PerRole.All.Where(r => Symbol[r] == PlaystationSymbol.Cross && Doom[r] == true).ToList(), "take a cross with Doom");
        foreach (var symbol in new[] { PlaystationSymbol.Triangle, PlaystationSymbol.Square })
        {
            conflicts.AtMost(1, PerRole.All.Where(r => Symbol[r] == symbol && Doom[r] == true).ToList(), $"the Doom {symbol}");
            conflicts.AtMost(1, PerRole.All.Where(r => Symbol[r] == symbol && Doom[r] == false).ToList(), $"the non-Doom {symbol}");
        }
        return conflicts;
    }
}
