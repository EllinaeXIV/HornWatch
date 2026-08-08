namespace Hornwatch.Modules.OccultCrescent;

public sealed class OccultDepths(OccultMapLayers layers)
{
    public const float SubterraneanCeiling = -70f;

    public bool IsUnderground(uint territoryId, float height) =>
        layers.Of(territoryId).Underground != null && height < SubterraneanCeiling;
}
