using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class SelectYesnoConfirmer : IConfirmationDialog
{
    private const string AddonName = "SelectYesno";

    private const int YesIndex = 0;

    private readonly IGameGui gameGui;

    private bool answeredCurrent;

    public SelectYesnoConfirmer(IGameGui gameGui)
    {
        this.gameGui = gameGui;
    }

    public unsafe void Accept()
    {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName(AddonName).Address;

        if (addon == null || !addon->IsVisible || addon->UldManager.LoadedState != AtkLoadState.Loaded)
        {
            answeredCurrent = false;
            return;
        }

        if (answeredCurrent)
        {
            return;
        }

        answeredCurrent = true;

        var choice = stackalloc AtkValue[1];
        choice->Type = AtkValueType.Int;
        choice->Int = YesIndex;
        addon->FireCallback(1, choice);
    }
}
