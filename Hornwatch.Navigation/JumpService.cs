using FFXIVClientStructs.FFXIV.Client.Game;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class JumpService : IJump
{
    private const uint JumpActionId = 2;

    public unsafe void Jump()
    {
        var manager = ActionManager.Instance();
        if (manager == null || manager->GetActionStatus(ActionType.GeneralAction, JumpActionId) != 0)
        {
            return;
        }

        manager->UseAction(ActionType.GeneralAction, JumpActionId);
    }
}
