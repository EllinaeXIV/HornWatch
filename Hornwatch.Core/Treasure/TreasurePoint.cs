using System.Collections.Generic;
using System.Numerics;

namespace Hornwatch.Core.Treasure;

public enum TreasureKind
{
    BronzeCoffer,

    SilverCoffer,

    PotNorth,

    PotSouth,

    SecondChance,

    Bunny,

    Survey,
}

public sealed record TreasurePoint(TreasureKind Kind, Vector3 Position, uint MapId, bool IsUnderground)
{
    public bool IsCoffer => Kind is TreasureKind.BronzeCoffer or TreasureKind.SilverCoffer;
}

public interface ITreasureSource
{
    IReadOnlyList<TreasurePoint> PointsIn(uint territoryId);
}
