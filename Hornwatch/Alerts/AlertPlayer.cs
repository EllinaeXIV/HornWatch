using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Hornwatch.Core.Alerts;
using Hornwatch.Core.Localization;

namespace Hornwatch.Alerts;

public sealed class AlertPlayer
{
    private readonly Configuration configuration;
    private readonly ILocalizer localizer;
    private readonly IChatGui chat;
    private readonly Func<string?> activeModuleKey;
    private readonly Func<uint> currentTerritory;

    public AlertPlayer(
        Configuration configuration,
        ILocalizer localizer,
        IChatGui chat,
        Func<string?> activeModuleKey,
        Func<uint> currentTerritory)
    {
        this.configuration = configuration;
        this.localizer = localizer;
        this.chat = chat;
        this.activeModuleKey = activeModuleKey;
        this.currentTerritory = currentTerritory;
    }

    public void Handle(AlertEvent alert)
    {
        var moduleKey = activeModuleKey();
        if (moduleKey == null)
        {
            return;
        }

        var setting = configuration.For(moduleKey, currentTerritory(), alert.Encounter.Kind);
        if (!setting.Enabled)
        {
            return;
        }

        Play(setting.SoundId);

        if (setting.ChatMessage)
        {
            var kind = localizer.Get($"kind.{alert.Encounter.Kind}");
            chat.Print($"[{Core.PluginMeta.Name}] {kind} : {alert.Encounter.Name}");
        }
    }

    public static unsafe void Play(int soundId)
    {
        if (soundId is < 1 or > 16)
        {
            return;
        }

        UIGlobals.PlayChatSoundEffect((uint)soundId);
    }
}
