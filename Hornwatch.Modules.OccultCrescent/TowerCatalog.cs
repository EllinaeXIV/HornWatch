using System.Numerics;

namespace Hornwatch.Modules.OccultCrescent;

public sealed record TowerSite(uint TerritoryId, float MapX, float MapY);

public sealed class TowerCatalog(OccultMapProjection projection)
{
    private static readonly TowerSite[] Sites =
    [
        new(OccultTerritories.NorthHorn, 15.0f, 30.0f),
    ];

    public Vector3? PositionIn(uint territoryId)
    {
        foreach (var site in Sites)
        {
            if (site.TerritoryId == territoryId)
            {
                return projection.ToWorld(territoryId, site.MapX, site.MapY);
            }
        }

        return null;
    }
}
