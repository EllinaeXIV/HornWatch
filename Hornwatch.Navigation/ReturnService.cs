using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class ReturnService : IRecall
{
    private const uint ReturnActionId = 8;

    private readonly ICondition condition;

    public ReturnService(ICondition condition)
    {
        this.condition = condition;
    }

    public bool IsBusy =>
        condition[ConditionFlag.Casting] ||
        condition[ConditionFlag.BetweenAreas] ||
        condition[ConditionFlag.BetweenAreas51];

    public unsafe bool Cast()
    {
        if (condition[ConditionFlag.InCombat] ||
            condition[ConditionFlag.Casting] ||
            condition[ConditionFlag.Occupied] ||
            condition[ConditionFlag.BetweenAreas] ||
            condition[ConditionFlag.Unconscious])
        {
            return false;
        }

        var manager = ActionManager.Instance();
        if (manager == null)
        {
            return false;
        }

        if (manager->GetActionStatus(ActionType.GeneralAction, ReturnActionId) != 0)
        {
            return false;
        }

        return manager->UseAction(ActionType.GeneralAction, ReturnActionId);
    }
}
