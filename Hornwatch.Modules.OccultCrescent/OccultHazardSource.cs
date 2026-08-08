using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Hazards;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultHazardSource(IObjectTable objects, Func<uint> currentTerritory) : IHazardSource
{
    private static readonly Dictionary<uint, byte> ThresholdByTerritory = new()
    {
        [OccultTerritories.SouthHorn] = 21,
        [OccultTerritories.NorthHorn] = 41,
    };

    private const byte NoThreshold = byte.MaxValue;

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
            if (candidate is not IBattleNpc npc || npc.OwnerId != 0)
            {
                continue;
            }

            if (npc.IsDead || npc.MaxHp == 0 || npc.Level < threshold)
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
            if (Ground(hazard.Position, point) <= radius)
            {
                found++;
            }
        }

        return found;
    }

    public Hazard? ClosestTo(Vector3 point)
    {
        Hazard? closest = null;
        var bestDistance = float.MaxValue;

        foreach (var hazard in active)
        {
            var distance = Ground(hazard.Position, point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = hazard;
            }
        }

        return closest;
    }

    private static float Ground(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
