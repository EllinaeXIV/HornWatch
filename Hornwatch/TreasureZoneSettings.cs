using System;
using System.Collections.Generic;
using Hornwatch.Core.Treasure;

namespace Hornwatch;

[Serializable]
public sealed class TreasureZoneSettings
{
    public HashSet<TreasureKind> ShownMarkers { get; set; } =
        [TreasureKind.BronzeCoffer, TreasureKind.SilverCoffer];

    public bool ShowToolbar { get; set; } = true;

    public bool ShowOnMinimap { get; set; }

    public TreasureAlertSettings Alerts { get; set; } = new();

    public TreasureRouteOptions Route { get; set; } = new();
}
