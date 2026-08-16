using System;
using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Core.Encounters;

public sealed record RespawnEntry(
    string Name,
    uint TerritoryId,
    DateTimeOffset ExpectedAt,
    string? LabelKey,
    Vector3? Position);

public interface IRespawnStore
{
    Dictionary<string, RespawnEntry> Entries { get; }

    uint CalibratedTerritoryId { get; set; }

    SessionWitness? Witness { get; set; }

    Dictionary<uint, Vector3> SeenAt { get; }

    void Save();
}

public interface IRespawnRule
{
    string? SlotOf(TrackedEncounter encounter, uint territoryId);

    IReadOnlyList<string> SlotsIn(uint territoryId);

    RespawnEntry? Next(TrackedEncounter ended, uint territoryId, DateTimeOffset endedAt);

    Vector3? PositionFor(RespawnEntry entry, uint territoryId);
}

public sealed record RespawnPrediction(
    string? Name,
    string? LabelKey,
    Vector3? Position,
    DateTimeOffset? ExpectedAt)
{
    public static readonly RespawnPrediction Unknown = new(null, null, null, null);

    public bool IsKnown => ExpectedAt.HasValue;

    public TimeSpan Remaining => ExpectedAt is { } at ? at - DateTimeOffset.UtcNow : TimeSpan.Zero;

    public bool IsDue => IsKnown && Remaining <= TimeSpan.Zero;
}

public sealed class RespawnTracker(IRespawnStore store, IRespawnRule rule, Func<uint> currentTerritory)
{
    private static readonly TimeSpan CorroborationWindow = TimeSpan.FromSeconds(45);

    private readonly Dictionary<string, TrackedEncounter> previouslySeen = new();

    private readonly HashSet<string> occupied = new();

    private DateTimeOffset? corroborateBy;

    public bool IsUnproven => corroborateBy.HasValue;

    public void Observe(IReadOnlyList<TrackedEncounter> active)
    {
        var territory = currentTerritory();

        if (territory != store.CalibratedTerritoryId)
        {
            store.CalibratedTerritoryId = territory;
            store.Save();
            SuspendUntilRecognised(territory);
        }

        ResolveCorroboration(active, territory);

        Dictionary<string, TrackedEncounter> present = [];

        foreach (var encounter in active)
        {
            if (rule.SlotOf(encounter, territory) is not { } slot)
            {
                continue;
            }

            present[slot] = encounter;

            if (store.Entries.Remove(slot))
            {
                store.Save();
            }
        }

        foreach (var (slot, seen) in previouslySeen)
        {
            if (present.ContainsKey(slot))
            {
                continue;
            }

            var next = rule.Next(seen, territory, DateTimeOffset.UtcNow);
            if (next == null)
            {
                continue;
            }

            store.Entries[slot] = next;
            store.Save();
        }

        previouslySeen.Clear();
        occupied.Clear();
        foreach (var (slot, encounter) in present)
        {
            previouslySeen[slot] = encounter;
            occupied.Add(slot);
        }

        RenewWitness(active, territory);
    }

    private void ResolveCorroboration(IReadOnlyList<TrackedEncounter> active, uint territory)
    {
        if (corroborateBy is not { } deadline)
        {
            return;
        }

        if (store.Witness is { } witness)
        {
            foreach (var encounter in active)
            {
                if (witness.IsCorroboratedBy(encounter, territory))
                {
                    corroborateBy = null;
                    return;
                }
            }
        }

        if (DateTimeOffset.UtcNow <= deadline)
        {
            return;
        }

        corroborateBy = null;
        Forget();
    }

    private void RenewWitness(IReadOnlyList<TrackedEncounter> active, uint territory)
    {
        if (corroborateBy.HasValue)
        {
            return;
        }

        TrackedEncounter? oldest = null;

        foreach (var encounter in active)
        {
            if (encounter.StartedAt is not { } started || encounter.SourceId == 0)
            {
                continue;
            }

            if (oldest?.StartedAt is not { } best || started < best)
            {
                oldest = encounter;
            }
        }

        if (oldest == null || SessionWitness.Of(oldest, territory) is not { } witness || witness == store.Witness)
        {
            return;
        }

        store.Witness = witness;
        store.Save();
    }

    public void SuspendUntilRecognised(uint territoryId)
    {
        Detach();

        if (store.Entries.Count == 0 || store.Witness is not { } witness || witness.TerritoryId != territoryId)
        {
            corroborateBy = null;
            Forget();
            return;
        }

        corroborateBy = DateTimeOffset.UtcNow + CorroborationWindow;
    }

    public void Detach()
    {
        previouslySeen.Clear();
        occupied.Clear();
    }

    private void Forget()
    {
        var hadAnything = store.Entries.Count > 0 || store.Witness != null;

        store.Entries.Clear();
        store.Witness = null;

        if (hadAnything)
        {
            store.Save();
        }
    }

    public IReadOnlyList<RespawnPrediction> Predictions
    {
        get
        {
            var territory = currentTerritory();
            var slots = rule.SlotsIn(territory);
            var result = new List<RespawnPrediction>(slots.Count);

            foreach (var slot in slots)
            {
                if (occupied.Contains(slot))
                {
                    continue;
                }

                result.Add(!IsUnproven && store.Entries.TryGetValue(slot, out var entry) && entry.TerritoryId == territory
                    ? new RespawnPrediction(
                        entry.Name,
                        entry.LabelKey,
                        rule.PositionFor(entry, territory) ?? entry.Position,
                        entry.ExpectedAt)
                    : RespawnPrediction.Unknown);
            }

            result.Sort(CompareByExpectedTime);
            return result;
        }
    }

    public void PruneStale(TimeSpan graceAfterDue)
    {
        var cutoff = DateTimeOffset.UtcNow - graceAfterDue;
        var removed = false;

        foreach (var key in new List<string>(store.Entries.Keys))
        {
            var entry = store.Entries[key];

            if (entry.ExpectedAt < cutoff || entry.TerritoryId == 0)
            {
                store.Entries.Remove(key);
                removed = true;
            }
        }

        if (removed)
        {
            store.Save();
        }
    }

    public void Invalidate(uint calibratedFor = 0)
    {
        Detach();
        corroborateBy = null;

        var hadEntries = store.Entries.Count > 0 || store.Witness != null;
        store.Entries.Clear();
        store.Witness = null;
        store.CalibratedTerritoryId = calibratedFor;

        if (hadEntries || calibratedFor != 0)
        {
            store.Save();
        }
    }

    private static int CompareByExpectedTime(RespawnPrediction a, RespawnPrediction b)
    {
        if (a.ExpectedAt is not { } left)
        {
            return b.ExpectedAt is null ? 0 : 1;
        }

        return b.ExpectedAt is { } right ? left.CompareTo(right) : -1;
    }
}
