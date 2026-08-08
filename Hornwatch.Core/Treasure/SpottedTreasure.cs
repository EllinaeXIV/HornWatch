using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Core.Treasure;

public enum TreasureRarity
{
    Bronze,

    Silver,

    Gold,
}

public sealed record SpottedTreasure(ulong ObjectId, TreasureRarity Rarity, Vector3 Position);

public interface ISpottedTreasureSource
{
    IReadOnlyList<SpottedTreasure> Spotted { get; }
}
