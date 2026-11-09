using AnoMech.Core.EnemyActions;
using AnoMech.Core.Native.Interfaces;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr;

public static class DsrActions
{
    // -- P5 Death of the Heavens --
    public static readonly EnemyAction DeathOfTheHeavens = new(ActionId.DeathOfTheHeavens)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    // The sheet's range-80 circle is the whole party; retail dooms four, so it is aimed per target.
    public static readonly EnemyAction Deathstorm = new(ActionId.Deathstorm)
    {
        Cast = new() { AnimationLock = 1.1f, FaceTarget = false },
        Area = new() { CastTypeOverride = CastType.SingleTarget },
        Effects = [ApplyStatus(StatusId.Doom, Duration.Doom)],
    };

    public static readonly EnemyAction HeavyImpact = new(ActionId.HeavyImpact)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction HeavyImpactHit1 = new(ActionId.HeavyImpactHit1)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.5f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction HeavyImpactHit2 = HeavyImpactRing(ActionId.HeavyImpactHit2, 1);
    public static readonly EnemyAction HeavyImpactHit3 = HeavyImpactRing(ActionId.HeavyImpactHit3, 2);
    public static readonly EnemyAction HeavyImpactHit4 = HeavyImpactRing(ActionId.HeavyImpactHit4, 3);
    public static readonly EnemyAction HeavyImpactHit5 = HeavyImpactRing(ActionId.HeavyImpactHit5, 4);

    private static EnemyAction HeavyImpactRing(uint actionId, int ring) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = ring * Geometry.HeavyImpactRingWidth },
        Timing = new() { DamageDelay = 0.5f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction LightningStorm = new(ActionId.LightningStorm)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction LightningStormAoe = new(ActionId.LightningStormAoe)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(split: Exactly(1))],
        DeathExplanation = "Lightning Storm hit more than one player",
    };

    public static readonly EnemyAction TwistingDive = Dive(ActionId.TwistingDive);
    public static readonly EnemyAction CauterizeDarkscale = Dive(ActionId.CauterizeDarkscale);
    public static readonly EnemyAction SpearOfTheFury = Dive(ActionId.SpearOfTheFury);

    private static EnemyAction Dive(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction WingsOfSalvation = new(ActionId.WingsOfSalvation)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    // The puddle's 10s telegraph; the cleanse voidzone it leaves is the scenario's own.
    public static readonly EnemyAction WingsOfSalvationAoe = new(ActionId.WingsOfSalvationAoe)
    {
        Cast = new() { AnimationLock = 1.1f },
    };

    public static readonly EnemyAction DragonsGaze = new(ActionId.DragonsGaze)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction DragonsGazeAoe = Gaze(ActionId.DragonsGazeAoe, "looked at Thordan");
    public static readonly EnemyAction DragonsGlory = Gaze(ActionId.DragonsGlory, "looked at the Dragon's Eye");

    private static EnemyAction Gaze(uint actionId, string explanation) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f, FaceTarget = false },
        Timing = new() { DamageDelay = 0.5f },
        Effects = [EnemyActionEffects.Gaze(lookAway: true)],
        DeathExplanation = explanation,
    };

    public static readonly EnemyAction FaithUnmoving = new(ActionId.FaithUnmoving)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Knockback(KnockbackId.FaithUnmoving, knockbackDelay: 0.6f)],
    };

    public static readonly EnemyAction Heavensflame = new(ActionId.Heavensflame)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction HeavensflameAoe = new(ActionId.HeavensflameAoe)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(split: Exactly(1))],
        DeathExplanation = "Heavensflame hit more than one player",
    };

    // Retail hits hard and leaves a Damage Down; here a chain left unbroken is the failure itself.
    public static readonly EnemyAction HolyChain = new(ActionId.HolyChain)
    {
        Cast = new() { AnimationLock = 1.1f, FaceTarget = false },
        Area = new() { CastTypeOverride = CastType.SingleTarget },
        Timing = new() { DamageDelay = 0.5f },
        Effects = [Damage(Lethal)],
        DeathExplanation = "fire chain not broken",
    };

    // -- P6 Wroth Flames --
    public static readonly EnemyAction WrothFlames = new(ActionId.WrothFlames)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction AkhMornFirst = AkhMorn(ActionId.AkhMornFirst, 3.1f);
    public static readonly EnemyAction AkhMornRest = AkhMorn(ActionId.AkhMornRest, 1.1f);

    private static EnemyAction AkhMorn(uint actionId, float animationLock) => new(actionId)
    {
        Cast = new() { AnimationLock = animationLock },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(split: Stack(6))],
    };

    public static readonly EnemyAction CauterizeHraesvelgr = new(ActionId.CauterizeHraesvelgr)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction FlameBlast = new(ActionId.FlameBlast)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.5f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction HotWing = new(ActionId.HotWing) { Cast = new() { AnimationLock = 3.1f } };
    public static readonly EnemyAction HotTail = new(ActionId.HotTail) { Cast = new() { AnimationLock = 3.1f } };

    public static readonly EnemyAction HotWingAoe = HotLine(ActionId.HotWingAoe);
    public static readonly EnemyAction HotTailAoe = HotLine(ActionId.HotTailAoe);

    private static EnemyAction HotLine(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction SpreadingFlames = new(ActionId.SpreadingFlames)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(split: Exactly(1))],
        DeathExplanation = "Spreading Flames hit more than one player",
    };

    public static readonly EnemyAction EntangledFlames = new(ActionId.EntangledFlames)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(split: Stack(2))],
        DeathExplanation = "Entangled Flames needs exactly a partner",
    };
}
