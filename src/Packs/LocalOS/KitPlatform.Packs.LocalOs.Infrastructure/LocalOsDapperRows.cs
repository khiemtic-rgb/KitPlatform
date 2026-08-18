using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

/// <summary>Dapper needs a parameterless ctor. Records with optional args fail to map.</summary>
internal sealed class LocalOsSourceRow
{
    public Guid Id { get; set; }
    public string SourceKind { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Url { get; set; }
    public string Status { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Category { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Geo { get; set; } = "";
    public string? Notes { get; set; }
    public bool WatchEnabled { get; set; }
    public DateTimeOffset? LastWatchedAt { get; set; }

    public LocalSourceDto ToDto() => new(
        Id, SourceKind, Name, Url, Status, Platform, Category, Audience, Geo, Notes,
        WatchEnabled, LastWatchedAt);
}

internal sealed class LocalOsWatchRunRow
{
    public Guid Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Trigger { get; set; } = "";
    public int SourcesScanned { get; set; }
    public int LinksSeen { get; set; }
    public int CreatedCount { get; set; }
    public int SkippedExisting { get; set; }
    public int SkippedFilter { get; set; }
    public int ErrorCount { get; set; }
    public string? Note { get; set; }

    public LocalWatchRunDto ToDto() => new(
        Id, StartedAt, FinishedAt, Trigger, SourcesScanned, LinksSeen, CreatedCount,
        SkippedExisting, SkippedFilter, ErrorCount, Note);
}
