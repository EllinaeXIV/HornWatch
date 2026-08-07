using System.Numerics;
using Hornwatch.Core.Encounters;

namespace Hornwatch.Windows;

public static class UiTheme
{
    public static readonly Vector4 Good = new(0.16f, 0.55f, 0.49f, 1f);
    public static readonly Vector4 Warning = new(0.72f, 0.46f, 0.10f, 1f);
    public static readonly Vector4 Danger = new(0.72f, 0.26f, 0.21f, 1f);

    public static Vector4 ForKind(EncounterKind kind) => kind switch
    {
        EncounterKind.CriticalEncounter => Danger,
        EncounterKind.NotableFate => Warning,
        EncounterKind.Raid => new Vector4(0.55f, 0.38f, 0.62f, 1f),
        _ => Good,
    };
}
