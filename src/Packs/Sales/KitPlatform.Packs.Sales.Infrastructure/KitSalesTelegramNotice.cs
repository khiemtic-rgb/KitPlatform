using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using KitPlatform.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesTelegramNotice
{
    public const string ClientName = "kit-sales-telegram";

    private static readonly Regex TokenRx = new(@"^\d{5,15}:[A-Za-z0-9_-]{20,100}$", RegexOptions.CultureInvariant);
    private static readonly Regex ChatRx = new(@"^-?\d{5,20}$", RegexOptions.CultureInvariant);

    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KitSalesTelegramNotice> _log;

    public KitSalesTelegramNotice(
        IDbConnectionFactory db,
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger<KitSalesTelegramNotice> log)
    {
        _db = db;
        _http = http;
        _configuration = configuration;
        _log = log;
    }

    public async Task NotifyUnsentAsync(CancellationToken cancellationToken)
    {
        var token = First("KitSales:Telegram:BotToken", "KIT_SALES_TELEGRAM_BOT_TOKEN");
        var chat = First("KitSales:Telegram:ChatId", "KIT_SALES_TELEGRAM_CHAT_ID");
        if (!TokenRx.IsMatch(token ?? "") || !ChatRx.IsMatch(chat ?? ""))
        {
            _log.LogInformation("KIT Sales telegram skipped: set KitSales:Telegram:BotToken and ChatId");
            return;
        }

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<NoticeRow>(
            new CommandDefinition(
                """
                SELECT l.id AS LeadId, b.name AS Name, b.province AS Province, b.address AS Address, b.phone AS Phone
                FROM pack_sales.lead l
                INNER JOIN pack_sales.business b ON b.id = l.business_id
                WHERE l.tenant_id = @TenantId
                  AND NOT EXISTS (
                      SELECT 1 FROM pack_sales.lead_event e
                      WHERE e.tenant_id = l.tenant_id AND e.lead_id = l.id AND e.event_type = 'telegram_notified')
                ORDER BY l.created_at
                LIMIT 60
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();
        if (rows.Count == 0)
            return;

        var sent = new List<Guid>();
        foreach (var batch in rows.Chunk(20))
        {
            if (!await SendAsync(token!, chat!, batch, cancellationToken))
                break;
            sent.AddRange(batch.Select(row => row.LeadId));
        }

        if (sent.Count == 0)
            return;

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        foreach (var leadId in sent)
        {
            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, tenantId, leadId, "telegram_notified", null, null, "{}",
                null, null, "telegram:" + leadId.ToString("N"), cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        _log.LogInformation("KIT Sales telegram sent {Count} prospects", sent.Count);
    }

    private async Task<bool> SendAsync(string token, string chat, NoticeRow[] batch, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        text.Append("KIT Sales — ").Append(batch.Length).AppendLine(" cơ sở mới");
        foreach (var row in batch)
        {
            text.Append("- ").AppendLine(Trim(row.Name) ?? "(không tên)");
            var place = string.Join(", ", new[] { Trim(row.Province), Trim(row.Address), Trim(row.Phone) }.Where(part => part is not null));
            if (place.Length > 0)
                text.Append("  ").AppendLine(place.Length > 180 ? place[..180] : place);
        }

        if (text.Length > 3500)
            text.Length = 3500;

        try
        {
            var client = _http.CreateClient(ClientName);
            using var response = await client.PostAsJsonAsync(
                "bot" + token + "/sendMessage",
                new Dictionary<string, string> { ["chat_id"] = chat, ["text"] = text.ToString() },
                cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;
            _log.LogWarning("KIT Sales telegram send failed with HTTP {Status}", (int)response.StatusCode);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning("KIT Sales telegram send failed: {Kind}", ex.GetType().Name);
            return false;
        }
    }

    private string? First(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key]?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class NoticeRow
    {
        public Guid LeadId { get; init; }
        public string? Name { get; init; }
        public string? Province { get; init; }
        public string? Address { get; init; }
        public string? Phone { get; init; }
    }
}
