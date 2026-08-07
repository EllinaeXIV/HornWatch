using System;

namespace Hornwatch.Core.Caching;

public interface IDataCache
{
    bool Bypass { get; }

    T GetOrCreate<T>(string key, Func<T> factory) where T : class;

    void Invalidate(string key);

    void Clear();
}
