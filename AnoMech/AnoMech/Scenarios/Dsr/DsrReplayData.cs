using System.Numerics;
using AnoMech.Core.Map;

namespace AnoMech.Scenarios.Dsr;

// What the duty's server sends on entering it. No replay of this duty was available: the helper
// pool is the one every pull spawns (24 of the generic helper); the entrance barrier's layouts are
// UNVERIFIED, so only its position (where the player spawns) is used.
public static class DsrReplayData
{
    public static readonly DutyInitData DutyInitData = new(
        new EntranceBarrier(2007457, 0, 0, new Vector3(100f, 0f, 118f), 0f),
        new HelperPool(9020, 24),
        [],
        [],
        []);
}
