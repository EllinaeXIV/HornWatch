using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Guides;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class PhantomBlueMageGuide(IDataManager data, IDataCache cache) : IZoneGuide
{
    private const string NorthHorn = "North Horn";

    public string TitleKey => "guide.blueMage.title";
    public string IntroKey => "guide.blueMage.intro";

    public IReadOnlyList<GuideSection> Sections => cache.GetOrCreate("occult.blueMageGuide", Build);

    private IReadOnlyList<GuideSection> Build()
    {
        var icons = BuildIconIndex();

        uint Icon(string englishName) => icons.GetValueOrDefault(englishName, 0u);
        string Display(string englishName) => ResolveLocalizedName(englishName);

        return
        [
            new GuideSection("guide.blueMage.level1",
            [
                new GuideEntry
                {
                    Name = Display("Occult Aero"),
                    IconId = Icon("Occult Aero"),
                    RequiredLevel = 1,
                    UnlockKind = GuideUnlockKind.Automatic,
                },
                new GuideEntry
                {
                    Name = Display("Occult Missile"),
                    IconId = Icon("Occult Missile"),
                    RequiredLevel = 1,
                    UnlockKind = GuideUnlockKind.CriticalEncounter,
                    SourceName = "Pallmagia",
                    ZoneName = "Appalling Behavior",
                },
                new GuideEntry
                {
                    Name = Display("Occult Aqua Breath"),
                    IconId = Icon("Occult Aqua Breath"),
                    RequiredLevel = 1,
                    UnlockKind = GuideUnlockKind.Monster,
                    SourceName = "Crescent Stoneshell",
                    SourceLevel = 29,
                    ZoneName = NorthHorn,
                    X = 31, Y = 8,
                },
            ]),

            new GuideSection("guide.blueMage.level2",
            [
                new GuideEntry
                {
                    Name = Display("Occult Aero II"),
                    IconId = Icon("Occult Aero II"),
                    RequiredLevel = 2,
                    UnlockKind = GuideUnlockKind.Monster,
                    SourceName = "Crescent Anila",
                    SourceLevel = 32,
                    ZoneName = NorthHorn,
                    X = 16, Y = 37,
                    NoteKey = "guide.blueMage.replacesPrevious",
                },
                new GuideEntry
                {
                    Name = Display("Occult Mighty Guard"),
                    IconId = Icon("Occult Mighty Guard"),
                    RequiredLevel = 2,
                    UnlockKind = GuideUnlockKind.Monster,
                    SourceName = "Crescent Bibliotaph",
                    SourceLevel = 22,
                    ZoneName = NorthHorn,
                    X = 38, Y = 31,
                },
            ]),

            new GuideSection("guide.blueMage.level3",
            [
                new GuideEntry
                {
                    Name = Display("Occult Aero III"),
                    IconId = Icon("Occult Aero III"),
                    RequiredLevel = 3,
                    UnlockKind = GuideUnlockKind.CriticalEncounter,
                    SourceName = "Alabaster Blade",
                    ZoneName = "Quarried Away",
                    NoteKey = "guide.blueMage.requiresAeroII",
                },
                new GuideEntry
                {
                    Name = Display("Occult White Wind"),
                    IconId = Icon("Occult White Wind"),
                    RequiredLevel = 3,
                    UnlockKind = GuideUnlockKind.Monster,
                    SourceName = "Crescent Flame",
                    SourceLevel = 42,
                    ZoneName = NorthHorn,
                    X = 5, Y = 36,
                },
            ]),
        ];
    }

    private Dictionary<string, uint> BuildIconIndex()
    {
        var index = new Dictionary<string, uint>(StringComparer.Ordinal);

        var english = data.GetExcelSheet<LuminaAction>(ClientLanguage.English);
        if (english == null)
        {
            return index;
        }

        foreach (var row in english)
        {
            var name = row.Name.ExtractText();
            if (name.StartsWith("Occult ", StringComparison.Ordinal))
            {
                index.TryAdd(name, row.Icon);
            }
        }

        return index;
    }

    private string ResolveLocalizedName(string englishName)
    {
        var localized = cache.GetOrCreate("occult.actionNames", BuildLocalizedIndex);
        return localized.GetValueOrDefault(englishName, englishName);
    }

    private Dictionary<string, string> BuildLocalizedIndex()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        var english = data.GetExcelSheet<LuminaAction>(ClientLanguage.English);
        var native = data.GetExcelSheet<LuminaAction>();
        if (english == null || native == null)
        {
            return map;
        }

        foreach (var row in english)
        {
            var name = row.Name.ExtractText();
            if (!name.StartsWith("Occult ", StringComparison.Ordinal))
            {
                continue;
            }

            if (native.TryGetRow(row.RowId, out var localizedRow))
            {
                var localized = localizedRow.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(localized))
                {
                    map.TryAdd(name, localized);
                }
            }
        }

        return map;
    }
}
