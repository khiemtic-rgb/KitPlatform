using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;

namespace KitPlatform.Infrastructure.Configuration;

internal sealed class TenantPlatformSettingsService : ITenantPlatformSettings
{
    private static readonly string[] PharmacyModules =
    [
        "inventory", "procurement", "sales", "loyalty", "customer_app",
        "medication", "health_wallet", "reservations", "reports",
    ];

    private static readonly Dictionary<string, bool> PharmacyFeatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["batch_tracking"] = true,
        ["national_drug_catalog"] = true,
        ["order_level_repurchase"] = true,
        ["family_members"] = true,
        ["branch_price_overrides"] = true,
        ["branch_product_listings"] = false,
    };

    private static readonly Dictionary<string, string> DefaultViLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sales.order_reminder_default"] = "Đơn thuốc ngày {{date}}",
        ["customer.repurchase_section_title"] = "Nhắc hết đơn thuốc",
        ["customer.medication_reminders_title"] = "Nhắc uống thuốc",
        ["customer.health_wallet_title"] = "Hồ sơ sức khỏe",
    };

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly WorkspacePackProvisioner _workspaceProvisioner;

    public TenantPlatformSettingsService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        WorkspacePackProvisioner workspaceProvisioner)
    {
        _db = db;
        _tenant = tenant;
        _workspaceProvisioner = workspaceProvisioner;
    }

    public async Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(cancellationToken);
        return Parse(row);
    }

    public async Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                module_code AS ModuleCode,
                module_name AS ModuleName,
                description AS Description,
                verticals AS Verticals,
                sort_order AS SortOrder
            FROM platform_module_registry
            WHERE status = 1
            ORDER BY sort_order, module_code
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PlatformModuleRegistryRow>(sql);
        return rows
            .Select(row => new PlatformModuleRegistryItemDto(
                row.ModuleCode,
                row.ModuleName,
                row.Description,
                row.Verticals ?? [],
                row.SortOrder))
            .ToList();
    }

    public async Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(
        UpdateTenantPlatformSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(cancellationToken);
        var current = Parse(row);

        var registry = await ListModulesAsync(cancellationToken);
        var registryCodes = registry.Select(m => m.ModuleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedCeiling = current.AllowedModules.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Tenant ADMIN may toggle modules within Core ceiling; vertical is Core-owned.
        // Do not UPDATE tenants.business_vertical here — DEMO_FAMILY keeps column=hybrid
        // until migration 192a; settings.platform.vertical may already be "family".
        var vertical = current.Vertical;
        var (modules, ignored) = TenantPlatformSettingsValidator.NormalizeModules(
            request.EnabledModules,
            registryCodes,
            allowedCeiling);
        var features = TenantPlatformSettingsValidator.MergeFeatures(current.Features, request.Features);

        var platformNode = new JsonObject
        {
            ["schema_version"] = current.SchemaVersion,
            ["vertical"] = vertical,
            ["enabled_modules"] = new JsonArray(modules.Select(m => JsonValue.Create(m)).ToArray()),
            ["allowed_modules"] = new JsonArray(
                current.AllowedModules.Select(m => JsonValue.Create(m)).ToArray()),
            ["i18n"] = new JsonObject
            {
                ["default_locale"] = current.I18n.DefaultLocale,
                ["supported_locales"] = new JsonArray(
                    current.I18n.SupportedLocales.Select(l => JsonValue.Create(l)).ToArray()),
                ["fallback_locale"] = current.I18n.FallbackLocale,
                ["admin_default_locale"] = current.I18n.AdminDefaultLocale,
                ["customer_app_default_locale"] = current.I18n.CustomerAppDefaultLocale,
            },
            ["features"] = new JsonObject(
                features.ToDictionary(
                    pair => pair.Key,
                    pair => (JsonNode?)JsonValue.Create(pair.Value),
                    StringComparer.OrdinalIgnoreCase)),
        };
        if (current.MaxBranches is int maxBranches)
            platformNode["max_branches"] = maxBranches;

        var root = string.IsNullOrWhiteSpace(row.SettingsJson)
            ? new JsonObject()
            : JsonNode.Parse(row.SettingsJson) as JsonObject ?? new JsonObject();
        root["platform"] = platformNode;

        const string sql = """
            UPDATE tenants
            SET
                settings = @SettingsJson::jsonb,
                updated_at = NOW()
            WHERE id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            SettingsJson = root.ToJsonString(),
        });

        if (affected == 0)
            throw new InvalidOperationException("Không cập nhật được cấu hình platform cho tenant.");

        await _workspaceProvisioner.EnsurePacksForTenantAsync(_tenant.TenantId, modules, cancellationToken);

        var updatedRow = await LoadRowAsync(cancellationToken);
        return new TenantPlatformSettingsUpdateResultDto(Parse(updatedRow), ignored);
    }

    public async Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleCode))
            return false;

        var settings = await GetAsync(cancellationToken);
        return settings.EnabledModules.Contains(moduleCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsModuleEnabledForTenantCodeAsync(
        string tenantCode,
        string moduleCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleCode) || string.IsNullOrWhiteSpace(tenantCode))
            return false;

        var row = await LoadRowByCodeAsync(tenantCode.Trim(), cancellationToken);
        if (row is null)
            return false;

        var settings = Parse(row);
        return settings.EnabledModules.Contains(moduleCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureCode))
            return false;

        var settings = await GetAsync(cancellationToken);
        return settings.Features.TryGetValue(featureCode.Trim(), out var enabled) && enabled;
    }

    public async Task<bool> IsFeatureEnabledForTenantCodeAsync(
        string tenantCode,
        string featureCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureCode) || string.IsNullOrWhiteSpace(tenantCode))
            return false;

        var row = await LoadRowByCodeAsync(tenantCode.Trim(), cancellationToken);
        if (row is null)
            return false;

        var settings = Parse(row);
        return settings.Features.TryGetValue(featureCode.Trim(), out var enabled) && enabled;
    }

    public async Task<string?> GetLabelAsync(
        string translationKey,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(translationKey))
            return null;

        var settings = await GetAsync(cancellationToken);
        var effectiveLocale = locale?.Trim() ?? settings.I18n.CustomerAppDefaultLocale;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var dbLabel = await conn.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT translated_value
            FROM tenant_string_translations
            WHERE tenant_id = @TenantId
              AND translation_key = @Key
              AND locale_code = @Locale
            """,
            new { TenantId = _tenant.TenantId, Key = translationKey.Trim(), Locale = effectiveLocale });

        if (!string.IsNullOrWhiteSpace(dbLabel))
            return dbLabel;

        if (settings.Labels.TryGetValue(translationKey.Trim(), out var fromSettings))
            return fromSettings;

        return DefaultViLabels.TryGetValue(translationKey.Trim(), out var fallback) ? fallback : null;
    }

    public async Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(
        Guid? branchId = null,
        Guid? customerAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        var tenantDefault = settings.I18n.DefaultLocale;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        string? branchLocale = null;
        if (branchId is not null)
        {
            branchLocale = await conn.QuerySingleOrDefaultAsync<string?>(
                """
                SELECT locale_code
                FROM branches
                WHERE id = @BranchId AND tenant_id = @TenantId AND deleted_at IS NULL
                """,
                new { BranchId = branchId, TenantId = _tenant.TenantId });
        }

        string? customerLocale = null;
        if (customerAccountId is not null)
        {
            customerLocale = await conn.QuerySingleOrDefaultAsync<string?>(
                """
                SELECT preferred_locale
                FROM customer_accounts
                WHERE id = @AccountId AND tenant_id = @TenantId
                """,
                new { AccountId = customerAccountId, TenantId = _tenant.TenantId });
        }

        var effective = customerLocale
            ?? branchLocale
            ?? tenantDefault;

        if (!settings.I18n.SupportedLocales.Contains(effective, StringComparer.OrdinalIgnoreCase))
            effective = settings.I18n.FallbackLocale;

        return new ResolvedTenantLocaleDto(
            effective,
            tenantDefault,
            branchLocale,
            customerLocale);
    }

    private async Task<PlatformSettingsRow> LoadRowAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                business_vertical AS BusinessVertical,
                country_code AS CountryCode,
                settings AS SettingsJson
            FROM tenants
            WHERE id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<PlatformSettingsRow>(sql, new { TenantId = _tenant.TenantId });
    }

    private async Task<PlatformSettingsRow?> LoadRowByCodeAsync(
        string tenantCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                business_vertical AS BusinessVertical,
                country_code AS CountryCode,
                settings AS SettingsJson
            FROM tenants
            WHERE tenant_code = @TenantCode AND deleted_at IS NULL AND status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PlatformSettingsRow>(sql, new { TenantCode = tenantCode });
    }

    private static TenantPlatformSettingsDto Parse(PlatformSettingsRow row)
    {
        var vertical = row.BusinessVertical?.Trim();
        if (string.IsNullOrWhiteSpace(vertical))
            vertical = "pharmacy";

        var modules = PharmacyModules.ToList();
        List<string>? allowedModules = null;
        var features = new Dictionary<string, bool>(PharmacyFeatures, StringComparer.OrdinalIgnoreCase);
        var i18n = DefaultI18n(row.CountryCode);
        var labels = new Dictionary<string, string>(DefaultViLabels, StringComparer.OrdinalIgnoreCase);
        var schemaVersion = 1;
        int? maxBranches = null;

        if (!string.IsNullOrWhiteSpace(row.SettingsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(row.SettingsJson);
                if (doc.RootElement.TryGetProperty("platform", out var platform))
                {
                    if (platform.TryGetProperty("schema_version", out var sv) && sv.TryGetInt32(out var v))
                        schemaVersion = v;

                    if (platform.TryGetProperty("vertical", out var vertEl))
                    {
                        var verticalValue = vertEl.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(verticalValue))
                            vertical = verticalValue;
                    }

                    if (platform.TryGetProperty("enabled_modules", out var mods) && mods.ValueKind == JsonValueKind.Array)
                    {
                        modules = mods.EnumerateArray()
                            .Select(m => m.GetString()?.Trim())
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .Select(m => m!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }

                    if (platform.TryGetProperty("allowed_modules", out var allowed)
                        && allowed.ValueKind == JsonValueKind.Array)
                    {
                        allowedModules = allowed.EnumerateArray()
                            .Select(m => m.GetString()?.Trim())
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .Select(m => m!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }

                    if (platform.TryGetProperty("max_branches", out var mbEl)
                        && mbEl.ValueKind == JsonValueKind.Number
                        && mbEl.TryGetInt32(out var mb)
                        && mb >= 1)
                    {
                        maxBranches = mb;
                    }

                    if (platform.TryGetProperty("features", out var feat) && feat.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in feat.EnumerateObject())
                        {
                            if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                                features[prop.Name] = prop.Value.GetBoolean();
                        }
                    }

                    if (platform.TryGetProperty("i18n", out var i18nEl))
                        i18n = ParseI18n(i18nEl, row.CountryCode);

                    if (platform.TryGetProperty("labels", out var labelsEl)
                        && labelsEl.TryGetProperty(i18n.CustomerAppDefaultLocale, out var localeLabels))
                    {
                        foreach (var prop in localeLabels.EnumerateObject())
                            labels[prop.Name] = prop.Value.GetString() ?? labels.GetValueOrDefault(prop.Name, "");
                    }
                }
            }
            catch (JsonException)
            {
                // Fallback pharmacy pilot
            }
        }

        // Legacy tenants without allowed_modules: ceiling = currently enabled set.
        allowedModules ??= modules.ToList();
        if (allowedModules.Count == 0)
            allowedModules = modules.ToList();

        return new TenantPlatformSettingsDto(
            schemaVersion,
            vertical,
            modules,
            allowedModules,
            i18n,
            features,
            labels,
            maxBranches);
    }

    private static TenantPlatformI18nDto DefaultI18n(string? countryCode)
    {
        var locale = string.Equals(countryCode, "VN", StringComparison.OrdinalIgnoreCase) ? "vi-VN" : "vi-VN";
        return new TenantPlatformI18nDto(
            locale,
            [locale],
            locale,
            locale,
            locale);
    }

    private static TenantPlatformI18nDto ParseI18n(JsonElement i18nEl, string? countryCode)
    {
        var fallback = DefaultI18n(countryCode);
        string Read(string name, string def) =>
            i18nEl.TryGetProperty(name, out var el) && !string.IsNullOrWhiteSpace(el.GetString())
                ? el.GetString()!.Trim()
                : def;

        var supported = fallback.SupportedLocales.ToList();
        if (i18nEl.TryGetProperty("supported_locales", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            supported = arr.EnumerateArray()
                .Select(x => x.GetString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (supported.Count == 0)
                supported = fallback.SupportedLocales.ToList();
        }

        var defaultLocale = Read("default_locale", fallback.DefaultLocale);
        var fallbackLocale = Read("fallback_locale", defaultLocale);

        return new TenantPlatformI18nDto(
            defaultLocale,
            supported,
            fallbackLocale,
            Read("admin_default_locale", defaultLocale),
            Read("customer_app_default_locale", defaultLocale));
    }

    private sealed class PlatformSettingsRow
    {
        public string? BusinessVertical { get; set; }
        public string? CountryCode { get; set; }
        public string? SettingsJson { get; set; }
    }

    private sealed class PlatformModuleRegistryRow
    {
        public string ModuleCode { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public string? Description { get; set; }
        public string[]? Verticals { get; set; }
        public int SortOrder { get; set; }
    }
}
