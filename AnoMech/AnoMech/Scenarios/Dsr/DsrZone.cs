using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr;

public sealed class DsrZone : IZone
{
    public static readonly DsrZone Instance = new();
    public static readonly Phase P5 = new(Instance, "P5", null, 0,
        init: world => world.EnforceArenaBoundary(Geometry.P5ArenaRadius));
    public static readonly Phase P6 = new(Instance, "P6", null, 0,
        init: world => world.EnforceSquareArenaBoundary(Geometry.P6ArenaHalfSize));

    public string Name => "Dragonsong's Reprise";
    public uint TerritoryId => 968;
    public Vector3 Origin => new(100f, 0f, 100f);

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Ring", Core.Game.WaymarkPresets.Ring(12f))];

    public DutyInitData DutyInitData => DsrReplayData.DutyInitData;
}
