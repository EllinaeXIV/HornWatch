using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Hornwatch.Core;
using Hornwatch.Core.Hazards;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultHazardSource(IObjectTable objects, OccultDepths depths, Func<uint> currentTerritory) : IHazardSource
{
    private static readonly Dictionary<uint, byte> ThresholdByTerritory = new()
    {
        [OccultTerritories.SouthHorn] = 21,
        [OccultTerritories.NorthHorn] = 41,
    };

    private const byte NoThreshold = byte.MaxValue;

    private const uint NoOwner = 0xE0000000;

    private readonly List<Hazard> active = [];

    public byte DangerousFromLevel =>
        ThresholdByTerritory.TryGetValue(currentTerritory(), out var threshold) ? threshold : NoThreshold;

    public IReadOnlyList<Hazard> Active => active;

    public void Refresh()
    {
        active.Clear();

        var threshold = DangerousFromLevel;
        if (threshold == NoThreshold)
        {
            return;
        }

        foreach (var candidate in objects)
        {
            if (candidate is not IBattleNpc npc || npc.IsDead || npc.MaxHp == 0)
            {
                continue;
            }

            if (npc.OwnerId is not (0 or NoOwner) || npc.Level < threshold)
            {
                continue;
            }

            active.Add(new Hazard(npc.GameObjectId, npc.Name.TextValue, npc.Level, npc.Position));
        }
    }

    public int CountAround(Vector3 point, float radius)
    {
        var found = 0;

        foreach (var hazard in active)
        {
            if (hazard.Position.GroundDistanceTo(point) <= radius)
            {
                found++;
            }
        }

        return found;
    }

    public bool IsDangerous(Vector3 point, float radius) =>
        IsUnderground(point) ||
        OccultDangerAreas.IsHostileArea(currentTerritory(), point) ||
        CountAround(point, radius) > 0;

    public string? AreaNameAt(Vector3 point) => OccultDangerAreas.Around(currentTerritory(), point)?.Name;

    public bool IsUnderground(Vector3 point) => depths.IsUnderground(currentTerritory(), point.Y);

    public bool IsInHostileArea(Vector3 point) => OccultDangerAreas.IsHostileArea(currentTerritory(), point);

    public Hazard? ClosestTo(Vector3 point)
    {
        Hazard? closest = null;
        var bestDistance = float.MaxValue;

        foreach (var hazard in active)
        {
            var distance = hazard.Position.GroundDistanceTo(point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = hazard;
            }
        }

        return closest;
    }
}
