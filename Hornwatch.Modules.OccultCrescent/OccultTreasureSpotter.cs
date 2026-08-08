using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultTreasureSpotter(IObjectTable objects, IPluginLog log) : ISpottedTreasureSource
{
    private static readonly Dictionary<uint, TreasureRarity> RarityByBaseId = new()
    {
        [2009530] = TreasureRarity.Gold,
        [2009531] = TreasureRarity.Silver,
        [2009532] = TreasureRarity.Bronze,
    };

    private readonly List<SpottedTreasure> spotted = [];
    private readonly HashSet<uint> reportedUnknown = [];

    public IReadOnlyList<SpottedTreasure> Spotted => spotted;

    public void Refresh()
    {
        spotted.Clear();

        foreach (var candidate in objects)
        {
            if (candidate.ObjectKind != ObjectKind.Treasure)
            {
                continue;
            }

            spotted.Add(new SpottedTreasure(candidate.GameObjectId, RarityOf(candidate.BaseId), candidate.Position));
        }
    }

    private TreasureRarity RarityOf(uint baseId)
    {
        if (RarityByBaseId.TryGetValue(baseId, out var known))
        {
            return known;
        }

        if (reportedUnknown.Add(baseId))
        {
            log.Information($"[treasure] chest base id {baseId} is not in the rarity table; announcing it as bronze.");
        }

        return TreasureRarity.Bronze;
    }
}
