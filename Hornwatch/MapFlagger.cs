using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace Hornwatch;

public sealed unsafe class MapFlagger(IGameGui gameGui, IDataManager data, IClientState clientState)
{
    public void Place(Vector3 worldPosition)
    {
        if (MapOf(clientState.TerritoryType) is not { } map)
        {
            return;
        }

        gameGui.OpenMapWithMapLink(clientState.TerritoryType, map, worldPosition);
    }

    public void Mark(Vector3 worldPosition)
    {
        var agent = AgentMap.Instance();

        if (agent == null || MapOf(clientState.TerritoryType) is not { } map)
        {
            return;
        }

        agent->SetFlagMapMarker(clientState.TerritoryType, map, worldPosition);
    }

    private uint? MapOf(uint territoryId)
    {
        var sheet = data.GetExcelSheet<LuminaTerritoryType>();

        return sheet != null && sheet.TryGetRow(territoryId, out var territory) ? territory.Map.RowId : null;
    }
}
