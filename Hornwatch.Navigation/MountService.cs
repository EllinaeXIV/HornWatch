using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class MountService : IMountController
{
    private const uint MountRouletteActionId = 9;
    private const uint DismountActionId = 23;

    private readonly ICondition condition;
    private readonly Func<uint?> choice;

    public MountService(ICondition condition, Func<uint?> choice)
    {
        this.condition = condition;
        this.choice = choice;
    }

    public bool IsEnabled => choice() != null;

    public bool IsMounted => condition[ConditionFlag.Mounted];

    public bool CanMount =>
        !IsMounted &&
        !condition[ConditionFlag.InCombat] &&
        !condition[ConditionFlag.Casting] &&
        !condition[ConditionFlag.Occupied] &&
        !condition[ConditionFlag.BetweenAreas] &&
        !condition[ConditionFlag.Unconscious];

    public unsafe bool Mount()
    {
        if (choice() is not { } mountId || !CanMount)
        {
            return false;
        }

        var manager = ActionManager.Instance();
        if (manager == null)
        {
            return false;
        }

        if (mountId != 0 && IsUnlocked(mountId) && manager->GetActionStatus(ActionType.Mount, mountId) == 0)
        {
            return manager->UseAction(ActionType.Mount, mountId);
        }

        if (manager->GetActionStatus(ActionType.GeneralAction, MountRouletteActionId) != 0)
        {
            return false;
        }

        return manager->UseAction(ActionType.GeneralAction, MountRouletteActionId);
    }

    public unsafe void Dismount()
    {
        if (!IsMounted)
        {
            return;
        }

        var manager = ActionManager.Instance();
        if (manager == null)
        {
            return;
        }

        if (manager->GetActionStatus(ActionType.GeneralAction, DismountActionId) != 0)
        {
            return;
        }

        manager->UseAction(ActionType.GeneralAction, DismountActionId);
    }

    private static unsafe bool IsUnlocked(uint mountId)
    {
        var state = PlayerState.Instance();
        return state != null && state->IsMountUnlocked(mountId);
    }
}
