namespace Hornwatch.Windows;

public interface ITab
{
    string TitleKey { get; }

    void Draw();
}
