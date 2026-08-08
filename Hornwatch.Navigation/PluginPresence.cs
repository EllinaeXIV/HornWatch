using Dalamud.Interface;
using Dalamud.Plugin;

namespace Hornwatch.Navigation;

public sealed class PluginPresence(IDalamudPluginInterface pluginInterface)
{
    public const string Vnavmesh = "vnavmesh";
    public const string Lifestream = "Lifestream";

    public bool IsLoaded(string internalName)
    {
        foreach (var plugin in pluginInterface.InstalledPlugins)
        {
            if (plugin.InternalName == internalName && plugin.IsLoaded)
            {
                return true;
            }
        }

        return false;
    }

    public void OpenInstallerFor(string internalName) =>
        pluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.AllPlugins, internalName);
}
