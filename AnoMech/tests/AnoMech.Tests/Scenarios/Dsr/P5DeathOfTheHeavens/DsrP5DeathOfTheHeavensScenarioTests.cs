using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Dsr.P5DeathOfTheHeavens;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Guerrique pinned north, so the AI's authored coordinates are the arena's. The four supports
// hold Doom: MainTank is Doom 1 (lightning spot W at (-12.5, 0), west circle), OffTank Doom 2
// (NW at (-12.5, -16)); MeleeDpsA is the first cross, knocked to the north (0, -19).
public class DsrP5DeathOfTheHeavensScenarioTests
{
    private static NegativeRun<DsrP5DeathOfTheHeavensScenario> Tank()
        => Negative<DsrP5DeathOfTheHeavensScenario>(MainTank)
            .Overrides<DsrP5DeathOfTheHeavensStateOverrides>(o =>
            {
                o.GuerriqueDirection = Direction.N;
                o.EyeDirection = Direction.N;
                foreach (var role in new[] { MainTank, OffTank, RegenHealer, ShieldHealer }) o.Doom[role] = true;
                foreach (var role in new[] { MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps }) o.Doom[role] = false;
                o.Symbol[MainTank] = PlaystationSymbol.Circle;
                o.Symbol[MeleeDpsA] = PlaystationSymbol.Cross;
            });

    // Once the dives have resolved: 8 from Guerrique, in the 6-12 ring.
    [Test]
    public void StandingInTheSecondHeavyImpactRingDies()
        => Tank().TeleportAt(24.15f, to: new(0f, -1f)).ShouldKill(ActionId.HeavyImpactHit2, MainTank);

    // In Darkscale's line down the middle, clear of the two side dives through the centre.
    [Test]
    public void StandingInDarkscalesDiveDies()
        => Tank().TeleportAt(20f, to: new(0f, 17f)).ShouldKill(ActionId.CauterizeDarkscale, MainTank);

    [Test]
    public void SharingALightningSpotKillsBoth()
        => Tank().TeleportAt(23f, to: new(-12.5f, -16f)).ShouldKill(ActionId.LightningStormAoe, MainTank, OffTank);

    [Test]
    public void StandingStillWhenTwistersFormDies()
        => Tank().FreezeAt(25f).ShouldKill(ActionId.Twister, MainTank);

    [Test]
    public void LookingAtTheEyeDies()
        => Tank().TeleportAt(36f, to: new(-2.5f, 0f), facing: MathF.PI).ShouldKill(ActionId.DragonsGlory, MainTank);

    [Test]
    public void OverlappingHeavensflameKillsBoth()
        => Tank().TeleportAt(38.2f, to: new(0f, -17f)).ShouldKill(ActionId.HeavensflameAoe, MainTank, MeleeDpsA);

    // Knocked from just off the west circle spot to (-18.3, 2.9), 3.6 from the puddle; facing ENE,
    // away from the eye in the north and Thordan anywhere in the south.
    [Test]
    public void DoomNotCleansedKills()
        => Tank().TeleportAt(36f, to: new(-2.5f, 0.4f), facing: 2.01f).ShouldDieOfStatus(StatusId.Doom, MainTank).ShouldKillNoOneElse();
}
