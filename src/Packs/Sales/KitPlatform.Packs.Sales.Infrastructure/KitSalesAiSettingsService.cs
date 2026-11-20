using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Packs.Sales.Infrastructure;

public sealed class KitSalesAiSettingsService : IKitSalesAiSettings, IKitSalesAiGate
{
    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogService _audit;

    public KitSalesAiSettingsService(
        IDbConnectionFactory db,
        IHttpClientFactory http,
        IConfiguration configuration,
        IAuditLogService audit)
    {
        _db = db;
        _http = http;
        _configuration = configuration;
        _audit = audit;
    }

    public long WeeklyTokenBudget => Math.Max(0, _configuration.GetValue<long?>(SalesAiBudget.ConfigKey) ?? 0);

    public async Task<(string? ApiKey, string? TextModel)> GetEffectiveAsync(
        CancellationToken cancellationToken)
    {
        var row = await LoadRowAsync(cancellationToken);
        return (TrimOrNull(row?.GeminiApiKey), TrimOrNull(row?.TextModel));
    }

    public async Task<KitSalesGeminiSettingsDto> GetMaskedAsync(
        CancellationToken cancellationToken = default)
    {
        var (key, model) = await GetEffectiveAsync(cancellationToken);
        var used = await UsedTokensAsync(cancellationToken);
        var budget = WeeklyTokenBudget;
        var hasKey = !string.IsNullOrWhiteSpace(key);
        var last4 = !hasKey || key!.Length < 4 ? null : key[^4..];
        return new KitSalesGeminiSettingsDto(
            hasKey,
            last4,
            model ?? SalesAiBudget.DefaultModel,
            SalesAiBudget.Deny(key, budget, used, SalesAiBudget.OutputReserve) is null,
            hasKey ? "sales" : "none",
            budget,
            used);
    }

    public async Task<KitSalesGeminiSettingsDto> SaveAsync(
        SaveKitSalesGeminiSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var existing = await conn.QuerySingleOrDefaultAsync<AiRow>(
            """
            SELECT tenant_id AS TenantId, gemini_api_key AS GeminiApiKey, text_model AS TextModel
            FROM pack_sales.ai_settings
            WHERE tenant_id = @TenantId
            FOR UPDATE
            """,
            new { TenantId = tenantId },
            tx);

        var key = request.ClearApiKey
            ? null
            : FirstNonEmpty(request.GeminiApiKey, existing?.GeminiApiKey);
        var model = TrimOrNull(request.TextModel);

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
                TextModel = model,
            },
            tx);

        await KitSalesAuditedCommit.CommitAfterAuditAsync(tx, () => _audit.WriteAsync(
            SalesSettingsAudit.AiEntity,
            tenantId,
            "update",
            SalesSettingsAudit.Ai(existing?.GeminiApiKey, key, existing?.TextModel, model),
            CancellationToken.None));

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

        return new KitSalesGeminiTestDto(true, model ?? SalesAiBudget.DefaultModel, null);
    }

    async Task<KitSalesAiTicket> IKitSalesAiGate.AcquireAsync(
        string purpose,
        long reserveTokens,
        CancellationToken cancellationToken)
    {
        var (key, configuredModel) = await GetEffectiveAsync(cancellationToken);
        var model = configuredModel ?? SalesAiBudget.DefaultModel;
        var budget = WeeklyTokenBudget;
        if (SalesAiBudget.Deny(key, budget, 0, reserveTokens) is { } early)
            return KitSalesAiTicket.Denied(early, model);

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                "SELECT pg_advisory_xact_lock(hashtext('kit_sales.ai_budget'), hashtext(@TenantId::text))",
                new { TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
        var used = await SumUsedAsync(conn, tx, tenantId, cancellationToken);
        if (SalesAiBudget.Deny(key, budget, used, reserveTokens) is { } reason)
        {
            await tx.RollbackAsync(cancellationToken);
            return KitSalesAiTicket.Denied(reason, model);
        }

        var runId = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.agent_run (
                    tenant_id, agent_code, status, input_json, output_json, policy_json)
                VALUES (
                    @TenantId, @AgentCode, 'running', @Input::jsonb, @Output::jsonb, @Policy::jsonb)
                RETURNING id
                """,
                new
                {
                    TenantId = tenantId,
                    AgentCode = SalesAiBudget.AgentCode,
                    Input = JsonSerializer.Serialize(new { purpose }),
                    Output = JsonSerializer.Serialize(new { model, usage = new { totalTokens = reserveTokens, estimated = true } }),
                    Policy = JsonSerializer.Serialize(new { weeklyTokenBudget = budget, usedBefore = used, reserve = reserveTokens, fallbackModels = false }),
                },
                tx,
                cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return new KitSalesAiTicket(runId, key, model, null, reserveTokens);
    }

    async Task IKitSalesAiGate.SettleAsync(
        KitSalesAiTicket ticket,
        KitSalesAiUsage usage,
        CancellationToken cancellationToken)
    {
        if (ticket.RunId is not { } runId)
            return;

        var charged = SalesAiBudget.Charge(usage.TotalTokens, ticket.Reserve);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.agent_run
                SET status = @Status, output_json = @Output::jsonb
                WHERE id = @Id AND tenant_id = @TenantId AND agent_code = @AgentCode
                """,
                new
                {
                    Id = runId,
                    TenantId = SalesPackDefinition.DedicatedTenantId,
                    AgentCode = SalesAiBudget.AgentCode,
                    Status = usage.Ok ? "completed" : "failed",
                    Output = JsonSerializer.Serialize(new
                    {
                        model = ticket.Model,
                        usage = new
                        {
                            promptTokens = usage.PromptTokens,
                            outputTokens = usage.OutputTokens,
                            totalTokens = charged,
                            estimated = usage.TotalTokens is not > 0,
                        },
                        error = usage.Error,
                    }),
                },
                cancellationToken: cancellationToken));
    }

    private async Task<long> UsedTokensAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await SumUsedAsync(conn, null, SalesPackDefinition.DedicatedTenantId, cancellationToken);
    }

    private static Task<long> SumUsedAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction? tx,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        conn.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COALESCE(SUM(COALESCE((output_json->'usage'->>'totalTokens')::bigint, 0)), 0)
                FROM pack_sales.agent_run
                WHERE tenant_id = @TenantId
                  AND agent_code = @AgentCode
                  AND created_at > NOW() - make_interval(days => @Days)
                """,
                new { TenantId = tenantId, AgentCode = SalesAiBudget.AgentCode, Days = SalesAiBudget.WindowDays },
                tx,
                cancellationToken: cancellationToken));

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
