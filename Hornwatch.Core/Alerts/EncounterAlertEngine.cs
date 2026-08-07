using System;
using System.Collections.Generic;
using Hornwatch.Core.Encounters;

namespace Hornwatch.Core.Alerts;

public sealed record AlertEvent(TrackedEncounter Encounter);

public sealed class EncounterAlertEngine
{
    private readonly HashSet<string> known = new();

    public event Action<AlertEvent>? Appeared;

    public void Observe(IReadOnlyList<TrackedEncounter> current)
    {
        var stillPresent = new HashSet<string>(current.Count);

        foreach (var encounter in current)
        {
            stillPresent.Add(encounter.Id);

            if (known.Add(encounter.Id))
            {
                Appeared?.Invoke(new AlertEvent(encounter));
            }
        }

        known.IntersectWith(stillPresent);
    }

    public void Reset() => known.Clear();
}
