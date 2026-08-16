namespace Hornwatch.Core.Treasure;

public interface ITreasureSurvey
{
    bool IsUsable { get; }

    string AbilityNameKey { get; }

    bool Sweep();
}
