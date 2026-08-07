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
using Hornwatch.Navigation;
using Hornwatch.Theming;

namespace Hornwatch.Windows;

public sealed class ConfigWindow : ThemedWindow, IDisposable
{
    private static readonly EncounterKind[] AlertKinds =
    {
        EncounterKind.CriticalEncounter,
        EncounterKind.NotableFate,
        EncounterKind.Fate,
        EncounterKind.Raid,
    };

    private readonly Configuration configuration;
    private readonly ILocalizer localizer;
    private readonly FieldModuleRegistry modules;
    private readonly MountCatalog mounts;

    private readonly Dictionary<string, uint> editedTerritory = new();

    public ConfigWindow(
        Plugin plugin,
        ILocalizer localizer,
        FieldModuleRegistry modules,
        MountCatalog mounts,
        ThemeManager theme)
        : base($"{PluginMeta.Name}{PluginMeta.WindowId("config")}", theme)
    {
        configuration = plugin.Configuration;
        this.localizer = localizer;
        this.modules = modules;
        this.mounts = mounts;

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

        var currentLabel = localizer.Get("config.themeFollowGame");
        foreach (var option in options)
        {
            if (option.Key == currentKey && option.Key != ThemeManager.FollowGameKey)
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
                    var label = option.Key == ThemeManager.FollowGameKey
                        ? localizer.Get("config.themeFollowGame")
                        : option.DisplayName;

                    if (ImGui.Selectable(label, option.Key == currentKey))
                    {
                        configuration.ThemeKey = option.Key;
                        configuration.Save();
                    }
                }
            }
        }

        DrawSwatches();

        if (currentKey == ThemeManager.FollowGameKey)
        {
            ImGui.TextWrapped(localizer.Get("config.themeFollowGameHint"));
        }
    }

    private void DrawSwatches()
    {
        var palette = Theme.Current;
        var swatches = new[] { palette.WindowBg, palette.FrameBg, palette.Accent, palette.Text };

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
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("config.alerts"));
        ImGui.Separator();

        foreach (var kind in AlertKinds)
        {
            DrawAlertSetting(module.Key, territory, kind);
        }
    }

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

        if (mount.IconId != 0)
        {
            var texture = Svc.Textures.GetFromGameIcon(new GameIconLookup(mount.IconId)).GetWrapOrDefault();
            if (texture != null)
            {
                ImGui.Image(texture.Handle, new Vector2(20, 20));
                ImGui.SameLine();
            }
        }

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

        using (ImRaii.PushColor(ImGuiCol.Text, UiTheme.Danger))
        {
            ImGui.TextUnformatted(localizer.Get("nav.warningTitle"));
        }

        ImGui.TextWrapped(localizer.Get("nav.warningBody"));
        ImGui.Spacing();

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

        using (ImRaii.Disabled(!configuration.AutoTravelRiskAcknowledged))
        {
            var enabled = configuration.AutoTravelEnabled;
            if (ImGui.Checkbox(localizer.Get("config.autoTravelEnable"), ref enabled))
            {
                configuration.AutoTravelEnabled = enabled;
                configuration.Save();
            }

            ImGui.Spacing();
            var teleport = configuration.UseTeleport;
            if (ImGui.Checkbox(localizer.Get("config.useTeleport"), ref teleport))
            {
                configuration.UseTeleport = teleport;
                configuration.Save();
            }

            ImGui.TextWrapped(localizer.Get("config.useTeleportHint"));

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

        ImGui.Spacing();
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("config.dependencies"));
        ImGui.Separator();
        ImGui.TextWrapped(localizer.Get("config.dependencyVnavmesh"));
        ImGui.Spacing();
        ImGui.TextWrapped(localizer.Get("config.dependencyLifestream"));
    }
}
