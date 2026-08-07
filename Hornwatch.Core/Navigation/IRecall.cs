namespace Hornwatch.Core.Navigation;

public interface IRecall
{
    bool IsBusy { get; }

    bool Cast();
}
