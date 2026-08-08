using System;
using System.Collections.Generic;
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

    public void Save() => configuration.Save();
}
