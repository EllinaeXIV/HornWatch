using System;
using System.Collections.Generic;
using System.Numerics;
using Hornwatch.Core.Encounters;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class PotRotationRule(PotCatalog catalog) : IRespawnRule
{
    private static readonly TimeSpan RespawnCycle = TimeSpan.FromMinutes(30);

    private static readonly string[] NoSlots = [];

    private readonly Dictionary<uint, string[]> slotsByTerritory = new();

    public string? SlotOf(TrackedEncounter encounter, uint territoryId)
    {
        if (encounter.Kind != EncounterKind.NotableFate)
        {
            return null;
        }

        var pot = catalog.Find(encounter.SourceId);
        return pot != null && pot.TerritoryId == territoryId ? SlotKey(territoryId) : null;
    }

    public IReadOnlyList<string> SlotsIn(uint territoryId)
    {
        if (slotsByTerritory.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        string[] slots = HasPots(territoryId) ? [SlotKey(territoryId)] : NoSlots;
        slotsByTerritory[territoryId] = slots;
        return slots;
    }

    public RespawnEntry? Next(TrackedEncounter ended, uint territoryId, DateTimeOffset endedAt)
    {
        if (catalog.Find(ended.SourceId) is not { } finished || finished.TerritoryId != territoryId)
        {
            return null;
        }

        if (catalog.Counterpart(finished) is not { } next)
        {
            return null;
        }

        var anchor = ended.StartedAt ?? endedAt;

        return new RespawnEntry(
            catalog.NameOf(next),
            territoryId,
            anchor + RespawnCycle,
            PotCatalog.LabelKeyFor(next.Side),
            catalog.PositionOf(next));
    }

    public Vector3? PositionFor(RespawnEntry entry, uint territoryId)
    {
        foreach (var pot in catalog.All)
        {
            if (pot.TerritoryId == territoryId && PotCatalog.LabelKeyFor(pot.Side) == entry.LabelKey)
            {
                return catalog.PositionOf(pot);
            }
        }

        return null;
    }

    private bool HasPots(uint territoryId)
    {
        foreach (var pot in catalog.All)
        {
            if (pot.TerritoryId == territoryId)
            {
                return true;
            }
        }

        return false;
    }

    private static string SlotKey(uint territoryId) => $"pot@{territoryId}";
}
