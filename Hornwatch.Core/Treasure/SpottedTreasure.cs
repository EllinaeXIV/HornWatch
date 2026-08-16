using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Core.Treasure;

public enum TreasureRarity
{
    Bronze,

    Silver,

    Gold,
}

public sealed record SpottedTreasure(
    ulong ObjectId, TreasureRarity Rarity, Vector3 Position, uint MapId, bool IsUnderground)
{
    public TreasureKind Kind =>
        Rarity == TreasureRarity.Silver ? TreasureKind.SilverCoffer : TreasureKind.BronzeCoffer;

    public TreasurePoint AsWaypoint() => new(Kind, Position, MapId, IsUnderground);
}

public interface ISpottedTreasureSource
{
    IReadOnlyList<SpottedTreasure> Spotted { get; }
}
