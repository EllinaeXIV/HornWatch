using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Theming;

namespace Hornwatch.Theming;

public sealed class ThemeManager
{
    public const string FollowGameKey = "auto";

    private readonly IDataManager data;
    private readonly IGameConfig gameConfig;
    private readonly IDataCache cache;
    private readonly Configuration configuration;

    public ThemeManager(IDataManager data, IGameConfig gameConfig, IDataCache cache, Configuration configuration)
    {
        this.data = data;
        this.gameConfig = gameConfig;
        this.cache = cache;
        this.configuration = configuration;
    }

    public IReadOnlyList<(string Key, string DisplayName)> Options
    {
        get
        {
            var options = new List<(string, string)> { (FollowGameKey, "Follow the game") };
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

            if (key == FollowGameKey)
            {
                key = GamePalettes.FollowGameKey(gameConfig) ?? "dark";
            }

            if (key == GamePalettes.Modern.Key)
            {
                return GamePalettes.Modern;
            }

            return GamePalettes.FromGame(data, cache, key);
        }
    }

    public ThemeScope Push() => new(Current);
}
