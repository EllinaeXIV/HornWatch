using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Hornwatch.Core;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Alerts;

public sealed class TreasureSpottedWatcher(
    Func<ISpottedTreasureSource?> treasures,
    Func<Vector3?> playerPosition,
    Func<uint> currentTerritory,
    Func<uint> currentMap,
    Func<TreasureAlertSettings> settings,
    ILocalizer localizer,
    IChatGui chat,
    IToastGui toasts,
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
        var name = localizer.Get($"treasure.rarity.{treasure.Rarity}");
        var distance = playerPosition() is { } here ? (int)Vector3.Distance(here, treasure.Position) : 0;

        if (options.Toast)
        {
            toasts.ShowQuest(localizer.Format("treasure.foundToast", name, distance));
        }

        if (options.MapFlag)
        {
            flagger.Mark(treasure.Position);
        }

        if (options.ChatMessage)
        {
            chat.Print(Message(treasure, name, distance));
        }
    }

    private SeString Message(SpottedTreasure treasure, string name, int distance)
    {
        var link = new MapLinkPayload(
            currentTerritory(), currentMap(), treasure.Position.X, treasure.Position.Z);

        return new SeStringBuilder()
            .AddUiForeground(45)
            .AddText($"[{PluginMeta.Name}] ")
            .AddUiForegroundOff()
            .AddText(localizer.Format("treasure.foundChat", name, distance))
            .AddText(" ")
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
