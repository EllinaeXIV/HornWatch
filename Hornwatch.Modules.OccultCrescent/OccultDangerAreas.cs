using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Modules.OccultCrescent;

public sealed record DangerArea(string Name, float MapLeft, float MapRight, float MapTop, float MapBottom)
{
    public bool Contains(Vector3 point) =>
        ToMap(point.X) >= MapLeft && ToMap(point.X) <= MapRight &&
        ToMap(point.Z) >= MapTop && ToMap(point.Z) <= MapBottom;

    private static float ToMap(float world) => (((world + 1024f) / 2048f) * 41f) + 1f;
}

public static class OccultDangerAreas
{
    private static readonly DangerArea[] None = [];

    private static readonly DangerArea[] NorthHorn =
    [
        new("north-west ruins", 1f, 18f, 2f, 9f),
        new("north-east ruins", 31f, 42f, 2f, 17f),
    ];

    private static readonly DangerArea[] SouthHorn =
    [
        new("north-west temple", 3f, 10f, 6f, 13f),
        new("western caverns", 2f, 15f, 14f, 23f),
        new("south-west ruins", 5f, 14f, 27f, 39f),
        new("eastern statues", 28f, 38f, 23f, 32f),
    ];

    public static IReadOnlyList<DangerArea> In(uint territoryId) => territoryId switch
    {
        OccultTerritories.NorthHorn => NorthHorn,
        OccultTerritories.SouthHorn => SouthHorn,
        _ => None,
    };

    public static DangerArea? Around(uint territoryId, Vector3 point)
    {
        foreach (var area in In(territoryId))
        {
            if (area.Contains(point))
            {
                return area;
            }
        }

        return null;
    }

    public static bool IsHostileArea(uint territoryId, Vector3 point) => Around(territoryId, point) != null;
}
