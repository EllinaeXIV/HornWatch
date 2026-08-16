using System;
using System.Collections.Generic;
using System.Numerics;
using Hornwatch.Core.Encounters;

namespace Hornwatch;

public sealed class ConfigurationRespawnStore(Configuration configuration) : IRespawnStore
{

    public Dictionary<string, RespawnEntry> Entries => configuration.PotForecasts;

    public uint CalibratedTerritoryId
    {
        get => configuration.PotForecastTerritory;
        set => configuration.PotForecastTerritory = value;
    }

    public SessionWitness? Witness
    {
        get => configuration.PotForecastWitness;
        set => configuration.PotForecastWitness = value;
    }

    public Dictionary<uint, Vector3> SeenAt => configuration.PotPositions;

    public void Save() => configuration.Save();
}
