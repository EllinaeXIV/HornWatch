using System;
using System.Collections.Generic;

namespace Hornwatch.Core.Modules;

public sealed class FieldModuleRegistry
{
    private readonly IReadOnlyList<IFieldModule> modules;

    public FieldModuleRegistry(IReadOnlyList<IFieldModule> modules)
    {
        this.modules = modules;
    }

    public IReadOnlyList<IFieldModule> All => modules;

    public IFieldModule? Active { get; private set; }

    public bool InSupportedZone => Active != null;

    public event Action<IFieldModule?>? ActiveChanged;

    public void SetTerritory(uint territoryId)
    {
        var next = Resolve(territoryId);
        if (ReferenceEquals(next, Active))
        {
            return;
        }

        Active?.OnDeactivated();
        Active = next;
        Active?.OnActivated();

        ActiveChanged?.Invoke(Active);
    }

    public T? Capability<T>() where T : class => Active?.GetCapability<T>();

    private IFieldModule? Resolve(uint territoryId)
    {
        foreach (var module in modules)
        {
            if (module.Handles(territoryId))
            {
                return module;
            }
        }

        return null;
    }
}
