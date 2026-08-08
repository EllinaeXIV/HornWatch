using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Jobs;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class PhantomJobCatalog(IDataManager data, IDataCache cache)
{
    private static readonly uint[] StatusIdByJobId =
    [
        4242, 
        4358, 
        4359, 
        4360, 
        4361, 
        4362, 
        4363, 
        4364, 
        4365, 
        4366, 
        4367, 
        4368, 
        4369, 
        4803, 
        4804, 
        4805, 
        5328, 
        5329, 
        5330, 
        5331, 
        5332, 
        5333, 
        5334, 
        5335, 
    ];

    private static readonly string[] FallbackNames =
    [
        "Freelancer", "Knight", "Berserker", "Monk", "Ranger", "Samurai", "Bard",
        "Geomancer", "Time Mage", "Cannoneer", "Chemist", "Oracle", "Thief",
        "Mystic Knight", "Gladiator", "Dancer", "Ninja", "White Mage", "Black Mage",
        "Dragoon", "Summoner", "Blue Mage", "Red Mage", "Necromancer",
    ];

    public const int JobCount = 24;

    public IReadOnlyList<SpecialJob> All => cache.GetOrCreate("occult.phantomjobs", Build);

    private IReadOnlyList<SpecialJob> Build()
    {
        var sheet = data.GetExcelSheet<LuminaStatus>();
        var jobs = new List<SpecialJob>(JobCount);

        for (var jobId = 0; jobId < StatusIdByJobId.Length; jobId++)
        {
            var statusId = StatusIdByJobId[jobId];
            var name = FallbackNames[jobId];
            uint icon = 0;

            if (sheet != null && sheet.TryGetRow(statusId, out var row))
            {
                var sheetName = row.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(sheetName))
                {
                    name = sheetName.StartsWith("Phantom ") ? sheetName["Phantom ".Length..] : sheetName;
                }

                icon = row.Icon;
            }

            jobs.Add(new SpecialJob(jobId, name, icon));
        }

        return jobs;
    }

    public uint StatusIdFor(int jobId)
    {
        return jobId >= 0 && jobId < StatusIdByJobId.Length ? StatusIdByJobId[jobId] : 0;
    }

    public int JobIdForStatus(uint statusId)
    {
        for (var i = 0; i < StatusIdByJobId.Length; i++)
        {
            if (StatusIdByJobId[i] == statusId)
            {
                return i;
            }
        }

        return -1;
    }
}
