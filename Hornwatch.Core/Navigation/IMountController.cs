namespace Hornwatch.Core.Navigation;

public interface IMountController
{
    bool IsEnabled { get; }

    bool IsMounted { get; }

    bool Mount();

    void Dismount();
}
