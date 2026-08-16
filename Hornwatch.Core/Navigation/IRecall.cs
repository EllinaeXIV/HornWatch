namespace Hornwatch.Core.Navigation;

public enum RecallAttempt
{
    Refused,

    Sent,
}

public interface IRecall
{
    bool IsBusy { get; }

    RecallAttempt Cast();
}
