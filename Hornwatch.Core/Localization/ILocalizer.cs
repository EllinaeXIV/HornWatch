namespace Hornwatch.Core.Localization;

public interface ILocalizer
{
    string Language { get; }

    string Get(string key);

    string Format(string key, params object[] args);
}
