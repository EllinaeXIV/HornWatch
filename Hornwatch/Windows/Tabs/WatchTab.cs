using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Hornwatch.Core.Encounters;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;
using Hornwatch.Theming;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Windows.Tabs;

public sealed class WatchTab(FieldModuleRegistry modules, ILocalizer localizer, ITravelService travel, MapFlagger flagger, ThemeManager theme) : ITab
{

    public string TitleKey => "tab.watch";

    public void Draw()
    {
        var source = modules.Capability<IEncounterSource>();
        var encounters = source?.Active;

        ImGui.Spacing();

        if (encounters == null || encounters.Count == 0)
        {
            ImGui.TextDisabled(localizer.Get("watch.empty"));
            ImGui.Spacing();
            ImGui.TextWrapped(localizer.Get("watch.emptyHint"));
            ImGui.Spacing();

            DrawRespawns();
            DrawTravelFooter();
            return;
        }

        for (var i = 0; i < encounters.Count; i++)
        {
            DrawEncounter(encounters[i], i);
        }

        DrawRespawns();
        DrawTravelFooter();
    }

    private void DrawRespawns()
    {
        var tracker = modules.Capability<RespawnTracker>();
        var predictions = tracker?.Predictions;
        if (predictions == null || predictions.Count == 0)
        {
            return;
        }

        for (var i = 0; i < predictions.Count; i++)
        {
            using var id = ImRaii.PushId($"respawn{i}");
            var prediction = predictions[i];

            DrawKind(EncounterKind.NotableFate);
            DrawBadge(prediction.LabelKey);

            ImGui.SameLine();
            ImGui.TextUnformatted(string.IsNullOrEmpty(prediction.Name)
                ? localizer.Get("watch.unknownValue")
                : prediction.Name);

            if (!prediction.IsKnown)
            {
                ImGui.TextDisabled(localizer.Get("watch.respawnUnknown"));
                ImGui.Separator();
                continue;
            }

            ImGui.SameLine();
            if (prediction.IsDue)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, UiTheme.Warning))
                {
                    ImGui.TextUnformatted(localizer.Get("watch.respawnDue"));
                }
            }
            else
            {
                DrawTimer(prediction.Remaining);
            }

            ImGui.TextDisabled(localizer.Get("watch.awaiting"));

            if (prediction.Position is { } destination)
            {
                DrawFlagButton(destination);

                if (travel.IsEnabled)
                {
                    ImGui.SameLine();
                    DrawTravelButton(destination);
                }
            }

            ImGui.Separator();
        }
    }

    private void DrawEncounter(TrackedEncounter encounter, int index)
    {
        using var id = ImRaii.PushId(index);

        DrawKind(encounter.Kind);
        DrawBadge(encounter.LabelKey);

        ImGui.SameLine();
        ImGui.TextUnformatted(encounter.Name);

        if (encounter.TimeRemaining is { } remaining && remaining > TimeSpan.Zero)
        {
            ImGui.SameLine();
            DrawTimer(remaining);
        }

        ImGui.TextDisabled(BuildMeta(encounter));

        if (encounter.Position is { } destination)
        {
            DrawFlagButton(destination);

            if (travel.IsEnabled && encounter.IsJoinable)
            {
                ImGui.SameLine();
                DrawTravelButton(destination, encounter.Id, encounter.Radius);
            }
        }

        ImGui.Separator();
    }

    private void DrawKind(EncounterKind kind)
    {
        using var colour = ImRaii.PushColor(ImGuiCol.Text, UiTheme.ForKind(kind));
        ImGui.TextUnformatted(localizer.Get($"kind.{kind}"));
    }

    private void DrawBadge(string? labelKey)
    {
        if (string.IsNullOrEmpty(labelKey))
        {
            return;
        }

        ImGui.SameLine();
        using var colour = ImRaii.PushColor(ImGuiCol.Text, theme.Current.Accent);
        ImGui.TextUnformatted($"[{localizer.Get(labelKey)}]");
    }

    private void DrawTimer(TimeSpan remaining)
    {
        using var colour = ImRaii.PushColor(ImGuiCol.Text, theme.Current.Accent);
        ImGui.TextUnformatted($"{(int)remaining.TotalMinutes}:{remaining.Seconds:00}");
    }

    private string BuildMeta(TrackedEncounter encounter)
    {
        var phase = localizer.Get($"phase.{encounter.Phase}");

        if (encounter.MaxParticipants > 0)
        {
            var players = localizer.Format("watch.participants", encounter.Participants, encounter.MaxParticipants);
            return $"{phase} - {players}";
        }

        if (encounter.Progress is { } progress && progress > 0f)
        {
            return $"{phase} - {localizer.Format("watch.progress", (int)(progress * 100))}";
        }

        return phase;
    }

    private void DrawFlagButton(Vector3 destination)
    {
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
        {
            flagger.Place(destination);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get("watch.placeFlag"));
        }
    }

    private void DrawTravelButton(Vector3 destination, string? targetId = null, float? radius = null)
    {
        var available = travel.IsAvailable;

        using (ImRaii.Disabled(!available))
        {
            if (ImGui.Button(localizer.Get("watch.travelTo")))
            {
                travel.TravelTo(destination, targetId, radius);
            }
        }

        if (!available && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var reason = travel.UnavailableReasonKey;
            if (reason != null)
            {
                ImGui.SetTooltip(localizer.Get(reason));
            }
        }
        else if (available && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(localizer.Get("nav.warningShort"));
        }
    }

    private void DrawTravelFooter()
    {
        if (travel.Phase == TravelPhase.Idle)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.Separator();

        using (ImRaii.PushColor(ImGuiCol.Button, UiTheme.Danger))
        {
            if (ImGui.Button(localizer.Get("watch.stopTravel")))
            {
                travel.Stop();
            }
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, theme.Current.Accent))
        {
            ImGui.TextUnformatted(localizer.Get($"travel.phase.{travel.Phase}"));
        }

        ImGui.SameLine();
        ImGui.TextDisabled(localizer.Get("nav.warningShort"));
    }
}
