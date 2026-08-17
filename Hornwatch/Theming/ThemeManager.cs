using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Theming;

namespace Hornwatch.Theming;

public sealed class ThemeManager
{
    public const string DefaultKey = "modern";

    private readonly IDataManager data;
    private readonly IDataCache cache;
    private readonly Configuration configuration;

    public ThemeManager(IDataManager data, IDataCache cache, Configuration configuration)
    {
        this.data = data;
        this.cache = cache;
        this.configuration = configuration;

        if (!Knows(configuration.ThemeKey))
        {
            configuration.ThemeKey = DefaultKey;
            configuration.Save();
        }
    }

    private bool Knows(string key)
    {
        foreach (var option in Options)
        {
            if (option.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<(string Key, string DisplayName)> Options
    {
        get
        {
            var options = new List<(string, string)>();

            foreach (var theme in GamePalettes.GameThemes)
            {
                options.Add(theme);
            }

            options.Add((GamePalettes.Modern.Key, GamePalettes.Modern.DisplayName));
            return options;
        }
    }

    public UiPalette Current
    {
        get
        {
            var key = configuration.ThemeKey;

            if (key == GamePalettes.Modern.Key)
            {
                return GamePalettes.Modern;
            }

            return GamePalettes.FromGame(data, cache, key);
        }
    }

    public ThemeScope Push() => new(Current);
}
