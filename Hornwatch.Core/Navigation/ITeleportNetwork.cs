using System;
using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Core.Navigation;

public sealed record ZoneTeleportPoint(
    uint PlaceNameId, Vector3 Position, float MaxUsefulDistance = float.MaxValue);

public interface ITeleportNetwork
{
    IReadOnlyList<ZoneTeleportPoint> Points { get; }

    ZoneTeleportPoint? NearestTo(Vector3 destination, Func<ZoneTeleportPoint, bool>? usable = null);
}
