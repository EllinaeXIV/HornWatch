using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Modules.OccultCrescent;

public sealed unsafe class OccultTreasuresight(Func<bool> allowed) : ITreasureSurvey
{
    private const uint TreasuresightActionId = 41651;

    public string AbilityNameKey => "treasure.survey";

    public bool IsUsable
    {
        get
        {
            if (!allowed())
            {
                return false;
            }

            var manager = ActionManager.Instance();

            return manager != null
                   && manager->GetActionStatus(ActionType.Action, TreasuresightActionId) == 0;
        }
    }

    public bool Sweep()
    {
        if (!IsUsable)
        {
            return false;
        }

        var manager = ActionManager.Instance();

        return manager != null && manager->UseAction(ActionType.Action, TreasuresightActionId);
    }
}
