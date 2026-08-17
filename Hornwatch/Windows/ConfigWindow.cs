using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Hornwatch.Alerts;
using Hornwatch.Core;
using Hornwatch.Core.Encounters;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;
using Hornwatch.Core.Treasure;
using Hornwatch.Navigation;
using Hornwatch.Theming;

namespace Hornwatch.Windows;

public sealed class ConfigWindow : ThemedWindow, IDisposable
{
    private const float MountIconSize = 20f;

    private static readonly EncounterKind[] AlertKinds =
    [
        EncounterKind.CriticalEncounter,
        EncounterKind.NotableFate,
        EncounterKind.Fate,
        EncounterKind.Raid,
    ];

    private readonly Configuration configuration;
    private readonly ILocalizer localizer;
    private readonly FieldModuleRegistry modules;
    private readonly MountCatalog mounts;
    private readonly PluginPresence installed;
    private readonly Action<uint, TreasureKind, bool> setMarkerShown;
    private readonly Action<uint, bool> setOverlayShown;

    private readonly Dictionary<string, uint> editedTerritory = new();

    public ConfigWindow(
        Plugin plugin,
        ILocalizer localizer,
        FieldModuleRegistry modules,
        MountCatalog mounts,
        PluginPresence installed,
        Action<uint, TreasureKind, bool> setMarkerShown,
        Action<uint, bool> setOverlayShown,
        ThemeManager theme)
        : base($"{PluginMeta.Name}{PluginMeta.WindowId("config")}", theme)
    {
        configuration = plugin.Configuration;
        this.localizer = localizer;
        this.modules = modules;
        this.mounts = mounts;
        this.installed = installed;
        this.setMarkerShown = setMarkerShown;
        this.setOverlayShown = setOverlayShown;

        Size = new Vector2(560, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        using var bar = ImRaii.TabBar($"{PluginMeta.InternalName}_config");
        if (!bar.Success)
        {
            return;
        }

        DrawGeneralTab();

        foreach (var module in modules.All)
        {
            DrawZoneTab(module);
        }

        DrawAutoPilotTab();
    }

    private void DrawGeneralTab()
    {
        using var tab = ImRaii.TabItem(localizer.Get("config.tabGeneral"));
        if (!tab.Success)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("config.language"));
        ImGui.Separator();

        DrawLanguageOption("config.languageAuto", string.Empty);
        ImGui.SameLine();
        DrawLanguageOption("config.languageFrench", "fr");
        ImGui.SameLine();
        DrawLanguageOption("config.languageEnglish", "en");

        ImGui.Spacing();
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("config.appearance"));
        ImGui.Separator();
        DrawThemePicker();

        ImGui.Spacing();

        var openOnEntry = configuration.OpenOnZoneEntry;
        if (ImGui.Checkbox(localizer.Get("config.openOnEntry"), ref openOnEntry))
        {
            configuration.OpenOnZoneEntry = openOnEntry;
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("config.openOnEntryHint"));

        var keepOpen = configuration.KeepOpenOnEscape;
        if (ImGui.Checkbox(localizer.Get("config.keepOpenOnEscape"), ref keepOpen))
        {
            configuration.KeepOpenOnEscape = keepOpen;
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("config.keepOpenOnEscapeHint"));

        ImGui.Spacing();

        var potBar = configuration.ShowPotBarEntry;
        if (ImGui.Checkbox(localizer.Get("config.showPotBar"), ref potBar))
        {
            configuration.ShowPotBarEntry = potBar;
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("config.showPotBarHint"));

        if (!BuildFlavour.DeveloperToolsAvailable)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("config.developer"));
        ImGui.Separator();

        var developer = configuration.DeveloperMode;
        if (ImGui.Checkbox(localizer.Get("config.developerMode"), ref developer))
        {
            configuration.DeveloperMode = developer;
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("config.developerModeHint"));
    }

    private void DrawThemePicker()
    {
        var options = Theme.Options;
        var currentKey = configuration.ThemeKey;

        var currentLabel = string.Empty;
        foreach (var option in options)
        {
            if (option.Key == currentKey)
            {
                currentLabel = option.DisplayName;
            }
        }

        ImGui.SetNextItemWidth(260);
        using (var combo = ImRaii.Combo(localizer.Get("config.theme"), currentLabel))
        {
            if (combo.Success)
            {
                foreach (var option in options)
                {
                    if (ImGui.Selectable(option.DisplayName, option.Key == currentKey))
                    {
                        configuration.ThemeKey = option.Key;
                        configuration.Save();
                    }
                }
            }
        }

        DrawSwatches();
    }

    private void DrawSwatches()
    {
        var palette = Theme.Current;
        Vector4[] swatches = [palette.WindowBg, palette.FrameBg, palette.Accent, palette.Text];

        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        const float size = 16f;
        const float gap = 4f;

        for (var i = 0; i < swatches.Length; i++)
        {
            var min = origin with { X = origin.X + (i * (size + gap)) };
            var max = new Vector2(min.X + size, min.Y + size);

            draw.AddRectFilled(min, max, ImGui.GetColorU32(swatches[i] with { W = 1f }));
            draw.AddRect(min, max, ImGui.GetColorU32(palette.Border));
        }

        ImGui.Dummy(new Vector2((swatches.Length * (size + gap)) - gap, size));
        ImGui.SameLine();
        ImGui.TextDisabled(palette.DisplayName);
    }

    private void DrawLanguageOption(string labelKey, string value)
    {
        if (ImGui.RadioButton(localizer.Get(labelKey), configuration.LanguageOverride == value))
        {
            configuration.LanguageOverride = value;
            configuration.Save();
        }
    }

    private void DrawZoneTab(IFieldModule module)
    {
        using var tab = ImRaii.TabItem(localizer.Get(module.DisplayNameKey));
        if (!tab.Success)
        {
            return;
        }

        using var moduleId = ImRaii.PushId(module.Key);

        ImGui.Spacing();

        var territory = SelectedTerritory(module);

        if (module.TerritoryIds.Count > 1)
        {
            DrawTerritoryPicker(module, ref territory);
            ImGui.Spacing();
        }

        if (modules.Active is { } active && ReferenceEquals(module, active) && Svc.ClientState.TerritoryType == territory)
        {
            ImGui.TextColored(UiTheme.Good, localizer.Get("config.zoneActive"));
        }
        else
        {
            ImGui.TextDisabled(localizer.Get("config.zoneInactive"));
        }

        ImGui.Spacing();

        using var zoneTabs = ImRaii.TabBar($"{module.Key}_sections");
        if (!zoneTabs.Success)
        {
            return;
        }

        DrawEncounterAlertSection(module, territory);
        DrawTreasureSection(module, territory);
    }

    private void DrawEncounterAlertSection(IFieldModule module, uint territory)
    {
        using var tab = ImRaii.TabItem(localizer.Get("config.sectionEncounters"));
        if (!tab.Success)
        {
            return;
        }

        ImGui.Spacing();

        foreach (var kind in AlertKinds)
        {
            DrawAlertSetting(module.Key, territory, kind);
        }
    }

    private void DrawTreasureSection(IFieldModule module, uint territory)
    {
        using var tab = ImRaii.TabItem(localizer.Get("config.sectionTreasure"));
        if (!tab.Success)
        {
            return;
        }

        if (module.GetCapability<ITreasureSource>() == null)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(localizer.Get("treasure.noData"));
            return;
        }

        ImGui.Spacing();
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("treasure.markers"));
        ImGui.Separator();

        foreach (var kind in TreasureVisuals.Order)
        {
            DrawTreasureMarkerToggle(territory, kind);
        }

        ImGui.Spacing();

        var overlay = configuration.TreasureFor(territory).ShowToolbar;
        if (ImGui.Checkbox(localizer.Get("treasure.showOverlay"), ref overlay))
        {
            setOverlayShown(territory, overlay);
        }

        ImGui.TextWrapped(localizer.Get("treasure.showOverlayHint"));

        var minimap = configuration.TreasureFor(territory).ShowOnMinimap;
        if (ImGui.Checkbox(localizer.Get("treasure.showMinimap"), ref minimap))
        {
            configuration.TreasureFor(territory).ShowOnMinimap = minimap;
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("treasure.showMinimapHint"));

        ImGui.Spacing();
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("treasure.alerts"));
        ImGui.Separator();

        var alerts = configuration.TreasureFor(territory).Alerts;

        ImGui.TextWrapped(localizer.Get("treasure.alertsHint"));
        ImGui.Spacing();

        var toast = alerts.Toast;
        if (ImGui.Checkbox(localizer.Get("treasure.toast"), ref toast))
        {
            alerts.Toast = toast;
            configuration.Save();
        }

        ImGui.SameLine(0f, ImGui.GetStyle().ItemSpacing.X * 3f);

        var chat = alerts.ChatMessage;
        if (ImGui.Checkbox(localizer.Get("treasure.chat"), ref chat))
        {
            alerts.ChatMessage = chat;
            configuration.Save();
        }

        var flag = alerts.MapFlag;
        if (ImGui.Checkbox(localizer.Get("treasure.flag"), ref flag))
        {
            alerts.MapFlag = flag;
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("treasure.flagHint"));

        ImGui.Spacing();

        foreach (var rarity in TreasureVisuals.Rarities)
        {
            using var id = ImRaii.PushId($"alert{rarity}");
            var wanted = alerts.Wants(rarity);

            DrawRarityIcon(rarity);
            if (ImGui.Checkbox(localizer.Get($"treasure.rarity.{rarity}"), ref wanted))
            {
                alerts.Set(rarity, wanted);
                configuration.Save();
            }
        }

        ImGui.Spacing();

        var forget = alerts.ForgetAfterSeconds;
        ImGui.SetNextItemWidth(140f);
        if (ImGui.SliderInt(localizer.Get("treasure.forget"), ref forget, 10, 900, "%d s"))
        {
            alerts.ForgetAfterSeconds = forget;
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("treasure.forgetHint"));
    }

    private static void DrawRarityIcon(TreasureRarity rarity) =>
        GameIcon.DrawBefore(TreasureVisuals.IconOf(rarity), ImGui.GetTextLineHeight() + 4f);

    private void DrawTreasureMarkerToggle(uint territory, TreasureKind kind)
    {
        using var id = ImRaii.PushId($"marker{kind}");

        var shown = configuration.TreasureFor(territory).ShownMarkers.Contains(kind);

        DrawTreasureIcon(kind);

        if (ImGui.Checkbox(localizer.Get($"treasure.kind.{kind}"), ref shown))
        {
            setMarkerShown(territory, kind, shown);
        }
    }

    private static void DrawTreasureIcon(TreasureKind kind) =>
        GameIcon.DrawBefore(TreasureVisuals.IconOf(kind), ImGui.GetTextLineHeight() + 4f);

    private uint SelectedTerritory(IFieldModule module)
    {
        if (editedTerritory.TryGetValue(module.Key, out var stored) && module.Handles(stored))
        {
            return stored;
        }

        var current = Svc.ClientState.TerritoryType;
        return module.Handles(current) ? current : module.TerritoryIds[0];
    }

    private void DrawTerritoryPicker(IFieldModule module, ref uint territory)
    {
        ImGui.SetNextItemWidth(260);
        using var combo = ImRaii.Combo(localizer.Get("config.territory"), TerritoryName(territory));
        if (!combo.Success)
        {
            return;
        }

        foreach (var id in module.TerritoryIds)
        {
            if (ImGui.Selectable(TerritoryName(id), id == territory))
            {
                editedTerritory[module.Key] = id;
                territory = id;
            }
        }
    }

    private string TerritoryName(uint territoryId)
    {
        var sheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
        if (sheet != null && sheet.TryGetRow(territoryId, out var row))
        {
            var name = row.PlaceName.ValueNullable?.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return territoryId.ToString();
    }

    private void DrawAlertSetting(string moduleKey, uint territoryId, EncounterKind kind)
    {
        using var id = ImRaii.PushId(kind.ToString());
        var setting = configuration.For(moduleKey, territoryId, kind);

        using (ImRaii.PushColor(ImGuiCol.Text, UiTheme.ForKind(kind)))
        {
            ImGui.TextUnformatted(localizer.Get($"kind.{kind}"));
        }

        var enabled = setting.Enabled;
        if (ImGui.Checkbox(localizer.Get("config.alertEnabled"), ref enabled))
        {
            setting.Enabled = enabled;
            configuration.Save();
        }

        ImGui.SameLine();
        var chat = setting.ChatMessage;
        if (ImGui.Checkbox(localizer.Get("config.alertChat"), ref chat))
        {
            setting.ChatMessage = chat;
            configuration.Save();
        }

        var sound = setting.SoundId;
        ImGui.SetNextItemWidth(120);
        if (ImGui.SliderInt(localizer.Get("config.alertSound"), ref sound, 1, 16))
        {
            setting.SoundId = sound;
            configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button(localizer.Get("config.testSound")))
        {
            AlertPlayer.Play(setting.SoundId);
        }

        ImGui.Spacing();
    }

    private void DrawMountPicker()
    {
        var current = configuration.MountId == 0
            ? localizer.Get("config.mountRoulette")
            : mounts.Find(configuration.MountId)?.Name ?? localizer.Get("config.mountUnknown");

        ImGui.SetNextItemWidth(260);
        using (var combo = ImRaii.Combo(localizer.Get("config.mount"), current))
        {
            if (combo.Success)
            {
                if (ImGui.Selectable(localizer.Get("config.mountRoulette"), configuration.MountId == 0))
                {
                    configuration.MountId = 0;
                    configuration.Save();
                }

                ImGui.Separator();

                foreach (var mount in mounts.Unlocked())
                {
                    DrawMountRow(mount);
                }
            }
        }

        ImGui.TextWrapped(localizer.Get(configuration.MountId == 0
            ? "config.mountRouletteHint"
            : "config.mountSpecificHint"));
    }

    private void DrawMountRow(MountOption mount)
    {
        using var id = ImRaii.PushId((int)mount.Id);

        GameIcon.DrawBefore(mount.IconId, MountIconSize);

        if (ImGui.Selectable(mount.Name, mount.Id == configuration.MountId))
        {
            configuration.MountId = mount.Id;
            configuration.Save();
        }
    }

    private void DrawAutoPilotTab()
    {
        using var tab = ImRaii.TabItem(localizer.Get("config.autoTravel"));
        if (!tab.Success)
        {
            return;
        }

        ImGui.Spacing();

        var pathfinderPresent = installed.IsLoaded(PluginPresence.Vnavmesh);
        var teleporterPresent = installed.IsLoaded(PluginPresence.Lifestream);

        DrawDependencies(pathfinderPresent, teleporterPresent);

        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, UiTheme.Danger))
        {
            ImGui.TextUnformatted(localizer.Get("nav.warningTitle"));
        }

        ImGui.TextWrapped(localizer.Get("nav.warningBody"));
        ImGui.Spacing();

        if (!pathfinderPresent && configuration.AutoTravelEnabled)
        {
            configuration.AutoTravelEnabled = false;
            configuration.Save();
        }

        if (!teleporterPresent && configuration.UseTeleport)
        {
            configuration.UseTeleport = false;
            configuration.Save();
        }

        var acknowledged = configuration.AutoTravelRiskAcknowledged;
        if (ImGui.Checkbox(localizer.Get("nav.warningAccept"), ref acknowledged))
        {
            configuration.AutoTravelRiskAcknowledged = acknowledged;

            if (!acknowledged)
            {
                configuration.AutoTravelEnabled = false;
            }

            configuration.Save();
        }

        using (ImRaii.Disabled(!configuration.AutoTravelRiskAcknowledged || !pathfinderPresent))
        {
            var enabled = configuration.AutoTravelEnabled;
            if (ImGui.Checkbox(localizer.Get("config.autoTravelEnable"), ref enabled))
            {
                configuration.AutoTravelEnabled = enabled;
                configuration.Save();
            }

            ImGui.Spacing();

            using (ImRaii.Disabled(!teleporterPresent))
            {
                var teleport = configuration.UseTeleport;
                if (ImGui.Checkbox(localizer.Get("config.useTeleport"), ref teleport))
                {
                    configuration.UseTeleport = teleport;
                    configuration.Save();
                }

                ImGui.TextWrapped(localizer.Get("config.useTeleportHint"));
            }

            using (ImRaii.Disabled(!configuration.UseTeleport))
            {
                var useReturn = configuration.UseReturn;
                if (ImGui.Checkbox(localizer.Get("config.useReturn"), ref useReturn))
                {
                    configuration.UseReturn = useReturn;
                    configuration.Save();
                }

                ImGui.TextWrapped(localizer.Get("config.useReturnHint"));
            }

            ImGui.Spacing();

            var routeOverlay = configuration.ShowRouteOverlay;
            if (ImGui.Checkbox(localizer.Get("config.showRouteOverlay"), ref routeOverlay))
            {
                configuration.ShowRouteOverlay = routeOverlay;
                configuration.Save();
            }

            ImGui.TextWrapped(localizer.Get("config.showRouteOverlayHint"));

            ImGui.Spacing();
            var mount = configuration.UseMount;
            if (ImGui.Checkbox(localizer.Get("config.useMount"), ref mount))
            {
                configuration.UseMount = mount;
                configuration.Save();
            }

            using (ImRaii.Disabled(!configuration.UseMount))
            {
                DrawMountPicker();
            }
        }
    }

    private void DrawDependencies(bool pathfinderPresent, bool teleporterPresent)
    {
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("config.dependencies"));
        ImGui.Separator();

        DrawDependencyBadge(PluginPresence.Vnavmesh, pathfinderPresent, "config.dependencyVnavmesh");
        ImGui.SameLine();
        DrawDependencyBadge(PluginPresence.Lifestream, teleporterPresent, "config.dependencyLifestream");

        if (!pathfinderPresent)
        {
            ImGui.Spacing();
            ImGui.TextColored(UiTheme.Danger, localizer.Get("config.dependencyBlocked"));
        }
        else if (!teleporterPresent)
        {
            ImGui.Spacing();
            ImGui.TextColored(UiTheme.Warning, localizer.Get("config.dependencyWalkOnly"));
        }
    }

    private void DrawDependencyBadge(string internalName, bool present, string tooltipKey)
    {
        var colour = present ? UiTheme.Good : UiTheme.Danger;

        using (ImRaii.PushColor(ImGuiCol.Button, colour)
                     .Push(ImGuiCol.ButtonHovered, colour with { W = 0.8f })
                     .Push(ImGuiCol.ButtonActive, colour with { W = 0.6f }))
        {
            if (ImGui.Button(internalName) && !present)
            {
                installed.OpenInstallerFor(internalName);
            }
        }

        if (!ImGui.IsItemHovered())
        {
            return;
        }

        using var tooltip = ImRaii.Tooltip();
        ImGui.TextColored(colour, localizer.Get(present ? "config.dependencyFound" : "config.dependencyMissing"));
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24f);
        ImGui.TextWrapped(localizer.Get(tooltipKey));
        ImGui.PopTextWrapPos();
    }
}
