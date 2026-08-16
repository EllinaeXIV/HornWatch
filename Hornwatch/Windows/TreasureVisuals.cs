using System.Collections.Generic;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Windows;

public static class TreasureVisuals
{
    public static readonly TreasureKind[] Order =
    [
        TreasureKind.BronzeCoffer,
        TreasureKind.SilverCoffer,
        TreasureKind.PotNorth,
        TreasureKind.PotSouth,
        TreasureKind.SecondChance,
        TreasureKind.Bunny,
        TreasureKind.Survey,
    ];

    public static readonly TreasureRarity[] Rarities =
    [
        TreasureRarity.Bronze,
        TreasureRarity.Silver,
        TreasureRarity.Gold,
    ];

    private static readonly Dictionary<TreasureKind, uint> KindIcons = new()
    {
        [TreasureKind.BronzeCoffer] = 60356,
        [TreasureKind.SilverCoffer] = 60355,
        [TreasureKind.PotNorth] = 60354,
        [TreasureKind.PotSouth] = 60354,
        [TreasureKind.SecondChance] = 61473,
        [TreasureKind.Bunny] = 25207,
        [TreasureKind.Survey] = 60357,
    };

    private static readonly Dictionary<TreasureRarity, uint> RarityIcons = new()
    {
        [TreasureRarity.Bronze] = 60356,
        [TreasureRarity.Silver] = 60355,
        [TreasureRarity.Gold] = 60354,
    };

    public static uint IconOf(TreasureKind kind) => KindIcons.GetValueOrDefault(kind);

    public static uint IconOf(TreasureRarity rarity) => RarityIcons.GetValueOrDefault(rarity);
}
