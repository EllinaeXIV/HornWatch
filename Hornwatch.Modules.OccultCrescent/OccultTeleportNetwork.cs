using System;
using System.Collections.Generic;
using System.Numerics;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultTeleportNetwork : ITeleportNetwork
{
    private const float UnhallowedHamletRange = 60f;

    private static readonly ZoneTeleportPoint[] SouthHorn =
    {
        new(4944, new Vector3(830.7f, 0f, -696.0f)),   
        new(4928, new Vector3(-173.0f, 0f, -611.1f)),  
        new(4929, new Vector3(-358.1f, 0f, -121.0f)),  
        new(4930, new Vector3(306.9f, 0f, 305.7f)),    
        new(4947, new Vector3(-384.1f, 0f, 281.4f)),   
    };

    private static readonly ZoneTeleportPoint[] NorthHorn =
    {
        new(5571, new Vector3(880.0f, 0f, 880.1f)),    
        new(5576, new Vector3(451.7f, 0f, 528.8f)),    
        new(5572, new Vector3(357.7f, 0f, -554.3f)),   
        new(5573, new Vector3(-547.2f, 0f, 594.4f)),   
        new(5574, new Vector3(-388.6f, 0f, -440.5f)),  
        new(5575, new Vector3(-13.7f, 0f, -40.5f), UnhallowedHamletRange),
    };

    private readonly Func<uint> currentTerritory;

    public OccultTeleportNetwork(Func<uint> currentTerritory)
    {
        this.currentTerritory = currentTerritory;
    }

    public IReadOnlyList<ZoneTeleportPoint> Points => currentTerritory() switch
    {
        OccultTerritories.SouthHorn => SouthHorn,
        OccultTerritories.NorthHorn => NorthHorn,
        _ => Array.Empty<ZoneTeleportPoint>(),
    };

    public ZoneTeleportPoint? NearestTo(Vector3 destination)
    {
        ZoneTeleportPoint? best = null;
        var bestDistance = float.MaxValue;

        foreach (var point in Points)
        {
            var dx = point.Position.X - destination.X;
            var dz = point.Position.Z - destination.Z;
            var distance = (dx * dx) + (dz * dz);

            if (distance > point.MaxUsefulDistance * point.MaxUsefulDistance)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        return best;
    }
}
