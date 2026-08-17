using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using LuminaUIColor = Lumina.Excel.Sheets.UIColor;

namespace Hornwatch.Core.Theming;

public static class GamePalettes
{
    private const uint RowText = 1;
    private const uint RowTextMuted = 3;
    private const uint RowTextDisabled = 5;
    private const uint RowWindowBg = 7;
    private const uint RowFrameBg = 6;
    private const uint RowAccent = 8;

    public static readonly (string Key, string DisplayName)[] GameThemes =
    [
        ("dark", "Dark"),
        ("light", "Light"),
        ("classic", "Classic FF"),
        ("clearwhite", "Clear White"),
    ];

    public static readonly UiPalette Modern = new()
    {
        Key = "modern",
        DisplayName = "Modern",
        Text = new Vector4(0.88f, 0.90f, 0.87f, 1f),
        TextMuted = new Vector4(0.62f, 0.66f, 0.63f, 1f),
        TextDisabled = new Vector4(0.42f, 0.46f, 0.44f, 1f),
        WindowBg = new Vector4(0.07f, 0.08f, 0.08f, 0.96f),
        FrameBg = new Vector4(0.12f, 0.14f, 0.13f, 1f),
        Border = new Vector4(0.24f, 0.27f, 0.26f, 1f),
        Accent = new Vector4(0.31f, 0.70f, 0.64f, 1f),
        Rounding = 5f,
    };

    public static UiPalette FromGame(IDataManager data, IDataCache cache, string key)
    {
        return cache.GetOrCreate($"theme.{key}", () => Build(data, key) ?? Modern);
    }

    private static UiPalette? Build(IDataManager data, string key)
    {
        var sheet = data.GetExcelSheet<LuminaUIColor>();
        if (sheet == null)
        {
            return null;
        }

        var display = Array.Find(GameThemes, t => t.Key == key).DisplayName;
        if (display == null)
        {
            return null;
        }

        Vector4 Read(uint row)
        {
            return sheet.TryGetRow(row, out var value) ? ToVector(Select(value, key)) : Vector4.One;
        }

        var windowBg = Read(RowWindowBg);

        return new UiPalette
        {
            Key = key,
            DisplayName = display,
            Text = Read(RowText),
            TextMuted = Read(RowTextMuted),
            TextDisabled = Read(RowTextDisabled),

            WindowBg = windowBg with { W = 0.94f },
            FrameBg = Read(RowFrameBg) with { W = 0.85f },
            Border = Read(RowTextDisabled) with { W = 0.55f },
            Accent = Read(RowAccent),

            Rounding = 0f,
        };
    }

    private static uint Select(LuminaUIColor row, string key) => key switch
    {
        "dark" => row.Dark,
        "light" => row.Light,
        "classic" => row.ClassicFF,
        "clearblue" => row.ClearBlue,
        "clearwhite" => row.ClearWhite,
        "cleargreen" => row.ClearGreen,
        _ => row.Dark,
    };

    private static Vector4 ToVector(uint packed)
    {
        return new Vector4(
            ((packed >> 24) & 0xFF) / 255f,
            ((packed >> 16) & 0xFF) / 255f,
            ((packed >> 8) & 0xFF) / 255f,
            (packed & 0xFF) / 255f);
    }
}
