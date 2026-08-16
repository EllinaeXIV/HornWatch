using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Hornwatch.Core;
using Hornwatch.Core.Guides;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;
using Hornwatch.Theming;

namespace Hornwatch.Windows.Tabs;

public sealed class GuideTab(FieldModuleRegistry modules, ILocalizer localizer, ThemeManager theme) : ITab
{
    private const float IconSize = 30f;

    private int selected;

    public string TitleKey => "tab.guides";

    public void Draw()
    {
        var catalog = modules.Capability<IGuideCatalog>();
        ImGui.Spacing();

        if (catalog == null || catalog.Guides.Count == 0)
        {
            ImGui.TextDisabled(localizer.Get("guide.none"));
            return;
        }

        var guides = catalog.Guides;

        if (selected >= guides.Count)
        {
            selected = 0;
        }

        DrawPicker(guides);

        ImGui.Spacing();
        ImGui.Separator();

        var guide = guides[selected];
        ImGui.Spacing();
        ImGui.TextWrapped(localizer.Get(guide.IntroKey));
        ImGui.Spacing();
        ImGui.Separator();

        using var scroll = ImRaii.Child("hornwatch-guide-body", Vector2.Zero, false);
        if (!scroll.Success)
        {
            return;
        }

        foreach (var section in guide.Sections)
        {
            DrawSection(section);
        }
    }

    private void DrawPicker(IReadOnlyList<IZoneGuide> guides)
    {
        ImGui.TextUnformatted(localizer.Get("guide.picker"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(260);

        using var combo = ImRaii.Combo("##hornwatch-guide-picker", localizer.Get(guides[selected].TitleKey));
        if (!combo.Success)
        {
            return;
        }

        for (var i = 0; i < guides.Count; i++)
        {
            if (ImGui.Selectable(localizer.Get(guides[i].TitleKey), i == selected))
            {
                selected = i;
            }
        }
    }

    private void DrawSection(GuideSection section)
    {
        ImGui.Spacing();
        ImGui.TextColored(theme.Current.TextMuted, localizer.Get(section.TitleKey));
        ImGui.Spacing();

        foreach (var entry in section.Entries)
        {
            DrawEntry(entry);
        }
    }

    private void DrawEntry(GuideEntry entry)
    {
        using var id = ImRaii.PushId(entry.Name);

        GameIcon.DrawOrSpace(entry.IconId, IconSize);
        ImGui.SameLine();

        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(entry.Name);
            ImGui.TextDisabled(BuildUnlockText(entry));

            if (entry.NoteKey != null)
            {
                using var note = ImRaii.PushColor(ImGuiCol.Text, UiTheme.Warning);
                ImGui.TextUnformatted(localizer.Get(entry.NoteKey));
            }
        }

        ImGui.Spacing();
    }

    private string BuildUnlockText(GuideEntry entry)
    {
        return entry.UnlockKind switch
        {
            GuideUnlockKind.Automatic =>
                localizer.Get("guide.unlock.automatic"),

            GuideUnlockKind.CriticalEncounter =>
                localizer.Format("guide.unlock.criticalEncounter", entry.SourceName ?? "?", entry.ZoneName ?? "?"),

            GuideUnlockKind.Monster when entry.SourceLevel is { } level =>
                localizer.Format("guide.unlock.monster",
                    entry.SourceName ?? "?", level, entry.ZoneName ?? "?", entry.X ?? 0, entry.Y ?? 0),

            _ => localizer.Format("guide.unlock.monsterNoLevel",
                    entry.SourceName ?? "?", entry.ZoneName ?? "?", entry.X ?? 0, entry.Y ?? 0),
        };
    }
}
