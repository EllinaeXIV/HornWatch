using System;
using System.Collections.Generic;

namespace Hornwatch.Core.Modules;

public abstract class FieldModuleBase : IFieldModule
{
    private readonly Dictionary<Type, object> capabilities = new();
    private readonly uint[] territoryIds;

    protected FieldModuleBase(string key, string displayNameKey, params uint[] territoryIds)
    {
        Key = key;
        DisplayNameKey = displayNameKey;
        this.territoryIds = territoryIds;
    }

    public string Key { get; }
    public string DisplayNameKey { get; }
    public IReadOnlyList<uint> TerritoryIds => territoryIds;

    public bool Handles(uint territoryId) => Array.IndexOf(territoryIds, territoryId) >= 0;

    protected void Provide<T>(T implementation) where T : class
    {
        capabilities[typeof(T)] = implementation;
    }

    public T? GetCapability<T>() where T : class
    {
        return capabilities.TryGetValue(typeof(T), out var found) ? (T)found : null;
    }

    public virtual void Update() { }
    public virtual void OnActivated() { }
    public virtual void OnDeactivated() { }
}
