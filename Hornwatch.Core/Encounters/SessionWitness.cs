using System;

namespace Hornwatch.Core.Encounters;

public sealed record SessionWitness(uint TerritoryId, uint EncounterId, long StartedAtEpoch)
{
    public static SessionWitness? Of(TrackedEncounter encounter, uint territoryId) =>
        encounter.StartedAt is { } started && encounter.SourceId != 0
            ? new SessionWitness(territoryId, encounter.SourceId, started.ToUnixTimeSeconds())
            : null;

    public bool IsCorroboratedBy(TrackedEncounter encounter, uint territoryId) =>
        territoryId == TerritoryId
        && encounter.SourceId == EncounterId
        && encounter.StartedAt is { } started
        && started.ToUnixTimeSeconds() == StartedAtEpoch;

    public override string ToString() =>
        $"encounter {EncounterId} started at {DateTimeOffset.FromUnixTimeSeconds(StartedAtEpoch):HH:mm:ss} in territory {TerritoryId}";
}
