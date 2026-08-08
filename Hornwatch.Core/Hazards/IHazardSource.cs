using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Core.Hazards;

public sealed record Hazard(ulong ObjectId, string Name, byte Level, Vector3 Position);

public interface IHazardSource
{
    byte DangerousFromLevel { get; }

    IReadOnlyList<Hazard> Active { get; }

    int CountAround(Vector3 point, float radius);

    Hazard? ClosestTo(Vector3 point);

    bool IsDangerous(Vector3 point, float radius);

    bool IsUnderground(Vector3 point);

    bool IsInHostileArea(Vector3 point);

    string? AreaNameAt(Vector3 point);
}
