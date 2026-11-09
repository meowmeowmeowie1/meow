using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr.P6WrothFlames;

// One orb set: three Scarlet Prices around a centre, in a diagonal line or a zigzag, each exploding
// in a 6y-wide cross across the arena.
public sealed record OrbSet(Vector3 Centre, IReadOnlyList<Vector3> Orbs);

// Hraesvelgr's dive lane, the orb sets' corners, the Akh Morn target, who spreads, who stacks and
// who is free, and Hot Wing or Hot Tail.
public sealed class DsrP6WrothFlamesState
{
    public DiveLane HraesvelgrLane { get; }
    public bool HraesvelgrFromNorth { get; }
    public Direction SecondOrbSet { get; }
    public bool HotWing { get; }
    public RoleList Roles { get; }
    public PartyRole AkhMornTarget { get; }
    public IReadOnlyList<OrbSet> OrbSets { get; }

    public DsrP6WrothFlamesState(Rng rng, SimParty party, DsrP6WrothFlamesStateOverrides overrides)
    {
        HraesvelgrLane = overrides.HraesvelgrLane ?? rng.NextObj(DiveLane.West, DiveLane.Centre, DiveLane.East);
        HraesvelgrFromNorth = overrides.HraesvelgrFromNorth ?? rng.NextBool();
        SecondOrbSet = overrides.SecondOrbSet ?? rng.NextIntercardinal();
        HotWing = overrides.HotWing ?? rng.NextBool();
        Roles = new RoleListBuilder
        {
            Slots = overrides.Role.Resolve(party.PlayerRole).ToDictionary(r => r.Role, r => r.Value switch
            {
                WrothRole.Spread => new[] { 0, 1, 2, 3 },
                WrothRole.Stack => new[] { 4, 5 },
                _ => new[] { 6, 7 },
            }),
        }.Build(rng, party);
        AkhMornTarget = new RoleListBuilder
        {
            Size = 1,
            Membership = overrides.AkhMornTarget.Resolve(party.PlayerRole).ToDictionary(r => r.Role, r => r.Value),
        }.Build(rng, party)[0];
        var second = SecondOrbSet.Apply(new Vector3(0f, 0f, -Geometry.OrbSetDistance * MathF.Sqrt(2f)));
        OrbSets = [BuildSet(rng, Vector3.Zero), BuildSet(rng, second), BuildSet(rng, -second)];
    }

    private static OrbSet BuildSet(Rng rng, Vector3 centre)
    {
        var s = Geometry.OrbSpacing;
        Vector3[] shape = rng.NextBool()
            ? [new(0f, 0f, 0f), new(s, 0f, s), new(-s, 0f, -s)]
            : [new(0f, 0f, -s), new(s, 0f, s), new(-s, 0f, 0f)];
        var turn = rng.NextBool() ? Direction.N : Direction.E;
        return new OrbSet(centre, shape.Select(o => centre + turn.Apply(o)).ToList());
    }

    public WrothRole Role(PartyRole role) => Roles.List.ToList().IndexOf(role) switch
    {
        < 4 => WrothRole.Spread,
        < 6 => WrothRole.Stack,
        _ => WrothRole.None,
    };

    public IEnumerable<PartyRole> With(WrothRole wrothRole) => Roles.List.Where(r => Role(r) == wrothRole);

    public float LaneX => HraesvelgrLane switch
    {
        DiveLane.West => -Geometry.DiveLaneSpacing,
        DiveLane.East => Geometry.DiveLaneSpacing,
        _ => 0f,
    };

    public Vector3 DiveStart => new(LaneX, 0f, HraesvelgrFromNorth ? -Geometry.DiveStartDistance : Geometry.DiveStartDistance);
    public Vector3 DiveEnd => DiveStart with { Z = -DiveStart.Z };

    // The party opens on the half Hraesvelgr spares, Nidhogg's side when both are spared, and
    // away from the second orb set.
    public int StartSideX => HraesvelgrLane == DiveLane.West ? 1 : -1;
    public int StartSideZ => SecondOrbSet.Apply(new Vector3(0f, 0f, -1f)).Z > 0f ? -1 : 1;
}
