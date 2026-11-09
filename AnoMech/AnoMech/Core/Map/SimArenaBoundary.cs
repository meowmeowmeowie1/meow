using System;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Map;

// Per-frame arena fence. Walks every active party member each tick (player
// included, since SimParty exposes the player slot through ActiveMembers) and
// kills anyone whose XZ distance from `center` exceeds `radius`. Geometry is
// XZ-plane only — Y is ignored, matching how scenarios reason about positions.
//
// One per world: SimWorld.EnforceArenaBoundary replaces any previous boundary.
internal sealed class SimArenaBoundary : ISimObject
{
    private readonly SimParty party;
    private readonly Func<Vector3, bool> outside;
    private readonly string cause;

    public bool IsAlive => true;
    public bool IsActive => true;

    internal SimArenaBoundary(SimParty party, float radius, string cause)
        : this(party, local => local.X * local.X + local.Z * local.Z > radius * radius, cause)
    {
    }

    // A square platform (DSR's dragon phases): axis-aligned, `halfSize` from the centre to each edge.
    internal static SimArenaBoundary Square(SimParty party, float halfSize, string cause)
        => new(party, local => MathF.Abs(local.X) > halfSize || MathF.Abs(local.Z) > halfSize, cause);

    private SimArenaBoundary(SimParty party, Func<Vector3, bool> outside, string cause)
    {
        this.party = party;
        this.outside = outside;
        this.cause = cause;
    }

    // XZ test against the fence; shared by the per-frame fence and external callers
    // (teleport-to-spawn on reset) so they always agree.
    internal bool IsOutside(Vector3 local) => outside(local);

    public void Tick(float deltaSeconds)
    {
        // Member positions are scenario-local; the boundary is centered on local zero.
        foreach (var member in party.ActiveMembers())
        {
            if (!IsOutside(member.Position)) continue;
            member.Die(SimCharacterDeathExtensions.Environment, cause);
        }
    }

    public void Despawn() { }
}
