using System.Numerics;

namespace Hornwatch.Core.Navigation;

public sealed record TransportStep(string Name, Vector3 Entrance);

public interface ITransportNetwork
{
    TransportStep? StepTowards(Vector3 from, Vector3 destination);
}
