using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Localization;

namespace Hornwatch.Localization;

public sealed class JsonLocalizer : ILocalizer
{
    private const string DefaultLanguage = "en";

    private readonly string resourceDirectory;
    private readonly Func<string> overrideProvider;
    private readonly IClientState clientState;

    private Dictionary<string, string> strings = new();
    private Dictionary<string, string> fallback = new();
    private string loadedLanguage = string.Empty;

    public JsonLocalizer(string resourceDirectory, IClientState clientState, Func<string> overrideProvider)
    {
        this.resourceDirectory = resourceDirectory;
        this.clientState = clientState;
        this.overrideProvider = overrideProvider;

        fallback = Load(DefaultLanguage);
        Reload();
    }

    public string Language => loadedLanguage;

    public void Reload()
    {
        var wanted = Resolve();
        if (wanted == loadedLanguage)
        {
            return;
        }

        strings = wanted == DefaultLanguage ? fallback : Load(wanted);
        loadedLanguage = wanted;
    }

    public string Get(string key)
    {
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

    private Dictionary<string, string> Load(string language)
    {
        try
        {
            var path = Path.Combine(resourceDirectory, $"{language}.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
        }
        catch
        {
        }

        return new Dictionary<string, string>();
    }
}
