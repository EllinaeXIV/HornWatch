using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class VnavmeshPathfinder : IPathfinder
{
    private const string VnavmeshInternalName = "vnavmesh";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;

    private const float SnapRadiusHorizontal = 20f;
    private const float SnapRadiusVertical = 500f;

    private readonly ICallGateSubscriber<bool> navIsReady;
    private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindAndMoveTo;
    private readonly ICallGateSubscriber<object> pathStop;
    private readonly ICallGateSubscriber<bool> pathIsRunning;
    private readonly ICallGateSubscriber<bool> pathfindInProgress;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> meshNearestPoint;

    private readonly HashSet<string> reported = new();

    public VnavmeshPathfinder(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.log = log;

        navIsReady = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        pathfindAndMoveTo = pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        pathStop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        pathIsRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        meshNearestPoint = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
    }

    public Vector3? Destination { get; private set; }

    public bool IsAvailable => IsInstalled && TryNavReady();

    public string? UnavailableReasonKey
    {
        get
        {
            if (!IsInstalled)
            {
                return "nav.reason.notInstalled";
            }

            return TryNavReady() ? null : "nav.reason.meshBuilding";
        }
    }

    public bool IsMoving
    {
        get
        {
            try
            {
                return pathIsRunning.InvokeFunc() || pathfindInProgress.InvokeFunc();
            }
            catch (Exception ex)
            {
                Report("Path.IsRunning / SimpleMove.PathfindInProgress", ex);
                return false;
            }
        }
    }

    public Vector3 SnapToGround(Vector3 approximate)
    {
        if (!IsAvailable)
        {
            return approximate;
        }

        try
        {
            if (meshNearestPoint.InvokeFunc(approximate, SnapRadiusHorizontal, SnapRadiusVertical) is { } onMesh)
            {
                return onMesh;
            }
        }
        catch (Exception ex)
        {
            Report("Query.Mesh.NearestPoint", ex);
        }

        return approximate;
    }

    public void MoveTo(Vector3 destination)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            pathfindAndMoveTo.InvokeFunc(destination, false);
            Destination = destination;
        }
        catch (Exception ex)
        {
            Report("SimpleMove.PathfindAndMoveTo", ex);
            Destination = null;
        }
    }

    public void Stop()
    {
        Destination = null;

        try
        {
            pathStop.InvokeAction();
        }
        catch (Exception ex)
        {
            Report("Path.Stop", ex);
        }
    }

    private bool IsInstalled
    {
        get
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                if (plugin.InternalName == VnavmeshInternalName && plugin.IsLoaded)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private bool TryNavReady()
    {
        try
        {
            return navIsReady.InvokeFunc();
        }
        catch (Exception ex)
        {
            Report("Nav.IsReady", ex);
            return false;
        }
    }

    private void Report(string call, Exception ex)
    {
        if (reported.Add(call))
        {
            log.Error(ex, $"vnavmesh IPC '{call}' failed - the feature behind it is not working.");
        }
    }
}
