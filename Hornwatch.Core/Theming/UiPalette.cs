using System.Numerics;

namespace Hornwatch.Core.Theming;

public sealed record UiPalette
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public required Vector4 Text { get; init; }
    public required Vector4 TextMuted { get; init; }
    public required Vector4 TextDisabled { get; init; }

    public required Vector4 WindowBg { get; init; }
    public required Vector4 FrameBg { get; init; }

    public required Vector4 Border { get; init; }

    public required Vector4 Accent { get; init; }

    public required float Rounding { get; init; }

    public Vector4 OnAccent
    {
        get
        {
            var luminance = (0.2126f * Accent.X) + (0.7152f * Accent.Y) + (0.0722f * Accent.Z);
            return luminance > 0.55f
                ? new Vector4(0.06f, 0.05f, 0.04f, 1f)
                : new Vector4(0.98f, 0.97f, 0.94f, 1f);
        }
    }
}
