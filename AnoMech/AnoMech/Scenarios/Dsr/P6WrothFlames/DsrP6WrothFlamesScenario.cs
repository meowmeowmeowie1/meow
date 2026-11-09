using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using Actions = AnoMech.Scenarios.Dsr.DsrActions;

namespace AnoMech.Scenarios.Dsr.P6WrothFlames;

// DSR P6 "Wroth Flames": Nidhogg hands out four Spreading and two Entangled Flames, then a
// four-hit Akh Morn stack that leaves a puddle under each hit while Hraesvelgr dives down one
// lane and three sets of Scarlet Price orbs explode in crosses, centre first, then two opposite
// corners. Hot Wing or Hot Tail cleaves as the debuffs resolve.
public sealed class DsrP6WrothFlamesScenario : IScenario
{
    public string Name => "Wroth Flames";
    public IPhase Phase => DsrZone.P6;
    public bool SupportsSolo => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new DsrP6WrothFlamesLpduAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly DsrP6WrothFlamesSettingsWindow settingsWindow = new();

    public const float DebuffsAt = 6.5f;
    public const float DebuffsResolveAt = DebuffsAt + Duration.WrothDebuffs;
    private const float FlameBlastCastSeconds = 5f;
    private const float AkhMornVoidzoneArmsIn = 1.5f;
    private const float AkhMornVoidzoneLifetime = 14f;

    // Each orb set spawns, explodes, then goes.
    private static readonly float[] OrbSetSpawnAt = [7.6f, 9.6f, 12.6f];
    public static readonly float[] OrbSetExplodesAt = [19.7f, 21.7f, 24.7f];

    private SimWorld world = null!;
    private DsrP6WrothFlamesState state = null!;
    private SimEnemy? nidhogg, hraesvelgr;
    private readonly List<SimEnemy?>[] orbs = [[], [], []];
    private readonly DsrVoidzones puddles = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new DsrP6WrothFlamesState(world.Rng, world.Party, settingsWindow.Overrides);
        foreach (var set in orbs) set.Clear();
        puddles.Clear();
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<DsrP6WrothFlamesState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnDragons);
        world.Events.Add(3f, () => nidhogg?.Cast(Actions.WrothFlames));
        world.Events.Add(6f, () => hraesvelgr?.SetTargetable(false));
        world.Events.Add(6f, () => hraesvelgr?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(DebuffsAt, Debuffs);
        world.Events.Add(7.3f, () => hraesvelgr?.SetPosition(new Placement(state.DiveStart, 0f).Face(state.DiveEnd)));
        world.Events.Add(7.3f, () => hraesvelgr?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(7.3f, () => hraesvelgr?.SetTargetable(true));
        for (var set = 0; set < 3; set++)
        {
            var s = set;
            world.Events.Add(OrbSetSpawnAt[s], () => SpawnOrbs(s));
            world.Events.Add(OrbSetExplodesAt[s] - FlameBlastCastSeconds, () => Explode(s));
            world.Events.Add(OrbSetExplodesAt[s] + 0.5f, () => RemoveOrbs(s));
        }
        world.Events.Add(8.3f, AkhMornFirst);
        world.Events.Add(11.4f, () => hraesvelgr?.Cast(Actions.CauterizeHraesvelgr));
        world.Events.Add(16.5f, () => hraesvelgr?.SetTargetable(false));
        world.Events.Add(16.5f, () => hraesvelgr?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(18f, AkhMornRest);
        world.Events.Add(19.6f, AkhMornRest);
        world.Events.Add(21f, () => hraesvelgr?.SetPosition(new Placement(Geometry.HraesvelgrSpot, 0f).Face(Vector3.Zero)));
        world.Events.Add(21f, () => hraesvelgr?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(21f, () => hraesvelgr?.SetTargetable(true));
        world.Events.Add(21.2f, AkhMornRest);
        world.Events.Add(21.8f, HotCleaveTelegraphs);
        world.Events.Add(22.8f, () => nidhogg?.Cast(state.HotWing ? Actions.HotWing : Actions.HotTail));
        world.Events.Add(DebuffsResolveAt, ResolveDebuffs);
        world.Events.Add(33f, () => nidhogg?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false));
    }

    private void SpawnDragons()
    {
        nidhogg = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.NidhoggP6,
            NameId: BNpcNameId.Nidhogg,
            Level: Level,
            Targetable: true,
            Placement: new Placement(Geometry.NidhoggSpot, 0f).Face(Vector3.Zero)));
        hraesvelgr = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.HraesvelgrP6,
            NameId: BNpcNameId.Hraesvelgr,
            Level: Level,
            Targetable: true,
            Placement: new Placement(Geometry.HraesvelgrSpot, 0f).Face(Vector3.Zero)));
    }

    private void Debuffs()
    {
        foreach (var role in state.With(WrothRole.Spread))
            world.Party.Get(role)?.AddStatus(StatusId.SpreadingFlames, Duration.WrothDebuffs)?.ShownAsFrom(nidhogg!);
        foreach (var role in state.With(WrothRole.Stack))
            world.Party.Get(role)?.AddStatus(StatusId.EntangledFlames, Duration.WrothDebuffs)?.ShownAsFrom(nidhogg!);
    }

    private void SpawnOrbs(int set)
    {
        foreach (var position in state.OrbSets[set].Orbs)
            orbs[set].Add(world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.ScarletPrice,
                Level: Level,
                EnemyList: EnemyListMode.Never,
                Placement: new Placement(position, 0f))));
    }

    private void Explode(int set)
    {
        foreach (var orb in orbs[set]) orb?.Cast(Actions.FlameBlast);
    }

    private void RemoveOrbs(int set)
    {
        foreach (var orb in orbs[set]) orb?.Despawn();
        orbs[set].Clear();
    }

    private void AkhMornFirst()
    {
        if (world.Party.Get(state.AkhMornTarget) is not { } target || !target.IsAlive()) return;
        target.AttachLockonVfx(LockonId.Stack);
        nidhogg?.Cast(Actions.AkhMornFirst, target);
    }

    private void AkhMornRest()
    {
        if (world.Party.Get(state.AkhMornTarget) is { } target && target.IsAlive())
            nidhogg?.Cast(Actions.AkhMornRest, target);
    }

    private void HotCleaveTelegraphs()
    {
        var facingEast = MathF.PI / 2f;
        if (state.HotWing)
        {
            nidhogg?.CastFromHelper(Actions.HotWingAoe, at: new Placement(Geometry.NidhoggSpot with { Z = -Geometry.HotWingOffset }, facingEast));
            nidhogg?.CastFromHelper(Actions.HotWingAoe, at: new Placement(Geometry.NidhoggSpot with { Z = Geometry.HotWingOffset }, facingEast));
        }
        else
            nidhogg?.CastFromHelper(Actions.HotTailAoe, at: new Placement(Geometry.NidhoggSpot, facingEast));
    }

    private void ResolveDebuffs()
    {
        foreach (var role in state.With(WrothRole.Spread))
            if (world.Party.Get(role) is { } member && member.IsAlive())
                nidhogg?.CastFromHelper(Actions.SpreadingFlames, member, at: nidhogg.Placement());
        foreach (var role in state.With(WrothRole.Stack))
            if (world.Party.Get(role) is { } member && member.IsAlive())
                nidhogg?.CastFromHelper(Actions.EntangledFlames, member, at: nidhogg.Placement());
    }

    // Every Akh Morn hit burns the ground it lands on.
    public void OnActionResolved(ActionResolved resolved)
    {
        if (resolved.Action.ActionId is not (ActionId.AkhMornFirst or ActionId.AkhMornRest)) return;
        puddles.Add(world, EObjId.AkhMornVoidzone, resolved.Origin.Position, Geometry.AkhMornVoidzoneRadius,
            AkhMornVoidzoneArmsIn, AkhMornVoidzoneLifetime, SimCharacterDeathExtensions.Environment, "stood in an Akh Morn puddle");
    }

    public void Tick(float delta, float elapsed) => puddles.KillWhoeverStandsIn(world);
}
