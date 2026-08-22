using System.Net;
using Dapper;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal static class LocalOsEventLeadRefresh
{
    private static readonly HttpClient Http = CreateHttp();

    public static bool TryParsePublicUri(string? sourceUrl, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate((sourceUrl ?? "").Trim(), UriKind.Absolute, out var parsed))
            return false;
        if (parsed.Scheme is not ("http" or "https"))
            return false;
        if (LocalOsSourceLink.IsFacebookHost(LocalOsSourceLink.NormalizeHost(parsed.Host)))
            return false;
        uri = parsed;
        return true;
    }

    public static async Task<string?> TryExtractAsync(Uri uri, CancellationToken cancellationToken)
    {
        var html = await FetchHtmlAsync(uri, cancellationToken);
        if (html.Length < 80)
            return null;
        var pageText = LocalOsTextExtract.StripHtml(html);
        if (pageText.Length < 40)
            return null;
        var title = LocalOsTextExtract.GuessTitle(pageText);
        var summary = LocalOsTextExtract.GuessSummary(title, pageText);
        return summary.Length >= 80 ? summary : null;
    }

    public static async Task<bool> TryStoreAsync(
        System.Data.IDbConnection conn,
        Guid id,
        string summary,
        CancellationToken cancellationToken)
    {
        var n = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.listing
                SET summary = @Summary, last_checked_at = NOW(), updated_at = NOW()
                WHERE id = @Id
                  AND kind = 'event'
                  AND @Summary IS NOT NULL
                  AND char_length(@Summary) >= 80
                """,
                new { Id = id, Summary = summary },
                cancellationToken: cancellationToken));
        return n > 0;
    }

    public static async Task<int> RefreshThinAsync(
        System.Data.IDbConnection conn,
        int max,
        CancellationToken cancellationToken)
    {
        if (max < 1)
            return 0;
        var rows = (await conn.QueryAsync<(Guid Id, string? SourceUrl, string? Summary)>(
            new CommandDefinition(
                """
                SELECT id, source_url, summary
                FROM pack_local.listing
                WHERE kind = 'event'
                  AND status = 'ACTIVE'
                  AND source_url ~* '^https?://'
                  AND source_url NOT ILIKE '%facebook%'
                ORDER BY updated_at NULLS FIRST
                LIMIT 40
                """,
                cancellationToken: cancellationToken))).ToList();

        var done = 0;
        foreach (var row in rows)
        {
            if (done >= max || cancellationToken.IsCancellationRequested)
                break;
            if (!LocalOsTextExtract.IsThinLead(row.Summary))
                continue;
            if (!TryParsePublicUri(row.SourceUrl, out var uri))
                continue;
            var next = await TryExtractAsync(uri, cancellationToken);
            if (next is null || !LocalOsTextExtract.IsBetterLead(row.Summary, next))
                continue;
            if (await TryStoreAsync(conn, row.Id, next, cancellationToken))
                done++;
        }

        return done;
    }

    private static async Task<string> FetchHtmlAsync(Uri uri, CancellationToken cancellationToken)
    {
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

    private static HttpClient CreateHttp()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        return client;
    }
}
