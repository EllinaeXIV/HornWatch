namespace Hornwatch;

public static class BuildFlavour
{
    public static readonly bool DeveloperToolsAvailable =
#if DEBUG
        true;
#else
        false;
#endif
}
