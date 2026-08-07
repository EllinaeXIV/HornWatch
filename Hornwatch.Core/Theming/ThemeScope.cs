using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Hornwatch.Core.Theming;

public sealed class ThemeScope : IDisposable
{
    private readonly int colours;
    private readonly int styles;
    private bool disposed;

    public ThemeScope(UiPalette palette)
    {
        var accent = palette.Accent;

        var selection = accent with { W = 0.38f };
        var selectionHover = accent with { W = 0.26f };
        var selectionActive = accent with { W = 0.52f };

        var buttonIdle = Mix(palette.FrameBg, accent, 0.18f);
        var buttonHover = Mix(palette.FrameBg, accent, 0.34f);
        var buttonActive = Mix(palette.FrameBg, accent, 0.5f);

        Push(ImGuiCol.Text, palette.Text);
        Push(ImGuiCol.TextDisabled, palette.TextDisabled);

        Push(ImGuiCol.WindowBg, palette.WindowBg);
        Push(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        Push(ImGuiCol.PopupBg, palette.WindowBg with { W = 0.98f });

        Push(ImGuiCol.Border, palette.Border);
        Push(ImGuiCol.BorderShadow, new Vector4(0f, 0f, 0f, 0f));

        Push(ImGuiCol.FrameBg, palette.FrameBg);
        Push(ImGuiCol.FrameBgHovered, Mix(palette.FrameBg, accent, 0.22f));
        Push(ImGuiCol.FrameBgActive, Mix(palette.FrameBg, accent, 0.34f));

        Push(ImGuiCol.TitleBg, palette.FrameBg);
        Push(ImGuiCol.TitleBgActive, Mix(palette.FrameBg, accent, 0.25f));
        Push(ImGuiCol.TitleBgCollapsed, palette.FrameBg with { W = 0.7f });

        Push(ImGuiCol.MenuBarBg, palette.FrameBg);

        Push(ImGuiCol.ScrollbarBg, new Vector4(0f, 0f, 0f, 0.18f));
        Push(ImGuiCol.ScrollbarGrab, Mix(palette.FrameBg, palette.Text, 0.25f));
        Push(ImGuiCol.ScrollbarGrabHovered, Mix(palette.FrameBg, accent, 0.4f));
        Push(ImGuiCol.ScrollbarGrabActive, accent);

        Push(ImGuiCol.CheckMark, accent);
        Push(ImGuiCol.SliderGrab, Mix(accent, palette.WindowBg, 0.25f));
        Push(ImGuiCol.SliderGrabActive, accent);

        Push(ImGuiCol.Button, buttonIdle);
        Push(ImGuiCol.ButtonHovered, buttonHover);
        Push(ImGuiCol.ButtonActive, buttonActive);

        Push(ImGuiCol.Header, selection);
        Push(ImGuiCol.HeaderHovered, selectionHover);
        Push(ImGuiCol.HeaderActive, selectionActive);

        Push(ImGuiCol.Separator, palette.Border);
        Push(ImGuiCol.SeparatorHovered, accent with { W = 0.6f });
        Push(ImGuiCol.SeparatorActive, accent);

        Push(ImGuiCol.Tab, palette.FrameBg);
        Push(ImGuiCol.TabHovered, selectionHover);
        Push(ImGuiCol.TabActive, selection);
        Push(ImGuiCol.TabUnfocused, palette.FrameBg);
        Push(ImGuiCol.TabUnfocusedActive, selectionHover);

        Push(ImGuiCol.TableHeaderBg, Mix(palette.FrameBg, accent, 0.12f));
        Push(ImGuiCol.TableBorderStrong, palette.Border);
        Push(ImGuiCol.TableBorderLight, palette.Border with { W = 0.35f });
        Push(ImGuiCol.TableRowBg, new Vector4(0f, 0f, 0f, 0f));
        Push(ImGuiCol.TableRowBgAlt, new Vector4(1f, 1f, 1f, 0.022f));

        Push(ImGuiCol.TextSelectedBg, selection);
        Push(ImGuiCol.NavHighlight, accent);

        colours = pushedColours;

        PushStyle(ImGuiStyleVar.WindowRounding, palette.Rounding);
        PushStyle(ImGuiStyleVar.ChildRounding, palette.Rounding);
        PushStyle(ImGuiStyleVar.FrameRounding, palette.Rounding);
        PushStyle(ImGuiStyleVar.PopupRounding, palette.Rounding);
        PushStyle(ImGuiStyleVar.ScrollbarRounding, palette.Rounding);
        PushStyle(ImGuiStyleVar.TabRounding, palette.Rounding);
        PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        PushStyle(ImGuiStyleVar.FrameBorderSize, palette.Rounding == 0f ? 1f : 0f);

        styles = pushedStyles;
    }

    private int pushedColours;
    private int pushedStyles;

    private void Push(ImGuiCol target, Vector4 colour)
    {
        ImGui.PushStyleColor(target, colour);
        pushedColours++;
    }

    private void PushStyle(ImGuiStyleVar target, float value)
    {
        ImGui.PushStyleVar(target, value);
        pushedStyles++;
    }

    private static Vector4 Mix(Vector4 a, Vector4 b, float amount)
    {
        return new Vector4(
            a.X + ((b.X - a.X) * amount),
            a.Y + ((b.Y - a.Y) * amount),
            a.Z + ((b.Z - a.Z) * amount),
            a.W);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ImGui.PopStyleVar(styles);
        ImGui.PopStyleColor(colours);
    }
}
