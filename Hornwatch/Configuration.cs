using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Hornwatch.Core.Encounters;
using Hornwatch.Core.Treasure;
using Hornwatch.Theming;

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

    public string ThemeKey { get; set; } = ThemeManager.DefaultKey;

    public bool AutoTravelEnabled { get; set; }

    public bool AutoTravelRiskAcknowledged { get; set; }

    public bool UseMount { get; set; } = true;

    public uint MountId { get; set; }

    public bool UseTeleport { get; set; } = true;

    public bool UseReturn { get; set; } = true;

    public bool ShowRouteOverlay { get; set; }

    public bool ShowPotBarEntry { get; set; } = true;

    public bool OpenOnZoneEntry { get; set; }

    public bool KeepOpenOnEscape { get; set; }

    public Dictionary<uint, TreasureZoneSettings> TreasureByTerritory { get; set; } = new();

    public bool ShowTreasureToolbar { get; set; } = true;

    public TreasureAlertSettings TreasureAlerts { get; set; } = new();

    public TreasureRouteOptions TreasureRoute { get; set; } = new();

    public HashSet<TreasureKind> ShownTreasureMarkers { get; set; } =
        [TreasureKind.BronzeCoffer, TreasureKind.SilverCoffer];

    public TreasureZoneSettings TreasureFor(uint territoryId)
    {
        if (TreasureByTerritory.TryGetValue(territoryId, out var existing))
        {
            return existing;
        }

        var created = new TreasureZoneSettings
        {
            ShownMarkers = [.. ShownTreasureMarkers],
            ShowToolbar = ShowTreasureToolbar,
            Alerts = new TreasureAlertSettings
            {
                Toast = TreasureAlerts.Toast,
                ChatMessage = TreasureAlerts.ChatMessage,
                MapFlag = TreasureAlerts.MapFlag,
                ForgetAfterSeconds = TreasureAlerts.ForgetAfterSeconds,
                Rarities = new Dictionary<TreasureRarity, bool>(TreasureAlerts.Rarities),
            },
            Route = TreasureRoute,
        };

        TreasureByTerritory[territoryId] = created;
        return created;
    }

    public Dictionary<string, AlertSetting> Alerts { get; set; } = new();

    public Dictionary<string, RespawnEntry> PotForecasts { get; set; } = new();

    public uint PotForecastTerritory { get; set; }

    public SessionWitness? PotForecastWitness { get; set; }

    public Dictionary<uint, Vector3> PotPositions { get; set; } = new();

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
