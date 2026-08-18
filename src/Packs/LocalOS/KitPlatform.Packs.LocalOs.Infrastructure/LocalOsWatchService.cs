using System.Net;
using System.Net.Http.Headers;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsWatchService : ILocalOsWatchService
{
    private const int MaxLinksPerSource = 10;
    private const int MaxCreatesPerSource = 3;

    private static readonly HttpClient Http = CreateHttp();

    private readonly IDbConnectionFactory _db;
    private readonly ILocalOsIngestService _ingest;
    private readonly ILocalOsHomepagePush _homepage;

    public LocalOsWatchService(
        IDbConnectionFactory db,
        ILocalOsIngestService ingest,
        ILocalOsHomepagePush homepage)
    {
        _db = db;
        _ingest = ingest;
        _homepage = homepage;
    }

    public async Task<LocalWatchRunDto> RunAsync(string trigger, CancellationToken cancellationToken = default)
    {
        await CloseStaleAsync(cancellationToken);
        if (await HasInFlightAsync(TimeSpan.FromMinutes(2), cancellationToken))
        {
            var open = (await ListRunsAsync(1, cancellationToken)).FirstOrDefault();
            if (open is not null)
                return open;
        }

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
        var budget = trig == "scheduled" ? TimeSpan.FromSeconds(90) : TimeSpan.FromSeconds(50);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(budget);
        var workCt = budgetCts.Token;

        try
        {
            IReadOnlyList<LocalSourceDto> sources;
            await using (var conn = await _db.CreateOpenConnectionAsync(workCt))
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
                        cancellationToken: workCt));
                sources = rows.ToList();
            }

            foreach (var source in sources)
            {
                if (workCt.IsCancellationRequested)
                {
                    notes.Add("Hết giờ canh — dừng, không treo.");
                    break;
                }

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
                    var html = await FetchHtmlAsync(indexUri, workCt);
                    if (html.Length < 40)
                    {
                        errors++;
                        notes.Add($"{source.Name}: không đọc được mục lục.");
                        continue;
                    }

                    var links = LocalOsIndexLinks.ExtractHits(html, indexUri, MaxLinksPerSource);
                    linksSeen += links.Count;
                    if (links.Count == 0)
                    {
                        notes.Add($"{source.Name}: mục lục không có tin việc/sự kiện.");
                        continue;
                    }

                    var made = 0;
                    foreach (var hit in links)
                    {
                        if (made >= MaxCreatesPerSource || workCt.IsCancellationRequested)
                            break;

                        var preview = LocalOsWatchFilter.Decide(hit.Title, hit.Uri.AbsoluteUri, source.Category);
                        if (preview != LocalOsWatchDecision.Allow)
                        {
                            filtered++;
                            continue;
                        }

                        if (await UrlExistsAsync(hit.Uri, workCt))
                        {
                            existing++;
                            continue;
                        }

                        try
                        {
                            var pageText = await FetchTextAsync(hit.Uri, workCt);
                            var title = pageText.Length > 0
                                ? LocalOsTextExtract.GuessTitle(pageText)
                                : (hit.Title.Length > 0 ? hit.Title : hit.Uri.AbsolutePath);
                            var decision = LocalOsWatchFilter.Decide(title, hit.Uri.AbsoluteUri, source.Category);
                            if (decision != LocalOsWatchDecision.Allow)
                            {
                                filtered++;
                                continue;
                            }

                            var result = await _ingest.IngestAsync(
                                new IngestFromSourceRequest(
                                    hit.Uri.AbsoluteUri,
                                    pageText.Length >= 12 ? pageText : null,
                                    source.Category is "job" or "event" ? source.Category : null,
                                    source.Id,
                                    FromWatch: true),
                                workCt);
                            if (result.Existing)
                                existing++;
                            else
                            {
                                created++;
                                made++;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception)
                        {
                            errors++;
                        }
                    }

                    await using var mark = await _db.CreateOpenConnectionAsync(CancellationToken.None);
                    await mark.ExecuteAsync(
                        new CommandDefinition(
                            "UPDATE pack_local.source SET last_watched_at = NOW(), updated_at = NOW() WHERE id = @Id",
                            new { source.Id },
                            cancellationToken: CancellationToken.None));
                }
                catch (OperationCanceledException)
                {
                    notes.Add("Hết giờ canh — dừng, không treo.");
                    break;
                }
                catch (Exception ex)
                {
                    errors++;
                    notes.Add($"{source.Name}: {TrimNote(ex.Message)}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            notes.Add("Hết giờ canh — dừng, không treo.");
        }
        catch (Exception ex)
        {
            errors++;
            notes.Add(TrimNote(ex.Message));
        }

        var note = notes.Count == 0
            ? (created > 0
                ? $"Canh xong: +{created} tin."
                : "Canh xong — chưa thấy tin việc/sự kiện mới trên mục lục.")
            : string.Join(" · ", notes.Distinct().Take(8));

        await using (var conn = await _db.CreateOpenConnectionAsync(CancellationToken.None))
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
                    cancellationToken: CancellationToken.None));
        }

        if (created > 0)
            await _homepage.PushAfterTrustedPublishAsync(created, CancellationToken.None);

        return (await GetRunAsync(runId, CancellationToken.None))!;
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

    public async Task<DateTimeOffset?> LastScheduledFinishedAtAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DateTimeOffset?>(
            new CommandDefinition(
                """
                SELECT MAX(finished_at) FROM pack_local.watch_run
                WHERE finished_at IS NOT NULL AND trigger = 'scheduled'
                """,
                cancellationToken: cancellationToken));
    }

    public async Task<bool> HasInFlightAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await CloseStaleAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var started = await conn.QuerySingleOrDefaultAsync<DateTimeOffset?>(
            new CommandDefinition(
                """
                SELECT started_at FROM pack_local.watch_run
                WHERE finished_at IS NULL
                ORDER BY started_at DESC
                LIMIT 1
                """,
                cancellationToken: cancellationToken));
        return started is DateTimeOffset at && DateTimeOffset.UtcNow - at < maxAge;
    }

    private async Task CloseStaleAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.watch_run
                SET finished_at = NOW(),
                    note = CASE
                        WHEN note IS NULL OR btrim(note) = '' THEN 'Dừng vì treo — bấm Canh nguồn lại.'
                        WHEN note ILIKE '%treo%' THEN note
                        ELSE note || ' Dừng vì treo.'
                    END
                WHERE finished_at IS NULL
                  AND started_at < NOW() - INTERVAL '2 minutes'
                """,
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
            using var resp = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resp.IsSuccessStatusCode)
                return "";
            var media = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (media.Length > 0
                && !media.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !media.Contains("text", StringComparison.OrdinalIgnoreCase)
                && !media.Contains("xml", StringComparison.OrdinalIgnoreCase))
                return "";
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
            return raw.Length > 400_000 ? raw[..400_000] : raw;
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

    private static HttpClient CreateHttp()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        return client;
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
