using System;
using System.Collections.Concurrent;

namespace Hornwatch.Core.Caching;

public sealed class MemoryDataCache : IDataCache
{
    private readonly ConcurrentDictionary<string, object> entries = new();
    private readonly Func<bool> bypassProvider;

    public MemoryDataCache(Func<bool> bypassProvider)
    {
        this.bypassProvider = bypassProvider;
    }

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
