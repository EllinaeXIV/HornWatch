using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Hornwatch.Core;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Navigation;

public sealed class VnavmeshPathfinder : IPathfinder
{
    private readonly PluginPresence installed;
    private readonly IPluginLog log;

    private const float SnapRadiusHorizontal = 20f;
    private const float SnapRadiusVertical = 20f;

    private const float MaximumSnapDisplacement = 5f;

    private const float FloorSearchRadius = 40f;

    private const float MaximumFloorDrift = 6f;

    private const float ProbeAltitude = 1000f;

    private readonly ICallGateSubscriber<bool> navIsReady;
    private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindAndMoveTo;
    private readonly ICallGateSubscriber<object> pathStop;
    private readonly ICallGateSubscriber<bool> pathIsRunning;
    private readonly ICallGateSubscriber<bool> pathfindInProgress;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> meshNearestPoint;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> meshPointOnFloor;

    private readonly HashSet<string> reported = new();

    public VnavmeshPathfinder(IDalamudPluginInterface pluginInterface, PluginPresence installed, IPluginLog log)
    {
        this.installed = installed;
        this.log = log;

        navIsReady = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        pathfindAndMoveTo = pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        pathStop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        pathIsRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        meshNearestPoint = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        meshPointOnFloor = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
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
            if (meshNearestPoint.InvokeFunc(approximate, SnapRadiusHorizontal, SnapRadiusVertical) is { } onMesh &&
                Vector3.Distance(approximate, onMesh) <= MaximumSnapDisplacement)
            {
                return onMesh;
            }
        }
        catch (Exception ex)
        {
            Report("Query.Mesh.NearestPoint", ex);
            return approximate;
        }

        var floor = GroundLevelAt(approximate);

        if (floor == approximate)
        {
            log.Warning($"{approximate} is not on the mesh and no floor sits under it - a route there will not start.");
            return approximate;
        }

        log.Information($"{approximate} was off the mesh; the floor under it is {floor}.");

        return floor;
    }

    public Vector3 GroundLevelAt(Vector3 column)
    {
        if (!IsAvailable)
        {
            return column;
        }

        try
        {
            if (meshPointOnFloor.InvokeFunc(column with { Y = ProbeAltitude }, true, FloorSearchRadius) is { } floor)
            {
                var drift = column.GroundDistanceTo(floor);

                if (drift <= MaximumFloorDrift)
                {
                    return floor;
                }

                Report(
                    $"floor for {column}",
                    $"the nearest floor to {column} is {drift:F1}y sideways at height {floor.Y:F0} - that is a roof or another structure, not the ground under it.");
            }
        }
        catch (Exception ex)
        {
            Report("Query.Mesh.PointOnFloor", ex);
        }

        return column;
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

    private bool IsInstalled => installed.IsLoaded(PluginPresence.Vnavmesh);

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

    private void Report(string subject, string message)
    {
        if (reported.Add(subject))
        {
            log.Warning(message);
        }
    }
}
