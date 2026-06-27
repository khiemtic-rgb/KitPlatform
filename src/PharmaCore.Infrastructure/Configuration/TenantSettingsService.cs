using System.Data;
using System.Text.Json;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Configuration;
using PharmaCore.Application.Inventory;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Configuration;

internal sealed class TenantSettingsService : ITenantSettingsService
{
    private static readonly TenantReceiptSettingsDto DefaultReceipt = new(
        Name: "NHÀ THUỐC NOVIXA",
        Tagline: "Chăm sóc sức khỏe cộng đồng",
        Phone: "0984.660.399",
        Address: null);

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public TenantSettingsService(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TenantBatchMode> GetBatchModeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT settings->>'batch_mode' AS BatchMode
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var value = await conn.QuerySingleOrDefaultAsync<string?>(
            sql, new { TenantId = _tenant.TenantId });
        return TenantBatchModeParser.Parse(value);
    }

    public async Task<TenantReceiptSettingsDto> GetReceiptSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT settings->'receipt' AS ReceiptJson
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var json = await conn.QuerySingleOrDefaultAsync<string?>(
            sql, new { TenantId = _tenant.TenantId });
        return ParseReceiptJson(json) ?? DefaultReceipt;
    }

    public async Task<TenantReceiptSettingsDto> UpdateReceiptSettingsAsync(
        UpdateTenantReceiptSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Tên cửa hàng không được để trống.");

        var receipt = new TenantReceiptSettingsDto(
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Tagline) ? null : request.Tagline.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim());

        var receiptJson = JsonSerializer.Serialize(new
        {
            name = receipt.Name,
            tagline = receipt.Tagline,
            phone = receipt.Phone,
            address = receipt.Address,
        });

        const string sql = """
            UPDATE tenants
            SET settings = jsonb_set(
                COALESCE(settings, '{}'::jsonb),
                '{receipt}',
                @ReceiptJson::jsonb,
                true
            ),
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            ReceiptJson = receiptJson,
        });

        return receipt;
    }

    public async Task<TenantBatchModeSettingsDto> GetBatchModeSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var mode = await GetBatchModeAsync(cancellationToken);
        return new TenantBatchModeSettingsDto(TenantBatchModeParser.ToSettingValue(mode));
    }

    public async Task<TenantBatchModeSettingsDto> UpdateBatchModeAsync(
        UpdateTenantBatchModeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BatchMode))
            throw new InvalidOperationException("Chế độ quản lý lô không được để trống.");

        var mode = TenantBatchModeParser.Parse(request.BatchMode);
        var value = TenantBatchModeParser.ToSettingValue(mode);

        const string sql = """
            UPDATE tenants
            SET settings = jsonb_set(
                COALESCE(settings, '{}'::jsonb),
                '{batch_mode}',
                to_jsonb(@BatchMode::text),
                true
            ),
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            BatchMode = value,
        });

        return new TenantBatchModeSettingsDto(value);
    }

    public async Task<TenantDefaultMinStockDto> GetDefaultMinStockAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT NULLIF(settings->>'default_min_stock_qty', '')::numeric AS DefaultMinStockQty
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var value = await conn.QuerySingleOrDefaultAsync<decimal?>(sql, new { TenantId = _tenant.TenantId });
        return new TenantDefaultMinStockDto(value);
    }

    public async Task<TenantDefaultMinStockDto> UpdateDefaultMinStockAsync(
        UpdateTenantDefaultMinStockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DefaultMinStockQty is < 0)
            throw new InvalidOperationException("Ngưỡng tồn tối thiểu không được âm.");

        const string sql = """
            UPDATE tenants
            SET settings = CASE
                WHEN @DefaultMinStockQty IS NULL THEN COALESCE(settings, '{}'::jsonb) - 'default_min_stock_qty'
                ELSE jsonb_set(
                    COALESCE(settings, '{}'::jsonb),
                    '{default_min_stock_qty}',
                    to_jsonb(@DefaultMinStockQty::numeric),
                    true
                )
            END,
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            DefaultMinStockQty = request.DefaultMinStockQty,
        });

        return new TenantDefaultMinStockDto(request.DefaultMinStockQty);
    }

    private static TenantReceiptSettingsDto? ParseReceiptJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new TenantReceiptSettingsDto(
                name.Trim(),
                root.TryGetProperty("tagline", out var taglineEl) ? taglineEl.GetString() : null,
                root.TryGetProperty("phone", out var phoneEl) ? phoneEl.GetString() : null,
                root.TryGetProperty("address", out var addressEl) ? addressEl.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
