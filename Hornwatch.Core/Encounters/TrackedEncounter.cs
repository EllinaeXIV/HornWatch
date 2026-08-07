using System;
using System.Numerics;

namespace Hornwatch.Core.Encounters;

public enum EncounterKind
{
    Fate,

    NotableFate,

    CriticalEncounter,

    Raid,
}

public enum EncounterPhase
{
    Announced,

    Preparing,

    Running,
    Ending,
}

public sealed record TrackedEncounter
{
    public required string Id { get; init; }

    public uint SourceId { get; init; }

    public required EncounterKind Kind { get; init; }

    public required string Name { get; init; }

    public string? LabelKey { get; init; }

    public required EncounterPhase Phase { get; init; }

    public TimeSpan? TimeRemaining { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public float? Progress { get; init; }

    public int Participants { get; init; }
    public int MaxParticipants { get; init; }

    public Vector3? Position { get; init; }

    public float? Radius { get; init; }

    public bool IsNavigable => Position.HasValue;

    public bool IsJoinable =>
        Kind is not (EncounterKind.CriticalEncounter or EncounterKind.Raid)
        || Phase is EncounterPhase.Announced or EncounterPhase.Preparing;
}
