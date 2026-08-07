using System.Collections.Generic;

namespace Hornwatch.Core.Guides;

public enum GuideUnlockKind
{
    Automatic,

    Monster,

    CriticalEncounter,
}

public sealed record GuideEntry
{
    public required string Name { get; init; }

    public uint IconId { get; init; }

    public int RequiredLevel { get; init; } = 1;

    public required GuideUnlockKind UnlockKind { get; init; }

    public string? SourceName { get; init; }

    public int? SourceLevel { get; init; }

    public string? ZoneName { get; init; }

    public float? X { get; init; }
    public float? Y { get; init; }

    public string? NoteKey { get; init; }
}

public sealed record GuideSection(string TitleKey, IReadOnlyList<GuideEntry> Entries);

public interface IZoneGuide
{
    string TitleKey { get; }

    string IntroKey { get; }

    IReadOnlyList<GuideSection> Sections { get; }
}
