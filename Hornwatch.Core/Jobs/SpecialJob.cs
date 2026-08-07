using System.Collections.Generic;

namespace Hornwatch.Core.Jobs;

public sealed record SpecialJob(int Id, string Name, uint IconId);

public sealed record SpecialJobProgress(int JobId, int Level, uint Experience, uint ExperienceToNext)
{
    public float Fraction => ExperienceToNext == 0 ? 0f : (float)Experience / ExperienceToNext;
}

public sealed record ZoneResource(string LabelKey, long Value, long? Maximum = null);

public interface ISpecialJobSource
{
    string SystemNameKey { get; }

    IReadOnlyList<SpecialJob> AllJobs { get; }

    SpecialJob? GetActiveJob(ulong gameObjectId);

    IReadOnlyList<SpecialJobProgress> LocalProgress { get; }

    IReadOnlyList<ZoneResource> LocalResources { get; }

    bool SupportsRemoteProgress { get; }
}
