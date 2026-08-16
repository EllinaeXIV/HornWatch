using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;
using Hornwatch.Core.Navigation;
using Hornwatch.Core.Treasure;
using Hornwatch.Theming;

namespace Hornwatch.Windows.Tabs;

public sealed class TreasureTab(FieldModuleRegistry modules, ILocalizer localizer, Configuration configuration, TreasureHunt hunt, ITravelService travel, MapFlagger flagger, ThemeManager theme) : ITab
{

    public string TitleKey => "tab.treasure";

    public void Draw()
    {
        var source = modules.Capability<ITreasureSource>();
        if (source == null)
        {
            ImGui.TextDisabled(localizer.Get("treasure.noData"));
            return;
        }

        ImGui.Spacing();
        DrawHunt(source);
    }

    private void DrawHunt(ITreasureSource source)
    {
        ImGui.TextColored(theme.Current.Accent, localizer.Get("treasure.route"));
        ImGui.Separator();

        var territory = Svc.ClientState.TerritoryType;
        var zone = configuration.TreasureFor(territory);
        var options = zone.Route;

        var bronze = options.IncludeBronze;
        if (ImGui.Checkbox(localizer.Get("treasure.routeBronze"), ref bronze))
        {
            zone.Route = options with { IncludeBronze = bronze };
            configuration.Save();
        }

        ImGui.SameLine(0f, ImGui.GetStyle().ItemSpacing.X * 3f);

        var silver = options.IncludeSilver;
        if (ImGui.Checkbox(localizer.Get("treasure.routeSilver"), ref silver))
        {
            zone.Route = options with { IncludeSilver = silver };
            configuration.Save();
        }

        var underground = options.IncludeUnderground;
        if (ImGui.Checkbox(localizer.Get("treasure.routeUnderground"), ref underground))
        {
            zone.Route = options with { IncludeUnderground = underground };
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("treasure.routeUndergroundHint"));

        var hostile = options.IncludeHostileAreas;
        if (ImGui.Checkbox(localizer.Get("treasure.routeHostile"), ref hostile))
        {
            zone.Route = options with { IncludeHostileAreas = hostile };
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("treasure.routeHostileHint"));

        var returnWhenDone = options.ReturnToCampWhenDone;
        if (ImGui.Checkbox(localizer.Get("treasure.routeReturn"), ref returnWhenDone))
        {
            zone.Route = options with { ReturnToCampWhenDone = returnWhenDone };
            configuration.Save();
        }

        var sightedOnly = options.SightedOnly;
        if (ImGui.Checkbox(localizer.Get("treasure.routeSighted"), ref sightedOnly))
        {
            zone.Route = options with { SightedOnly = sightedOnly };
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("treasure.routeSightedHint"));

        var sweep = options.SweepWhileWalking;
        if (ImGui.Checkbox(localizer.Get("treasure.routeSweep"), ref sweep))
        {
            zone.Route = options with { SweepWhileWalking = sweep };
            configuration.Save();
        }

        ImGui.TextWrapped(localizer.Get("treasure.routeSweepHint"));

        ImGui.Spacing();

        if (ImGui.Button(localizer.Get("treasure.plan")))
        {
            hunt.Plan(source.PointsIn(territory), zone.Route);
        }

        if (hunt.Route.Count == 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(localizer.Get("treasure.noRoute"));
            return;
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(localizer.Format("treasure.progress", hunt.Index, hunt.Route.Count));

        DrawRunControls();
        DrawNextStops();
    }

    private void DrawRunControls()
    {
        var travelReady = travel.IsEnabled && travel.IsAvailable;

        using (ImRaii.Disabled(!travelReady || (hunt.State == HuntState.Idle && !hunt.CanStart)))
        {
            if (hunt.State == HuntState.Idle)
            {
                if (ImGui.Button(localizer.Get("treasure.start")))
                {
                    hunt.Start();
                }
            }
            else if (ImGui.Button(localizer.Get("treasure.stop")))
            {
                hunt.Stop();
            }
        }

        if (!travelReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(localizer.Get(travel.UnavailableReasonKey ?? "nav.reason.disabledInSettings"));
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(hunt.State == HuntState.Idle))
        {
            if (hunt.State == HuntState.Paused)
            {
                if (ImGui.Button(localizer.Get("treasure.resume")))
                {
                    hunt.Resume();
                }
            }
            else if (ImGui.Button(localizer.Get("treasure.pause")))
            {
                hunt.Pause();
            }
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(hunt.Current == null))
        {
            if (ImGui.Button(localizer.Get("treasure.skip")))
            {
                hunt.Skip();
            }
        }

        if (hunt.Current is { } current)
        {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
            {
                flagger.Place(current.Position);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(localizer.Get("watch.placeFlag"));
            }
        }
    }

    private void DrawNextStops()
    {
        ImGui.Spacing();

        using var table = ImRaii.Table("hornwatchTreasureRoute", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp);

        if (!table.Success)
        {
            return;
        }

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 34);
        ImGui.TableSetupColumn(localizer.Get("treasure.column.kind"));
        ImGui.TableSetupColumn(localizer.Get("treasure.column.position"));
        ImGui.TableHeadersRow();

        for (var i = hunt.Index; i < hunt.Route.Count; i++)
        {
            var point = hunt.Route[i];

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted((i + 1).ToString());

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text,
                       point.Kind == TreasureKind.SilverCoffer ? UiTheme.Warning : theme.Current.Text))
            {
                ImGui.TextUnformatted(localizer.Get($"treasure.kind.{point.Kind}"));
            }

            ImGui.TableNextColumn();
            ImGui.TextDisabled(MapText(point.Position));
        }
    }

    private static string MapText(Vector3 world) =>
        $"X {ToMap(world.X):F1}  Y {ToMap(world.Z):F1}";

    private static float ToMap(float world) => (41f * ((world + 1024f) / 2048f)) + 1f;
}
