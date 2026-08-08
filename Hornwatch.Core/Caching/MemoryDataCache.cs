using System;
using System.Collections.Concurrent;

namespace Hornwatch.Core.Caching;

public sealed class MemoryDataCache(Func<bool> bypassProvider) : IDataCache
{
    private readonly ConcurrentDictionary<string, object> entries = new();

    public bool Bypass => bypassProvider();

    public T GetOrCreate<T>(string key, Func<T> factory) where T : class
    {
        if (Bypass)
        {
            return factory();
        }

        return (T)entries.GetOrAdd(key, _ => factory());
    }

    public void Invalidate(string key) => entries.TryRemove(key, out _);

    public void Clear() => entries.Clear();
}
