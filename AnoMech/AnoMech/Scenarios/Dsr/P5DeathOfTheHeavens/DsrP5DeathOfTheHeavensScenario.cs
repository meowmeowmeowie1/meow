using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using Actions = AnoMech.Scenarios.Dsr.DsrActions;

namespace AnoMech.Scenarios.Dsr.P5DeathOfTheHeavens;

// DSR P5 "Death of the Heavens": Thordan leaves, four players are Doomed, Guerrique's Heavy Impact
// rings roll out from his landing spot while three dragons and knights dive through the arena and
// Lightning Storm spreads land on everyone. The non-Doom players' lightning spots become the
// Wings of Salvation cleanse puddles; Playstation pairs form, chains snap on Grinnaux's knockback
// while both gazes resolve, and Heavensflame spreads end it. A Doom not cleansed in time kills.
public sealed class DsrP5DeathOfTheHeavensScenario : IScenario
{
    public string Name => "Death of the Heavens";
    public IPhase Phase => DsrZone.P5;
    public bool SupportsSolo => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new DsrP5DeathOfTheHeavensLpduAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly DsrP5DeathOfTheHeavensSettingsWindow settingsWindow = new();

    private const float LightningStormAt = 24.3f;
    private const float CleansePuddlesAppearAt = 34.3f;
    private const float ChainsAt = 35.7f;
    private const float HeavensflameAt = 39.3f;
    private const float TwisterArmsIn = 0.5f;
    private const float TwisterLifetime = 6f;
    private const float CleanseLifetime = 20f;

    private SimWorld world = null!;
    private DsrP5DeathOfTheHeavensState state = null!;
    private SimEnemy? thordan, guerrique, grinnaux, charibert, zephirin, vedrfolnir, darkscale, vidofnir;
    private readonly DsrVoidzones twisters = new();
    private readonly List<(Vector3 Position, SimEventObject? Visual)> cleanses = [];
    private readonly List<Vector3> twisterSnapshot = [];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new DsrP5DeathOfTheHeavensState(world.Rng, world.Party, settingsWindow.Overrides);
        twisters.Clear();
        cleanses.Clear();
        twisterSnapshot.Clear();
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<DsrP5DeathOfTheHeavensState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnThordan);
        world.Events.Add(3f, () => thordan?.Cast(Actions.DeathOfTheHeavens));
        world.Events.Add(10.1f, () => thordan?.SetTargetable(false));
        world.Events.Add(10.1f, () => thordan?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(11.7f, () => thordan?.SetPosition(new Placement(state.ThordanPerch, 0f).Face(Vector3.Zero)));
        world.Events.Add(11.7f, () => thordan?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(11.7f, SpawnKnightsAndDragons);
        world.Events.Add(13.9f, () => world.Map.AddEffect(MapEffect.EyeShow, (byte)state.EyeDirection.Index()));
        world.Events.Add(14f, Deathstorm);
        world.Events.Add(16.2f, () => guerrique?.CastFromHelper(Actions.HeavyImpactHit1, at: guerrique.Placement()));
        world.Events.Add(17.9f, () => guerrique?.Cast(Actions.HeavyImpact));
        world.Events.Add(18.1f, () => thordan?.Cast(Actions.LightningStorm));
        world.Events.Add(18.1f, () => vedrfolnir?.Cast(Actions.TwistingDive));
        world.Events.Add(18.1f, () => zephirin?.Cast(Actions.SpearOfTheFury));
        world.Events.Add(18.1f, () => darkscale?.Cast(Actions.CauterizeDarkscale));
        world.Events.Add(23.6f, WingsOfSalvationTelegraphs);
        world.Events.Add(24.2f, () => guerrique?.CastFromHelper(Actions.HeavyImpactHit2, at: guerrique.Placement()));
        world.Events.Add(LightningStormAt, LightningStorm);
        world.Events.Add(25.2f, SnapshotTwisters);
        world.Events.Add(25.4f, SpawnTwisters);
        world.Events.Add(26f, () => guerrique?.CastFromHelper(Actions.HeavyImpactHit3, at: guerrique.Placement()));
        world.Events.Add(27.9f, () => guerrique?.CastFromHelper(Actions.HeavyImpactHit4, at: guerrique.Placement()));
        world.Events.Add(28.3f, () => vidofnir?.Cast(Actions.WingsOfSalvation));
        world.Events.Add(29.8f, () => guerrique?.CastFromHelper(Actions.HeavyImpactHit5, at: guerrique.Placement()));
        world.Events.Add(31.6f, PlaystationMarkers);
        world.Events.Add(31.7f, () => thordan?.Cast(Actions.DragonsGaze));
        world.Events.Add(31.7f, () => charibert?.Cast(Actions.Heavensflame));
        world.Events.Add(32.8f, () => grinnaux?.Cast(Actions.FaithUnmoving));
        world.Events.Add(CleansePuddlesAppearAt, ShowCleansePuddles);
        world.Events.Add(ChainsAt, Chains);
        world.Events.Add(36.9f, Gazes);
        world.Events.Add(HeavensflameAt, Heavensflame);
        world.Events.Add(41.5f, () => world.Map.AddEffect(MapEffect.EyeHide, (byte)state.EyeDirection.Index()));
        world.Events.Add(41.5f, LeaveArena);
        world.Events.Add(43f, () => thordan?.SetPosition(new Placement(Vector3.Zero, 0f)));
        world.Events.Add(43f, () => thordan?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(43.2f, () => thordan?.SetTargetable(true));
    }

    private void SpawnThordan()
    {
        thordan = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KingThordanP5,
            NameId: BNpcNameId.KingThordan,
            Level: Level,
            Targetable: true,
            Placement: new Placement(Vector3.Zero, 0f)));
    }

    private void SpawnKnightsAndDragons()
    {
        guerrique = SpawnAdd(BNpcBaseId.SerGuerrique, BNpcNameId.SerGuerrique, state.GuerriqueSpot);
        grinnaux = SpawnAdd(BNpcBaseId.SerGrinnaux, BNpcNameId.SerGrinnaux, Vector3.Zero, facing: state.Relative(new Vector3(0f, 0f, 1f)));
        charibert = SpawnAdd(BNpcBaseId.SerCharibert, BNpcNameId.SerCharibert, state.Relative(Geometry.CharibertSpot));
        zephirin = SpawnAdd(BNpcBaseId.SerZephirin, BNpcNameId.SerZephirin, state.ZephirinSpot);
        vedrfolnir = SpawnAdd(BNpcBaseId.Vedrfolnir, BNpcNameId.Vedrfolnir, state.VedrfolnirSpot);
        darkscale = SpawnAdd(BNpcBaseId.Darkscale, BNpcNameId.Darkscale, state.DarkscaleSpot);
        vidofnir = SpawnAdd(BNpcBaseId.Vidofnir, BNpcNameId.Vidofnir, state.Relative(Geometry.VidofnirSpot));
    }

    private SimEnemy? SpawnAdd(uint bnpcBaseId, uint nameId, Vector3 position, Vector3? facing = null)
        => world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: bnpcBaseId,
            NameId: nameId,
            Level: Level,
            EnemyList: EnemyListMode.Never,
            Placement: new Placement(position, 0f).Face(facing ?? Vector3.Zero)));

    private void Deathstorm()
    {
        state.Dooms.ForEach(member => darkscale?.Cast(Actions.Deathstorm, member));
    }

    private void WingsOfSalvationTelegraphs()
    {
        state.CleansePuddles.Clear();
        foreach (var member in world.Party.ActiveMembers().ToList())
        {
            if (member is not ISimPartyMember seat || state.HasDoom(seat.Role)) continue;
            var position = member.Position;
            state.CleansePuddles.Add(position);
            vidofnir?.CastFromHelper(Actions.WingsOfSalvationAoe, position, at: new Placement(position, 0f));
        }
    }

    private void LightningStorm()
    {
        foreach (var member in world.Party.ActiveMembers().ToList())
            thordan?.CastFromHelper(Actions.LightningStormAoe, member, at: thordan.Placement());
    }

    private void SnapshotTwisters()
    {
        twisterSnapshot.Clear();
        twisterSnapshot.AddRange(world.Party.ActiveMembers().Select(m => m.Position));
    }

    private void SpawnTwisters()
    {
        foreach (var position in twisterSnapshot)
            twisters.Add(world, EObjId.TwisterVoidzone, position, Geometry.TwisterRadius, TwisterArmsIn, TwisterLifetime,
                ActionId.Twister, "touched a twister");
    }

    private void PlaystationMarkers()
    {
        foreach (var role in PerRole.All)
            world.Party.Get(role)?.AttachLockonVfx(state.Symbol(role) switch
            {
                PlaystationSymbol.Circle => LockonId.FirechainCircle,
                PlaystationSymbol.Triangle => LockonId.FirechainTriangle,
                PlaystationSymbol.Square => LockonId.FirechainSquare,
                _ => LockonId.FirechainCross,
            });
    }

    private void ShowCleansePuddles()
    {
        var index = 0u;
        foreach (var position in state.CleansePuddles)
            cleanses.Add((position, world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.CleanseVoidzone,
                Placement = new Placement(position, 0f),
                EventId = DirectorEventId,
                TargetableStatus = 5,
                Arg2 = (EObjId.VoidzoneArg2Base + 0x10 + index++) << 16 | 3,
                Lifetime = CleanseLifetime,
            })));
    }

    private void Chains()
    {
        foreach (var symbol in new[] { PlaystationSymbol.Circle, PlaystationSymbol.Triangle, PlaystationSymbol.Square, PlaystationSymbol.Cross })
        {
            var pair = state.WithSymbol(symbol).Select(world.Party.Get).ToList();
            if (pair.Count != 2 || !pair[0].IsAlive() || !pair[1].IsAlive()) continue;
            world.Tether(pair[0], pair[1], TetherId.Heavensflame, duration: HeavensflameAt - ChainsAt, debuffStatusId: StatusId.BurningChains)
                 .BreaksWhen(t => t.StretchGt(Geometry.ChainBreakDistance));
        }
    }

    private void Gazes()
    {
        thordan?.CastFromHelper(Actions.DragonsGazeAoe, at: thordan.Placement());
        thordan?.CastFromHelper(Actions.DragonsGlory, at: new Placement(state.EyePosition, 0f).Face(Vector3.Zero));
    }

    private void Heavensflame()
    {
        foreach (var member in world.Party.ActiveMembers().ToList())
            charibert?.CastFromHelper(Actions.HeavensflameAoe, member, at: charibert.Placement());
    }

    private void LeaveArena()
    {
        foreach (var add in new[] { guerrique, grinnaux, charibert, zephirin, vedrfolnir, darkscale, vidofnir })
            add?.FadeOut();
        foreach (var (_, visual) in cleanses) visual?.Despawn();
        cleanses.Clear();
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (lost is { StatusId: StatusId.Doom, Reason: StatusLossReason.Expired })
            lost.Holder.DieOfStatus(StatusId.Doom, "not cleansed in a Wings of Salvation puddle");
    }

    public void OnTetherEnded(TetherEnded ended)
    {
        if (ended.Tether.TetherId != TetherId.Heavensflame || ended.Reason != TetherEndReason.Expired) return;
        if (ended.Tether.A is { } a) charibert?.CastFromHelper(Actions.HolyChain, a, at: charibert.Placement());
        if (ended.Tether.B is { } b) charibert?.CastFromHelper(Actions.HolyChain, b, at: charibert.Placement());
    }

    public void Tick(float delta, float elapsed)
    {
        twisters.KillWhoeverStandsIn(world);
        CleanseDooms();
    }

    // A Doomed player stepping into a puddle is cleansed and uses it up.
    private void CleanseDooms()
    {
        foreach (var member in world.Party.ActiveMembers().ToList())
        {
            if (!member.HasStatus(StatusId.Doom)) continue;
            var index = cleanses.FindIndex(c => Vector3.DistanceSquared(c.Position, member.Position) <= Geometry.CleanseRadius * Geometry.CleanseRadius);
            if (index < 0) continue;
            member.RemoveStatus(StatusId.Doom);
            cleanses[index].Visual?.Despawn();
            cleanses.RemoveAt(index);
        }
    }
}
