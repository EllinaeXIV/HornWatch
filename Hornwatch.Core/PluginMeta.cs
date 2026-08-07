namespace Hornwatch.Core;

public static class PluginMeta
{
    public const string Name = "Hornwatch";

    public const string InternalName = "Hornwatch";

    public const string Command = "/hornwatch";
    public const string ShortCommand = "/hw";

    public static string WindowId(string suffix) => $"###{InternalName}_{suffix}";
}
