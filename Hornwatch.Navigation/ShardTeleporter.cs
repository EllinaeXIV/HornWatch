using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Hornwatch.Core.Navigation;
using Lumina.Excel.Sheets;

namespace Hornwatch.Navigation;

public sealed class ShardTeleporter(IObjectTable objects, ITargetManager targets, IGameGui gameGui, IDataManager data, IConfirmationDialog confirmation, IPluginLog log, Func<Vector3?> playerPosition) : ITeleporter
{
    private const float InteractRange = 10f;

    private const float DetectRange = 150f;

    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2.5);

    private static readonly TimeSpan ConfirmationWindow = TimeSpan.FromSeconds(2);

    private enum Step
    {
        Idle,

        Opening,

        Choosing,

        Confirming,
    }

    private Step step = Step.Idle;
    private string destination = string.Empty;
    private DateTimeOffset stepStartedAt;
    private DateTimeOffset lastAttemptAt;
    private bool chosen;
    private Dalamud.Game.ClientState.Objects.Types.IGameObject? previousTarget;

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
            log.Information(
                $"[shard] no ObjectKind.Aetheryte within {InteractRange}y. Nearby instead: {DescribeNearby()}");
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
                    Enter(Step.Confirming);
                    return;
                }

                if (!chosen)
                {
                    chosen = true;

                    if (!Choose())
                    {
                        Cancel();
                        Abort();
                    }
                }

                return;

            case Step.Confirming:
                confirmation.Accept();

                if (DateTimeOffset.UtcNow - stepStartedAt > ConfirmationWindow)
                {
                    step = Step.Idle;
                    RestoreTarget();
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

    private unsafe bool Choose()
    {
        var addon = (AddonSelectString*)gameGui.GetAddonByName("SelectString").Address;
        if (addon == null)
        {
            return false;
        }

        ref var menu = ref addon->PopupMenu.PopupMenu;

        var index = FindEntry(ref menu, exact: true);
        if (index < 0)
        {
            index = FindEntry(ref menu, exact: false);
        }

        if (index < 0)
        {
            log.Warning(
                $"[shard] '{destination}' is not on the menu. Entries: {DescribeEntries(ref menu)}");
            return false;
        }

        log.Information($"[shard] choosing entry {index} for '{destination}'.");

        Answer(addon, index);
        return true;
    }

    private unsafe void Cancel()
    {
        var addon = (AddonSelectString*)gameGui.GetAddonByName("SelectString").Address;
        if (addon != null)
        {
            Answer(addon, -1);
        }
    }

    private static unsafe void Answer(AddonSelectString* addon, int index)
    {
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

    private string DescribeNearby()
    {
        if (playerPosition() is not { } here)
        {
            return "player position unknown";
        }

        var nearby = new List<(float Distance, string Text)>();
        foreach (var candidate in objects)
        {
            var distance = Vector3.Distance(here, candidate.Position);
            if (distance <= InteractRange * 3f)
            {
                nearby.Add((distance, $"{candidate.ObjectKind}:'{candidate.Name}'@{distance:F0}y"));
            }
        }

        if (nearby.Count == 0)
        {
            return "nothing";
        }

        nearby.Sort((left, right) => left.Distance.CompareTo(right.Distance));
        return string.Join(", ", nearby.ConvertAll(entry => entry.Text).GetRange(0, Math.Min(8, nearby.Count)));
    }

    private unsafe string DescribeEntries(ref PopupMenu menu)
    {
        var entries = new List<string>();
        for (var i = 0; i < menu.EntryCount; i++)
        {
            var entry = menu.EntryNames[i];
            entries.Add(entry.Value == null ? $"[{i}]<null>" : $"[{i}]'{entry.Value->ToString()}'");
        }

        return entries.Count == 0 ? "none" : string.Join(", ", entries);
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
