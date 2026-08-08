using System;
using System.Numerics;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class PathfinderRouter(IPathfinder enabled, Func<bool> isEnabled) : IPathfinder
{
    private readonly IPathfinder disabled = new DisabledPathfinder();

    private IPathfinder Current => isEnabled() ? enabled : disabled;

    public bool IsAvailable => Current.IsAvailable;

    public string? UnavailableReasonKey => Current.UnavailableReasonKey;

    public bool IsMoving => Current.IsMoving;

    public Vector3? Destination => Current.Destination;

    public Vector3 SnapToGround(Vector3 approximate) => Current.SnapToGround(approximate);

    public Vector3 GroundLevelAt(Vector3 column) => enabled.GroundLevelAt(column);

    public void MoveTo(Vector3 destination) => Current.MoveTo(destination);

    public void Stop() => enabled.Stop();
}
