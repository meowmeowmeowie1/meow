using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr.P5DeathOfTheHeavens;

public sealed class DsrP5DeathOfTheHeavensLpduAi : IScenarioAi<DsrP5DeathOfTheHeavensState>
{
    public string Name => "LPDU";

    private static readonly PartyRole[] Priority =
    [
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
    ];

    private static readonly Vector2[] DoomLightningSpots = [new(-12.5f, 0f), new(-12.5f, -16f), new(12.5f, -16f), new(12.5f, 0f)];
    private static readonly Vector2[] FreeLightningSpots = [new(-20.5f, 0f), new(-12.5f, 16f), new(12.5f, 16f), new(20.5f, 0f)];
    private static readonly Vector2[] DoomInnerSpots = [new(-4.5f, 0f), new(-1.3f, 1.3f), new(1.3f, 1.3f), new(4.5f, 0f)];
    private static readonly Vector2[] FreeCentreSpots = [new(0f, -1.3f), new(-1.3f, -1.3f), new(1.3f, -1.3f), new(0f, 1.3f)];

    private DsrP5DeathOfTheHeavensState state = null!;
    private SimWorld world = null!;

    public void Run(DsrP5DeathOfTheHeavensState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);
        ai.Move(3f, Lineup, arrivalTime: 9f);
        ai.Move(14.3f, DoomSplit, arrivalTime: 16.5f);
        ai.Move(17f, LightningSpots, jitter: 0.2f, arrivalTime: 21.8f);
        ai.Move(24.8f, DodgeInward, jitter: 0.2f);
        ai.Move(26.2f, FreesToCentre, arrivalTime: 28f);
        ai.Move(31.8f, PlaystationSpots, jitter: 0.15f, arrivalTime: 35.3f);
        world.Events.Add(36f, FaceAwayFromGazes);
        ai.Move(38.1f, AfterKnockback, jitter: 0.2f, arrivalTime: 39f);
    }

    private IAiMove Lineup() => ByRole(role => new Vector2(-7f + 2f * PriorityIndex(role), 0f));

    private IAiMove DoomSplit() => ByRole(role => new Vector2(-4.5f + 3f * GroupIndex(role), state.HasDoom(role) ? -2.5f : 1.5f));

    private IAiMove LightningSpots() => ByRole(role => (state.HasDoom(role) ? DoomLightningSpots : FreeLightningSpots)[GroupIndex(role)]);

    private IAiMove DodgeInward() => ByRole(role => state.HasDoom(role)
        ? DoomInnerSpots[GroupIndex(role)]
        : new Vector2(GroupIndex(role) < 2 ? -14.5f : 14.5f, 8f));

    private IAiMove FreesToCentre() => ByRole(role => state.HasDoom(role) ? null : FreeCentreSpots[GroupIndex(role)]);

    private IAiMove PlaystationSpots() => ByRole(role => (state.Symbol(role), state.HasDoom(role), SymbolIndex(role)) switch
    {
        (PlaystationSymbol.Cross, _, 0) => new Vector2(0f, -2.9f),
        (PlaystationSymbol.Cross, _, _) => new Vector2(0f, 2.9f),
        (PlaystationSymbol.Circle, _, 0) => new Vector2(-2.5f, 0f),
        (PlaystationSymbol.Circle, _, _) => new Vector2(2.5f, 0f),
        (PlaystationSymbol.Triangle, true, _) => new Vector2(1.95f, 1.9f),
        (PlaystationSymbol.Triangle, false, _) => new Vector2(-1.95f, -1.85f),
        (PlaystationSymbol.Square, true, _) => new Vector2(-1.95f, 1.9f),
        _ => new Vector2(1.92f, -1.85f),
    });

    private IAiMove AfterKnockback() => ByRole(role => (state.Symbol(role), state.HasDoom(role), SymbolIndex(role)) switch
    {
        (PlaystationSymbol.Cross, _, 0) => new Vector2(0f, -19f),
        (PlaystationSymbol.Cross, _, _) => new Vector2(0f, 19f),
        (PlaystationSymbol.Circle, _, 0) => NearestPuddle(new Vector2(-20.5f, 0f)),
        (PlaystationSymbol.Circle, _, _) => NearestPuddle(new Vector2(20.5f, 0f)),
        (PlaystationSymbol.Triangle, true, _) => NearestPuddle(new Vector2(12.5f, 16f)),
        (PlaystationSymbol.Triangle, false, _) => new Vector2(-13.4f, -13.4f),
        (PlaystationSymbol.Square, true, _) => NearestPuddle(new Vector2(-12.5f, 16f)),
        _ => new Vector2(13.4f, -13.4f),
    });

    private Vector2 NearestPuddle(Vector2 authored)
    {
        var wanted = state.Relative(new Vector3(authored.X, 0f, authored.Y));
        if (state.CleansePuddles.Count == 0) return authored;
        var puddle = state.CleansePuddles.MinBy(p => Vector3.DistanceSquared(p, wanted));
        var local = new Direction(-state.RelativeNorth.RadiansFromNorth).Apply(puddle);
        return new Vector2(local.X, local.Z);
    }

    private void FaceAwayFromGazes()
    {
        foreach (var bot in world.Party.ActiveMembers().Where(world.Party.IsBotDriven).ToList())
        {
            var toEye = Vector3.Normalize(state.EyePosition - bot.Position);
            var toThordan = Vector3.Normalize(state.ThordanPerch - bot.Position);
            var between = toEye - toThordan;
            var facing = new Vector3(-between.Z, 0f, between.X);
            bot.StopMoving();
            bot.Face(bot.Position + facing * 10f);
        }
    }

    private static int PriorityIndex(PartyRole role) => Array.IndexOf(Priority, role);

    private int GroupIndex(PartyRole role)
        => Priority.Where(r => state.HasDoom(r) == state.HasDoom(role)).ToList().IndexOf(role);

    private int SymbolIndex(PartyRole role)
        => Priority.Where(r => state.Symbol(r) == state.Symbol(role)).ToList().IndexOf(role);

    private IAiMove ByRole(Func<PartyRole, Vector2?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(at).ToArray()).NaturalOrder().ApplyPositions(state.RelativeNorth.Apply);
}
