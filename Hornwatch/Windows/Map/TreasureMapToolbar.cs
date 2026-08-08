using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Treasure;
using KamiToolKit.Classes;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Hornwatch.Windows.Map;

public sealed unsafe class TreasureMapToolbar : IDisposable
{
    private static readonly TreasureKind[] Order =
    [
        TreasureKind.BronzeCoffer,
        TreasureKind.SilverCoffer,
        TreasureKind.PotNorth,
        TreasureKind.PotSouth,
        TreasureKind.SecondChance,
        TreasureKind.Bunny,
        TreasureKind.Survey,
    ];

    private static readonly Dictionary<TreasureKind, uint> Icons = new()
    {
        [TreasureKind.BronzeCoffer] = 60356,
        [TreasureKind.SilverCoffer] = 60355,
        [TreasureKind.PotNorth] = 60354,
        [TreasureKind.PotSouth] = 60354,
        [TreasureKind.SecondChance] = 61473,
        [TreasureKind.Bunny] = 25207,
        [TreasureKind.Survey] = 60357,
    };

    private const uint HandleIcon = 60442;

    private const float ButtonSize = 44f;

    private const float Margin = 4f;

    private const float EdgeInset = 14f;

    private readonly AddonController<AddonAreaMap> controller;

    private readonly IFramework framework;
    private readonly ILocalizer localizer;
    private readonly Func<bool> overlayEnabled;
    private readonly Func<IReadOnlySet<TreasureKind>> shownKinds;
    private readonly Action<TreasureKind, bool> onToggle;
    private readonly IPluginLog log;

    private readonly Dictionary<TreasureKind, IconButtonNode> buttons = new();

    private IconButtonNode? handle;
    private VerticalListNode? row;
    private bool expanded = true;

    public TreasureMapToolbar(
        IFramework framework,
        ILocalizer localizer,
        Func<bool> overlayEnabled,
        Func<IReadOnlySet<TreasureKind>> shownKinds,
        Action<TreasureKind, bool> onToggle,
        IPluginLog log)
    {
        this.framework = framework;
        this.localizer = localizer;
        this.overlayEnabled = overlayEnabled;
        this.shownKinds = shownKinds;
        this.onToggle = onToggle;
        this.log = log;

        controller = new AddonController<AddonAreaMap>
        {
            AddonName = "AreaMap",
            OnSetup = Build,
            OnFinalize = Teardown,
        };

        framework.RunOnFrameworkThread(controller.Enable);
    }

    public void Dispose() => framework.RunOnFrameworkThread(() =>
    {
        controller.Dispose();
        Release();
    });

    public void Sync()
    {
        var visible = overlayEnabled();

        if (handle != null)
        {
            handle.IsVisible = visible;
        }

        if (row != null)
        {
            row.IsVisible = visible && expanded;
        }

        if (!visible)
        {
            return;
        }

        var kinds = shownKinds();

        foreach (var (kind, button) in buttons)
        {
            Paint(button, kinds.Contains(kind));
        }
    }

    private static void Paint(IconButtonNode button, bool active)
    {
        button.IsChecked = active;
        button.Alpha = active ? 1f : 0.75f;
        button.MultiplyColor = active ? Vector3.One : new Vector3(0.62f);
    }

    private unsafe void Build(AddonAreaMap* addon)
    {
        if (addon == null)
        {
            return;
        }

        try
        {
            var anchor = AnchorFrom(addon);

            row = new VerticalListNode
            {
                ItemSpacing = Margin,
                Size = new Vector2(ButtonSize, ((ButtonSize + Margin) * Order.Length) - Margin),
                Position = anchor + new Vector2(0f, ButtonSize + Margin),
                IsVisible = overlayEnabled() && expanded,
            };

            var kinds = shownKinds();

            foreach (var kind in Order)
            {
                var button = new IconButtonNode
                {
                    IconId = Icons[kind],
                    Size = new Vector2(ButtonSize),
                    IsVisible = true,
                    TextTooltip = localizer.Get($"treasure.kind.{kind}"),
                };

                Paint(button, kinds.Contains(kind));

                var captured = kind;
                button.OnClick = () => Toggle(captured);

                buttons[kind] = button;
                row.AddNode(button);
            }

            handle = new IconButtonNode
            {
                IconId = HandleIcon,
                Size = new Vector2(ButtonSize),
                Position = anchor,
                IsVisible = overlayEnabled(),
                IsChecked = expanded,
            };
            handle.OnClick = ToggleExpanded;

            handle.AttachNode(&addon->AtkUnitBase, NodePosition.AsLastChild);
            row.AttachNode(&addon->AtkUnitBase, NodePosition.AsLastChild);

            log.Information($"[toolbar] attached to the map with {buttons.Count} category buttons.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not attach the treasure toolbar to the map.");
            Release();
        }
    }

    private static unsafe Vector2 AnchorFrom(AddonAreaMap* addon)
    {
        var y = Margin;

        foreach (var dropdown in (AtkComponentDropDownList*[])[addon->RegionDropDownList, addon->TerritoryDropDownList])
        {
            if (dropdown == null || dropdown->AtkComponentBase.OwnerNode == null)
            {
                continue;
            }

            var node = &dropdown->AtkComponentBase.OwnerNode->AtkResNode;
            var bottom = node->Y + (node->Height * node->ScaleY);

            if (bottom > y)
            {
                y = bottom;
            }
        }

        if (y <= Margin && addon->TitleContainerNode != null)
        {
            y = addon->TitleContainerNode->Y + addon->TitleContainerNode->Height;
        }

        var x = addon->AtkUnitBase.GetScaledWidth(true) - ButtonSize - EdgeInset;

        return new Vector2(MathF.Max(Margin, x), y + Margin);
    }

    private unsafe void Teardown(AddonAreaMap* addon) => Release();

    private void Release()
    {
        foreach (var button in buttons.Values)
        {
            button.Dispose();
        }

        buttons.Clear();

        row?.Dispose();
        row = null;

        handle?.Dispose();
        handle = null;
    }

    private void ToggleExpanded()
    {
        expanded = !expanded;

        if (handle != null)
        {
            handle.IsChecked = expanded;
        }

        if (row != null)
        {
            row.IsVisible = expanded;
        }
    }

    private void Toggle(TreasureKind kind)
    {
        var wanted = !shownKinds().Contains(kind);

        onToggle(kind, wanted);

        if (buttons.TryGetValue(kind, out var button))
        {
            Paint(button, wanted);
        }
    }
}
