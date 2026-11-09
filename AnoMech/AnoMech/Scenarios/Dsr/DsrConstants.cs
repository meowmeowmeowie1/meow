using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Dsr;

// All IDs and tunables for Dragonsong's Reprise (Ultimate). Values in decimal unless noted.
public static class DsrConstants
{
    public const byte Level = 90;

    // The duty's director event, which its EObjs bind to: 0x80030000 | InstanceContent row.
    public const uint DirectorEventId = 0x8003759A;

    public static class BNpcBaseId
    {
        public const uint KingThordanP5 = 12611;      // 0x3143
        public const uint SerZephirin = 12592;        // 0x3130
        public const uint SerGrinnaux = 12602;        // 0x313A
        public const uint SerCharibert = 12603;       // 0x313B
        public const uint SerGuerrique = 12637;       // 0x315D
        public const uint Darkscale = 12630;          // 0x3156, the P5 Cauterize diver
        public const uint Vidofnir = 12631;           // 0x3157, casts Wings of Salvation
        public const uint Vedrfolnir = 12646;         // 0x3166, the Twisting Dive diver
        public const uint NidhoggP6 = 12612;          // 0x3144
        public const uint HraesvelgrP6 = 12613;       // 0x3145
        public const uint ScarletPrice = 13238;       // 0x33B6, a Wroth Flames orb
        public const uint Helper = 9020;              // 0x233C
    }

    public static class BNpcNameId
    {
        public const uint KingThordan = 3632;
        public const uint SerZephirin = 3633;
        public const uint SerGrinnaux = 3639;
        public const uint SerGuerrique = 3641;
        public const uint SerCharibert = 3642;
        public const uint Darkscale = 3983;
        public const uint Vedrfolnir = 3984;
        public const uint Vidofnir = 11314;
        public const uint Nidhogg = 3458;
        public const uint Hraesvelgr = 4954;
    }

    public static class EObjId
    {
        public const uint TwisterVoidzone = 0x1E8910;
        public const uint AkhMornVoidzone = 0x1EB683;
        public const uint CleanseVoidzone = 0x1EB685;
        // UNVERIFIED: the per-object word retail puts in the spawn's Arg2, never seen for this duty.
        public const uint VoidzoneArg2Base = 0x50;
    }

    public static class ActionId
    {
        // -- P5 Death of the Heavens --
        public const uint DeathOfTheHeavens = 27538;      // Thordan, 4.0s, visual
        public const uint Deathstorm = 27540;             // Darkscale, instant, applies Doom
        public const uint HeavyImpact = 25557;            // Guerrique, 4.3s, visual
        public const uint HeavyImpactHit1 = 25558;        // helper, 6.0s, 6y circle
        public const uint HeavyImpactHit2 = 25559;        // helper, instant, 6-12y donut
        public const uint HeavyImpactHit3 = 25560;        // 12-18y
        public const uint HeavyImpactHit4 = 25561;        // 18-24y
        public const uint HeavyImpactHit5 = 25562;        // 24-30y
        public const uint LightningStorm = 25548;         // Thordan, 5.7s, visual
        public const uint LightningStormAoe = 25549;      // helper->player, 5y spread
        public const uint TwistingDive = 27531;           // Vedrfolnir, 6.0s, 60x10 line
        public const uint Twister = 27532;                // helper, the twister voidzone's hit
        public const uint CauterizeDarkscale = 27533;     // Darkscale, 6.0s, 48x20 line
        public const uint SpearOfTheFury = 27539;         // Zephirin, 6.0s, 50x10 line
        public const uint WingsOfSalvation = 27541;       // Vidofnir, 5.3s, visual
        public const uint WingsOfSalvationAoe = 27542;    // helper->location, 10.0s, 4y puddle
        public const uint DragonsGaze = 25552;            // Thordan, 4.0s, visual
        public const uint DragonsGazeAoe = 25553;         // helper, gaze from Thordan
        public const uint DragonsGlory = 25554;           // helper, gaze from the eye
        public const uint FaithUnmoving = 25308;          // Grinnaux, 4.0s, 16y knockback
        public const uint Heavensflame = 25310;           // Charibert, 7.0s, visual
        public const uint HeavensflameAoe = 25311;        // helper->player, 10y spread
        public const uint HolyChain = 25312;              // helper, unbroken chain punishment

        // -- P6 Wroth Flames --
        public const uint WrothFlames = 27973;            // Nidhogg, 2.5s, visual
        public const uint AkhMornFirst = 27974;           // Nidhogg->player, 8.0s, 6y stack
        public const uint AkhMornRest = 27975;            // Nidhogg->player, instant, 6y stack
        public const uint CauterizeHraesvelgr = 27967;    // Hraesvelgr, 5.0s, 80x22 line
        public const uint FlameBlast = 26409;             // Scarlet Price, 5.0s, 44x6 cross
        public const uint HotWing = 27947;                // Nidhogg, 5.5s, visual
        public const uint HotWingAoe = 27948;             // helper, 6.5s, 50x21 line (both sides)
        public const uint HotTail = 27949;                // Nidhogg, 5.5s, visual
        public const uint HotTailAoe = 27950;             // helper, 6.5s, 50x16 line (centre)
        public const uint SpreadingFlames = 29739;        // helper->player, 5y spread
        public const uint EntangledFlames = 29740;        // helper->player, 4y two-man stack
    }

    public static class StatusId
    {
        public const ushort Doom = 2976;
        public const ushort BurningChains = 769;
        public const ushort SpreadingFlames = 2758;
        public const ushort EntangledFlames = 2759;
    }

    public static class TetherId
    {
        public const ushort Heavensflame = 9;
    }

    public static class LockonId
    {
        public const uint FirechainCircle = 281;      // r1fz_firechain_01x
        public const uint FirechainTriangle = 282;    // r1fz_firechain_02x
        public const uint FirechainSquare = 283;      // r1fz_firechain_03x
        public const uint FirechainCross = 284;       // r1fz_firechain_04x
        // UNVERIFIED: the generic stack marker; the duty's own Akh Morn icon was never captured.
        public const uint Stack = 100;
    }

    public static class KnockbackId
    {
        // 16y away from the caster. UNVERIFIED which 16y row Faith Unmoving sends; the distance is.
        public const uint FaithUnmoving = 77;
    }

    public static class MapEffect
    {
        // The Dragon's Eye on the arena rim, slot = compass index (0 = N, clockwise).
        public const uint EyeShow = 0x00020001;
        // UNVERIFIED: the flags retail sends with the eye's state 0x20 when it goes.
        public const uint EyeHide = 0x00080004;
    }

    public static class Duration
    {
        public const float Doom = 26f;
        public const float WrothDebuffs = 23.8f;
    }

    public static class Geometry
    {
        public const float P5ArenaRadius = 21f;
        public const float P6ArenaHalfSize = 21f;

        // -- P5 Death of the Heavens, authored with Guerrique at relative north --
        public static readonly Vector3 GuerriqueSpot = new(0f, 0f, -9f);
        public static readonly Vector3 CharibertSpot = new(0f, 0f, 9f);
        public static readonly Vector3 VidofnirSpot = new(0f, 0f, 25f);
        public static readonly Vector3 DarkscaleSpot = new(0f, 0f, 26.4f);
        // The two side divers sit on one side of the arena, both diving through its centre.
        public static readonly Vector3 NorthDiverSpot = new(-20f, 0f, -11.3f);
        public static readonly Vector3 SouthDiverSpot = new(-20f, 0f, 11.3f);
        public const float EyeDistance = 40f;
        public const float ThordanPerchDistance = 23f;
        public const float HeavyImpactRingWidth = 6f;
        public const float CleanseRadius = 2.5f;
        public const float TwisterRadius = 2f;
        public const float ChainBreakDistance = 32f;

        // -- P6 Wroth Flames --
        public static readonly Vector3 NidhoggSpot = new(-22f, 0f, 0f);
        public static readonly Vector3 HraesvelgrSpot = new(22f, 0f, 0f);
        public const float DiveLaneSpacing = 11f;
        public const float DiveStartDistance = 34f;
        public const float OrbSetDistance = 12.9f;
        public const float OrbSpacing = 4.3f;
        public const float AkhMornVoidzoneRadius = 6f;
        public const float HotWingOffset = 18.5f;
    }
}
