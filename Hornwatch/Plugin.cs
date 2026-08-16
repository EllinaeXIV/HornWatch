using System;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Hornwatch.Alerts;
using Hornwatch.Core;
using Hornwatch.Core.Alerts;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Encounters;
using Hornwatch.Core.Hazards;
using Hornwatch.Core.Modules;
using Hornwatch.Core.Navigation;
using Hornwatch.Core.Treasure;
using Hornwatch.Localization;
using Hornwatch.Modules.OccultCrescent;
using Hornwatch.Navigation;
using Hornwatch.Theming;
using Hornwatch.Windows;
using Hornwatch.Windows.Map;
using KamiToolKit;

namespace Hornwatch;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    private readonly FieldModuleRegistry modules;
    private readonly EncounterAlertEngine alertEngine = new();
    private readonly AlertPlayer alertPlayer;
    private readonly JsonLocalizer localizer;
    private readonly MemoryDataCache cache;
    private readonly PluginPresence installed;
    private readonly TreasureSpottedWatcher treasureWatcher;
    private readonly MapMarkers mapMarkers;
    private readonly TreasureMapToolbar treasureToolbar;
    private readonly TreasureHunt treasureHunt;
    private readonly RouteOverlay routeOverlay;
    private readonly NextPotBarEntry potBar;
    private readonly SelectYesnoConfirmer confirmation;

    private readonly WindowSystem windowSystem = new(PluginMeta.InternalName);
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    private uint lastTerritory = uint.MaxValue;

    public Plugin()
    {
        Svc.Init(PluginInterface);

        KamiToolKitLibrary.Initialize(PluginInterface, PluginMeta.Name);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        cache = new MemoryDataCache(() => BuildFlavour.DeveloperToolsAvailable && Configuration.DeveloperMode);

        var resourceDirectory = Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, "Resources");

        localizer = new JsonLocalizer(
            resourceDirectory,
            Svc.ClientState,
            () => Configuration.LanguageOverride,
            Svc.Log);

        var respawnStore = new ConfigurationRespawnStore(Configuration);

        installed = new PluginPresence(PluginInterface);

        var pathfinder = new PathfinderRouter(
            new VnavmeshPathfinder(PluginInterface, installed, Svc.Log),
            () => Configuration.AutoTravelEnabled && Configuration.AutoTravelRiskAcknowledged);

        modules = new FieldModuleRegistry(
        [
            new OccultCrescentModule(
                Svc.Data, Svc.Objects, Svc.Fates, cache, respawnStore,
                resourceDirectory, Svc.Log,
                () => Svc.ClientState.TerritoryType,
                pathfinder.GroundLevelAt,
                () => Configuration.AutoTravelEnabled && Configuration.AutoTravelRiskAcknowledged),
        ]);

        TravelCoordinator? coordinator = null;

        confirmation = new SelectYesnoConfirmer(
            Svc.GameGui, Svc.Data,
            Svc.AddonLifecycle,
            () => coordinator is { Phase: not TravelPhase.Idle },
            Svc.Log);

        coordinator = new TravelCoordinator(
            pathfinder,
            new FirstWorkingTeleporter(
                new LifestreamTeleporter(PluginInterface, installed, Svc.Data, Svc.Log),
                new ShardTeleporter(
                    Svc.Objects, Svc.Targets, Svc.GameGui, Svc.Data,
                    confirmation, Svc.Log,
                    () => Svc.Objects.LocalPlayer?.Position)),
            new MountService(Svc.Condition, () => Configuration.UseMount ? Configuration.MountId : null),
            new ReturnService(Svc.Condition),
            confirmation,
            () => modules.Capability<ITeleportNetwork>(),
            () => modules.Capability<ITransportNetwork>(),
            () => Svc.Objects.LocalPlayer?.Position,
            () => Svc.ClientState.TerritoryType,
            IsEncounterStillActive,
            () => Configuration.UseTeleport,
            () => Configuration.UseReturn,
            () => Configuration.AutoTravelEnabled && Configuration.AutoTravelRiskAcknowledged,
            () => Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat],
            new JumpService(),
            Svc.Log);

        Travel = coordinator;

        alertPlayer = new AlertPlayer(
            Configuration, localizer, Svc.Chat,
            () => modules.Active?.Key,
            () => Svc.ClientState.TerritoryType);
        alertEngine.Appeared += alertPlayer.Handle;

        var flagger = new MapFlagger(Svc.GameGui, Svc.Data, Svc.ClientState);

        treasureWatcher = new TreasureSpottedWatcher(
            () => modules.Capability<ISpottedTreasureSource>(),
            () => Svc.ClientState.TerritoryType,
            () => Svc.ClientState.MapId,
            () => Configuration.TreasureFor(Svc.ClientState.TerritoryType).Alerts,
            localizer, Svc.Chat, Svc.Toasts, Svc.Data, flagger);

        treasureHunt = new TreasureHunt(
            Travel,
            () => modules.Capability<ISpottedTreasureSource>(),
            () => modules.Capability<IHazardSource>(),
            () => modules.Capability<ITreasureSurvey>(),
            () => Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat],
            () => Svc.Objects.LocalPlayer?.Position,
            () => Svc.ClientState.TerritoryType,
            Svc.Log);

        mapMarkers = new MapMarkers(
            () => modules.Capability<ITreasureSource>(),
            () => Svc.ClientState.TerritoryType,
            () => Configuration.TreasureFor(Svc.ClientState.TerritoryType).ShownMarkers,
            () => treasureHunt.Route,
            () => treasureHunt.Index,
            () => Configuration.ShowRouteOverlay && treasureHunt.State != HuntState.Idle,
            Svc.AddonLifecycle, Svc.Framework, Svc.Log);

        treasureToolbar = new TreasureMapToolbar(
            Svc.Framework,
            Svc.GameGui,
            localizer,
            () => modules.Capability<ITreasureSource>() != null
                  && Configuration.TreasureFor(Svc.ClientState.TerritoryType).ShowToolbar,
            () => Configuration.TreasureFor(Svc.ClientState.TerritoryType).ShownMarkers,
            (kind, shown) => SetTreasureMarkerShown(Svc.ClientState.TerritoryType, kind, shown),
            Svc.Log);

        routeOverlay = new RouteOverlay(
            Travel, treasureHunt, Svc.GameGui, Svc.Objects, localizer,
            () => Configuration.ShowRouteOverlay,
            () => modules.Active != null);

        var theme = new ThemeManager(Svc.Data, Svc.GameConfig, cache, Configuration);

        mainWindow = new MainWindow(this, modules, localizer, Travel, flagger, treasureHunt, theme);

        potBar = new NextPotBarEntry(
            Svc.ServerBar, modules, localizer,
            () => Configuration.ShowPotBarEntry,
            () => mainWindow.Reveal(MainWindow.WatchTabKey));

        configWindow = new ConfigWindow(this, localizer, modules, new MountCatalog(Svc.Data, cache), installed,
            SetTreasureMarkerShown, SetTreasureOverlayShown, theme);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(configWindow);

        Svc.Commands.AddHandler(PluginMeta.Command, new CommandInfo(OnCommand)
        {
            HelpMessage = localizer.Get("plugin.commandHelp"),
        });
        Svc.Commands.AddHandler(PluginMeta.ShortCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = localizer.Get("plugin.commandHelp"),
            ShowInHelp = false,
        });

        Svc.Framework.Update += OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.Draw += routeOverlay.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Svc.Log.Information($"{PluginMeta.Name} loaded.");
    }

    public Configuration Configuration { get; }

    public ITravelService Travel { get; }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= routeOverlay.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        alertEngine.Appeared -= alertPlayer.Handle;
        confirmation.Dispose();

        treasureHunt.Stop();
        potBar.Dispose();
        treasureToolbar.Dispose();
        mapMarkers.Dispose();
        KamiToolKitLibrary.Dispose();

        windowSystem.RemoveAllWindows();
        Svc.Commands.RemoveHandler(PluginMeta.Command);
        Svc.Commands.RemoveHandler(PluginMeta.ShortCommand);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        localizer.Reload();

        Travel.Tick();

        var territory = Svc.ClientState.TerritoryType;
        if (territory != lastTerritory)
        {
            lastTerritory = territory;
            modules.SetTerritory(territory);

            alertEngine.Reset();
            treasureWatcher.Reset();
        }

        treasureToolbar.Sync();
        potBar.Refresh();

        var active = modules.Active;
        if (active == null)
        {
            return;
        }

        active.Update();

        treasureHunt.Tick();
        treasureWatcher.Tick();
        mapMarkers.Tick();

        var encounters = active.GetCapability<IEncounterSource>();
        if (encounters != null)
        {
            alertEngine.Observe(encounters.Active);
        }
    }

    internal void SetTreasureMarkerShown(uint territoryId, TreasureKind kind, bool shown)
    {
        var zone = Configuration.TreasureFor(territoryId).ShownMarkers;

        if (shown)
        {
            zone.Add(kind);
        }
        else
        {
            zone.Remove(kind);
        }

        Configuration.Save();
        treasureToolbar.Sync();
    }

    internal void SetTreasureOverlayShown(uint territoryId, bool shown)
    {
        Configuration.TreasureFor(territoryId).ShowToolbar = shown;
        Configuration.Save();
        treasureToolbar.Sync();
    }

    private bool IsEncounterStillActive(string id)
    {
        var encounters = modules.Active?.GetCapability<IEncounterSource>();
        if (encounters == null)
        {
            return true;
        }

        foreach (var encounter in encounters.Active)
        {
            if (encounter.Id == id)
            {
                return encounter.IsJoinable;
            }
        }

        return false;
    }

    private void OnCommand(string command, string args) => mainWindow.Toggle();

    internal void ToggleConfigWindow() => configWindow.Toggle();

    private void ToggleConfigUi() => ToggleConfigWindow();

    private void ToggleMainUi() => mainWindow.Toggle();
}
