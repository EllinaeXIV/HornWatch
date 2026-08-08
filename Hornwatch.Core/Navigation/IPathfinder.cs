using System.Numerics;

namespace Hornwatch.Core.Navigation;

public interface IPathfinder
{
    bool IsAvailable { get; }

    string? UnavailableReasonKey { get; }

    bool IsMoving { get; }

    Vector3? Destination { get; }

    Vector3 SnapToGround(Vector3 approximate);

    Vector3 GroundLevelAt(Vector3 column);

    void MoveTo(Vector3 destination);

    void Stop();
}
