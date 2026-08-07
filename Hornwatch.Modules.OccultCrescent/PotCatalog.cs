using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using LuminaFate = Lumina.Excel.Sheets.Fate;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace Hornwatch.Modules.OccultCrescent;

public enum PotSide
{
    North,
    South,
}

public sealed record PotFate(ushort FateId, uint TerritoryId, PotSide Side, float MapX, float MapY);

public sealed class PotCatalog
{
    private static readonly PotFate[] Pots =
    {
        new(1976, OccultTerritories.SouthHorn, PotSide.North, 25.6f, 17.1f),
        new(1977, OccultTerritories.SouthHorn, PotSide.South, 11.9f, 32.0f),
        new(2072, OccultTerritories.NorthHorn, PotSide.North, 26.2f, 11.6f),
        new(2073, OccultTerritories.NorthHorn, PotSide.South, 11.0f, 25.8f),
    };

    private readonly IDataManager data;
    private readonly IDataCache cache;

    private readonly Dictionary<ushort, Vector3> observed = new();

    public PotCatalog(IDataManager data, IDataCache cache)
    {
        this.data = data;
        this.cache = cache;
    }

    public static string LabelKeyFor(PotSide side) =>
        side == PotSide.North ? "pot.side.north" : "pot.side.south";

    public IReadOnlyList<PotFate> All => Pots;

    public PotFate? Find(uint fateId)
    {
        foreach (var pot in Pots)
        {
            if (pot.FateId == fateId)
            {
                return pot;
            }
        }

        return null;
    }

    public PotFate? Counterpart(PotFate pot)
    {
        foreach (var candidate in Pots)
        {
            if (candidate.TerritoryId == pot.TerritoryId && candidate.FateId != pot.FateId)
            {
                return candidate;
            }
        }

        return null;
    }

    public string NameOf(PotFate pot) => cache.GetOrCreate($"occult.pot.name.{pot.FateId}", () =>
    {
        var sheet = data.GetExcelSheet<LuminaFate>();
        if (sheet != null && sheet.TryGetRow(pot.FateId, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return string.Empty;
    });

    public void Observe(ushort fateId, Vector3 position) => observed[fateId] = position;

    public Vector3? PositionOf(PotFate pot)
    {
        if (observed.TryGetValue(pot.FateId, out var seen))
        {
            return seen;
        }

        var projection = ProjectionOf(pot.TerritoryId);
        if (projection == null)
        {
            return null;
        }

        return new Vector3(
            ToWorld(pot.MapX, projection.Factor, projection.OffsetX),
            0f,
            ToWorld(pot.MapY, projection.Factor, projection.OffsetY));
    }

    private sealed record MapProjection(float Factor, float OffsetX, float OffsetY)
    {
        public static readonly MapProjection Failed = new(0f, 0f, 0f);

        public bool IsUsable => Factor > 0f;
    }

    private MapProjection? ProjectionOf(uint territoryId)
    {
        var projection = cache.GetOrCreate($"occult.map.projection.{territoryId}", () =>
        {
            var territories = data.GetExcelSheet<LuminaTerritoryType>();
            if (territories == null || !territories.TryGetRow(territoryId, out var territory))
            {
                return MapProjection.Failed;
            }

            var map = territory.Map.ValueNullable;
            if (map == null || map.Value.SizeFactor == 0)
            {
                return MapProjection.Failed;
            }

            return new MapProjection(map.Value.SizeFactor / 100f, map.Value.OffsetX, map.Value.OffsetY);
        });

        return projection.IsUsable ? projection : null;
    }

    private static float ToWorld(float mapCoord, float factor, float offset) =>
        ((((mapCoord - 1f) * factor / 41f) * 2048f) - 1024f) / factor - offset;
}
