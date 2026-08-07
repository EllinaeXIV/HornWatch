using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Hornwatch.Core.Navigation;
using Lumina.Excel.Sheets;

namespace Hornwatch.Navigation;

public sealed class ShardTeleporter : ITeleporter
{
    private const float InteractRange = 10f;

    private const float DetectRange = 150f;

    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2.5);

    private enum Step
    {
        Idle,

        Opening,

        Choosing,
    }

    private readonly IObjectTable objects;
    private readonly ITargetManager targets;
    private readonly IGameGui gameGui;
    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly Func<Vector3?> playerPosition;

    private Step step = Step.Idle;
    private string destination = string.Empty;
    private DateTimeOffset stepStartedAt;
    private DateTimeOffset lastAttemptAt;
    private bool chosen;
    private Dalamud.Game.ClientState.Objects.Types.IGameObject? previousTarget;

    public ShardTeleporter(
        IObjectTable objects,
        ITargetManager targets,
        IGameGui gameGui,
        IDataManager data,
        IPluginLog log,
        Func<Vector3?> playerPosition)
    {
        this.objects = objects;
        this.targets = targets;
        this.gameGui = gameGui;
        this.data = data;
        this.log = log;
        this.playerPosition = playerPosition;
    }

    public bool IsAvailable => true;

    public string? UnavailableReasonKey => null;

    public bool IsBusy => step != Step.Idle;

    public Vector3? BoardingPoint => NearestShard(DetectRange)?.Position;

    public bool TeleportTo(uint placeNameId)
    {
        destination = PlaceName(placeNameId);
        if (string.IsNullOrEmpty(destination))
        {
            log.Warning($"No PlaceName row {placeNameId}; cannot name a teleport destination.");
            return false;
        }

        if (NearestShard() is not { } shard)
        {
            log.Information($"[shard] none within {InteractRange}y of the player; cannot board here.");
            return false;
        }

        log.Information($"[shard] '{shard.Name}' at {Vector3.Distance(playerPosition() ?? shard.Position, shard.Position):F1}y, destination '{destination}'.");
        previousTarget = targets.Target;
        Enter(Step.Opening);
        return true;
    }

    public void Tick()
    {
        if (step == Step.Idle)
        {
            return;
        }

        if (DateTimeOffset.UtcNow - stepStartedAt > StepTimeout)
        {
            log.Information($"Aethernet {step} timed out.");
            Abort();
            return;
        }

        switch (step)
        {
            case Step.Opening:
                if (MenuIsOpen())
                {
                    Enter(Step.Choosing);
                    return;
                }

                if (DateTimeOffset.UtcNow - lastAttemptAt > RetryInterval)
                {
                    lastAttemptAt = DateTimeOffset.UtcNow;
                    Interact();
                }

                return;

            case Step.Choosing:
                if (!MenuIsOpen())
                {
                    step = Step.Idle;
                    RestoreTarget();
                    return;
                }

                if (!chosen)
                {
                    chosen = true;
                    Choose();
                }

                return;
        }
    }

    public void Abort()
    {
        step = Step.Idle;
        RestoreTarget();
    }

    private void RestoreTarget()
    {
        targets.Target = previousTarget;
        previousTarget = null;
    }

    private unsafe void Interact()
    {
        if (NearestShard() is not { } shard)
        {
            return;
        }

        targets.Target = shard;

        var system = TargetSystem.Instance();
        if (system == null)
        {
            return;
        }

        system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)shard.Address, false);
    }

    private unsafe void Choose()
    {
        var addon = (AddonSelectString*)gameGui.GetAddonByName("SelectString").Address;
        if (addon == null)
        {
            return;
        }

        ref var menu = ref addon->PopupMenu.PopupMenu;

        var index = FindEntry(ref menu, exact: true);
        if (index < 0)
        {
            index = FindEntry(ref menu, exact: false);
        }

        if (index < 0)
        {
            log.Information($"[shard] '{destination}' is not on the menu ({menu.EntryCount} entries).");
            return;
        }

        log.Information($"[shard] choosing entry {index} for '{destination}'.");

        var choice = stackalloc AtkValue[1];
        choice->Type = AtkValueType.Int;
        choice->Int = index;
        addon->AtkUnitBase.FireCallback(1, choice);
    }

    private unsafe int FindEntry(ref PopupMenu menu, bool exact)
    {
        for (var i = 0; i < menu.EntryCount; i++)
        {
            var entry = menu.EntryNames[i];
            if (entry.Value == null)
            {
                continue;
            }

            var text = entry.Value->ToString();
            var hit = exact
                ? string.Equals(text, destination, StringComparison.OrdinalIgnoreCase)
                : text.Contains(destination, StringComparison.OrdinalIgnoreCase);

            if (hit)
            {
                return i;
            }
        }

        return -1;
    }

    private unsafe bool MenuIsOpen()
    {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName("SelectString").Address;
        return addon != null && addon->IsVisible && addon->UldManager.LoadedState == AtkLoadState.Loaded;
    }

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? NearestShard(float within = InteractRange)
    {
        if (playerPosition() is not { } here)
        {
            return null;
        }

        Dalamud.Game.ClientState.Objects.Types.IGameObject? best = null;
        var bestDistance = within;

        foreach (var candidate in objects)
        {
            if (candidate.ObjectKind != ObjectKind.Aetheryte)
            {
                continue;
            }

            var distance = Vector3.Distance(here, candidate.Position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private string PlaceName(uint placeNameId) =>
        data.GetExcelSheet<PlaceName>()?.GetRowOrDefault(placeNameId)?.Name.ExtractText() ?? string.Empty;

    private void Enter(Step next)
    {
        step = next;
        chosen = false;
        stepStartedAt = DateTimeOffset.UtcNow;
        lastAttemptAt = DateTimeOffset.MinValue;
    }
}
