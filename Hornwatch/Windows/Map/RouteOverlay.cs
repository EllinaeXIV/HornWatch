using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Localization;
using Hornwatch.Core.Navigation;
using Hornwatch.Core.Treasure;

namespace Hornwatch.Windows.Map;

public sealed class RouteOverlay(
    ITravelService travel,
    TreasureHunt hunt,
    IGameGui gameGui,
    IObjectTable objects,
    ILocalizer localizer,
    Func<bool> isShown,
    Func<bool> isSupportedZone)
{
    private const int UpcomingWaypoints = 6;

    private const float ArrowLength = 14f;

    private const float ArrowSpread = 0.45f;

    private const uint LegColour = 0xF2A6D933;
    private const uint DestinationColour = 0xF240C7FF;
    private const uint RouteColour = 0xB3FFB38C;
    private const uint LabelBackground = 0x8C000000;
    private const uint LabelText = 0xFFFFFFFF;

    public void Draw()
    {
        if (!isShown() || !isSupportedZone())
        {
            return;
        }

        var drawList = ImGui.GetBackgroundDrawList();
        var here = objects.LocalPlayer?.Position;

        if (here == null)
        {
            return;
        }

        DrawPlannedRoute(drawList);

        if (travel.Destination is not { } destination)
        {
            return;
        }

        if (here is { } from && travel.LegDestination is { } leg)
        {
            DrawArrow(drawList, from, leg, LegColour, 3.5f);
        }

        if (Project(destination) is { } target)
        {
            drawList.AddCircle(target, 11f, DestinationColour, 0, 2.5f);
            drawList.AddCircleFilled(target, 3.5f, DestinationColour);

            var distance = here is { } at ? Ground(at, destination) : 0f;
            Label(drawList, target + new Vector2(14f, -8f),
                $"{localizer.Get($"travel.phase.{travel.Phase}")} - {distance:F0}y");
        }
    }

    private void DrawPlannedRoute(ImDrawListPtr drawList)
    {
        var route = hunt.Route;
        if (route.Count == 0)
        {
            return;
        }

        var last = Math.Min(route.Count, hunt.Index + UpcomingWaypoints);

        for (var i = hunt.Index; i < last; i++)
        {
            if (Project(route[i].Position) is not { } point)
            {
                continue;
            }

            drawList.AddCircleFilled(point, 4f, RouteColour);
            Label(drawList, point + new Vector2(7f, -7f), (i + 1).ToString());

            if (i + 1 < last && Project(route[i + 1].Position) is { } next)
            {
                drawList.AddLine(point, next, RouteColour, 1.8f);
            }
        }
    }

    private void DrawArrow(ImDrawListPtr drawList, Vector3 from, Vector3 to, uint colour, float thickness)
    {
        if (Project(from) is not { } start || Project(to) is not { } end)
        {
            return;
        }

        var along = end - start;
        var length = along.Length();
        if (length < 1f)
        {
            return;
        }

        along /= length;

        drawList.AddLine(start, end, colour, thickness);

        var left = new Vector2(
            (along.X * MathF.Cos(ArrowSpread)) - (along.Y * MathF.Sin(ArrowSpread)),
            (along.X * MathF.Sin(ArrowSpread)) + (along.Y * MathF.Cos(ArrowSpread)));

        var right = new Vector2(
            (along.X * MathF.Cos(-ArrowSpread)) - (along.Y * MathF.Sin(-ArrowSpread)),
            (along.X * MathF.Sin(-ArrowSpread)) + (along.Y * MathF.Cos(-ArrowSpread)));

        drawList.AddLine(end, end - (left * ArrowLength), colour, thickness);
        drawList.AddLine(end, end - (right * ArrowLength), colour, thickness);
    }

    private static void Label(ImDrawListPtr drawList, Vector2 at, string text)
    {
        var size = ImGui.CalcTextSize(text);
        drawList.AddRectFilled(at - new Vector2(3f, 2f), at + size + new Vector2(3f, 2f), LabelBackground, 3f);
        drawList.AddText(at, LabelText, text);
    }

    private Vector2? Project(Vector3 world) =>
        gameGui.WorldToScreen(world, out var screen) ? screen : null;

    private static float Ground(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
