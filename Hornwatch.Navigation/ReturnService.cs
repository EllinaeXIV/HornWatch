using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class ReturnService(ICondition condition) : IRecall
{
    private const uint ReturnActionId = 8;

    private static readonly TimeSpan LeastTimeBetweenRequests = TimeSpan.FromSeconds(6);

    private DateTimeOffset sentAt = DateTimeOffset.MinValue;

    public bool IsBusy =>
        condition[ConditionFlag.Casting] ||
        condition[ConditionFlag.BetweenAreas] ||
        condition[ConditionFlag.BetweenAreas51];

    public unsafe RecallAttempt Cast()
    {
        if (DateTimeOffset.UtcNow - sentAt < LeastTimeBetweenRequests)
        {
            return RecallAttempt.Sent;
        }

        if (condition[ConditionFlag.InCombat] ||
            condition[ConditionFlag.Casting] ||
            condition[ConditionFlag.Occupied] ||
            condition[ConditionFlag.BetweenAreas] ||
            condition[ConditionFlag.Unconscious])
        {
            return RecallAttempt.Refused;
        }

        var manager = ActionManager.Instance();
        if (manager == null)
        {
            return RecallAttempt.Refused;
        }

        if (manager->GetActionStatus(ActionType.GeneralAction, ReturnActionId) != 0)
        {
            return RecallAttempt.Refused;
        }

        sentAt = DateTimeOffset.UtcNow;
        manager->UseAction(ActionType.GeneralAction, ReturnActionId);

        return RecallAttempt.Sent;
    }
}
