using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Hornwatch.Core.Jobs;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultJobSource(PhantomJobCatalog catalog, IObjectTable objects) : ISpecialJobSource
{

    public string SystemNameKey => "occult.phantomJobs";

    public IReadOnlyList<SpecialJob> AllJobs => catalog.All;

    public bool SupportsRemoteProgress => false;

    public SpecialJob? GetActiveJob(ulong gameObjectId)
    {
        if (objects.SearchById(gameObjectId) is not IBattleChara chara)
        {
            return null;
        }

        return FromStatuses(chara.StatusList);
    }

    public SpecialJob? FromStatuses(StatusList? statuses)
    {
        if (statuses == null)
        {
            return null;
        }

        foreach (var status in statuses)
        {
            var jobId = catalog.JobIdForStatus(status.StatusId);
            if (jobId < 0)
            {
                continue;
            }

            var all = catalog.All;
            return jobId < all.Count ? all[jobId] : null;
        }

        return null;
    }

    public unsafe IReadOnlyList<SpecialJobProgress> LocalProgress
    {
        get
        {
            var state = PublicContentOccultCrescent.GetState();
            if (state == null)
            {
                return [];
            }

            var levels = state->SupportJobLevels;
            var experience = state->SupportJobExperience;
            var toNext = state->NeededJobExperience;

            var result = new List<SpecialJobProgress>(PhantomJobCatalog.JobCount);
            for (var jobId = 0; jobId < PhantomJobCatalog.JobCount && jobId < levels.Length; jobId++)
            {
                var xp = jobId < experience.Length ? experience[jobId] : 0u;

                var isCurrent = jobId == state->CurrentSupportJob;
                result.Add(new SpecialJobProgress(jobId, levels[jobId], xp, isCurrent ? toNext : 0));
            }

            return result;
        }
    }

    public unsafe IReadOnlyList<ZoneResource> LocalResources
    {
        get
        {
            var state = PublicContentOccultCrescent.GetState();
            if (state == null)
            {
                return [];
            }

            return
            [
                new ZoneResource("occult.knowledge", state->CurrentKnowledge, state->NeededKnowledge),
                new ZoneResource("occult.silver", state->Silver),
                new ZoneResource("occult.gold", state->Gold),
            ];
        }
    }

    public unsafe int CurrentJobId
    {
        get
        {
            var state = PublicContentOccultCrescent.GetState();
            return state == null ? -1 : state->CurrentSupportJob;
        }
    }
}
