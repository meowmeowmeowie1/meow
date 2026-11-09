using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr.P5DeathOfTheHeavens;

// Guerrique's cardinal (relative north), four Dooms, the Playstation pairs, the eye and Thordan's
// perch, and which side the dragons dive from.
public sealed class DsrP5DeathOfTheHeavensState
{
    public Direction RelativeNorth { get; }
    public Direction EyeDirection { get; }
    public Direction ThordanDirection { get; }
    public RoleList Dooms { get; }
    public bool DarkscaleFromSouth { get; }
    public bool DiversOnWest { get; }
    public bool VedrfolnirNorth { get; }

    private readonly Dictionary<PartyRole, PlaystationSymbol> symbols;

    // Where the Wings of Salvation puddles landed, known once Lightning Storm is cast: the only
    // thing the Dooms can be cleansed on.
    public List<Vector3> CleansePuddles { get; } = [];

    public DsrP5DeathOfTheHeavensState(Rng rng, SimParty party, DsrP5DeathOfTheHeavensStateOverrides overrides)
    {
        RelativeNorth = overrides.GuerriqueDirection ?? rng.NextCardinal();
        EyeDirection = overrides.EyeDirection ?? rng.NextDirection();
        ThordanDirection = EyeDirection.Rotate(4 + rng.Next(3) - 1);
        var forcedSymbols = overrides.Symbol.Resolve(party.PlayerRole).ToDictionary(r => r.Role, r => r.Value);
        var doomMembership = overrides.Doom.Resolve(party.PlayerRole).ToDictionary(r => r.Role, r => r.Value);
        foreach (var (role, symbol) in forcedSymbols)
        {
            if (symbol == PlaystationSymbol.Circle) doomMembership.TryAdd(role, true);
            if (symbol == PlaystationSymbol.Cross) doomMembership.TryAdd(role, false);
        }
        Dooms = new RoleListBuilder { Size = 4, Membership = doomMembership }.Build(rng, party);
        symbols = AssignSymbols(rng, forcedSymbols);
        DarkscaleFromSouth = rng.NextBool();
        DiversOnWest = rng.NextBool();
        VedrfolnirNorth = rng.NextBool();
    }

    private Dictionary<PartyRole, PlaystationSymbol> AssignSymbols(Rng rng, IReadOnlyDictionary<PartyRole, PlaystationSymbol> forced)
    {
        var result = new Dictionary<PartyRole, PlaystationSymbol>();
        var doomPool = new List<PlaystationSymbol> { PlaystationSymbol.Circle, PlaystationSymbol.Circle, PlaystationSymbol.Triangle, PlaystationSymbol.Square };
        var freePool = new List<PlaystationSymbol> { PlaystationSymbol.Cross, PlaystationSymbol.Cross, PlaystationSymbol.Triangle, PlaystationSymbol.Square };
        foreach (var role in PerRole.All)
        {
            if (!forced.TryGetValue(role, out var symbol)) continue;
            var pool = Dooms.Contains(role) ? doomPool : freePool;
            if (!pool.Remove(symbol)) continue;
            result[role] = symbol;
        }
        var doomRest = rng.Shuffle(doomPool).ToList();
        var freeRest = rng.Shuffle(freePool).ToList();
        foreach (var role in PerRole.All)
        {
            if (result.ContainsKey(role)) continue;
            var pool = Dooms.Contains(role) ? doomRest : freeRest;
            result[role] = pool[0];
            pool.RemoveAt(0);
        }
        return result;
    }

    public bool HasDoom(PartyRole role) => Dooms.Contains(role);
    public PlaystationSymbol Symbol(PartyRole role) => symbols[role];
    public IEnumerable<PartyRole> WithSymbol(PlaystationSymbol symbol) => PerRole.All.Where(r => symbols[r] == symbol);

    public Vector3 EyePosition => EyeDirection.Apply(new Vector3(0f, 0f, -Geometry.EyeDistance));
    public Vector3 ThordanPerch => ThordanDirection.Apply(new Vector3(0f, 0f, -Geometry.ThordanPerchDistance));

    // Scenario-local spots authored with relative north up.
    public Vector3 Relative(Vector3 authored) => RelativeNorth.Apply(authored);

    public Vector3 GuerriqueSpot => Relative(Geometry.GuerriqueSpot);
    public Vector3 DarkscaleSpot => Relative(DarkscaleFromSouth ? Geometry.DarkscaleSpot : -Geometry.DarkscaleSpot);
    public Vector3 VedrfolnirSpot => Relative(Mirror(VedrfolnirNorth ? Geometry.NorthDiverSpot : Geometry.SouthDiverSpot));
    public Vector3 ZephirinSpot => Relative(Mirror(VedrfolnirNorth ? Geometry.SouthDiverSpot : Geometry.NorthDiverSpot));

    private Vector3 Mirror(Vector3 west) => DiversOnWest ? west : west with { X = -west.X };
}
