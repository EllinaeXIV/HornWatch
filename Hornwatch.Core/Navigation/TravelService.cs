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

    void TravelTo(Vector3 destination, string? targetId = null, float? radius = null);

    void Stop();

    void Tick();
}

public sealed class TravelCoordinator : ITravelService
{
    private const float TeleportPointRange = 3.5f;

    private const float MaximumWalkToTeleportPoint = 120f;

    private const float MountDistanceThreshold = 100f;

    private const float DismountDistanceMin = 15f;

    private const float DismountDistanceMax = 20f;

    private static readonly TimeSpan TeleportTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan RecallTimeout = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan RecallRefusedTimeout = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan PhaseSettleTime = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1.5);

    private static readonly TimeSpan BoardingTimeout = TimeSpan.FromSeconds(12);

    private const float RecallDisplacement = 100f;

    private static readonly TimeSpan BoardingWalkTimeout = TimeSpan.FromSeconds(30);

    private const float MaximumWalkFallback = 250f;

    private const int MaximumBoardingAttempts = 3;

    private const float StallDistance = 3f;
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(8);

    private const float DropzoneMaxOffset = 20f;

    private static readonly TimeSpan MountTimeout = TimeSpan.FromSeconds(10);

    private readonly IPathfinder pathfinder;
    private readonly ITeleporter teleporter;
    private readonly IMountController mount;
    private readonly IRecall recall;
    private readonly IConfirmationDialog confirmation;
    private readonly Func<ITeleportNetwork?> network;
    private readonly Func<Vector3?> playerPosition;
    private readonly Func<uint> currentTerritory;
    private readonly Func<string, bool> targetIsActive;
    private readonly IPluginLog log;
    private readonly Func<bool> teleportEnabled;
    private readonly Func<bool> recallEnabled;
    private readonly Func<bool> travelEnabled;

    private Vector3 finalDestination;
    private uint startedInTerritory;
    private DateTimeOffset phaseStartedAt;
    private uint? plannedTeleport;
    private bool recallAccepted;
    private int boardingAttempts;
    private bool teleportAccepted;
    private string? targetId;
    private DateTimeOffset lastAttemptAt;
    private Vector3? recallFrom;
    private Vector3? boardingShard;
    private Vector3? progressFrom;
    private DateTimeOffset progressAt;
    private readonly Random random = new();
    private float dismountDistance;

    public TravelCoordinator(
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
        IPluginLog log)
    {
        this.log = log;
        this.pathfinder = pathfinder;
        this.teleporter = teleporter;
        this.mount = mount;
        this.recall = recall;
        this.confirmation = confirmation;
        this.network = network;
        this.playerPosition = playerPosition;
        this.currentTerritory = currentTerritory;
        this.targetIsActive = targetIsActive;
        this.teleportEnabled = teleportEnabled;
        this.recallEnabled = recallEnabled;
        this.travelEnabled = travelEnabled;
    }

    public TravelPhase Phase { get; private set; } = TravelPhase.Idle;

    public bool IsEnabled => travelEnabled();

    public bool IsAvailable => pathfinder.IsAvailable;

    public string? UnavailableReasonKey => pathfinder.UnavailableReasonKey;

    public void TravelTo(Vector3 destination, string? targetId = null, float? radius = null)
    {
        if (!IsAvailable)
        {
            return;
        }

        this.targetId = targetId;

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

                if (Elapsed > BoardingTimeout)
                {
                    GiveUpOnTeleport();
                    return;
                }

                if (DueForRetry)
                {
                    BeginTeleport();
                }

                return;

            case TravelPhase.Mounting:
                if (mount.IsMounted || Elapsed > MountTimeout)
                {
                    StartWalking();
                    return;
                }

                mount.Mount();
                return;

            case TravelPhase.Walking:
                if ((Settled && !pathfinder.IsMoving) || IsStalled())
                {
                    pathfinder.Stop();
                    Phase = TravelPhase.Idle;
                    return;
                }

                if (mount.IsMounted && RemainingDistance() <= dismountDistance)
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

        return nearest.PlaceNameId;
    }

    private void BeginBoarding()
    {
        boardingShard = NearestShardWithinWalk();

        if (boardingShard is { } shard)
        {
            if (Horizontal(playerPosition() ?? shard, shard) <= TeleportPointRange)
            {
                BeginTeleport();
                return;
            }

            boardingAttempts = 0;
            pathfinder.MoveTo(BoardingDestination(shard));
            EnterPhase(TravelPhase.WalkingToTeleport);
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

    private void BeginTeleport()
    {
        if (Phase != TravelPhase.Teleporting)
        {
            teleportAccepted = false;
            EnterPhase(TravelPhase.Teleporting);
        }

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
        if (!DueForRetry)
        {
            return;
        }

        lastAttemptAt = DateTimeOffset.UtcNow;

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

            return CanBoardHere();
        }
    }

    private bool CanBoardHere() => NearestShardWithinWalk() != null;

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
