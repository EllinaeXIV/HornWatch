using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using Hornwatch.Core;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;
using Hornwatch.Core.Navigation;
using Hornwatch.Core.Treasure;
using Hornwatch.Theming;
using Hornwatch.Windows.Tabs;

namespace Hornwatch.Windows;

public sealed class MainWindow : ThemedWindow, IDisposable
{
    private readonly FieldModuleRegistry modules;
    private readonly ILocalizer localizer;
    private readonly List<ITab> tabs;

    private string? pendingTab;

    public const string WatchTabKey = "tab.watch";

    public MainWindow(
        Plugin plugin,
        FieldModuleRegistry modules,
        ILocalizer localizer,
        ITravelService travel,
        MapFlagger flagger,
        TreasureHunt hunt,
        ThemeManager theme)
        : base($"{PluginMeta.Name}{PluginMeta.WindowId("main")}", theme)
    {
        this.modules = modules;
        this.localizer = localizer;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => plugin.ToggleConfigWindow(),
            ShowTooltip = () => ImGui.SetTooltip(localizer.Get("plugin.openSettings")),
        });

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 100),
            MaximumSize = new Vector2(1400, 1000),
        };
        Size = new Vector2(620, 460);
        SizeCondition = ImGuiCond.FirstUseEver;

        tabs =
        [
            new WatchTab(modules, localizer, travel, flagger, theme),
            new TreasureTab(modules, localizer, plugin.Configuration, hunt, travel, flagger, theme),
            new PartyTab(modules, localizer, theme),
            new MyJobsTab(modules, localizer, theme),
            new GuideTab(modules, localizer, theme),
        ];
    }

    public void Dispose() { }

    public void Reveal(string titleKey)
    {
        pendingTab = titleKey;
        IsOpen = true;
        BringToFront();
    }

    public override void Draw()
    {
        if (!modules.InSupportedZone)
        {
            DrawIdleState();
            return;
        }

        using var bar = ImRaii.TabBar($"{PluginMeta.InternalName}_tabs");
        if (!bar.Success)
        {
            return;
        }

        var wanted = pendingTab;
        pendingTab = null;

        foreach (var tab in tabs)
        {
            var flags = tab.TitleKey == wanted ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;

            using var item = ImRaii.TabItem(localizer.Get(tab.TitleKey), flags);
            if (!item.Success)
            {
                continue;
            }

            tab.Draw();
        }
    }

    private void DrawIdleState()
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.Current.Accent, localizer.Get("zone.unsupported"));
        ImGui.Spacing();
        ImGui.TextWrapped(localizer.Get("zone.unsupportedHint"));
    }
}
