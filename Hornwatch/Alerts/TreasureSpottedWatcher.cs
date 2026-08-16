using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Hornwatch.Core;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Treasure;
using LuminaMap = Lumina.Excel.Sheets.Map;

namespace Hornwatch.Alerts;

public sealed class TreasureSpottedWatcher(
    Func<ISpottedTreasureSource?> treasures,
    Func<uint> currentTerritory,
    Func<uint> currentMap,
    Func<TreasureAlertSettings> settings,
    ILocalizer localizer,
    IChatGui chat,
    IToastGui toasts,
    IDataManager data,
    MapFlagger flagger)
{
    private const ushort LinkForeground = 500;
    private const ushort LinkGlow = 501;
    private const ushort TagForeground = 45;

    private static readonly Dictionary<TreasureRarity, ushort> RarityForeground = new()
    {
        [TreasureRarity.Bronze] = 752,
        [TreasureRarity.Silver] = 2,
        [TreasureRarity.Gold] = 548,
    };

    private readonly Dictionary<ulong, DateTimeOffset> seen = new();

    public void Reset() => seen.Clear();

    public void Tick()
    {
        var options = settings();
        if (!options.AnyEnabled || treasures() is not { } source)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var treasure in source.Spotted)
        {
            var known = seen.ContainsKey(treasure.ObjectId);
            seen[treasure.ObjectId] = now;

            if (!known && options.Wants(treasure.Rarity))
            {
                Announce(treasure, options);
            }
        }

        Forget(now, TimeSpan.FromSeconds(Math.Max(1, options.ForgetAfterSeconds)));
    }

    private void Forget(DateTimeOffset now, TimeSpan after)
    {
        List<ulong>? stale = null;

        foreach (var (objectId, lastSeen) in seen)
        {
            if (now - lastSeen > after)
            {
                (stale ??= []).Add(objectId);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (var objectId in stale)
        {
            seen.Remove(objectId);
        }
    }

    private void Announce(SpottedTreasure treasure, TreasureAlertSettings options)
    {
        var message = Message(treasure, LinkTo(treasure));

        if (options.Toast)
        {
            toasts.ShowQuest(message);
        }

        if (options.MapFlag)
        {
            flagger.Mark(treasure.Position);
        }

        if (options.ChatMessage)
        {
            chat.Print(message);
        }
    }

    private SeString Rarity(TreasureRarity rarity) =>
        new SeStringBuilder()
            .AddUiForeground(RarityForeground[rarity])
            .AddText($"[{localizer.Get($"treasure.rarity.{rarity}")}]")
            .AddUiForegroundOff()
            .Build();

    private MapLinkPayload LinkTo(SpottedTreasure treasure)
    {
        var mapId = treasure.MapId != 0 ? treasure.MapId : currentMap();
        var onMap = data.GetExcelSheet<LuminaMap>()?.GetRowOrDefault(mapId);

        var readable = onMap is { } row
            ? MapUtil.WorldToMap(new Vector2(treasure.Position.X, treasure.Position.Z), row)
            : new Vector2(treasure.Position.X, treasure.Position.Z);

        return new MapLinkPayload(currentTerritory(), mapId, readable.X, readable.Y);
    }

    private SeString Message(SpottedTreasure treasure, MapLinkPayload link) =>
        new SeStringBuilder()
            .AddUiForeground(TagForeground)
            .AddText($"[{PluginMeta.Name} ({localizer.Get("treasure.tag")} ")
            .AddUiForegroundOff()
            .Append(Rarity(treasure.Rarity))
            .AddUiForeground(TagForeground)
            .AddText(")] ")
            .AddUiForegroundOff()
            .Add(link)
            .AddUiForeground(LinkForeground)
            .AddUiGlow(LinkGlow)
            .AddText($"{(char)SeIconChar.LinkMarker}{link.PlaceName}{link.CoordinateString}")
            .AddUiGlowOff()
            .AddUiForegroundOff()
            .Add(RawPayload.LinkTerminator)
            .Build();
}
