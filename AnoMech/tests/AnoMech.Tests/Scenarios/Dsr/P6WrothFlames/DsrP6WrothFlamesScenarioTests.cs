using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Dsr.P6WrothFlames;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Hraesvelgr dives down the west lane and the second orb set is SW, so the party opens NE
// (19, -19.8) and steps west along the north wall; the debuffs resolve on the south row (z = 12).
// The supports spread, the DPS stack or go free, so by priority MainTank spreads first (wall,
// (-20, 12)) and OffTank second ((-12.6, 12)).
public class DsrP6WrothFlamesScenarioTests
{
    private static NegativeRun<DsrP6WrothFlamesScenario> Tank(WrothRole tank = WrothRole.Spread, WrothRole offTank = WrothRole.Spread, bool hotWing = false)
        => Negative<DsrP6WrothFlamesScenario>(MainTank)
            .Overrides<DsrP6WrothFlamesStateOverrides>(o =>
            {
                o.HraesvelgrLane = DiveLane.West;
                o.HraesvelgrFromNorth = true;
                o.SecondOrbSet = Direction.SW;
                o.HotWing = hotWing;
                o.Role[MainTank] = tank;
                o.Role[OffTank] = offTank;
                o.Role[RegenHealer] = WrothRole.Spread;
                o.Role[ShieldHealer] = WrothRole.Spread;
                o.Role[MeleeDpsA] = tank == WrothRole.Stack ? WrothRole.Spread : WrothRole.Stack;
                o.Role[MeleeDpsB] = tank == WrothRole.Stack ? WrothRole.Spread : WrothRole.Stack;
                o.Role[PhysRangedDps] = tank == WrothRole.Stack ? WrothRole.Stack : WrothRole.None;
                o.Role[CasterDps] = WrothRole.None;
                o.AkhMornTarget[MainTank] = false;
            });

    [Test]
    public void StandingInHraesvelgrsLaneDies()
        => Tank().TeleportAt(12f, to: new(-10f, 0f)).ShouldKill(ActionId.CauterizeHraesvelgr, MainTank);

    [Test]
    public void StayingOnTheFirstAkhMornDies()
        => Tank().FreezeAt(16.5f).ShouldKill(TheEnvironment, MainTank);

    [Test]
    public void StandingOnTheCentreOrbsCrossDies()
        => Tank().TeleportAt(19f, to: new(0f, -13f)).ShouldKill(ActionId.FlameBlast, MainTank);

    [Test]
    public void StandingInHotTailDies()
        => Tank().TeleportAt(26f, to: new(-10f, 3f)).ShouldKill(ActionId.HotTailAoe, MainTank);

    [Test]
    public void StandingInHotWingDies()
        => Tank(hotWing: true).TeleportAt(26f, to: new(-10f, 14f)).ShouldKill(ActionId.HotWingAoe, MainTank);

    [Test]
    public void OverlappingSpreadsKillsBoth()
        => Tank().TeleportAt(29f, to: new(-12.6f, 12f)).ShouldKill(ActionId.SpreadingFlames, MainTank, OffTank);

    [Test]
    public void EntangledFlamesAloneDies()
        => Tank(tank: WrothRole.Stack, offTank: WrothRole.None).TeleportAt(29f, to: new(19.5f, 20f)).ShouldKill(ActionId.EntangledFlames, MainTank);
}
