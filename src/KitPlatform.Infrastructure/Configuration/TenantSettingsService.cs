using System.Data;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Infrastructure.Configuration;

internal sealed class TenantSettingsService : ITenantSettingsService
{
    private static readonly TenantReceiptSettingsDto DefaultReceipt = new(
        Name: "NHÀ THUỐC NOVIXA",
        Tagline: "Chăm sóc sức khỏe cộng đồng",
        Phone: "0984.660.399",
        Address: null);

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _configuration;

    public TenantSettingsService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IConfiguration configuration)
    {
        _db = db;
        _tenant = tenant;
        _configuration = configuration;
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

    public async Task<TenantCustomerAppSettingsDto> GetCustomerAppSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                tenant_name AS TenantName,
                settings->'customer_app'->'branding' AS BrandingJson
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<(string TenantName, string? BrandingJson)>(
            sql,
            new { TenantId = _tenant.TenantId });
        return ParseCustomerAppSettings(row.TenantName, row.BrandingJson);
    }

    public async Task<TenantCustomerAppSettingsDto> UpdateCustomerAppSettingsAsync(
        UpdateTenantCustomerAppSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AppName))
            throw new InvalidOperationException("Tên app không được để trống.");
        if (string.IsNullOrWhiteSpace(request.ShortName))
            throw new InvalidOperationException("Tên rút gọn không được để trống.");

        var settings = new TenantCustomerAppSettingsDto(
            request.AppName.Trim(),
            request.ShortName.Trim(),
            string.IsNullOrWhiteSpace(request.LogoUrl) ? "/customer-app/icon.svg" : request.LogoUrl.Trim(),
            string.IsNullOrWhiteSpace(request.PrimaryColor) ? "#0F52BA" : request.PrimaryColor.Trim(),
            string.IsNullOrWhiteSpace(request.SecondaryColor) ? "#3CB371" : request.SecondaryColor.Trim(),
            request.SupportPhone?.Trim() ?? "",
            request.Tagline?.Trim() ?? "");

        var brandingJson = JsonSerializer.Serialize(new
        {
            app_name = settings.AppName,
            short_name = settings.ShortName,
            logo_url = settings.LogoUrl,
            primary_color = settings.PrimaryColor,
            secondary_color = settings.SecondaryColor,
            support_phone = settings.SupportPhone,
            tagline = settings.Tagline,
        });

        const string sql = """
            UPDATE tenants
            SET settings = jsonb_set(
                jsonb_set(
                    COALESCE(settings, '{}'::jsonb),
                    '{customer_app}',
                    COALESCE(settings->'customer_app', '{}'::jsonb),
                    true
                ),
                '{customer_app,branding}',
                @BrandingJson::jsonb,
                true
            ),
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            BrandingJson = brandingJson,
        });

        return settings;
    }

    private static TenantCustomerAppSettingsDto ParseCustomerAppSettings(string tenantName, string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null")
        {
            return new TenantCustomerAppSettingsDto(
                tenantName,
                tenantName,
                "/customer-app/icon.svg",
                "#0F52BA",
                "#3CB371",
                "",
                "");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string Read(string key, string fallback) =>
                root.TryGetProperty(key, out var el) && !string.IsNullOrWhiteSpace(el.GetString())
                    ? el.GetString()!.Trim()
                    : fallback;

            return new TenantCustomerAppSettingsDto(
                Read("app_name", tenantName),
                Read("short_name", tenantName),
                Read("logo_url", "/customer-app/icon.svg"),
                Read("primary_color", "#0F52BA"),
                Read("secondary_color", "#3CB371"),
                Read("support_phone", ""),
                Read("tagline", ""));
        }
        catch (JsonException)
        {
            return new TenantCustomerAppSettingsDto(
                tenantName,
                tenantName,
                "/customer-app/icon.svg",
                "#0F52BA",
                "#3CB371",
                "",
                "");
        }
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

    public async Task<GppChecklistSettingsDto> GetGppChecklistAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT settings->'gpp_checklist'->'checked' AS CheckedJson
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var json = await conn.QuerySingleOrDefaultAsync<string?>(sql, new { TenantId = _tenant.TenantId });
        return new GppChecklistSettingsDto(ParseGppChecklist(json));
    }

    public async Task<GppChecklistSettingsDto> UpdateGppChecklistAsync(
        UpdateGppChecklistRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { @checked = request.Checked });
        const string sql = """
            UPDATE tenants
            SET settings = jsonb_set(
                COALESCE(settings, '{}'::jsonb),
                '{gpp_checklist}',
                @Payload::jsonb,
                true
            ),
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantId = _tenant.TenantId, Payload = payload });
        return new GppChecklistSettingsDto(request.Checked);
    }

    public async Task<TenantRxSettingsDto> GetRxSettingsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                settings->'rx'->>'enforcement_mode' AS EnforcementMode,
                COALESCE((settings->'rx'->>'pos_blocked_audit')::boolean, true) AS PosBlockedAudit
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<(string? EnforcementMode, bool PosBlockedAudit)>(
            sql, new { TenantId = _tenant.TenantId });
        var mode = KitPlatform.Packs.Pharmacy.Rx.RxEnforcementMode.Parse(row.EnforcementMode);
        return new TenantRxSettingsDto(mode, row.EnforcementMode is null || row.PosBlockedAudit);
    }

    public async Task<TenantRxSettingsDto> UpdateRxSettingsAsync(
        UpdateTenantRxSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var mode = KitPlatform.Packs.Pharmacy.Rx.RxEnforcementMode.Parse(request.EnforcementMode);

        var rxJson = JsonSerializer.Serialize(new
        {
            enforcement_mode = mode,
            pos_blocked_audit = request.PosBlockedAudit,
        });

        const string sql = """
            UPDATE tenants
            SET settings = jsonb_set(
                COALESCE(settings, '{}'::jsonb),
                '{rx}',
                @RxJson::jsonb,
                true
            ),
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantId = _tenant.TenantId, RxJson = rxJson });
        return new TenantRxSettingsDto(mode, request.PosBlockedAudit);
    }

    public async Task<TenantPharmacyConsultationAiSettingsDto> GetPharmacyConsultationAiSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var row = await LoadPharmacyConsultationAiRowAsync(cancellationToken);
        var secretRef = string.IsNullOrWhiteSpace(row.GeminiApiKeySecretRef)
            ? "GEMINI_API_KEY"
            : row.GeminiApiKeySecretRef.Trim();
        var textModel = string.IsNullOrWhiteSpace(row.TextModel)
            ? "gemini-2.5-flash-lite"
            : row.TextModel.Trim();
        var configured = !string.IsNullOrWhiteSpace(row.GeminiApiKey)
                         || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(secretRef))
                         || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
        var envFallback = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
                          || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(secretRef));
        var contentKey = await ContentParkGeminiKeyResolver.ResolveAsync(_db, _configuration, cancellationToken);
        var contentFallback = !string.IsNullOrWhiteSpace(contentKey);
        if (contentFallback)
            configured = true;

        return new TenantPharmacyConsultationAiSettingsDto(
            secretRef,
            configured,
            textModel,
            envFallback,
            contentFallback && string.IsNullOrWhiteSpace(row.GeminiApiKey));
    }

    public async Task<TenantPharmacyConsultationAiSettingsDto> UpdatePharmacyConsultationAiSettingsAsync(
        UpdateTenantPharmacyConsultationAiSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await LoadPharmacyConsultationAiRowAsync(cancellationToken);
        var secretRef = request.GeminiApiKeySecretRef is null
            ? (string.IsNullOrWhiteSpace(current.GeminiApiKeySecretRef) ? "GEMINI_API_KEY" : current.GeminiApiKeySecretRef.Trim())
            : request.GeminiApiKeySecretRef.Trim();
        var textModel = string.IsNullOrWhiteSpace(request.TextModel)
            ? (string.IsNullOrWhiteSpace(current.TextModel) ? "gemini-2.5-flash-lite" : current.TextModel.Trim())
            : request.TextModel.Trim();

        string? apiKey;
        if (request.GeminiApiKey is null)
            apiKey = current.GeminiApiKey;
        else if (string.IsNullOrWhiteSpace(request.GeminiApiKey))
            apiKey = null;
        else
            apiKey = request.GeminiApiKey.Trim();

        var payload = JsonSerializer.Serialize(new
        {
            gemini_api_key_secret_ref = secretRef,
            gemini_api_key = apiKey,
            text_model = textModel,
        });

        const string sql = """
            UPDATE tenants
            SET settings = jsonb_set(
                COALESCE(settings, '{}'::jsonb),
                '{pharmacy_consultation_ai}',
                @Payload::jsonb,
                true
            ),
            updated_at = NOW()
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantId = _tenant.TenantId, Payload = payload });
        return await GetPharmacyConsultationAiSettingsAsync(cancellationToken);
    }

    public async Task<PharmacyConsultationAiResolvedDto> ResolvePharmacyConsultationAiAsync(
        CancellationToken cancellationToken = default)
    {
        var row = await LoadPharmacyConsultationAiRowAsync(cancellationToken);
        var textModel = string.IsNullOrWhiteSpace(row.TextModel)
            ? "gemini-2.5-flash-lite"
            : row.TextModel.Trim();
        return new PharmacyConsultationAiResolvedDto(row.GeminiApiKey, row.GeminiApiKeySecretRef, textModel);
    }

    private async Task<(string? GeminiApiKey, string? GeminiApiKeySecretRef, string? TextModel)> LoadPharmacyConsultationAiRowAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                settings->'pharmacy_consultation_ai'->>'gemini_api_key' AS GeminiApiKey,
                settings->'pharmacy_consultation_ai'->>'gemini_api_key_secret_ref' AS GeminiApiKeySecretRef,
                settings->'pharmacy_consultation_ai'->>'text_model' AS TextModel
            FROM tenants
            WHERE id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<(string? GeminiApiKey, string? GeminiApiKeySecretRef, string? TextModel)>(
            sql, new { TenantId = _tenant.TenantId });
    }

    private static IReadOnlyDictionary<string, bool> ParseGppChecklist(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null")
            return new Dictionary<string, bool>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("checked", out var checkedEl) || checkedEl.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, bool>();

            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var prop in checkedEl.EnumerateObject())
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.True;
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, bool>();
        }
    }
}
