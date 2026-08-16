using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Hazards;
using Hornwatch.Core.Navigation;

namespace Hornwatch.Core.Treasure;

public enum HuntState
{
    Idle,

    Running,

    Paused,
}

public sealed class TreasureHunt(
    ITravelService travel,
    Func<ISpottedTreasureSource?> spotted,
    Func<IHazardSource?> hazards,
    Func<ITreasureSurvey?> survey,
    Func<bool> inCombat,
    Func<Vector3?> playerPosition,
    Func<uint> currentTerritory,
    IPluginLog log)
{
    private const float CollectedRange = 1.2f;

    private const float ApproachRange = 12f;

    private const int MaximumApproachAttempts = 4;

    private static readonly TimeSpan BetweenApproaches = TimeSpan.FromSeconds(2);

    private const float CofferPresenceRange = 15f;

    private const float SightRange = 50f;

    private const float ReachedRange = 25f;

    private static readonly TimeSpan LingerAfterArrival = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan DwellOnCoffer = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan CofferDeadline = TimeSpan.FromMinutes(2.5);

    private static readonly TimeSpan BetweenSweeps = TimeSpan.FromSeconds(12);

    private readonly List<TreasurePoint> route = [];

    private readonly HuntReport report = new();

    private TreasureRouteOptions options = new();
    private DateTimeOffset observedAt = DateTimeOffset.UtcNow;
    private uint plannedFor;
    private int index;
    private DateTimeOffset arrivedAt = DateTimeOffset.MaxValue;
    private DateTimeOffset standingOnCofferSince = DateTimeOffset.MaxValue;
    private DateTimeOffset legStartedAt = DateTimeOffset.MaxValue;
    private int approachAttempts;
    private DateTimeOffset lastApproachAt = DateTimeOffset.MinValue;
    private DateTimeOffset lastSweepAt = DateTimeOffset.MinValue;

    public HuntState State { get; private set; } = HuntState.Idle;

    public bool IsRunning => State == HuntState.Running;

    public bool IsPaused => State == HuntState.Paused;

    public bool CanStart => route.Count > 0 && index < route.Count;

    public IReadOnlyList<TreasurePoint> Route => route;

    public int Index => index;

    public int Remaining => Math.Max(0, route.Count - index);

    public TreasurePoint? Current => index >= 0 && index < route.Count ? route[index] : null;

    public void Plan(IReadOnlyList<TreasurePoint> points, TreasureRouteOptions options)
    {
        var from = playerPosition() ?? Vector3.Zero;

        this.options = options;

        var candidates = options.SightedOnly ? SightedNow(points) : points;

        route.Clear();
        route.AddRange(TreasureRoutePlanner.Plan(Reachable(candidates, options), from, options));

        plannedFor = currentTerritory();
        index = 0;
        State = HuntState.Idle;

        log.Information($"[hunt] planned {route.Count} coffers in territory {plannedFor}.");
    }

    public void Start()
    {
        if (!CanStart)
        {
            return;
        }

        State = HuntState.Running;
        ForgetArrival();
        report.Begin();
        observedAt = DateTimeOffset.UtcNow;
        TravelToCurrent();
    }

    public void Pause()
    {
        if (State != HuntState.Running)
        {
            return;
        }

        log.Information($"[hunt] paused on coffer {index + 1}/{route.Count}.");

        State = HuntState.Paused;
        ForgetArrival();
        travel.Stop();
    }

    public void Resume()
    {
        if (State != HuntState.Paused || !CanStart)
        {
            return;
        }

        log.Information($"[hunt] resumed on coffer {index + 1}/{route.Count}.");

        State = HuntState.Running;
        ForgetArrival();
        TravelToCurrent();
    }

    public void Stop()
    {
        if (State == HuntState.Running && report.HasStarted && index < route.Count)
        {
            log.Information(report.Summarise(index, route.Count, hazards()?.DangerousFromLevel ?? 0));
        }

        State = HuntState.Idle;
        index = 0;
        ForgetArrival();
        travel.Stop();
    }

    private void ForgetArrival()
    {
        arrivedAt = DateTimeOffset.MaxValue;
        standingOnCofferSince = DateTimeOffset.MaxValue;
        approachAttempts = 0;
        lastApproachAt = DateTimeOffset.MinValue;
    }

    public void Skip()
    {
        if (index >= route.Count)
        {
            return;
        }

        index++;
        ForgetArrival();

        if (State == HuntState.Running)
        {
            TravelToCurrent();
        }
    }

    public void Tick()
    {
        if (State != HuntState.Running)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        report.Observe(playerPosition(), inCombat(), now - observedAt, hazards());
        observedAt = now;

        SweepForCoffers(now);

        if (currentTerritory() != plannedFor)
        {
            log.Information("[hunt] zone changed - stopping.");
            Stop();
            return;
        }

        if (Current is not { } target)
        {
            log.Information("[hunt] every coffer on the route has been visited.");
            Stop();
            return;
        }

        var cofferHere = CofferNear(target.Position);
        var aim = cofferHere ?? target.Position;
        var toTarget = playerPosition() is { } here ? here.GroundDistanceTo(aim) : float.MaxValue;

        if (CanSeeCoffers && cofferHere == null && toTarget > CollectedRange && toTarget <= SightRange)
        {
            Advance(CofferOutcome.Empty, "nothing there - moving on");
            return;
        }

        if (toTarget <= CollectedRange)
        {
            if (standingOnCofferSince == DateTimeOffset.MaxValue)
            {
                standingOnCofferSince = DateTimeOffset.UtcNow;
                travel.Stop();
                return;
            }

            if (DateTimeOffset.UtcNow - standingOnCofferSince < DwellOnCoffer)
            {
                return;
            }

            Advance(CofferOutcome.Collected, "collected");
            return;
        }

        if (DateTimeOffset.UtcNow - legStartedAt > CofferDeadline)
        {
            Advance(CofferOutcome.TimedOut, "gave up - the leg ran past its deadline");
            return;
        }

        if (travel.Phase != TravelPhase.Idle)
        {
            arrivedAt = DateTimeOffset.MaxValue;
            return;
        }

        if (toTarget <= ApproachRange && approachAttempts < MaximumApproachAttempts)
        {
            if (DateTimeOffset.UtcNow - lastApproachAt < BetweenApproaches)
            {
                return;
            }

            lastApproachAt = DateTimeOffset.UtcNow;
            approachAttempts++;
            log.Information($"[hunt] {toTarget:F1}y short of coffer {index + 1} - closing in ({approachAttempts}).");
            MoveOnto(aim, isLiveObject: cofferHere != null);
            return;
        }

        if (arrivedAt == DateTimeOffset.MaxValue)
        {
            arrivedAt = DateTimeOffset.UtcNow;
            return;
        }

        if (DateTimeOffset.UtcNow - arrivedAt < LingerAfterArrival)
        {
            return;
        }

        Advance(
            toTarget <= ReachedRange ? CofferOutcome.Reached : CofferOutcome.Unreachable,
            toTarget <= ReachedRange ? "reached" : $"unreachable ({toTarget:F0}y away)");
    }

    private void Advance(CofferOutcome outcome, string reason)
    {
        log.Information($"[hunt] coffer {index + 1}/{route.Count}: {reason}.");

        report.Record(outcome);

        index++;
        ForgetArrival();

        if (index >= route.Count)
        {
            log.Information("[hunt] route finished.");
            log.Information(report.Summarise(index, route.Count, hazards()?.DangerousFromLevel ?? 0));
            Stop();

            if (options.ReturnToCampWhenDone)
            {
                travel.ReturnToCamp();
            }

            return;
        }

        TravelToCurrent();
    }

    private void TravelToCurrent()
    {
        if (Current is not { } target)
        {
            return;
        }

        legStartedAt = DateTimeOffset.UtcNow;

        var coffer = CofferNear(target.Position);
        MoveOnto(coffer ?? target.Position, isLiveObject: coffer != null);
    }

    private void MoveOnto(Vector3 aim, bool isLiveObject) =>
        travel.TravelTo(aim, null, 0f, dismountOnArrival: false, snapToGround: !isLiveObject);

    private IReadOnlyList<TreasurePoint> Reachable(
        IReadOnlyList<TreasurePoint> points, TreasureRouteOptions wanted)
    {
        if (wanted.IncludeHostileAreas || hazards() is not { } danger)
        {
            return points;
        }

        var kept = new List<TreasurePoint>(points.Count);
        var dropped = 0;

        foreach (var point in points)
        {
            if (danger.IsInHostileArea(point.Position))
            {
                dropped++;
                continue;
            }

            kept.Add(point);
        }

        if (dropped > 0)
        {
            log.Information($"[hunt] {dropped} coffers left out: they sit in an area marked lethal.");
        }

        return kept;
    }

    private void SweepForCoffers(DateTimeOffset now)
    {
        if (!options.SweepWhileWalking || now - lastSweepAt < BetweenSweeps || inCombat())
        {
            return;
        }

        if (survey() is not { IsUsable: true } ability)
        {
            return;
        }

        lastSweepAt = now;
        ability.Sweep();
    }

    private bool CanSeeCoffers => spotted() != null;

    private IReadOnlyList<TreasurePoint> SightedNow(IReadOnlyList<TreasurePoint> catalogue)
    {
        if (spotted() is not { Spotted.Count: > 0 } source)
        {
            log.Information("[hunt] nothing in sight, so the route falls back to the catalogue.");
            return catalogue;
        }

        var onFloor = new List<TreasurePoint>(source.Spotted.Count);

        foreach (var coffer in source.Spotted)
        {
            onFloor.Add(coffer.AsWaypoint());
        }

        log.Information($"[hunt] routing over {onFloor.Count} coffers in sight instead of {catalogue.Count} catalogue points.");

        return onFloor;
    }

    private Vector3? CofferNear(Vector3 waypoint)
    {
        if (spotted() is not { } source)
        {
            return null;
        }

        Vector3? closest = null;
        var bestDistance = CofferPresenceRange;

        foreach (var coffer in source.Spotted)
        {
            var distance = coffer.Position.GroundDistanceTo(waypoint);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                closest = coffer.Position;
            }
        }

        return closest;
    }
}
