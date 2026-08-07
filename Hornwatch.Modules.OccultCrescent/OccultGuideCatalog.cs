using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Hornwatch.Core.Caching;
using Hornwatch.Core.Guides;

namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultGuideCatalog : IGuideCatalog
{
    public OccultGuideCatalog(IDataManager data, IDataCache cache)
    {
        Guides = [new PhantomBlueMageGuide(data, cache)];
    }

    public IReadOnlyList<IZoneGuide> Guides { get; }
}
