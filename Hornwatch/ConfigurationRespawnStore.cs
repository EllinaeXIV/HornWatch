using System;
using System.Collections.Generic;
using Hornwatch.Core.Encounters;

namespace Hornwatch;

public sealed class ConfigurationRespawnStore : IRespawnStore
{
    private readonly Configuration configuration;

    public ConfigurationRespawnStore(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public Dictionary<string, RespawnEntry> Entries => configuration.PotForecasts;

    public uint CalibratedTerritoryId
    {
        get => configuration.PotForecastTerritory;
        set => configuration.PotForecastTerritory = value;
    }

    public void Save() => configuration.Save();
}
