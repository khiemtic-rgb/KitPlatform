using System.Collections.Concurrent;
using System.Net;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsWatchService : ILocalOsWatchService
{
    private const int MaxLinksPerSource = 20;
    private const int MaxCreatesPerSource = 6;

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
        var start = await StartAsync(trigger, cancellationToken);
        if (!start.Began)
            return start.Run;
        return await CompleteAsync(start.Run.Id, trigger, cancellationToken);
    }

    public async Task<LocalWatchStartResult> StartAsync(string trigger, CancellationToken cancellationToken = default)
    {
        await CloseStaleAsync(cancellationToken);
        if (await HasInFlightAsync(TimeSpan.FromMinutes(2), cancellationToken))
        {
            var open = (await ListRunsAsync(1, cancellationToken)).FirstOrDefault();
            if (open is not null)
                return new LocalWatchStartResult(open, Began: false);
        }

        var runId = Guid.CreateVersion7();
        var trig = trigger.Trim().ToLowerInvariant() == "scheduled" ? "scheduled" : "manual";
        await using (var conn = await _db.CreateOpenConnectionAsync(cancellationToken))
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_local.watch_run (id, started_at, trigger, note)
                    VALUES (@Id, NOW(), @Trigger, 'Đang canh mục lục…')
                    """,
                    new { Id = runId, Trigger = trig },
                    cancellationToken: cancellationToken));
        }

        var run = await GetRunAsync(runId, cancellationToken)
            ?? new LocalWatchRunDto(runId, DateTimeOffset.UtcNow, null, trig, 0, 0, 0, 0, 0, 0, "Đang canh mục lục…");
        return new LocalWatchStartResult(run, Began: true);
    }

    public async Task<LocalWatchRunDto> CompleteAsync(
        Guid runId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        var trig = trigger.Trim().ToLowerInvariant() == "scheduled" ? "scheduled" : "manual";
        var scanned = 0;
        var linksSeen = 0;
        var created = 0;
        var existing = 0;
        var filtered = 0;
        var errors = 0;
        var notes = new ConcurrentBag<string>();
        var budget = trig == "scheduled" ? TimeSpan.FromSeconds(180) : TimeSpan.FromSeconds(70);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(budget);
        var workCt = budgetCts.Token;

        try
        {
            IReadOnlyList<LocalSourceDto> sources;
            await using (var conn = await _db.CreateOpenConnectionAsync(workCt))
            {
                var rows = await conn.QueryAsync<LocalOsSourceRow>(
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
                        ORDER BY last_watched_at NULLS FIRST, name
                        """,
                        cancellationToken: workCt));
                sources = rows.Select(r => r.ToDto()).ToList();
            }

            if (sources.Count == 0)
                notes.Add("Chưa có nguồn website đang bật canh.");

            var htmlById = new ConcurrentDictionary<Guid, string>();
            await Parallel.ForEachAsync(
                sources,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = workCt },
                async (source, ct) =>
                {
                    if (!LocalOsSourceLink.TryParse(source.Url, out var indexUri, out var kind) || indexUri is null)
                        return;
                    if (kind != LocalOsSourceLinkKind.PublicWeb
                        || LocalOsSourceLink.IsFacebookHost(LocalOsSourceLink.NormalizeHost(indexUri.Host)))
                    {
                        notes.Add($"{source.Name}: bỏ (không canh Facebook).");
                        return;
                    }

                    var html = await FetchHtmlAsync(indexUri, ct);
                    htmlById[source.Id] = html;
                    if (html.Length < 40)
                        notes.Add($"{source.Name}: không đọc được mục lục.");
                });

            foreach (var source in sources)
            {
                if (workCt.IsCancellationRequested)
                {
                    notes.Add("Hết giờ canh — dừng, không treo.");
                    break;
                }

                if (!htmlById.TryGetValue(source.Id, out var html))
                    continue;

                scanned++;
                if (html.Length < 40)
                {
                    errors++;
                    continue;
                }

                if (!LocalOsSourceLink.TryParse(source.Url, out var indexUri, out _) || indexUri is null)
                    continue;

                try
                {
                    var links = LocalOsIndexLinks.ExtractHits(html, indexUri, MaxLinksPerSource);
                    linksSeen += links.Count;
                    if (links.Count == 0)
                    {
                        notes.Add($"{source.Name}: mục lục không có tin việc/sự kiện.");
                        continue;
                    }

                    var filterCat = source.Category is "job" or "event" ? source.Category : null;
                    var made = 0;
                    foreach (var hit in links)
                    {
                        if (made >= MaxCreatesPerSource || workCt.IsCancellationRequested)
                            break;

                        var preview = LocalOsWatchFilter.Decide(hit.Title, hit.Uri.AbsoluteUri, filterCat);
                        if (preview != LocalOsWatchDecision.Allow)
                        {
                            filtered++;
                            continue;
                        }

                        var already = await FindByUrlAsync(hit.Uri, workCt);
                        if (already is { } row)
                        {
                            existing++;
                            if (row.Kind == "event" && row.SummaryLen < 280)
                                await TryRefreshShortSummaryAsync(row.Id, hit.Uri, workCt);
                            continue;
                        }

                        try
                        {
                            var pageText = await FetchTextAsync(hit.Uri, workCt);
                            var title = pageText.Length > 0
                                ? LocalOsTextExtract.GuessTitle(pageText)
                                : (hit.Title.Length > 0 ? hit.Title : hit.Uri.AbsolutePath);
                            var decision = LocalOsWatchFilter.Decide(title, hit.Uri.AbsoluteUri, filterCat);
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

        var noteList = notes.Distinct().Take(8).ToList();
        var note = noteList.Count == 0
            ? (created > 0
                ? $"Canh xong: +{created} tin."
                : "Canh xong — chưa thấy tin việc/sự kiện mới trên mục lục.")
            : string.Join(" · ", noteList);

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
        var rows = await conn.QueryAsync<LocalOsWatchRunRow>(
            new CommandDefinition(
                $"""
                SELECT {RunColumns}
                FROM pack_local.watch_run
                ORDER BY started_at DESC
                LIMIT @Take
                """,
                new { Take = Math.Clamp(take, 1, 30) },
                cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDto()).ToList();
    }

    public Task<DateTimeOffset?> LastFinishedAtAsync(CancellationToken cancellationToken = default) =>
        QueryMaxFinishedAtAsync(scheduledOnly: false, cancellationToken);

    public Task<DateTimeOffset?> LastScheduledFinishedAtAsync(CancellationToken cancellationToken = default) =>
        QueryMaxFinishedAtAsync(scheduledOnly: true, cancellationToken);

    public async Task<bool> HasInFlightAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await CloseStaleAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var value = await conn.ExecuteScalarAsync(
            new CommandDefinition(
                """
                SELECT started_at FROM pack_local.watch_run
                WHERE finished_at IS NULL
                ORDER BY started_at DESC
                LIMIT 1
                """,
                cancellationToken: cancellationToken));
        return ToOffset(value) is DateTimeOffset at && DateTimeOffset.UtcNow - at < maxAge;
    }

    private async Task<DateTimeOffset?> QueryMaxFinishedAtAsync(bool scheduledOnly, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var sql = scheduledOnly
            ? "SELECT MAX(finished_at) FROM pack_local.watch_run WHERE finished_at IS NOT NULL AND trigger = 'scheduled'"
            : "SELECT MAX(finished_at) FROM pack_local.watch_run WHERE finished_at IS NOT NULL";
        var value = await conn.ExecuteScalarAsync(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return ToOffset(value);
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
        var row = await conn.QuerySingleOrDefaultAsync<LocalOsWatchRunRow>(
            new CommandDefinition(
                $"""
                SELECT {RunColumns}
                FROM pack_local.watch_run
                WHERE id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
        return row?.ToDto();
    }

    private async Task<UrlHit?> FindByUrlAsync(Uri uri, CancellationToken cancellationToken)
    {
        var url = uri.GetLeftPart(UriPartial.Query).TrimEnd('?');
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<UrlHit>(
            new CommandDefinition(
                """
                SELECT id AS Id, kind AS Kind, char_length(coalesce(summary, '')) AS SummaryLen
                FROM pack_local.listing
                WHERE source_url = @Url
                LIMIT 1
                """,
                new { Url = url },
                cancellationToken: cancellationToken));
    }

    private sealed class UrlHit
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = "";
        public int SummaryLen { get; set; }
    }

    private async Task TryRefreshShortSummaryAsync(Guid id, Uri uri, CancellationToken cancellationToken)
    {
        var pageText = await FetchTextAsync(uri, cancellationToken);
        if (pageText.Length < 40)
            return;
        var title = LocalOsTextExtract.GuessTitle(pageText);
        if (LocalOsEventDate.IsPastInText($"{title} {pageText}"))
        {
            await using var hide = await _db.CreateOpenConnectionAsync(cancellationToken);
            await hide.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_local.listing
                    SET status = 'EXPIRED', last_checked_at = NOW(), updated_at = NOW()
                    WHERE id = @Id AND kind IN ('event', 'grant') AND status = 'ACTIVE'
                    """,
                    new { Id = id },
                    cancellationToken: cancellationToken));
            return;
        }
        var summary = LocalOsTextExtract.GuessSummary(title, pageText);
        if (summary.Length < 80)
            return;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.listing
                SET summary = @Summary, last_checked_at = NOW(), updated_at = NOW()
                WHERE id = @Id
                  AND kind = 'event'
                  AND char_length(coalesce(summary, '')) < 280
                  AND char_length(@Summary) > char_length(coalesce(summary, '')) + 40
                """,
                new { Id = id, Summary = summary },
                cancellationToken: cancellationToken));
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
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        return client;
    }

    private static DateTimeOffset? ToOffset(object? value)
    {
        if (value is null or DBNull)
            return null;
        if (value is DateTimeOffset dto)
            return dto;
        if (value is DateTime dt)
            return new DateTimeOffset(ToUtc(dt), TimeSpan.Zero);
        return null;
    }

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static string TrimNote(string s) =>
        s.Length <= 160 ? s : s[..160].TrimEnd() + "…";

    private const string RunColumns = """
        id AS Id, started_at AS StartedAt, finished_at AS FinishedAt, trigger AS Trigger,
        sources_scanned AS SourcesScanned, links_seen AS LinksSeen, created_count AS CreatedCount,
        skipped_existing AS SkippedExisting, skipped_filter AS SkippedFilter,
        error_count AS ErrorCount, note AS Note
        """;
}
