using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Hornwatch.Core.Encounters;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultEncounterSource(IFateTable fates, PotCatalog pots, TowerCatalog towers, Func<uint> currentTerritory) : IEncounterSource
{
    private static readonly ushort[] ForkedTowerEventIds = { 48, 64, 65 };

    private readonly List<TrackedEncounter> active = new();

    public IReadOnlyList<TrackedEncounter> Active => active;

    public void Refresh()
    {
        active.Clear();
        CollectCriticalEncounters();
        CollectFates();
    }

    private unsafe void CollectCriticalEncounters()
    {
        var container = DynamicEventContainer.GetInstance();
        if (container == null)
        {
            return;
        }

        var events = container->Events;
        for (var i = 0; i < events.Length; i++)
        {
            ref var slot = ref events[i];
            if (slot.State == DynamicEventState.Inactive)
            {
                continue;
            }

            var phase = slot.State switch
            {
                DynamicEventState.Register => EncounterPhase.Announced,
                DynamicEventState.Warmup => EncounterPhase.Preparing,
                DynamicEventState.Battle => EncounterPhase.Running,
                _ => EncounterPhase.Running,
            };

            var name = slot.Name.ToString();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var isTower = Array.IndexOf(ForkedTowerEventIds, slot.DynamicEventId) >= 0;

            active.Add(new TrackedEncounter
            {
                Id = $"ce:{slot.DynamicEventId}",
                SourceId = slot.DynamicEventId,
                Kind = isTower ? EncounterKind.Raid : EncounterKind.CriticalEncounter,
                Name = name,
                Phase = phase,
                TimeRemaining = TimeSpan.FromSeconds(slot.SecondsLeft),
                Progress = slot.Progress / 100f,
                Participants = slot.Participants,
                MaxParticipants = slot.MaxParticipants,
                Position = ReadMarkerPosition(slot.MapMarker) ?? (isTower ? towers.PositionIn(currentTerritory()) : null),
            });
        }
    }

    private static Vector3? ReadMarkerPosition(MapMarkerData marker)
    {
        var position = marker.Position;
        if (position == Vector3.Zero)
        {
            return null;
        }

        return position;
    }

    private void CollectFates()
    {
        foreach (var fate in fates)
        {
            if (fate.State is FateState.Ended or FateState.Failed)
            {
                continue;
            }

            var pot = pots.Find(fate.FateId);

            if (pot != null && fate.Position != Vector3.Zero)
            {
                pots.Observe(pot.FateId, fate.Position);
            }

            var phase = fate.State switch
            {
                FateState.Preparing => EncounterPhase.Preparing,
                FateState.Ending => EncounterPhase.Ending,
                _ => EncounterPhase.Running,
            };

            active.Add(new TrackedEncounter
            {
                Id = $"fate:{fate.FateId}",
                SourceId = fate.FateId,
                Kind = pot != null ? EncounterKind.NotableFate : EncounterKind.Fate,
                Name = fate.Name.TextValue,
                LabelKey = pot != null ? PotCatalog.LabelKeyFor(pot.Side) : null,
                Phase = phase,
                TimeRemaining = TimeSpan.FromSeconds(Math.Max(0, fate.TimeRemaining)),
                StartedAt = fate.StartTimeEpoch > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(fate.StartTimeEpoch)
                    : null,
                Progress = fate.Progress / 100f,
                Position = fate.Position,
                Radius = fate.Radius,
            });
        }
    }
}
