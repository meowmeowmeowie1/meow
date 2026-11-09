using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr;

// Ground the party must not stand on: P5's twisters and P6's Akh Morn puddles. Each zone arms a
// moment after it appears (the party stands where an Akh Morn lands) and kills whoever is inside
// from then on. Visuals are the duty's own voidzone EObjs; UNVERIFIED that they render without a
// replay's layout data, the kill works regardless.
public sealed class DsrVoidzones
{
    private sealed record Zone(Vector3 Position, float Radius, float ArmedAt, float ExpiresAt, uint ActionId, string Explanation, SimEventObject? Visual);

    private readonly List<Zone> zones = [];
    private uint spawned;

    public void Clear()
    {
        zones.Clear();
        spawned = 0;
    }

    public IEnumerable<Vector3> Positions => zones.Select(z => z.Position);

    public void Add(SimWorld world, uint eobjId, Vector3 position, float radius, float armsIn, float lifetime, uint actionId, string explanation)
    {
        var now = world.Events.Elapsed;
        var visual = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = eobjId,
            Placement = new Placement(position, 0f),
            EventId = DirectorEventId,
            TargetableStatus = 5,
            Arg2 = (EObjId.VoidzoneArg2Base + spawned++) << 16 | 3,
            Lifetime = lifetime,
        });
        zones.Add(new Zone(position, radius, now + armsIn, now + lifetime, actionId, explanation, visual));
    }

    public void KillWhoeverStandsIn(SimWorld world)
    {
        var now = world.Events.Elapsed;
        zones.RemoveAll(z => now >= z.ExpiresAt);
        foreach (var member in world.Party.ActiveMembers().ToList())
        {
            var zone = zones.FirstOrDefault(z => now >= z.ArmedAt && Vector3.DistanceSquared(z.Position, member.Position) <= z.Radius * z.Radius);
            if (zone != null) member.Die(zone.ActionId, zone.Explanation);
        }
    }
}
