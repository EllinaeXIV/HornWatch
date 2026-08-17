using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Hornwatch.Core;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Windows.Map;

public sealed unsafe class MinimapMarkers(
    Func<ITreasureSource?> treasures,
    Func<uint> currentTerritory,
    Func<uint> currentMap,
    Func<Vector3?> playerPosition,
    Func<IReadOnlySet<TreasureKind>> shownKinds,
    Func<bool> wanted,
    IPluginLog log)
{
    private const float MarkerRange = 200f;

    private const float RebuildEveryYalms = 20f;

    private string signature = string.Empty;
    private bool placedAny;

    public void Tick()
    {
        if (!wanted())
        {
            Clear();
            return;
        }

        if (playerPosition() is not { } here)
        {
            return;
        }

        var wantedSignature =
            $"{currentTerritory()}:{currentMap()}:{Fingerprint(shownKinds())}:{Cell(here)}";

        if (wantedSignature == signature)
        {
            return;
        }

        signature = wantedSignature;
        Rebuild(here);
    }

    public void Clear()
    {
        if (!placedAny)
        {
            return;
        }

        signature = string.Empty;
        placedAny = false;

        if (AgentMap.Instance() is var agent && agent != null)
        {
            agent->ResetMiniMapMarkers();
        }
    }

    private void Rebuild(Vector3 here)
    {
        var agent = AgentMap.Instance();
        if (agent == null)
        {
            return;
        }

        agent->ResetMiniMapMarkers();
        placedAny = false;

        var kinds = shownKinds();
        if (kinds.Count == 0 || treasures() is not { } source)
        {
            return;
        }

        var onThisLayer = currentMap();
        var placed = 0;

        foreach (var point in source.PointsIn(currentTerritory()))
        {
            var icon = TreasureVisuals.IconOf(point.Kind);

            if (icon == 0 ||
                point.MapId != onThisLayer ||
                !kinds.Contains(point.Kind) ||
                here.GroundDistanceTo(point.Position) > MarkerRange)
            {
                continue;
            }

            agent->AddMiniMapMarker(point.Position, icon);
            placed++;
        }

        placedAny = placed > 0;

        log.Debug($"[minimap] {placed} markers within {MarkerRange:F0}y on map {onThisLayer}.");
    }

    private static string Cell(Vector3 position) =>
        $"{(int)(position.X / RebuildEveryYalms)}/{(int)(position.Z / RebuildEveryYalms)}";

    private static string Fingerprint(IReadOnlySet<TreasureKind> kinds)
    {
        var mask = 0;

        foreach (var kind in kinds)
        {
            mask |= 1 << (int)kind;
        }

        return mask.ToString();
    }
}
