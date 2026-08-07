using System;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Encounters;
using Hornwatch.Core.Guides;
using Hornwatch.Core.Jobs;
using Hornwatch.Core.Modules;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultCrescentModule : FieldModuleBase
{
    private readonly OccultEncounterSource encounters;
    private readonly RespawnTracker potTracker;

    public OccultCrescentModule(
        IDataManager data,
        IObjectTable objects,
        IFateTable fates,
        IDataCache cache,
        IRespawnStore respawnStore,
        Func<uint> currentTerritory)
        : base("occult-crescent", "module.occultCrescent", OccultTerritories.All)
    {
        var catalog = new PhantomJobCatalog(data, cache);
        var pots = new PotCatalog(data, cache);

        encounters = new OccultEncounterSource(fates, pots);
        JobSource = new OccultJobSource(catalog, objects);
        potTracker = new RespawnTracker(respawnStore, new PotRotationRule(pots), currentTerritory);

        Provide<IEncounterSource>(encounters);
        Provide<ISpecialJobSource>(JobSource);
        Provide<RespawnTracker>(potTracker);
        Provide<IGuideCatalog>(new OccultGuideCatalog(data, cache));
        Provide<ITeleportNetwork>(new OccultTeleportNetwork(currentTerritory));
    }

    public OccultJobSource JobSource { get; }

    public override void Update()
    {
        encounters.Refresh();
        potTracker.Observe(encounters.Active);
    }

    public override void OnActivated()
    {
        potTracker.PruneStale(TimeSpan.FromHours(1));
    }

    public override void OnDeactivated() => potTracker.Invalidate();
}
