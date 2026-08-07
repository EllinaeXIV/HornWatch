using System;
using System.Collections.Generic;

namespace Hornwatch.Core.Modules;

public interface IFieldModule
{
    string Key { get; }

    string DisplayNameKey { get; }

    IReadOnlyList<uint> TerritoryIds { get; }

    bool Handles(uint territoryId);

    T? GetCapability<T>() where T : class;

    void Update();

    void OnActivated();
    void OnDeactivated();
}
