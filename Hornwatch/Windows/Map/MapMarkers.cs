using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Treasure;
using KamiToolKit.Classes;
using KamiToolKit.MapOverlay;

namespace Hornwatch.Windows.Map;

public sealed class MapMarkers : IDisposable
{
    private static readonly Dictionary<TreasureKind, uint> Icons = new()
    {
        [TreasureKind.BronzeCoffer] = 60356,
        [TreasureKind.SilverCoffer] = 60355,
        [TreasureKind.PotNorth] = 60354,
        [TreasureKind.PotSouth] = 60354,
        [TreasureKind.SecondChance] = 61473,
        [TreasureKind.Bunny] = 25207,
        [TreasureKind.Survey] = 60357,
    };

    private const uint NextWaypointIcon = 60442;

    private readonly MapOverlayController overlay = new();

    private readonly Func<ITreasureSource?> treasures;
    private readonly Func<uint> currentTerritory;
    private readonly Func<IReadOnlySet<TreasureKind>> shownKinds;
    private readonly Func<IReadOnlyList<TreasurePoint>> plannedRoute;
    private readonly Func<int> routeIndex;
    private readonly Func<bool> showRoute;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IFramework framework;
    private readonly IPluginLog log;

    private string signature = string.Empty;
    private bool enabled;
    private bool needsRefresh;

    public MapMarkers(
        Func<ITreasureSource?> treasures,
        Func<uint> currentTerritory,
        Func<IReadOnlySet<TreasureKind>> shownKinds,
        Func<IReadOnlyList<TreasurePoint>> plannedRoute,
        Func<int> routeIndex,
        Func<bool> showRoute,
        IAddonLifecycle addonLifecycle,
        IFramework framework,
        IPluginLog log)
    {
        this.treasures = treasures;
        this.currentTerritory = currentTerritory;
        this.shownKinds = shownKinds;
        this.plannedRoute = plannedRoute;
        this.routeIndex = routeIndex;
        this.showRoute = showRoute;
        this.addonLifecycle = addonLifecycle;
        this.framework = framework;
        this.log = log;

        addonLifecycle.RegisterListener(AddonEvent.PostRefresh, "AreaMap", OnMapRefresh);
    }

    public void Tick()
    {
        if (!enabled)
        {
            enabled = true;
            overlay.Enable();
        }

        var wanted = $"{currentTerritory()}:{Fingerprint(shownKinds())}:{showRoute()}:{plannedRoute().Count}:{routeIndex()}";
        if (wanted == signature && !needsRefresh)
        {
            return;
        }

        signature = wanted;
        needsRefresh = false;
        Rebuild();
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "AreaMap", OnMapRefresh);

        framework.RunOnFrameworkThread(() =>
        {
            try
            {
                overlay.RemoveAllMarkers();
                overlay.Disable();
                overlay.Dispose();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Tearing down the map overlay failed.");
            }
        });
    }

    private void OnMapRefresh(AddonEvent type, AddonArgs args) => needsRefresh = true;

    private void Rebuild()
    {
        overlay.RemoveAllMarkers();

        var territory = currentTerritory();

        var placed = PlaceTreasure(territory) + PlaceRoute();

        log.Information($"[markers] {placed} markers in territory {territory}.");
    }

    private int PlaceRoute()
    {
        if (!showRoute())
        {
            return 0;
        }

        var route = plannedRoute();
        var placed = 0;

        for (var i = routeIndex(); i < route.Count; i++)
        {
            overlay.AddMarker(new MapMarkerInfo
            {
                AllowAnyMap = false,
                MapId = route[i].MapId,
                Position = new Vector2(route[i].Position.X, route[i].Position.Z),
                IconId = i == routeIndex() ? NextWaypointIcon : Icons[route[i].Kind],
            });

            placed++;
        }

        return placed;
    }

    private int PlaceTreasure(uint territory)
    {
        var kinds = shownKinds();

        if (kinds.Count == 0 || treasures() is not { } source)
        {
            return 0;
        }

        var placed = 0;

        foreach (var point in source.PointsIn(territory))
        {
            if (!kinds.Contains(point.Kind) || !Icons.TryGetValue(point.Kind, out var icon))
            {
                continue;
            }

            overlay.AddMarker(new MapMarkerInfo
            {
                AllowAnyMap = false,
                MapId = point.MapId,
                Position = new Vector2(point.Position.X, point.Position.Z),
                IconId = icon,
            });

            placed++;
        }

        return placed;
    }

    private static string Fingerprint(IReadOnlySet<TreasureKind> kinds)
    {
        var mask = 0;
        foreach (var kind in kinds)
        {
            mask |= 1 << (int)kind;
        }

        return mask.ToString();
    }
}
