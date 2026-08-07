using System.Numerics;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class DisabledPathfinder : IPathfinder
{
    public bool IsAvailable => false;

    public string? UnavailableReasonKey => "nav.reason.disabledInSettings";

    public bool IsMoving => false;

    public Vector3? Destination => null;

    public Vector3 SnapToGround(Vector3 approximate) => approximate;

    public void MoveTo(Vector3 destination) { }

    public void Stop() { }
}
