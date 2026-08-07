using System.Numerics;

namespace Hornwatch.Core.Navigation;

public interface ITeleporter
{
    bool IsAvailable { get; }

    string? UnavailableReasonKey { get; }

    bool IsBusy { get; }

    Vector3? BoardingPoint { get; }

    bool TeleportTo(uint placeNameId);

    void Tick();

    void Abort();
}
