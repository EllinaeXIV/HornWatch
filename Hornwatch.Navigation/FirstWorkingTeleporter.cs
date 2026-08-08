using System.Numerics;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class FirstWorkingTeleporter(params ITeleporter[] candidates) : ITeleporter
{

    private ITeleporter? active;

    public bool IsAvailable
    {
        get
        {
            foreach (var candidate in candidates)
            {
                if (candidate.IsAvailable)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public string? UnavailableReasonKey => IsAvailable ? null : candidates[0].UnavailableReasonKey;

    public bool IsBusy => active?.IsBusy ?? false;

    public Vector3? BoardingPoint
    {
        get
        {
            foreach (var candidate in candidates)
            {
                if (candidate.BoardingPoint is { } point)
                {
                    return point;
                }
            }

            return null;
        }
    }

    public bool TeleportTo(uint placeNameId)
    {
        active = null;

        foreach (var candidate in candidates)
        {
            if (!candidate.IsAvailable || !candidate.TeleportTo(placeNameId))
            {
                continue;
            }

            active = candidate;
            return true;
        }

        return false;
    }

    public void Tick() => active?.Tick();

    public void Abort()
    {
        active?.Abort();
        active = null;
    }
}
