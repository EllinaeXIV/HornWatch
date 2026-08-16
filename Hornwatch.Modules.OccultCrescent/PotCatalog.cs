using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using LuminaFate = Lumina.Excel.Sheets.Fate;

namespace Hornwatch.Modules.OccultCrescent;

public enum PotSide
{
    North,
    South,
}

public sealed record PotFate(ushort FateId, uint TerritoryId, PotSide Side, float MapX, float MapY);

public sealed class PotCatalog(
    IDataManager data,
    IDataCache cache,
    OccultMapProjection projection,
    Action<ushort, Vector3> remember,
    Func<ushort, Vector3?> recall)
{
    private static readonly PotFate[] Pots =
    [
        new(1976, OccultTerritories.SouthHorn, PotSide.North, 25.6f, 17.1f),
        new(1977, OccultTerritories.SouthHorn, PotSide.South, 11.9f, 32.0f),
        new(2072, OccultTerritories.NorthHorn, PotSide.North, 26.2f, 11.6f),
        new(2073, OccultTerritories.NorthHorn, PotSide.South, 11.3f, 26.4f),
    ];

    private readonly Dictionary<ushort, Vector3> observed = new();

    public static string LabelKeyFor(PotSide side) =>
        side == PotSide.North ? "pot.side.north" : "pot.side.south";

    public IReadOnlyList<PotFate> All => Pots;

    public PotFate? Find(uint fateId)
    {
        foreach (var pot in Pots)
        {
            if (pot.FateId == fateId)
            {
                return pot;
            }
        }

        return null;
    }

    public PotFate? Counterpart(PotFate pot)
    {
        foreach (var candidate in Pots)
        {
            if (candidate.TerritoryId == pot.TerritoryId && candidate.FateId != pot.FateId)
            {
                return candidate;
            }
        }

        return null;
    }

    public string NameOf(PotFate pot) => cache.GetOrCreate($"occult.pot.name.{pot.FateId}", () =>
    {
        var sheet = data.GetExcelSheet<LuminaFate>();
        if (sheet != null && sheet.TryGetRow(pot.FateId, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return string.Empty;
    });

    public void Observe(ushort fateId, Vector3 position)
    {
        if (observed.TryGetValue(fateId, out var known) && known == position)
        {
            return;
        }

        observed[fateId] = position;
        remember(fateId, position);
    }

    public Vector3? PositionOf(PotFate pot)
    {
        if (observed.TryGetValue(pot.FateId, out var seen))
        {
            return seen;
        }

        if (recall(pot.FateId) is { } remembered)
        {
            observed[pot.FateId] = remembered;
            return remembered;
        }

        return projection.ToWorld(pot.TerritoryId, pot.MapX, pot.MapY);
    }
}
