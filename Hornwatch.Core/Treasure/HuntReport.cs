using System;
using System.Collections.Generic;
using System.Numerics;
using Hornwatch.Core.Hazards;

namespace Hornwatch.Core.Treasure;

public sealed class HuntReport
{
    private const float BrushRange = 30f;

    private readonly Dictionary<string, int> outcomes = [];
    private readonly HashSet<ulong> hazardsBrushed = [];
    private readonly HashSet<string> areasEntered = [];

    private DateTimeOffset startedAt;
    private Vector3? lastPosition;
    private TimeSpan inCombat;
    private byte highestHazardLevel;
    private int closestHazardYalms = int.MaxValue;

    public bool HasStarted { get; private set; }

    public double WalkedYalms { get; private set; }

    public void Begin()
    {
        outcomes.Clear();
        hazardsBrushed.Clear();
        areasEntered.Clear();

        startedAt = DateTimeOffset.UtcNow;
        lastPosition = null;
        inCombat = TimeSpan.Zero;
        highestHazardLevel = 0;
        closestHazardYalms = int.MaxValue;
        WalkedYalms = 0;
        HasStarted = true;
    }

    public void Record(string outcome) =>
        outcomes[outcome] = outcomes.TryGetValue(outcome, out var seen) ? seen + 1 : 1;

    public void Observe(Vector3? here, bool fighting, TimeSpan since, IHazardSource? hazards)
    {
        if (!HasStarted)
        {
            return;
        }

        if (fighting)
        {
            inCombat += since;
        }

        if (here is { } at)
        {
            if (lastPosition is { } previous)
            {
                WalkedYalms += Ground(previous, at);
            }

            lastPosition = at;

            if (hazards != null)
            {
                Watch(hazards, at);

                if (hazards.AreaNameAt(at) is { } area)
                {
                    areasEntered.Add(area);
                }

                if (hazards.IsUnderground(at))
                {
                    areasEntered.Add("subterrane");
                }
            }
        }
    }

    private void Watch(IHazardSource hazards, Vector3 at)
    {
        foreach (var hazard in hazards.Active)
        {
            var distance = Ground(hazard.Position, at);
            if (distance > BrushRange)
            {
                continue;
            }

            hazardsBrushed.Add(hazard.ObjectId);

            if (hazard.Level > highestHazardLevel)
            {
                highestHazardLevel = hazard.Level;
            }

            if ((int)distance < closestHazardYalms)
            {
                closestHazardYalms = (int)distance;
            }
        }
    }

    public string Summarise(int visited, int planned, byte dangerousFromLevel)
    {
        var elapsed = DateTimeOffset.UtcNow - startedAt;
        var breakdown = new List<string>();

        foreach (var (outcome, count) in outcomes)
        {
            breakdown.Add($"{outcome} {count}");
        }

        var closest = closestHazardYalms == int.MaxValue ? "none" : $"{closestHazardYalms}y";

        return $"[run] {visited}/{planned} coffers in {elapsed:mm\\:ss}, {WalkedYalms:F0}y walked, " +
               $"{inCombat:mm\\:ss} in combat | {string.Join(", ", breakdown)} | " +
               $"hazards (level {dangerousFromLevel}+) brushed within {BrushRange:F0}y: " +
               $"{hazardsBrushed.Count}, highest level {highestHazardLevel}, closest {closest} | " +
               $"marked areas entered: {(areasEntered.Count == 0 ? "none" : string.Join(", ", areasEntered))}";
    }

    private static float Ground(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
