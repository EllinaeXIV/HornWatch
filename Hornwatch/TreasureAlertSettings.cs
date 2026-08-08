using System;
using System.Collections.Generic;
using Hornwatch.Core.Treasure;

namespace Hornwatch;

[Serializable]
public sealed class TreasureAlertSettings
{
    public bool Toast { get; set; } = true;

    public bool ChatMessage { get; set; } = true;

    public bool MapFlag { get; set; } = true;

    public int ForgetAfterSeconds { get; set; } = 180;

    public Dictionary<TreasureRarity, bool> Rarities { get; set; } = new()
    {
        [TreasureRarity.Bronze] = true,
        [TreasureRarity.Silver] = true,
        [TreasureRarity.Gold] = true,
    };

    public bool AnyEnabled
    {
        get
        {
            if (!Toast && !ChatMessage && !MapFlag)
            {
                return false;
            }

            foreach (var wanted in Rarities.Values)
            {
                if (wanted)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool Wants(TreasureRarity rarity) => Rarities.TryGetValue(rarity, out var wanted) && wanted;

    public void Set(TreasureRarity rarity, bool wanted) => Rarities[rarity] = wanted;
}
