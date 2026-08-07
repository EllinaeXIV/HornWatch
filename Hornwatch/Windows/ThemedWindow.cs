using Dalamud.Interface.Windowing;
using Hornwatch.Core.Theming;
using Hornwatch.Theming;

namespace Hornwatch.Windows;

public abstract class ThemedWindow : Window
{
    private readonly ThemeManager theme;
    private ThemeScope? scope;

    protected ThemedWindow(string name, ThemeManager theme) : base(name)
    {
        this.theme = theme;
    }

    protected ThemeManager Theme => theme;

    public override void PreDraw()
    {
        scope = theme.Push();
    }

    public override void PostDraw()
    {
        scope?.Dispose();
        scope = null;
    }
}
