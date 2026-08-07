using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Hornwatch.Core.Encounters;

namespace Hornwatch;

[Serializable]
public class AlertSetting
{
    public bool Enabled { get; set; } = true;

    public int SoundId { get; set; } = 1;

    public bool ChatMessage { get; set; } = true;
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string LanguageOverride { get; set; } = string.Empty;

    public bool DeveloperMode { get; set; }

    public string ThemeKey { get; set; } = "auto";

    public bool AutoTravelEnabled { get; set; }

    public bool AutoTravelRiskAcknowledged { get; set; }

    public bool UseMount { get; set; } = true;

    public uint MountId { get; set; }

    public bool UseTeleport { get; set; } = true;

    public bool UseReturn { get; set; } = true;

    public Dictionary<string, AlertSetting> Alerts { get; set; } = new();

    public Dictionary<string, RespawnEntry> PotForecasts { get; set; } = new();

    public uint PotForecastTerritory { get; set; }

    public AlertSetting For(string moduleKey, uint territoryId, EncounterKind kind)
    {
        var key = $"{moduleKey}:{territoryId}:{kind}";
        if (Alerts.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = kind switch
        {
            EncounterKind.CriticalEncounter => new AlertSetting { SoundId = 6 },
            EncounterKind.NotableFate => new AlertSetting { SoundId = 4 },
            EncounterKind.Raid => new AlertSetting { SoundId = 10 },
            _ => new AlertSetting { SoundId = 1 },
        };

        Alerts[key] = created;
        return created;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
