using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr.P6WrothFlames;

public sealed class DsrP6WrothFlamesLpduAi : IScenarioAi<DsrP6WrothFlamesState>
{
    public string Name => "LPDU";

    private static readonly PartyRole[] Priority =
    [
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
    ];

    private static readonly float[] SpreadX = [-20f, -12.6f, -5.2f, 2.2f];
    private static readonly float[] StackX = [19.5f, 11f];
    private const float TailRowZ = 12f;
    private const float CrossArmClearance = 9.5f;

    private DsrP6WrothFlamesState state = null!;
    private SimWorld world = null!;

    public void Run(DsrP6WrothFlamesState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);
        ai.Move(3f, () => Everyone(Vector2.Zero), jitter: 1f, arrivalTime: 7f);
        ai.Move(9.8f, () => Everyone(Corner(19f, 19.8f)), jitter: 0.15f, arrivalTime: 15.8f);
        ai.Move(16.4f, () => Everyone(Corner(19f, 13f)), jitter: 0.15f, arrivalTime: 17.7f);
        ai.Move(18.1f, () => Everyone(Corner(12.2f, 13f)), jitter: 0.15f, arrivalTime: 19.4f);
        ai.Move(19.75f, () => Everyone(Corner(1.5f, 13f)), jitter: 0.15f, sprint: true);
        ai.Move(21.3f, () => Everyone(Vector2.Zero), sprint: true);
        ai.Move(21.9f, () => ByRole(role => ClearOfLastOrbSet(FinalSpot(role)) ? FinalSpot(role) : null), arrivalTime: 27.8f);
        ai.Move(24.8f, () => ByRole(role => ClearOfLastOrbSet(FinalSpot(role)) ? null : FinalSpot(role)), arrivalTime: 28f);
    }

    private Vector2 Corner(float x, float z) => new(x * state.StartSideX, z * state.StartSideZ);

    private Vector2 FinalSpot(PartyRole role)
    {
        var z = state.HotWing ? 0f : -TailRowZ * state.StartSideZ;
        return state.Role(role) switch
        {
            WrothRole.Spread => new Vector2(SpreadX[GroupIndex(role, WrothRole.Spread)], z),
            WrothRole.Stack => new Vector2(StackX[GroupIndex(role, WrothRole.Stack)], z),
            _ => new Vector2(StackX[GroupIndex(role, WrothRole.None)], z),
        };
    }

    private bool ClearOfLastOrbSet(Vector2 spot)
    {
        var centre = state.OrbSets[2].Centre;
        return MathF.Abs(spot.X - centre.X) >= CrossArmClearance && MathF.Abs(spot.Y - centre.Z) >= CrossArmClearance;
    }

    private int GroupIndex(PartyRole role, WrothRole group)
        => Priority.Where(r => state.Role(r) == group).ToList().IndexOf(role);

    private static IAiMove Everyone(Vector2 spot) => AiMove.All(spot);

    private static IAiMove ByRole(Func<PartyRole, Vector2?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(at).ToArray()).NaturalOrder();
}
