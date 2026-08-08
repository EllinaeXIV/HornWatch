using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class LifestreamTeleporter : ITeleporter
{
    private readonly PluginPresence installed;
    private readonly IDataManager data;
    private readonly IPluginLog log;

    private readonly ICallGateSubscriber<uint, bool> aethernetTeleportByPlaceNameId;
    private readonly ICallGateSubscriber<string, bool> teleportToAethernetDestination;
    private readonly ICallGateSubscriber<bool> isBusy;
    private readonly ICallGateSubscriber<object> abort;

    public LifestreamTeleporter(IDalamudPluginInterface pluginInterface, PluginPresence installed, IDataManager data, IPluginLog log)
    {
        this.installed = installed;
        this.data = data;
        this.log = log;

        aethernetTeleportByPlaceNameId = pluginInterface.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportByPlaceNameId");
        teleportToAethernetDestination = pluginInterface.GetIpcSubscriber<string, bool>("Lifestream.TeleportToAethernetDestination");
        isBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        abort = pluginInterface.GetIpcSubscriber<object>("Lifestream.Abort");
    }

    public bool IsAvailable => IsInstalled;

    public Vector3? BoardingPoint => null;

    public string? UnavailableReasonKey => IsInstalled ? null : "nav.reason.lifestreamMissing";

    public bool IsBusy
    {
        get
        {
            try
            {
                return isBusy.InvokeFunc();
            }
            catch
            {
                return false;
            }
        }
    }

    public bool TeleportTo(uint placeNameId)
    {
        if (!IsAvailable)
        {
            return false;
        }

        if (Try(() => aethernetTeleportByPlaceNameId.InvokeFunc(placeNameId), "AethernetTeleportByPlaceNameId"))
        {
            return true;
        }

        var name = PlaceName(placeNameId);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return Try(() => teleportToAethernetDestination.InvokeFunc(name), "TeleportToAethernetDestination");
    }

    private string PlaceName(uint placeNameId)
    {
        var row = data.GetExcelSheet<Lumina.Excel.Sheets.PlaceName>()?.GetRowOrDefault(placeNameId);
        return row?.Name.ExtractText() ?? string.Empty;
    }

    private bool Try(Func<bool> call, string name)
    {
        try
        {
            var accepted = call();
            if (!accepted)
            {
                log.Debug($"Lifestream.{name} declined the teleport.");
            }

            return accepted;
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Lifestream.{name} failed.");
            return false;
        }
    }

    public void Tick()
    {
    }

    public void Abort()
    {
        try
        {
            abort.InvokeAction();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Lifestream.Abort failed; a teleport it had started may still be running.");
        }
    }

    private bool IsInstalled => installed.IsLoaded(PluginPresence.Lifestream);
}
