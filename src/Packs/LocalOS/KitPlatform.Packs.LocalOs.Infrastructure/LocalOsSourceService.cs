using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsSourceService : ILocalOsSourceService
{
    private const string SelectColumns = """
        id AS Id, source_kind AS SourceKind, name AS Name, url AS Url, status AS Status,
        platform AS Platform, category AS Category, audience AS Audience, geo AS Geo, notes AS Notes,
        watch_enabled AS WatchEnabled, last_watched_at AS LastWatchedAt
        """;

    private static readonly HashSet<string> Kinds =
        ["facebook_group", "facebook_page", "official_web", "rss", "partner", "user"];

    private readonly IDbConnectionFactory _db;

    public LocalOsSourceService(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LocalSourceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LocalOsSourceRow>(
            new CommandDefinition(
                $"""
                SELECT {SelectColumns}
                FROM pack_local.source
                ORDER BY source_kind, name
                """,
                cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<LocalSourceDto> CreateAsync(
        UpsertLocalSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.CreateVersion7();
        var bound = Bind(id, request);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_local.source (
                        id, source_kind, name, url, status, platform, category, audience, geo, notes
                    ) VALUES (
                        @Id, @SourceKind, @Name, @Url, @Status, @Platform, @Category, @Audience, @Geo, @Notes
                    )
                    """,
                    bound,
                    cancellationToken: cancellationToken));
        }
        catch (Exception ex) when (ex.Message.Contains("uq_local_source_url", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("URL nguồn này đã có trong sổ.");
        }

        return (await GetAsync(id, cancellationToken))!;
    }

    public async Task<LocalSourceDto?> UpdateAsync(
        Guid id,
        UpsertLocalSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var bound = Bind(id, request);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            var n = await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_local.source SET
                        source_kind = @SourceKind, name = @Name, url = @Url, status = @Status,
                        platform = @Platform, category = @Category, audience = @Audience,
                        geo = @Geo, notes = @Notes, updated_at = NOW()
                    WHERE id = @Id
                    """,
                    bound,
                    cancellationToken: cancellationToken));
            if (n == 0)
                return null;
        }
        catch (Exception ex) when (ex.Message.Contains("uq_local_source_url", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("URL nguồn này đã có trong sổ.");
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task<LocalSourceDto?> SetStatusAsync(
        Guid id,
        string status,
        CancellationToken cancellationToken = default)
    {
        var next = status.Trim().ToLowerInvariant() == "paused" ? "paused" : "active";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.source SET status = @Status, updated_at = NOW()
                WHERE id = @Id
                """,
                new { Id = id, Status = next },
                cancellationToken: cancellationToken));
        if (n == 0)
            return null;
        return await GetAsync(id, cancellationToken);
    }

    private async Task<LocalSourceDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<LocalOsSourceRow>(
            new CommandDefinition(
                $"""
                SELECT {SelectColumns}
                FROM pack_local.source
                WHERE id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
        return row?.ToDto();
    }

    private static object Bind(Guid id, UpsertLocalSourceRequest r)
    {
        var kind = (r.SourceKind ?? "").Trim().ToLowerInvariant();
        if (!Kinds.Contains(kind))
            throw new InvalidOperationException(
                "Loại nguồn: facebook_group, facebook_page, official_web, rss, partner, user.");

        var url = string.IsNullOrWhiteSpace(r.Url) ? null : r.Url.Trim();
        if (kind is "facebook_group" or "facebook_page" or "official_web" or "rss")
        {
            if (!LocalOsSourceLink.TryParse(url, out var uri, out var linkKind) || uri is null)
                throw new InvalidOperationException("Nguồn này cần URL http/https hợp lệ.");
            if (kind == "facebook_group" && linkKind != LocalOsSourceLinkKind.FacebookGroupFeed)
                throw new InvalidOperationException(
                    "facebook_group phải là link hội nhóm (trang group), không phải link một bài. Hệ thống không quét group.");
            if (kind == "official_web" && LocalOsSourceLink.IsFacebookHost(LocalOsSourceLink.NormalizeHost(uri.Host)))
                throw new InvalidOperationException("official_web không dùng host Facebook. Dùng facebook_group / facebook_page.");
        }

        var name = (r.Name ?? "").Trim();
        if (name.Length < 2)
            throw new InvalidOperationException("Đặt tên nguồn (tối thiểu 2 ký tự).");

        var status = (r.Status ?? "active").Trim().ToLowerInvariant() == "paused" ? "paused" : "active";
        var platform = string.IsNullOrWhiteSpace(r.Platform)
            ? kind.StartsWith("facebook", StringComparison.Ordinal) ? "facebook" : "web"
            : r.Platform.Trim();

        return new
        {
            Id = id,
            SourceKind = kind,
            Name = name,
            Url = url,
            Status = status,
            Platform = platform,
            Category = string.IsNullOrWhiteSpace(r.Category) ? "mixed" : r.Category.Trim(),
            Audience = string.IsNullOrWhiteSpace(r.Audience) ? "mixed" : r.Audience.Trim(),
            Geo = string.IsNullOrWhiteSpace(r.Geo) ? LocalOsPackDefinition.DefaultCityCode : r.Geo.Trim(),
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
        };
    }
}
