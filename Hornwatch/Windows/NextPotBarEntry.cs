using System;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Hornwatch.Core;
using Hornwatch.Core.Encounters;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Modules;

namespace Hornwatch.Windows;

public sealed class NextPotBarEntry : IDisposable
{
    private readonly IDtrBarEntry entry;
    private readonly FieldModuleRegistry modules;
    private readonly ILocalizer localizer;
    private readonly Func<bool> wanted;

    private string shownText = string.Empty;

    public NextPotBarEntry(
        IDtrBar bar,
        FieldModuleRegistry modules,
        ILocalizer localizer,
        Func<bool> wanted,
        Action reveal)
    {
        this.modules = modules;
        this.localizer = localizer;
        this.wanted = wanted;

        entry = bar.Get(PluginMeta.InternalName);
        entry.OnClick = _ => reveal();
        entry.Shown = false;
    }

    public void Dispose()
    {
        entry.Shown = false;
        entry.Remove();
    }

    public void Refresh()
    {
        if (!wanted() || Compose() is not { } text)
        {
            entry.Shown = false;
            return;
        }

        if (text != shownText)
        {
            shownText = text;
            entry.Text = new SeStringBuilder().AddText(text).Build();
            entry.Tooltip = new SeStringBuilder().AddText(localizer.Get("bar.tooltip")).Build();
        }

        entry.Shown = true;
    }

    private string? Compose()
    {
        if (modules.Capability<RespawnTracker>() is not { } tracker)
        {
            return null;
        }

        var soonest = Soonest(tracker.Predictions);

        if (soonest == null)
        {
            return tracker.IsUnproven || !modules.InSupportedZone
                ? null
                : $"{PotGlyph} {localizer.Get("bar.potUnknown")}";
        }

        return soonest.IsDue
            ? $"{PotGlyph} {localizer.Get("bar.potDue")}"
            : $"{PotGlyph} {Countdown(soonest.Remaining)}";
    }

    private static RespawnPrediction? Soonest(System.Collections.Generic.IReadOnlyList<RespawnPrediction> predictions)
    {
        RespawnPrediction? best = null;

        foreach (var prediction in predictions)
        {
            if (!prediction.IsKnown)
            {
                continue;
            }

            if (best?.ExpectedAt is not { } bestAt || prediction.ExpectedAt < bestAt)
            {
                best = prediction;
            }
        }

        return best;
    }

    private static string Countdown(TimeSpan remaining) =>
        remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:0}:{remaining.Seconds:00}";

    private static char PotGlyph => (char)SeIconChar.Clock;
}
