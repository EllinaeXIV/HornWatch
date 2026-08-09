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

    private const float LargestButtonSize = 44f;

    private const float SmallestButtonSize = 28f;

    private const float Margin = 3f;

    private const float EdgeInset = 12f;

    private readonly AddonController<AddonAreaMap> controller;

    private readonly IFramework framework;
    private readonly ILocalizer localizer;
    private readonly Func<bool> overlayEnabled;
    private readonly Func<IReadOnlySet<TreasureKind>> shownKinds;
    private readonly Action<TreasureKind, bool> onToggle;
    private readonly IPluginLog log;

    private readonly Dictionary<TreasureKind, IconButtonNode> buttons = new();

    private AddonAreaMap* map;
    private IconButtonNode? handle;
    private VerticalListNode? row;
    private bool expanded = true;
    private Vector4 lastFrame;

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
        FollowFrame();

        var visible = overlayEnabled();

        if (handle != null)
        {
            handle.IsVisible = visible;
            Paint(handle, expanded);
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
            row = new VerticalListNode
            {
                ItemSpacing = Margin,
                IsVisible = overlayEnabled() && expanded,
            };

            var kinds = shownKinds();

            foreach (var kind in Order)
            {
                var button = new IconButtonNode
                {
                    IconId = Icons[kind],
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
                IsVisible = overlayEnabled(),
                TextTooltip = localizer.Get("treasure.markers"),
            };
            handle.OnClick = ToggleExpanded;

            Paint(handle, expanded);

            handle.AttachNode(&addon->AtkUnitBase, NodePosition.AsLastChild);
            row.AttachNode(&addon->AtkUnitBase, NodePosition.AsLastChild);

            map = addon;
            Layout(addon);

            log.Information(
                $"[toolbar] attached to the map with {buttons.Count} category buttons, " +
                $"anchor {handle.Position.X:0}/{handle.Position.Y:0} of {handle.Size.X:0}px " +
                $"in a frame of {lastFrame.Z:0}x{lastFrame.W:0} at {lastFrame.X:0}/{lastFrame.Y:0}.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not attach the treasure toolbar to the map.");
            Release();
        }
    }

    private void FollowFrame()
    {
        if (map == null || handle == null || row == null)
        {
            return;
        }

        var frame = FrameOf(map);

        if (new Vector4(frame.X, frame.Y, frame.Width, frame.Height) != lastFrame)
        {
            Layout(map);
        }
    }

    private unsafe void Layout(AddonAreaMap* addon)
    {
        if (handle == null || row == null)
        {
            return;
        }

        var frame = FrameOf(addon);
        var buttonSize = ButtonSizeWithin(frame.Height);
        var anchor = AnchorFrom(addon, frame, buttonSize);

        handle.Size = new Vector2(buttonSize);
        handle.Position = anchor;

        foreach (var button in buttons.Values)
        {
            button.Size = new Vector2(buttonSize);
        }

        row.Size = new Vector2(buttonSize, ((buttonSize + Margin) * Order.Length) - Margin);
        row.Position = anchor + new Vector2(0f, buttonSize + Margin);
        row.RecalculateLayout();

        lastFrame = new Vector4(frame.X, frame.Y, frame.Width, frame.Height);
    }

    private static float ButtonSizeWithin(float frameHeight)
    {
        var fitting = ((frameHeight - (EdgeInset * 2f) + Margin) / (Order.Length + 1)) - Margin;

        return Math.Clamp(fitting, SmallestButtonSize, LargestButtonSize);
    }

    private static unsafe Vector2 AnchorFrom(
        AddonAreaMap* addon, (float X, float Y, float Width, float Height) frame, float buttonSize)
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

        var columnHeight = ((buttonSize + Margin) * (Order.Length + 1)) - Margin;
        var lowestTop = frame.Y + frame.Height - EdgeInset - columnHeight;
        var x = frame.X + frame.Width - buttonSize - EdgeInset;

        return new Vector2(
            MathF.Max(Margin, x),
            MathF.Max(frame.Y + Margin, MathF.Min(frame.Y + y + Margin, lowestTop)));
    }

    private static unsafe (float X, float Y, float Width, float Height) FrameOf(AddonAreaMap* addon)
    {
        if (addon->WindowNode != null)
        {
            var node = &addon->WindowNode->AtkResNode;

            if (node->Width > 0 && node->Height > 0)
            {
                return (node->X, node->Y, node->Width * node->ScaleX, node->Height * node->ScaleY);
            }
        }

        return (0f, 0f,
            addon->AtkUnitBase.GetScaledWidth(false),
            addon->AtkUnitBase.GetScaledHeight(false));
    }

    private unsafe void Teardown(AddonAreaMap* addon) => Release();

    private void Release()
    {
        map = null;
        lastFrame = default;

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
        Sync();
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
