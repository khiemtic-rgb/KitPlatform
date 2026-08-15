using System.Net.Http.Headers;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsWatchService : ILocalOsWatchService
{
    private const int MaxLinksPerSource = 8;
    private const int MaxCreatesPerSource = 4;

    private readonly IDbConnectionFactory _db;
    private readonly ILocalOsIngestService _ingest;

    public LocalOsWatchService(IDbConnectionFactory db, ILocalOsIngestService ingest)
    {
        _db = db;
        _ingest = ingest;
    }

    public async Task<LocalWatchRunDto> RunAsync(string trigger, CancellationToken cancellationToken = default)
    {
        var runId = Guid.CreateVersion7();
        var trig = trigger.Trim().ToLowerInvariant() == "scheduled" ? "scheduled" : "manual";
        await using (var conn = await _db.CreateOpenConnectionAsync(cancellationToken))
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_local.watch_run (id, started_at, trigger)
                    VALUES (@Id, NOW(), @Trigger)
                    """,
                    new { Id = runId, Trigger = trig },
                    cancellationToken: cancellationToken));
        }

        var scanned = 0;
        var linksSeen = 0;
        var created = 0;
        var existing = 0;
        var filtered = 0;
        var errors = 0;
        var notes = new List<string>();

        IReadOnlyList<LocalSourceDto> sources;
        await using (var conn = await _db.CreateOpenConnectionAsync(cancellationToken))
        {
            var rows = await conn.QueryAsync<LocalSourceDto>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, source_kind AS SourceKind, name AS Name, url AS Url, status AS Status,
                           platform AS Platform, category AS Category, audience AS Audience, geo AS Geo,
                           notes AS Notes, watch_enabled AS WatchEnabled, last_watched_at AS LastWatchedAt
                    FROM pack_local.source
                    WHERE watch_enabled = TRUE
                      AND status = 'active'
                      AND source_kind IN ('official_web', 'partner', 'rss')
                      AND platform <> 'facebook'
                    ORDER BY name
                    """,
                    cancellationToken: cancellationToken));
            sources = rows.ToList();
        }

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LocalOsSourceLink.TryParse(source.Url, out var indexUri, out var kind) || indexUri is null)
                continue;
            if (kind != LocalOsSourceLinkKind.PublicWeb
                || LocalOsSourceLink.IsFacebookHost(LocalOsSourceLink.NormalizeHost(indexUri.Host)))
            {
                notes.Add($"{source.Name}: bỏ (không canh Facebook).");
                continue;
            }

            scanned++;
            try
            {
                var html = await FetchHtmlAsync(indexUri, cancellationToken);
                var links = LocalOsIndexLinks.Extract(html, indexUri, MaxLinksPerSource);
                linksSeen += links.Count;
                var made = 0;
                foreach (var link in links)
                {
                    if (made >= MaxCreatesPerSource)
                        break;
                    if (await UrlExistsAsync(link, cancellationToken))
                    {
                        existing++;
                        continue;
                    }

                    var pageText = await FetchTextAsync(link, cancellationToken);
                    var title = pageText.Length > 0 ? LocalOsTextExtract.GuessTitle(pageText) : link.AbsolutePath;
                    var decision = LocalOsWatchFilter.Decide(title, link.AbsoluteUri, source.Category);
                    if (decision != LocalOsWatchDecision.Allow)
                    {
                        filtered++;
                        continue;
                    }

                    var result = await _ingest.IngestAsync(
                        new IngestFromSourceRequest(
                            link.AbsoluteUri,
                            pageText.Length >= 12 ? pageText : null,
                            source.Category is "job" or "event" ? source.Category : null,
                            source.Id),
                        cancellationToken);
                    if (result.Existing)
                        existing++;
                    else
                    {
                        created++;
                        made++;
                    }
                    await Task.Delay(150, cancellationToken);
                }

                await using var mark = await _db.CreateOpenConnectionAsync(cancellationToken);
                await mark.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE pack_local.source SET last_watched_at = NOW(), updated_at = NOW() WHERE id = @Id",
                        new { source.Id },
                        cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                notes.Add($"{source.Name}: {TrimNote(ex.Message)}");
            }
        }

        var note = notes.Count == 0
            ? "Canh mục lục công khai. Tin mới vào chờ duyệt — không tự lên site, không đụng Facebook."
            : string.Join(" · ", notes.Take(6));

        await using (var conn = await _db.CreateOpenConnectionAsync(cancellationToken))
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_local.watch_run SET
                        finished_at = NOW(),
                        sources_scanned = @SourcesScanned,
                        links_seen = @LinksSeen,
                        created_count = @CreatedCount,
                        skipped_existing = @SkippedExisting,
                        skipped_filter = @SkippedFilter,
                        error_count = @ErrorCount,
                        note = @Note
                    WHERE id = @Id
                    """,
                    new
                    {
                        Id = runId,
                        SourcesScanned = scanned,
                        LinksSeen = linksSeen,
                        CreatedCount = created,
                        SkippedExisting = existing,
                        SkippedFilter = filtered,
                        ErrorCount = errors,
                        Note = note,
                    },
                    cancellationToken: cancellationToken));
        }

        return (await GetRunAsync(runId, cancellationToken))!;
    }

    public async Task<IReadOnlyList<LocalWatchRunDto>> ListRunsAsync(
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LocalWatchRunDto>(
            new CommandDefinition(
                $"""
                SELECT {RunColumns}
                FROM pack_local.watch_run
                ORDER BY started_at DESC
                LIMIT @Take
                """,
                new { Take = Math.Clamp(take, 1, 30) },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<DateTimeOffset?> LastFinishedAtAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DateTimeOffset?>(
            new CommandDefinition(
                "SELECT MAX(finished_at) FROM pack_local.watch_run WHERE finished_at IS NOT NULL",
                cancellationToken: cancellationToken));
    }

    private async Task<LocalWatchRunDto?> GetRunAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<LocalWatchRunDto>(
            new CommandDefinition(
                $"""
                SELECT {RunColumns}
                FROM pack_local.watch_run
                WHERE id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    private async Task<bool> UrlExistsAsync(Uri uri, CancellationToken cancellationToken)
    {
        var url = uri.GetLeftPart(UriPartial.Query).TrimEnd('?');
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var found = await conn.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT id FROM pack_local.listing WHERE source_url = @Url LIMIT 1",
                new { Url = url },
                cancellationToken: cancellationToken));
        return found is not null;
    }

    private static async Task<string> FetchHtmlAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (LocalOsSourceLink.IsFacebookHost(LocalOsSourceLink.NormalizeHost(uri.Host)))
            return "";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ThaiNguyenLife", "1.0"));
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resp.IsSuccessStatusCode)
                return "";
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
            return raw.Length > 500_000 ? raw[..500_000] : raw;
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static async Task<string> FetchTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        var html = await FetchHtmlAsync(uri, cancellationToken);
        return html.Length == 0 ? "" : LocalOsTextExtract.StripHtml(html);
    }

    private static string TrimNote(string s) =>
        s.Length <= 160 ? s : s[..160].TrimEnd() + "…";

    private const string RunColumns = """
        id AS Id, started_at AS StartedAt, finished_at AS FinishedAt, trigger AS Trigger,
        sources_scanned AS SourcesScanned, links_seen AS LinksSeen, created_count AS CreatedCount,
        skipped_existing AS SkippedExisting, skipped_filter AS SkippedFilter,
        error_count AS ErrorCount, note AS Note
        """;
}
