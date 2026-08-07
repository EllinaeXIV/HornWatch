using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Hornwatch.Core;
using Hornwatch.Core.Jobs;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;
using Hornwatch.Theming;
using Hornwatch.Modules.OccultCrescent;

namespace Hornwatch.Windows.Tabs;

public sealed class PartyTab : ITab
{
    private readonly FieldModuleRegistry modules;
    private readonly ILocalizer localizer;
    private readonly ThemeManager theme;

    public PartyTab(FieldModuleRegistry modules, ILocalizer localizer, ThemeManager theme)
    {
        this.modules = modules;
        this.localizer = localizer;
        this.theme = theme;
    }

    public string TitleKey => "tab.party";

    public void Draw()
    {
        var jobs = modules.Capability<ISpecialJobSource>();

        ImGui.Spacing();

        if (Svc.Party.Length == 0)
        {
            DrawSolo(jobs);
            return;
        }

        DrawTable(jobs);

        if (jobs is { SupportsRemoteProgress: false })
        {
            ImGui.Spacing();
            ImGui.TextDisabled(localizer.Get("party.remoteUnsupported"));
        }
    }

    private void DrawSolo(ISpecialJobSource? jobs)
    {
        ImGui.TextDisabled(localizer.Get("party.alone"));
        ImGui.Spacing();

        var me = Svc.Objects.LocalPlayer;
        if (me == null)
        {
            return;
        }

        using var table = ImRaii.Table("hornwatch-party-solo", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table.Success)
        {
            return;
        }

        SetupColumns();
        DrawLocalRow(jobs);
    }

    private void DrawTable(ISpecialJobSource? jobs)
    {
        using var table = ImRaii.Table("hornwatch-party", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table.Success)
        {
            return;
        }

        SetupColumns();

        var localName = Svc.Objects.LocalPlayer?.Name.TextValue ?? string.Empty;
        var occult = modules.Active?.GetCapability<ISpecialJobSource>() as OccultJobSource;
        var progress = jobs?.LocalProgress;

        for (var i = 0; i < Svc.Party.Length; i++)
        {
            var member = Svc.Party[i];
            if (member == null)
            {
                continue;
            }

            var name = member.Name.TextValue;
            var isLocal = name == localName;

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(isLocal ? $"{name} {localizer.Get("party.you")}" : name);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(member.ClassJob.ValueNullable?.Name.ExtractText() ?? "-");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(member.Level.ToString());

            var phantom = occult?.FromStatuses(member.Statuses);

            ImGui.TableNextColumn();
            if (phantom == null)
            {
                ImGui.TextDisabled(localizer.Get("party.none"));
            }
            else
            {
                ImGui.TextUnformatted(phantom.Name);
            }

            ImGui.TableNextColumn();
            DrawPhantomLevel(jobs, progress, phantom, isLocal);
        }
    }

    private void DrawLocalRow(ISpecialJobSource? jobs)
    {
        var me = Svc.Objects.LocalPlayer;
        if (me == null)
        {
            return;
        }

        var occult = jobs as OccultJobSource;
        var phantom = occult?.FromStatuses(me.StatusList);

        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{me.Name.TextValue} {localizer.Get("party.you")}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(me.ClassJob.ValueNullable?.Name.ExtractText() ?? "-");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(me.Level.ToString());

        ImGui.TableNextColumn();
        if (phantom == null)
        {
            ImGui.TextDisabled(localizer.Get("party.none"));
        }
        else
        {
            ImGui.TextUnformatted(phantom.Name);
        }

        ImGui.TableNextColumn();
        DrawPhantomLevel(jobs, jobs?.LocalProgress, phantom, true);
    }

    private void DrawPhantomLevel(
        ISpecialJobSource? jobs,
        System.Collections.Generic.IReadOnlyList<SpecialJobProgress>? progress,
        SpecialJob? phantom,
        bool isLocal)
    {
        if (phantom == null)
        {
            ImGui.TextDisabled("-");
            return;
        }

        if (!isLocal && jobs is { SupportsRemoteProgress: false })
        {
            using var muted = ImRaii.PushColor(ImGuiCol.Text, theme.Current.TextMuted);
            ImGui.TextUnformatted("-");
            return;
        }

        if (progress == null)
        {
            ImGui.TextDisabled("-");
            return;
        }

        foreach (var entry in progress)
        {
            if (entry.JobId == phantom.Id)
            {
                ImGui.TextUnformatted(entry.Level.ToString());
                return;
            }
        }

        ImGui.TextDisabled("-");
    }

    private void SetupColumns()
    {
        ImGui.TableSetupColumn(localizer.Get("party.player"), ImGuiTableColumnFlags.WidthStretch, 2.2f);
        ImGui.TableSetupColumn(localizer.Get("party.job"), ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn(localizer.Get("party.level"), ImGuiTableColumnFlags.WidthStretch, 0.5f);
        ImGui.TableSetupColumn(localizer.Get("party.phantomJob"), ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn(localizer.Get("party.phantomLevel"), ImGuiTableColumnFlags.WidthStretch, 0.6f);
        ImGui.TableHeadersRow();
    }
}
