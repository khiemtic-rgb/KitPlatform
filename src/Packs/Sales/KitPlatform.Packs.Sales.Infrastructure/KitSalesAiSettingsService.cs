using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Packs.Sales.Infrastructure;

public sealed class KitSalesAiSettingsService : IKitSalesAiSettings
{
    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;

    public KitSalesAiSettingsService(
        IDbConnectionFactory db,
        IHttpClientFactory http,
        IConfiguration configuration)
    {
        _db = db;
        _http = http;
        _configuration = configuration;
    }

    public async Task<(string? ApiKey, string? TextModel)> GetEffectiveAsync(
        CancellationToken cancellationToken)
    {
        var row = await LoadRowAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(row?.GeminiApiKey))
            return (row.GeminiApiKey.Trim(), TrimOrNull(row.TextModel));

        var fallback = await ContentParkGeminiKeyResolver.ResolveSettingsAsync(
            _db, _configuration, cancellationToken);
        return (fallback.ApiKey, TrimOrNull(row?.TextModel) ?? fallback.TextModel);
    }

    public async Task<KitSalesGeminiSettingsDto> GetMaskedAsync(
        CancellationToken cancellationToken = default)
    {
        var (key, model) = await GetEffectiveAsync(cancellationToken);
        var row = await LoadRowAsync(cancellationToken);
        var fromSales = !string.IsNullOrWhiteSpace(row?.GeminiApiKey);
        var source = fromSales ? "sales" : !string.IsNullOrWhiteSpace(key) ? "env" : "none";
        var last4 = string.IsNullOrWhiteSpace(key) || key.Length < 4 ? null : key[^4..];
        return new KitSalesGeminiSettingsDto(
            !string.IsNullOrWhiteSpace(key),
            last4,
            model,
            !string.IsNullOrWhiteSpace(key),
            source);
    }

    public async Task<KitSalesGeminiSettingsDto> SaveAsync(
        SaveKitSalesGeminiSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.QuerySingleOrDefaultAsync<AiRow>(
            """
            SELECT tenant_id AS TenantId, gemini_api_key AS GeminiApiKey, text_model AS TextModel
            FROM pack_sales.ai_settings
            WHERE tenant_id = @TenantId
            """,
            new { TenantId = tenantId });

        var key = request.ClearApiKey
            ? null
            : FirstNonEmpty(request.GeminiApiKey, existing?.GeminiApiKey);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.ai_settings (tenant_id, gemini_api_key, text_model, updated_at)
            VALUES (@TenantId, @GeminiApiKey, @TextModel, NOW())
            ON CONFLICT (tenant_id) DO UPDATE SET
                gemini_api_key = EXCLUDED.gemini_api_key,
                text_model = EXCLUDED.text_model,
                updated_at = NOW()
            """,
            new
            {
                TenantId = tenantId,
                GeminiApiKey = key,
                TextModel = TrimOrNull(request.TextModel),
            });

        return await GetMaskedAsync(cancellationToken);
    }

    public async Task<KitSalesGeminiTestDto> TestAsync(CancellationToken cancellationToken = default)
    {
        var (key, model) = await GetEffectiveAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            return new KitSalesGeminiTestDto(false, null, "Chưa có Gemini API key.");

        var client = _http.CreateClient("kit-sales-gemini");
        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1");
        req.Headers.TryAddWithoutValidation("x-goog-api-key", key);
        using var res = await client.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            string? err = null;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                if (doc.RootElement.TryGetProperty("error", out var error)
                    && error.TryGetProperty("message", out var msg))
                    err = msg.GetString();
            }
            catch (JsonException)
            {
                // keep status
            }

            return new KitSalesGeminiTestDto(false, model, err ?? $"Gemini trả {(int)res.StatusCode}.");
        }

        return new KitSalesGeminiTestDto(true, model ?? "gemini-flash-latest", null);
    }

    private async Task<AiRow?> LoadRowAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AiRow>(
            """
            SELECT tenant_id AS TenantId, gemini_api_key AS GeminiApiKey, text_model AS TextModel
            FROM pack_sales.ai_settings
            WHERE tenant_id = @TenantId
            """,
            new { TenantId = SalesPackDefinition.DedicatedTenantId });
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a.Trim() : TrimOrNull(b);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class AiRow
    {
        public Guid TenantId { get; init; }
        public string? GeminiApiKey { get; init; }
        public string? TextModel { get; init; }
    }
}
