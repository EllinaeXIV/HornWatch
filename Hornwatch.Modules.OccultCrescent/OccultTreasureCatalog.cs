using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultTreasureCatalog : ITreasureSource
{
    private const string ResourceFile = "occult-treasures.json";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<uint, IReadOnlyList<TreasurePoint>> byTerritory = new();

    public OccultTreasureCatalog(string resourceDirectory, OccultMapLayers layers, OccultDepths depths, IPluginLog log)
    {
        var path = Path.Combine(resourceDirectory, ResourceFile);

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<RawPoint>>>(File.ReadAllText(path), ReadOptions);
            if (raw == null)
            {
                log.Error($"{ResourceFile} is empty; treasure features have no data to work with.");
                return;
            }

            foreach (var (territory, points) in raw)
            {
                if (!uint.TryParse(territory, out var territoryId))
                {
                    continue;
                }

                byTerritory[territoryId] = Convert(points, territoryId, layers.Of(territoryId), depths, log);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Could not read {ResourceFile}; treasure features are unavailable.");
        }
    }

    public IReadOnlyList<TreasurePoint> PointsIn(uint territoryId) =>
        byTerritory.TryGetValue(territoryId, out var points) ? points : [];

    private static IReadOnlyList<TreasurePoint> Convert(
        List<RawPoint> points, uint territoryId, MapLayers layers, OccultDepths depths, IPluginLog log)
    {
        var converted = new List<TreasurePoint>(points.Count);
        var unknownKinds = new HashSet<string>();

        foreach (var point in points)
        {
            if (!Enum.TryParse<TreasureKind>(point.Kind, out var kind))
            {
                unknownKinds.Add(string.IsNullOrWhiteSpace(point.Kind) ? "<missing>" : point.Kind);
                continue;
            }

            var underground = depths.IsUnderground(territoryId, point.Y);

            converted.Add(new TreasurePoint(
                kind,
                new Vector3(point.X, point.Y, point.Z),
                underground ? layers.Underground!.Value : layers.Surface,
                underground));
        }

        if (unknownKinds.Count > 0)
        {
            log.Warning($"Territory {territoryId} lists treasure kinds this build does not know: {string.Join(", ", unknownKinds)}");
        }

        return converted;
    }

    private sealed record RawPoint(string Kind, float X, float Y, float Z);
}
