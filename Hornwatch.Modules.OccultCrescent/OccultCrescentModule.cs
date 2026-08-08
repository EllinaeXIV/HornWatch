using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Encounters;
using Hornwatch.Core.Guides;
using Hornwatch.Core.Hazards;
using Hornwatch.Core.Jobs;
using Hornwatch.Core.Modules;
using Hornwatch.Core.Navigation;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultCrescentModule : FieldModuleBase
{
    private readonly OccultEncounterSource encounters;
    private readonly RespawnTracker potTracker;
    private readonly OccultTreasureSpotter treasureSpotter;
    private readonly OccultHazardSource hazards;

    public OccultCrescentModule(
        IDataManager data,
        IObjectTable objects,
        IFateTable fates,
        IDataCache cache,
        IRespawnStore respawnStore,
        string resourceDirectory,
        IPluginLog log,
        Func<uint> currentTerritory,
        Func<Vector3, Vector3> resolveGround)
        : base("occult-crescent", "module.occultCrescent", OccultTerritories.All)
    {
        var catalog = new PhantomJobCatalog(data, cache);
        var projection = new OccultMapProjection(data, cache, resolveGround, log);
        var pots = new PotCatalog(data, cache, projection);

        encounters = new OccultEncounterSource(
            fates, pots, new TowerCatalog(projection), currentTerritory);
        JobSource = new OccultJobSource(catalog, objects);
        treasureSpotter = new OccultTreasureSpotter(objects, log);
        hazards = new OccultHazardSource(objects, currentTerritory);
        potTracker = new RespawnTracker(respawnStore, new PotRotationRule(pots), currentTerritory);

        Provide<IEncounterSource>(encounters);
        Provide<ISpecialJobSource>(JobSource);
        Provide<RespawnTracker>(potTracker);
        Provide<IGuideCatalog>(new OccultGuideCatalog(data, cache));
        Provide<ITeleportNetwork>(new OccultTeleportNetwork(currentTerritory));
        Provide<ITreasureSource>(new OccultTreasureCatalog(resourceDirectory, new OccultMapLayers(data, cache), log));
        Provide<ISpottedTreasureSource>(treasureSpotter);
        Provide<IHazardSource>(hazards);
    }

    public OccultJobSource JobSource { get; }

    public override void Update()
    {
        encounters.Refresh();
        potTracker.Observe(encounters.Active);
        treasureSpotter.Refresh();
        hazards.Refresh();
    }

    public override void OnActivated()
    {
        potTracker.PruneStale(TimeSpan.FromHours(1));
    }

    public override void OnDeactivated() => potTracker.Invalidate();
}
