using System.Collections.Generic;

namespace Hornwatch.Core.Encounters;

public interface IEncounterSource
{
    IReadOnlyList<TrackedEncounter> Active { get; }

    void Refresh();
}
