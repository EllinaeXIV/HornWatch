using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using LuminaMap = Lumina.Excel.Sheets.Map;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace Hornwatch.Modules.OccultCrescent;

public sealed record MapLayers(uint Surface, uint? Underground);

public sealed class OccultMapLayers(IDataManager data, IDataCache cache)
{
    public MapLayers Of(uint territoryId) => cache.GetOrCreate($"occult.map.layers.{territoryId}", () =>
    {
        var territories = data.GetExcelSheet<LuminaTerritoryType>();
        if (territories == null || !territories.TryGetRow(territoryId, out var territory))
        {
            return new MapLayers(0, null);
        }

        var surface = territory.Map.RowId;

        if (territory.Map.ValueNullable is not { MapIndex: > 0 } defaultMap)
        {
            return new MapLayers(surface, null);
        }

        var deepest = defaultMap;
        var maps = data.GetExcelSheet<LuminaMap>();

        if (maps != null)
        {
            foreach (var map in maps)
            {
                if (map.PlaceName.RowId == defaultMap.PlaceName.RowId && map.MapIndex > deepest.MapIndex)
                {
                    deepest = map;
                }
            }
        }

        return new MapLayers(surface, deepest.RowId == surface ? null : deepest.RowId);
    });
}
