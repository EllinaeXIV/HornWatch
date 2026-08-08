using System;
using System.Numerics;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Modules.OccultCrescent;

public sealed record TransportPad(string Name, float MapX, float MapY, float Radius)
{
    public Vector3 Position => new(ToWorld(MapX), 0f, ToWorld(MapY));

    private static float ToWorld(float map) => (((map - 1f) / 41f) * 2048f) - 1024f;
}

public sealed record DetachedRegion(
    string Name, float MapLeft, float MapRight, float MapTop, float MapBottom,
    TransportPad WayIn, TransportPad WayOut)
{
    public bool Contains(Vector3 point) =>
        ToMap(point.X) >= MapLeft && ToMap(point.X) <= MapRight &&
        ToMap(point.Z) >= MapTop && ToMap(point.Z) <= MapBottom;

    private static float ToMap(float world) => (((world + 1024f) / 2048f) * 41f) + 1f;
}

public sealed class OccultTransportNetwork(Func<uint> currentTerritory) : ITransportNetwork
{
    private static readonly DetachedRegion[] None = [];

    private static readonly DetachedRegion[] NorthHorn =
    [
        new("south-west island", 1f, 11f, 32f, 41f,
            new TransportPad("wind pad to the south-west island", 4.6f, 32.4f, 12f),
            new TransportPad("wind pad back to the mainland", 3.2f, 34.0f, 12f)),
    ];

    public TransportStep? StepTowards(Vector3 from, Vector3 destination)
    {
        foreach (var region in Regions)
        {
            var leaving = region.Contains(from);
            var arriving = region.Contains(destination);

            if (leaving == arriving)
            {
                continue;
            }

            var pad = arriving ? region.WayIn : region.WayOut;

            return new TransportStep(pad.Name, pad.Position);
        }

        return null;
    }

    private DetachedRegion[] Regions =>
        currentTerritory() == OccultTerritories.NorthHorn ? NorthHorn : None;
}
