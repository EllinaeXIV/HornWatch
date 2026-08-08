using System;
using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Core.Treasure;

public sealed record TreasureRouteOptions
{
    public bool IncludeUnderground { get; init; }

    public bool IncludeBronze { get; init; } = true;

    public bool IncludeSilver { get; init; } = true;

    public bool ReturnToCampWhenDone { get; init; } = true;
}

public static class TreasureRoutePlanner
{
    private const int ImprovementPasses = 40;

    public static IReadOnlyList<TreasurePoint> Plan(
        IReadOnlyList<TreasurePoint> points,
        Vector3 from,
        TreasureRouteOptions options)
    {
        var candidates = Eligible(points, options);
        if (candidates.Count < 2)
        {
            return candidates;
        }

        var route = NearestNeighbour(candidates, from);
        Improve(route, from);
        return route;
    }

    private static List<TreasurePoint> Eligible(IReadOnlyList<TreasurePoint> points, TreasureRouteOptions options)
    {
        var eligible = new List<TreasurePoint>();

        foreach (var point in points)
        {
            if (!point.IsCoffer)
            {
                continue;
            }

            if (point.IsUnderground && !options.IncludeUnderground)
            {
                continue;
            }

            var wanted = point.Kind == TreasureKind.SilverCoffer ? options.IncludeSilver : options.IncludeBronze;
            if (wanted)
            {
                eligible.Add(point);
            }
        }

        return eligible;
    }

    private static List<TreasurePoint> NearestNeighbour(List<TreasurePoint> candidates, Vector3 from)
    {
        var remaining = new List<TreasurePoint>(candidates);
        var route = new List<TreasurePoint>(candidates.Count);
        var at = from;

        while (remaining.Count > 0)
        {
            var best = 0;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < remaining.Count; i++)
            {
                var distance = Ground(at, remaining[i].Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            at = remaining[best].Position;
            route.Add(remaining[best]);
            remaining.RemoveAt(best);
        }

        return route;
    }

    private static void Improve(List<TreasurePoint> route, Vector3 from)
    {
        for (var pass = 0; pass < ImprovementPasses; pass++)
        {
            var improved = false;

            for (var i = 0; i < route.Count - 1; i++)
            {
                for (var j = i + 1; j < route.Count; j++)
                {
                    var before = LegBefore(route, i, from) + Ground(route[j].Position, NextAfter(route, j));
                    var after = Ground(PreviousOf(route, i, from), route[j].Position)
                                + Ground(route[i].Position, NextAfter(route, j));

                    if (after >= before - 0.01f)
                    {
                        continue;
                    }

                    route.Reverse(i, j - i + 1);
                    improved = true;
                }
            }

            if (!improved)
            {
                return;
            }
        }
    }

    private static float LegBefore(List<TreasurePoint> route, int index, Vector3 from) =>
        Ground(PreviousOf(route, index, from), route[index].Position);

    private static Vector3 PreviousOf(List<TreasurePoint> route, int index, Vector3 from) =>
        index == 0 ? from : route[index - 1].Position;

    private static Vector3 NextAfter(List<TreasurePoint> route, int index) =>
        index + 1 < route.Count ? route[index + 1].Position : route[index].Position;

    private static float Ground(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
