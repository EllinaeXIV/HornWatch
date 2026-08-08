using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Treasure;
using LuminaTreasure = Lumina.Excel.Sheets.Treasure;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultTreasureSpotter(IObjectTable objects, IDataManager data, IPluginLog log) : ISpottedTreasureSource
{
    private static readonly Dictionary<uint, TreasureRarity> RarityByModel = new()
    {
        [1596] = TreasureRarity.Bronze,
        [1597] = TreasureRarity.Silver,
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
        var model = data.GetExcelSheet<LuminaTreasure>()?.GetRowOrDefault(baseId)?.SGB.RowId ?? 0;

        if (RarityByModel.TryGetValue(model, out var known))
        {
            return known;
        }

        if (reportedUnknown.Add(baseId))
        {
            log.Information(
                $"[treasure] chest {baseId} uses model {model}, which is not in the rarity table; calling it bronze.");
        }

        return TreasureRarity.Bronze;
    }
}
