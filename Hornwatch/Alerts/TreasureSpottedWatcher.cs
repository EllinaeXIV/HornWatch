using System;
using System.Collections.Generic;
using System.Numerics;
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
        var found = localizer.Format("treasure.tag", localizer.Get($"treasure.rarity.{treasure.Rarity}"));

        if (options.Toast)
        {
            toasts.ShowQuest(found);
        }

        if (options.MapFlag)
        {
            flagger.Mark(treasure.Position);
        }

        if (options.ChatMessage)
        {
            chat.Print(Message(treasure, found));
        }
    }

    private SeString Message(SpottedTreasure treasure, string found)
    {
        var mapId = currentMap();
        var onMap = data.GetExcelSheet<LuminaMap>()?.GetRowOrDefault(mapId);

        // MapLinkPayload's float constructor wants the coordinates a player reads off the screen,
        // not world space. Handing it world space put the link a whole map away from the coffer.
        var readable = onMap is { } row
            ? MapUtil.WorldToMap(new Vector2(treasure.Position.X, treasure.Position.Z), row)
            : new Vector2(treasure.Position.X, treasure.Position.Z);

        var link = new MapLinkPayload(currentTerritory(), mapId, readable.X, readable.Y);

        return new SeStringBuilder()
            .AddUiForeground(45)
            .AddText($"[{PluginMeta.Name} ({found})] ")
            .AddUiForegroundOff()
            .Add(link)
            .AddUiForeground(LinkForeground)
            .AddUiGlow(LinkGlow)
            .AddText($"{link.PlaceName}{link.CoordinateString}")
            .AddUiGlowOff()
            .AddUiForegroundOff()
            .Add(RawPayload.LinkTerminator)
            .Build();
    }
}
