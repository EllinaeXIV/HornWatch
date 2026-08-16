using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace Hornwatch.Windows;

public static class GameIcon
{
    public static bool Draw(uint iconId, float size)
    {
        if (iconId == 0)
        {
            return false;
        }

        var texture = Svc.Textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
        if (texture == null)
        {
            return false;
        }

        ImGui.Image(texture.Handle, new Vector2(size));
        return true;
    }

    public static void DrawOrSpace(uint iconId, float size)
    {
        if (!Draw(iconId, size))
        {
            ImGui.Dummy(new Vector2(size));
        }
    }

    public static void DrawBefore(uint iconId, float size)
    {
        if (Draw(iconId, size))
        {
            ImGui.SameLine();
        }
    }
}
