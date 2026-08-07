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
using Hornwatch.Core.Modules;
using Hornwatch.Core.Navigation;
using Hornwatch.Localization;
using Hornwatch.Modules.OccultCrescent;
using Hornwatch.Navigation;
using Hornwatch.Theming;
using Hornwatch.Windows;

namespace Hornwatch;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    private readonly FieldModuleRegistry modules;
    private readonly EncounterAlertEngine alertEngine = new();
    private readonly AlertPlayer alertPlayer;
    private readonly JsonLocalizer localizer;
    private readonly MemoryDataCache cache;

    private readonly WindowSystem windowSystem = new(PluginMeta.InternalName);
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    private uint lastTerritory = uint.MaxValue;

    public Plugin()
    {
        Svc.Init(PluginInterface);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        cache = new MemoryDataCache(() => Configuration.DeveloperMode);

        localizer = new JsonLocalizer(
            Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, "Resources"),
            Svc.ClientState,
            () => Configuration.LanguageOverride);

        var respawnStore = new ConfigurationRespawnStore(Configuration);

        modules = new FieldModuleRegistry(
        [
            new OccultCrescentModule(
                Svc.Data, Svc.Objects, Svc.Fates, cache, respawnStore,
                () => Svc.ClientState.TerritoryType),
        ]);

        var pathfinder = new PathfinderRouter(
            new VnavmeshPathfinder(PluginInterface, Svc.Log),
            () => Configuration.AutoTravelEnabled && Configuration.AutoTravelRiskAcknowledged);

        Travel = new TravelCoordinator(
            pathfinder,
            new FirstWorkingTeleporter(
                new LifestreamTeleporter(PluginInterface, Svc.Data, Svc.Log),
                new ShardTeleporter(
                    Svc.Objects, Svc.Targets, Svc.GameGui, Svc.Data, Svc.Log,
                    () => Svc.Objects.LocalPlayer?.Position)),
            new MountService(Svc.Condition, () => Configuration.UseMount ? Configuration.MountId : null),
            new ReturnService(Svc.Condition),
            new SelectYesnoConfirmer(Svc.GameGui),
            () => modules.Capability<ITeleportNetwork>(),
            () => Svc.Objects.LocalPlayer?.Position,
            () => Svc.ClientState.TerritoryType,
            IsEncounterStillActive,
            () => Configuration.UseTeleport,
            () => Configuration.UseReturn,
            () => Configuration.AutoTravelEnabled && Configuration.AutoTravelRiskAcknowledged,
            Svc.Log);

        alertPlayer = new AlertPlayer(
            Configuration, localizer, Svc.Chat,
            () => modules.Active?.Key,
            () => Svc.ClientState.TerritoryType);
        alertEngine.Appeared += alertPlayer.Handle;

        var theme = new ThemeManager(Svc.Data, Svc.GameConfig, cache, Configuration);

        mainWindow = new MainWindow(this, modules, localizer, Travel, theme);
        configWindow = new ConfigWindow(this, localizer, modules, new MountCatalog(Svc.Data, cache), theme);
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
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        alertEngine.Appeared -= alertPlayer.Handle;

        Travel.Stop();

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
        }

        var active = modules.Active;
        if (active == null)
        {
            return;
        }

        active.Update();

        var encounters = active.GetCapability<IEncounterSource>();
        if (encounters != null)
        {
            alertEngine.Observe(encounters.Active);
        }
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

    private void ToggleConfigUi() => configWindow.Toggle();

    private void ToggleMainUi() => mainWindow.Toggle();
}
