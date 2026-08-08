namespace Hornwatch.Core.Navigation;

public interface IConfirmationDialog
{
    bool IsAwaitingAnswer { get; }

    void Accept();
}
