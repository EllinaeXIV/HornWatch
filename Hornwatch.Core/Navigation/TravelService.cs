using System;
using System.Numerics;
using Dalamud.Plugin.Services;

namespace Hornwatch.Core.Navigation;

public enum TravelPhase
{
    Idle,

    Recalling,

    Withdrawing,

    WalkingToTeleport,

    Teleporting,

    Mounting,

    Walking,
}

public interface ITravelService
{
    bool IsEnabled { get; }

    bool IsAvailable { get; }

    string? UnavailableReasonKey { get; }

    TravelPhase Phase { get; }

    Vector3? Destination { get; }

    Vector3? LegDestination { get; }

    void TravelTo(Vector3 destination, string? targetId = null, float? radius = null, bool dismountOnArrival = true);

    void ReturnToCamp();

    void Stop();

    void Tick();
}

public sealed class TravelCoordinator(
    IPathfinder pathfinder,
    ITeleporter teleporter,
    IMountController mount,
    IRecall recall,
    IConfirmationDialog confirmation,
    Func<ITeleportNetwork?> network,
    Func<Vector3?> playerPosition,
    Func<uint> currentTerritory,
    Func<string, bool> targetIsActive,
    Func<bool> teleportEnabled,
    Func<bool> recallEnabled,
    Func<bool> travelEnabled,
    Func<bool> inCombat,
    IJump jump,
    IPluginLog log) : ITravelService
{
    private const float TeleportPointRange = 3.5f;

    private const float AtTeleportPointRange = 25f;

    private const float DirectBoardingRange = 10f;

    private const float MaximumWalkToTeleportPoint = 120f;

    private const float MountDistanceThreshold = 100f;

    private const float MountForBoardingDistance = 40f;

    private const float SameShardRange = 5f;

    private const float DismountDistanceMin = 15f;

    private const float DismountDistanceMax = 20f;

    private static readonly TimeSpan TeleportTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan RecallTimeout = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan RecallRefusedTimeout = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan PhaseSettleTime = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1.5);

    private static readonly TimeSpan RecallRetryInterval = TimeSpan.FromSeconds(6);

    private static readonly TimeSpan TeleportRetryInterval = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan BoardingTimeout = TimeSpan.FromSeconds(12);

    private const float RecallDisplacement = 100f;

    private static readonly TimeSpan BoardingWalkTimeout = TimeSpan.FromSeconds(30);

    private const float MaximumWalkFallback = 250f;

    private const int MaximumBoardingAttempts = 3;

    private const float StallDistance = 3f;
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(8);

    private const float DropzoneMaxOffset = 20f;

    private const float MinimumArrivalRange = 3f;

    private const int MaximumUnstickAttempts = 6;

    private const float UnstickSidestep = 15f;

    private const float UnstickBackstep = 5f;

    private static readonly TimeSpan MountTimeout = TimeSpan.FromSeconds(10);

    private Vector3 finalDestination;
    private uint startedInTerritory;
    private DateTimeOffset phaseStartedAt;
    private uint? plannedTeleport;
    private bool recallAccepted;
    private int boardingAttempts;
    private bool teleportAccepted;
    private bool boardingWalkTried;
    private float? targetRadius;
    private int unstickAttempts;
    private string? targetId;
    private DateTimeOffset lastAttemptAt;
    private Vector3? recallFrom;
    private Vector3? boardingShard;
    private Vector3? progressFrom;
    private DateTimeOffset progressAt;
    private readonly Random random = new();
    private float dismountDistance;
    private bool dismountOnApproach = true;

    private Vector3? mountedWalkToShard;

    public TravelPhase Phase { get; private set; } = TravelPhase.Idle;

    public bool IsEnabled => travelEnabled();

    public Vector3? Destination => Phase == TravelPhase.Idle ? null : finalDestination;

    public Vector3? LegDestination => Phase switch
    {
        TravelPhase.Idle => null,
        TravelPhase.WalkingToTeleport => boardingShard,
        TravelPhase.Walking => pathfinder.Destination,
        _ => null,
    };

    public bool IsAvailable => pathfinder.IsAvailable;

    public string? UnavailableReasonKey => pathfinder.UnavailableReasonKey;

    public void TravelTo(
        Vector3 destination, string? targetId = null, float? radius = null, bool dismountOnArrival = true)
    {
        if (!IsAvailable)
        {
            return;
        }

        Stop();

        this.targetId = targetId;
        boardingWalkTried = false;
        targetRadius = radius;
        unstickAttempts = 0;
        mountedWalkToShard = null;
        dismountOnApproach = dismountOnArrival;

        finalDestination = pathfinder.SnapToGround(Dropzone(destination, radius));
        startedInTerritory = currentTerritory();

        plannedTeleport = PlanTeleport(finalDestination);
        if (plannedTeleport == null)
        {
            BeginMounting();
            return;
        }

        BeginBoarding();
    }

    public void ReturnToCamp()
    {
        if (!IsAvailable || !recallEnabled())
        {
            return;
        }

        Stop();

        startedInTerritory = currentTerritory();
        recallAccepted = false;
        recallFrom = playerPosition();
        EnterPhase(TravelPhase.Withdrawing);
    }

    public void Tick()
    {
        teleporter.Tick();

        if (Phase == TravelPhase.Idle)
        {
            return;
        }

        if (currentTerritory() != startedInTerritory)
        {
            Stop();
            return;
        }

        if (Phase != TravelPhase.Withdrawing && targetId is { } id && !targetIsActive(id))
        {
            BeginWithdrawal();
            return;
        }

        switch (Phase)
        {
            case TravelPhase.Withdrawing:
                if (Recalled || Elapsed > (recallAccepted ? RecallTimeout : RecallRefusedTimeout))
                {
                    Phase = TravelPhase.Idle;
                    return;
                }

                confirmation.Accept();
                TryRecallNow();
                return;

            case TravelPhase.Recalling:
                if (Recalled)
                {
                    BeginBoarding();
                    return;
                }

                if (Elapsed > (recallAccepted ? RecallTimeout : RecallRefusedTimeout))
                {
                    StartWalking();
                    return;
                }

                confirmation.Accept();
                TryRecallNow();
                return;

            case TravelPhase.WalkingToTeleport:
                if (AtBoardingShard())
                {
                    pathfinder.Stop();
                    BeginTeleport();
                    return;
                }

                if (Elapsed > BoardingWalkTimeout || IsStalled())
                {
                    pathfinder.Stop();
                    BeginTeleport();
                    return;
                }

                if (Settled && !pathfinder.IsMoving)
                {
                    if (boardingAttempts >= MaximumBoardingAttempts)
                    {
                        BeginTeleport();
                        return;
                    }

                    if (DueForRetry && boardingShard is { } shard)
                    {
                        boardingAttempts++;
                        lastAttemptAt = DateTimeOffset.UtcNow;
                        pathfinder.MoveTo(BoardingDestination(shard));
                    }
                }

                return;

            case TravelPhase.Teleporting:
                if (teleporter.IsBusy)
                {
                    teleportAccepted = true;

                    if (Elapsed > TeleportTimeout)
                    {
                        teleporter.Abort();
                        GiveUpOnTeleport();
                    }

                    return;
                }

                if (teleportAccepted)
                {
                    BeginMounting();
                    return;
                }

                if (!Settled)
                {
                    return;
                }

                if (Elapsed > BoardingTimeout)
                {
                    if (!boardingWalkTried && boardingShard is { } refusedShard)
                    {
                        BeginWalkToShard(refusedShard);
                        return;
                    }

                    GiveUpOnTeleport();
                    return;
                }

                if (DateTimeOffset.UtcNow - lastAttemptAt > TeleportRetryInterval)
                {
                    BeginTeleport();
                }

                return;

            case TravelPhase.Mounting:
                if (mount.IsMounted || Elapsed > MountTimeout)
                {
                    ResumeAfterMounting();
                    return;
                }

                mount.Mount();
                return;

            case TravelPhase.Walking:
                if (EngagedAtDestination())
                {
                    log.Information("[travel] engaged at the destination - the route is done.");
                    pathfinder.Stop();
                    Phase = TravelPhase.Idle;
                    return;
                }

                if ((Settled && !pathfinder.IsMoving) || IsStalled())
                {
                    if (RemainingDistance() > ArrivalRange && TryUnstick())
                    {
                        return;
                    }

                    pathfinder.Stop();
                    Phase = TravelPhase.Idle;
                    return;
                }

                if (dismountOnApproach && mount.IsMounted && RemainingDistance() <= dismountDistance)
                {
                    mount.Dismount();
                }

                return;
        }
    }

    public void Stop()
    {
        teleporter.Abort();
        pathfinder.Stop();
        targetId = null;
        mountedWalkToShard = null;
        Phase = TravelPhase.Idle;
    }

    private uint? PlanTeleport(Vector3 destination)
    {
        if (!teleportEnabled() || !teleporter.IsAvailable)
        {
            return null;
        }

        var points = network();
        var from = playerPosition();
        if (points == null || from == null)
        {
            return null;
        }

        var nearest = points.NearestTo(destination);
        if (nearest == null)
        {
            return null;
        }

        if (Horizontal(from.Value, destination) <= Horizontal(nearest.Position, destination))
        {
            return null;
        }

        if (NearestShardWithinWalk() is { } boardable && Horizontal(boardable, nearest.Position) < SameShardRange)
        {
            return null;
        }

        return nearest.PlaceNameId;
    }

    private void BeginBoarding()
    {
        boardingShard = NearestShardWithinWalk();

        if (boardingShard is { } shard)
        {
            if (Horizontal(playerPosition() ?? shard, shard) <= DirectBoardingRange)
            {
                boardingWalkTried = false;
                BeginTeleport();
                return;
            }

            BeginWalkToShard(shard);
            return;
        }

        if (recallEnabled())
        {
            recallAccepted = false;
            recallFrom = playerPosition();
            EnterPhase(TravelPhase.Recalling);
            return;
        }

        BeginMounting();
    }

    private bool EngagedAtDestination() =>
        inCombat() && RemainingDistance() <= ArrivalRange;

    private float ArrivalRange => MathF.Max(targetRadius ?? DropzoneMaxOffset, MinimumArrivalRange);

    private bool TryUnstick()
    {
        if (unstickAttempts >= MaximumUnstickAttempts)
        {
            log.Information($"[travel] still stuck after {unstickAttempts} attempts - abandoning the route.");
            return false;
        }

        unstickAttempts++;
        progressFrom = null;
        phaseStartedAt = DateTimeOffset.UtcNow;
        jump.Jump();

        if (unstickAttempts % 2 == 1 && SidestepTarget() is { } escape)
        {
            log.Information($"[travel] stuck - stepping aside (attempt {unstickAttempts}).");
            pathfinder.MoveTo(escape);
            return true;
        }

        log.Information($"[travel] stuck - re-routing to the destination (attempt {unstickAttempts}).");
        pathfinder.MoveTo(finalDestination);
        return true;
    }

    private Vector3? SidestepTarget()
    {
        if (playerPosition() is not { } here)
        {
            return null;
        }

        var toward = new Vector3(finalDestination.X - here.X, 0f, finalDestination.Z - here.Z);
        var length = toward.Length();
        if (length < 0.01f)
        {
            return null;
        }

        var forward = toward / length;
        var side = unstickAttempts / 2 % 2 == 0
            ? new Vector3(-forward.Z, 0f, forward.X)
            : new Vector3(forward.Z, 0f, -forward.X);

        var escape = here + (side * UnstickSidestep) - (forward * UnstickBackstep);
        return pathfinder.SnapToGround(escape with { Y = here.Y });
    }

    private void BeginWalkToShard(Vector3 shard)
    {
        boardingWalkTried = true;
        boardingAttempts = 0;

        if (mount.IsEnabled && !mount.IsMounted && !inCombat() && DistanceTo(shard) >= MountForBoardingDistance)
        {
            mountedWalkToShard = shard;
            EnterPhase(TravelPhase.Mounting);
            return;
        }

        WalkToShard(shard);
    }

    private void WalkToShard(Vector3 shard)
    {
        mountedWalkToShard = null;
        pathfinder.MoveTo(BoardingDestination(shard));
        EnterPhase(TravelPhase.WalkingToTeleport);
    }

    private void ResumeAfterMounting()
    {
        if (mountedWalkToShard is { } shard)
        {
            WalkToShard(shard);
            return;
        }

        StartWalking();
    }

    private void BeginTeleport()
    {
        if (Phase != TravelPhase.Teleporting)
        {
            teleportAccepted = false;
            EnterPhase(TravelPhase.Teleporting);
        }

        mount.Dismount();

        lastAttemptAt = DateTimeOffset.UtcNow;

        if (plannedTeleport is { } target)
        {
            teleporter.TeleportTo(target);
        }
    }

    private void BeginWithdrawal()
    {
        pathfinder.Stop();
        teleporter.Abort();
        targetId = null;
        recallAccepted = false;

        if (!recallEnabled() || CanBoardHere())
        {
            Phase = TravelPhase.Idle;
            return;
        }

        recallFrom = playerPosition();
        EnterPhase(TravelPhase.Withdrawing);
    }

    private void TryRecallNow()
    {
        if (confirmation.IsAwaitingAnswer)
        {
            recallAccepted = true;
            lastAttemptAt = DateTimeOffset.UtcNow;
            return;
        }

        if (recall.IsBusy)
        {
            recallAccepted = true;
            return;
        }

        if (DateTimeOffset.UtcNow - lastAttemptAt <= (recallAccepted ? RecallRetryInterval : RetryInterval))
        {
            return;
        }

        lastAttemptAt = DateTimeOffset.UtcNow;

        log.Information($"[recall] casting Return (accepted so far: {recallAccepted}).");

        recallAccepted |= recall.Cast() || recall.IsBusy;
    }

    private void GiveUpOnTeleport()
    {
        if (RemainingDistance() > MaximumWalkFallback)
        {
            pathfinder.Stop();
            Phase = TravelPhase.Idle;
            return;
        }

        BeginMounting();
    }

    private void BeginMounting()
    {
        mountedWalkToShard = null;

        if (!mount.IsEnabled || mount.IsMounted || RemainingDistance() < MountDistanceThreshold)
        {
            StartWalking();
            return;
        }

        EnterPhase(TravelPhase.Mounting);
    }

    private void StartWalking()
    {
        dismountDistance = DismountDistanceMin + ((float)random.NextDouble() * (DismountDistanceMax - DismountDistanceMin));
        pathfinder.MoveTo(finalDestination);
        EnterPhase(TravelPhase.Walking);
    }

    private void EnterPhase(TravelPhase phase)
    {
        log.Information(
            $"[travel] {Phase} -> {phase} | to destination {RemainingDistance():F0}y" +
            $" | to shard {(boardingShard is { } s ? DistanceTo(s).ToString("F0") : "n/a")}y" +
            $" | moving {pathfinder.IsMoving} | mounted {mount.IsMounted}");

        Phase = phase;
        phaseStartedAt = DateTimeOffset.UtcNow;

        lastAttemptAt = DateTimeOffset.MinValue;

        progressFrom = null;
    }

    private TimeSpan Elapsed => DateTimeOffset.UtcNow - phaseStartedAt;

    private bool Settled => Elapsed > PhaseSettleTime;

    private bool DueForRetry => DateTimeOffset.UtcNow - lastAttemptAt > RetryInterval;

    private bool Recalled
    {
        get
        {
            if (recallFrom is { } origin && playerPosition() is { } now &&
                Horizontal(origin, now) > RecallDisplacement)
            {
                return true;
            }

            return StandingAtTeleportPoint();
        }
    }

    private bool CanBoardHere() => NearestShardWithinWalk() != null;

    private bool StandingAtTeleportPoint()
    {
        if (playerPosition() is not { } here || NearestShardWithinWalk() is not { } shard)
        {
            return false;
        }

        return Horizontal(here, shard) <= AtTeleportPointRange;
    }

    private Vector3 BoardingDestination(Vector3 shard)
    {
        if (teleporter.BoardingPoint is { } actual)
        {
            return pathfinder.SnapToGround(actual);
        }

        var seeded = playerPosition() is { } here
            ? new Vector3(shard.X, here.Y, shard.Z)
            : shard;

        return pathfinder.SnapToGround(seeded);
    }

    private bool AtBoardingShard()
    {
        if (playerPosition() is not { } here)
        {
            return false;
        }

        var target = teleporter.BoardingPoint ?? boardingShard;
        return target is { } shard && Horizontal(here, shard) <= TeleportPointRange;
    }

    private bool IsStalled()
    {
        if (playerPosition() is not { } now)
        {
            return false;
        }

        if (progressFrom is not { } mark || Horizontal(mark, now) > StallDistance)
        {
            progressFrom = now;
            progressAt = DateTimeOffset.UtcNow;
            return false;
        }

        return DateTimeOffset.UtcNow - progressAt > StallTimeout;
    }

    private Vector3? NearestShardWithinWalk()
    {
        var from = playerPosition();
        var points = network();
        if (from == null || points == null)
        {
            return null;
        }

        Vector3? best = null;
        var bestDistance = MaximumWalkToTeleportPoint;

        foreach (var point in points.Points)
        {
            var distance = Horizontal(from.Value, point.Position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = point.Position;
            }
        }

        return best;
    }

    private float DistanceTo(Vector3 point) =>
        playerPosition() is { } here ? Horizontal(here, point) : float.NaN;

    private float RemainingDistance()
    {
        var from = playerPosition();
        return from == null ? float.MaxValue : Horizontal(from.Value, finalDestination);
    }

    private static float Horizontal(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    private Vector3 Dropzone(Vector3 centre, float? knownRadius)
    {
        if (playerPosition() is not { } from)
        {
            return centre;
        }

        var toPlayer = new Vector3(from.X - centre.X, 0f, from.Z - centre.Z);
        var distance = toPlayer.Length();
        if (distance < 0.01f)
        {
            return centre;
        }

        var entryDistance = MathF.Min(distance, knownRadius ?? DropzoneMaxOffset);
        var offset = (float)random.NextDouble() * entryDistance;

        return centre + (toPlayer / distance * offset);
    }
}
