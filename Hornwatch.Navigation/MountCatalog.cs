using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Hornwatch.Core.Caching;
using LuminaMount = Lumina.Excel.Sheets.Mount;

namespace Hornwatch.Navigation;

public sealed record MountOption(uint Id, string Name, uint IconId);

public sealed class MountCatalog
{
    private readonly IDataManager data;
    private readonly IDataCache cache;

    public MountCatalog(IDataManager data, IDataCache cache)
    {
        this.data = data;
        this.cache = cache;
    }

    public IReadOnlyList<MountOption> All => cache.GetOrCreate("mounts.all", Build);

    public IEnumerable<MountOption> Unlocked()
    {
        foreach (var mount in All)
        {
            if (IsUnlocked(mount.Id))
            {
                yield return mount;
            }
        }
    }

    public MountOption? Find(uint id)
    {
        foreach (var mount in All)
        {
            if (mount.Id == id)
            {
                return mount;
            }
        }

        return null;
    }

    public static unsafe bool IsUnlocked(uint mountId)
    {
        var state = PlayerState.Instance();
        return state != null && state->IsMountUnlocked(mountId);
    }

    private IReadOnlyList<MountOption> Build()
    {
        var result = new List<MountOption>();

        var sheet = data.GetExcelSheet<LuminaMount>();
        if (sheet == null)
        {
            return result;
        }

        foreach (var row in sheet)
        {
            if (row.Order < 0)
            {
                continue;
            }

            var name = row.Singular.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            result.Add(new MountOption(row.RowId, Capitalize(name), (uint)row.Icon));
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return result;
    }

    private static string Capitalize(string value)
    {
        return char.ToUpper(value[0]) + value[1..];
    }
}
