using System;
using System.Numerics;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class PathfinderRouter : IPathfinder
{
    private readonly IPathfinder enabled;
    private readonly IPathfinder disabled = new DisabledPathfinder();
    private readonly Func<bool> isEnabled;

    public PathfinderRouter(IPathfinder enabled, Func<bool> isEnabled)
    {
        this.enabled = enabled;
        this.isEnabled = isEnabled;
    }

    private IPathfinder Current => isEnabled() ? enabled : disabled;

    public bool IsAvailable => Current.IsAvailable;

    public string? UnavailableReasonKey => Current.UnavailableReasonKey;

    public bool IsMoving => Current.IsMoving;

    public Vector3? Destination => Current.Destination;

    public Vector3 SnapToGround(Vector3 approximate) => Current.SnapToGround(approximate);

    public void MoveTo(Vector3 destination) => Current.MoveTo(destination);

    public void Stop() => enabled.Stop();
}
