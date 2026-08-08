using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultMapProjection(
    IDataManager data, IDataCache cache, Func<Vector3, Vector3> resolveGround, IPluginLog log)
{
    private readonly Dictionary<(uint Territory, float MapX, float MapY), Vector3> grounded = new();

    public Vector3? ToWorld(uint territoryId, float mapX, float mapY)
    {
        if (Of(territoryId) is not { } projection)
        {
            return null;
        }

        var key = (territoryId, mapX, mapY);
        if (grounded.TryGetValue(key, out var known))
        {
            return known;
        }

        var column = new Vector3(
            Axis(mapX, projection.Factor, projection.OffsetX),
            0f,
            Axis(mapY, projection.Factor, projection.OffsetY));

        var ground = resolveGround(column);

        if (ground != column)
        {
            grounded[key] = ground;
            log.Information($"[map] {mapX:F1}/{mapY:F1} in territory {territoryId} is world {ground}.");
        }

        return ground;
    }

    private sealed record Projection(float Factor, float OffsetX, float OffsetY)
    {
        public static readonly Projection Failed = new(0f, 0f, 0f);

        public bool IsUsable => Factor > 0f;
    }

    private Projection? Of(uint territoryId)
    {
        var projection = cache.GetOrCreate($"occult.map.projection.{territoryId}", () =>
        {
            var territories = data.GetExcelSheet<LuminaTerritoryType>();
            if (territories == null || !territories.TryGetRow(territoryId, out var territory))
            {
                return Projection.Failed;
            }

            var map = territory.Map.ValueNullable;
            if (map == null || map.Value.SizeFactor == 0)
            {
                return Projection.Failed;
            }

            return new Projection(map.Value.SizeFactor / 100f, map.Value.OffsetX, map.Value.OffsetY);
        });

        return projection.IsUsable ? projection : null;
    }

    private static float Axis(float mapCoord, float factor, float offset) =>
        ((((mapCoord - 1f) * factor / 41f) * 2048f) - 1024f) / factor - offset;
}
