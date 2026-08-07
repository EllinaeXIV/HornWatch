using System.Collections.Generic;

namespace Hornwatch.Core.Guides;

public interface IGuideCatalog
{
    IReadOnlyList<IZoneGuide> Guides { get; }
}
