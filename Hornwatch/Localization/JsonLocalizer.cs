using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Localization;

namespace Hornwatch.Localization;

public sealed class JsonLocalizer(
    string resourceDirectory, IClientState clientState, Func<string> overrideProvider, IPluginLog log)
    : ILocalizer
{
    private const string DefaultLanguage = "en";

    private static readonly TimeSpan BetweenDiskChecks = TimeSpan.FromSeconds(1);

    private Dictionary<string, string> strings = [];
    private Dictionary<string, string> fallback = [];
    private string loadedLanguage = string.Empty;
    private DateTime loadedAt;
    private DateTimeOffset checkedAt = DateTimeOffset.MinValue;

    public string Language => loadedLanguage;

    public void Reload()
    {
        var wanted = Resolve();

        if (wanted == loadedLanguage && DateTimeOffset.UtcNow - checkedAt < BetweenDiskChecks)
        {
            return;
        }

        checkedAt = DateTimeOffset.UtcNow;

        var writtenAt = LastWrite(wanted);

        if (wanted == loadedLanguage && writtenAt == loadedAt)
        {
            return;
        }

        var english = Load(DefaultLanguage) ?? fallback;

        fallback = english;
        strings = wanted == DefaultLanguage ? english : Load(wanted) ?? strings;
        loadedLanguage = wanted;
        loadedAt = writtenAt;
    }

    private DateTime LastWrite(string language)
    {
        var current = WrittenAt(language);
        var english = WrittenAt(DefaultLanguage);

        return current > english ? current : english;
    }

    private DateTime WrittenAt(string language)
    {
        var path = Path.Combine(resourceDirectory, $"{language}.json");

        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
    }

    public string Get(string key)
    {
        if (loadedLanguage.Length == 0)
        {
            Reload();
        }

        if (strings.TryGetValue(key, out var value))
        {
            return value;
        }

        return fallback.TryGetValue(key, out var english) ? english : key;
    }

    public string Format(string key, params object[] args)
    {
        try
        {
            return string.Format(Get(key), args);
        }
        catch (FormatException)
        {
            return Get(key);
        }
    }

    private string Resolve()
    {
        var manual = overrideProvider();
        if (!string.IsNullOrEmpty(manual))
        {
            return manual;
        }

        return clientState.ClientLanguage == ClientLanguage.French ? "fr" : DefaultLanguage;
    }

    private Dictionary<string, string>? Load(string language)
    {
        var path = Path.Combine(resourceDirectory, $"{language}.json");

        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }

            log.Warning($"{path} is missing; keeping the strings already loaded.");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"{path} could not be read; keeping the strings already loaded.");
        }

        return null;
    }
}
