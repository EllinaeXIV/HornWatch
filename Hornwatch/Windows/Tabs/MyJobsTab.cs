using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Hornwatch.Core;
using Hornwatch.Core.Jobs;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;
using Hornwatch.Theming;
using Hornwatch.Modules.OccultCrescent;

namespace Hornwatch.Windows.Tabs;

public sealed class MyJobsTab(FieldModuleRegistry modules, ILocalizer localizer, ThemeManager theme) : ITab
{

    public string TitleKey => "tab.myJobs";

    public void Draw()
    {
        var jobs = modules.Capability<ISpecialJobSource>();
        ImGui.Spacing();

        if (jobs == null)
        {
            ImGui.TextDisabled(localizer.Get("jobs.noData"));
            return;
        }

        DrawResources(jobs);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawJobs(jobs);
    }

    private void DrawResources(ISpecialJobSource jobs)
    {
        var resources = jobs.LocalResources;
        if (resources.Count == 0)
        {
            ImGui.TextDisabled(localizer.Get("jobs.noData"));
            return;
        }

        ImGui.TextColored(theme.Current.Accent, localizer.Get("jobs.resources"));
        ImGui.Spacing();

        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            if (i > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("|");
                ImGui.SameLine();
            }

            var label = localizer.Get(resource.LabelKey);
            var text = resource.Maximum is { } max && max > 0
                ? $"{label} {resource.Value}/{max}"
                : $"{label} {resource.Value}";

            ImGui.TextUnformatted(text);
        }
    }

    private void DrawJobs(ISpecialJobSource jobs)
    {
        var progress = jobs.LocalProgress;
        if (progress.Count == 0)
        {
            ImGui.TextDisabled(localizer.Get("jobs.noData"));
            return;
        }

        var current = (jobs as OccultJobSource)?.CurrentJobId ?? -1;
        var all = jobs.AllJobs;

        using var table = ImRaii.Table("hornwatch-jobs", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table.Success)
        {
            return;
        }

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, 26f);
        ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("##level", ImGuiTableColumnFlags.WidthStretch, 1f);

        foreach (var entry in progress)
        {
            if (entry.JobId >= all.Count)
            {
                continue;
            }

            var job = all[entry.JobId];
            var isCurrent = entry.JobId == current;

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawIcon(job.IconId);

            ImGui.TableNextColumn();
            if (isCurrent)
            {
                ImGui.TextColored(theme.Current.Accent, job.Name);
                ImGui.SameLine();
                ImGui.TextDisabled($"({localizer.Get("jobs.current")})");
            }
            else if (entry.Level == 0)
            {
                ImGui.TextDisabled(job.Name);
            }
            else
            {
                ImGui.TextUnformatted(job.Name);
            }

            ImGui.TableNextColumn();
            if (entry.Level == 0)
            {
                ImGui.TextDisabled("-");
            }
            else if (isCurrent && entry.ExperienceToNext > 0)
            {
                ImGui.TextUnformatted(localizer.Format("jobs.level", entry.Level));
                ImGui.SameLine();
                ImGui.TextDisabled($"{entry.Experience}/{entry.ExperienceToNext}");
            }
            else
            {
                ImGui.TextUnformatted(localizer.Format("jobs.level", entry.Level));
            }
        }
    }

    private static void DrawIcon(uint iconId)
    {
        if (iconId == 0)
        {
            ImGui.Dummy(new System.Numerics.Vector2(22, 22));
            return;
        }

        var texture = Svc.Textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
        if (texture == null)
        {
            ImGui.Dummy(new System.Numerics.Vector2(22, 22));
            return;
        }

        ImGui.Image(texture.Handle, new System.Numerics.Vector2(22, 22));
    }
}
