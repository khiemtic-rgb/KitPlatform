using System.Text.Json;
using Dapper;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Application.Platform;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Platform;

internal sealed class PlatformTenantRepository
{
    private readonly IDbConnectionFactory _db;

    public PlatformTenantRepository(IDbConnectionFactory db) => _db = db;

    public async Task<int> CountTenantsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*)::int FROM tenants WHERE deleted_at IS NULL";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql);
    }

    public async Task<IReadOnlyList<PlatformTenantListItemDto>> ListTenantsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                tenant_code AS TenantCode,
                tenant_name AS TenantName,
                created_at AS CreatedAt,
                status AS Status,
                COALESCE(
                    NULLIF(TRIM(settings->'platform'->>'vertical'), ''),
                    NULLIF(TRIM(business_vertical), ''),
                    'pharmacy'
                ) AS Vertical,
                COALESCE(jsonb_array_length(
                    COALESCE(
                        settings->'platform'->'allowed_modules',
                        settings->'platform'->'enabled_modules',
                        '[]'::jsonb
                    )
                ), 0)::int AS AllowedModuleCount,
                COALESCE(jsonb_array_length(
                    COALESCE(settings->'platform'->'enabled_modules', '[]'::jsonb)
                ), 0)::int AS EnabledModuleCount
            FROM tenants
            WHERE deleted_at IS NULL
            ORDER BY created_at DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<TenantListRow>(sql);
        return rows
            .Select(r => new PlatformTenantListItemDto(
                r.Id,
                r.TenantCode,
                r.TenantName,
                r.CreatedAt,
                r.Status,
                r.Vertical,
                r.AllowedModuleCount,
                r.EnabledModuleCount))
            .ToList();
    }

    public async Task<bool> TenantCodeExistsAsync(string tenantCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM tenants
                WHERE tenant_code = @TenantCode AND deleted_at IS NULL
            )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { TenantCode = tenantCode });
    }

    public static string[] DefaultModulesForVertical(string vertical) =>
        vertical.Equals(PlatformVerticalCodes.Family, StringComparison.OrdinalIgnoreCase)
            ? ["family_os"]
            : vertical.Equals(PlatformVerticalCodes.Clinic, StringComparison.OrdinalIgnoreCase)
                ? ["clinic_appointments", "clinic_emr_lite", "novixa_connect", "crm_leads"]
                : [
                    "inventory", "procurement", "sales", "loyalty", "customer_app",
                    "medication", "health_wallet", "reservations", "reports",
                ];

    public async Task<CreatePlatformTenantResponse> CreateTenantAsync(
        CreatePlatformTenantRequest request,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var vertical = string.IsNullOrWhiteSpace(request.Vertical)
            ? PlatformVerticalCodes.Pharmacy
            : TenantPlatformSettingsValidator.NormalizeVertical(request.Vertical);
        var isFamily = vertical.Equals(PlatformVerticalCodes.Family, StringComparison.OrdinalIgnoreCase);
        var modules = DefaultModulesForVertical(vertical);
        var columnVertical = PlatformVerticalCodes.ToColumnValue(vertical, currentColumnValue: null);

        var platformNode = new Dictionary<string, object?>
        {
            ["schema_version"] = 1,
            ["vertical"] = vertical,
            ["enabled_modules"] = modules,
            ["allowed_modules"] = modules,
            ["i18n"] = new Dictionary<string, object?>
            {
                ["default_locale"] = "vi-VN",
                ["supported_locales"] = new[] { "vi-VN" },
                ["fallback_locale"] = "vi-VN",
                ["admin_default_locale"] = "vi-VN",
                ["customer_app_default_locale"] = "vi-VN",
            },
            ["features"] = isFamily
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>
                {
                    ["batch_tracking"] = true,
                    ["national_drug_catalog"] = vertical.Equals(
                        PlatformVerticalCodes.Pharmacy, StringComparison.OrdinalIgnoreCase),
                    ["order_level_repurchase"] = true,
                    ["family_members"] = true,
                    ["branch_price_overrides"] = true,
                    ["branch_product_listings"] = false,
                },
        };
        if (request.MaxBranches is int maxBranches)
            platformNode["max_branches"] = maxBranches;

        var settings = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["allow_negative_stock"] = false,
            ["loyalty_enabled"] = !isFamily && request.LoyaltyEnabled,
            ["batch_mode"] = "suggest",
            ["platform"] = platformNode,
        });

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string tenantSql = """
            INSERT INTO tenants (id, tenant_code, tenant_name, country_code, default_currency, business_vertical, settings)
            VALUES (@Id, @TenantCode, @TenantName, 'VN', 'VND', @BusinessVertical, @Settings::jsonb)
            """;

        await conn.ExecuteAsync(tenantSql, new
        {
            Id = tenantId,
            TenantCode = request.TenantCode,
            TenantName = request.TenantName,
            BusinessVertical = columnVertical,
            Settings = settings,
        }, tx);

        const string branchSql = """
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, address, phone, is_head_office)
            VALUES (@Id, @TenantId, @BranchCode, @BranchName, @Address, @Phone, TRUE)
            """;

        await conn.ExecuteAsync(branchSql, new
        {
            Id = branchId,
            TenantId = tenantId,
            BranchCode = request.BranchCode,
            BranchName = request.BranchName,
            Address = string.IsNullOrWhiteSpace(request.BranchAddress) ? null : request.BranchAddress.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.BranchPhone) ? null : request.BranchPhone.Trim(),
        }, tx);

        const string employeeSql = """
            INSERT INTO employees (id, tenant_id, employee_code, full_name, phone, email)
            VALUES (@Id, @TenantId, 'EMP001', @FullName, @Phone, @Email)
            """;

        await conn.ExecuteAsync(employeeSql, new
        {
            Id = employeeId,
            TenantId = tenantId,
            FullName = request.AdminFullName,
            Phone = string.IsNullOrWhiteSpace(request.BranchPhone) ? null : request.BranchPhone.Trim(),
            Email = request.AdminEmail,
        }, tx);

        await conn.ExecuteAsync(
            "INSERT INTO employee_branches (employee_id, branch_id, is_primary) VALUES (@EmployeeId, @BranchId, TRUE)",
            new { EmployeeId = employeeId, BranchId = branchId },
            tx);

        const string userSql = """
            INSERT INTO users (id, tenant_id, employee_id, username, email, password_hash)
            VALUES (@Id, @TenantId, @EmployeeId, @Username, @Email, @PasswordHash)
            """;

        await conn.ExecuteAsync(userSql, new
        {
            Id = userId,
            TenantId = tenantId,
            EmployeeId = employeeId,
            Username = request.AdminUsername,
            Email = request.AdminEmail,
            PasswordHash = passwordHash,
        }, tx);

        var roleName = isFamily ? "Phụ huynh / Quản trị" : "Quản trị viên";
        await conn.ExecuteAsync(
            "INSERT INTO roles (id, tenant_id, role_code, role_name) VALUES (@Id, @TenantId, 'ADMIN', @RoleName)",
            new { Id = roleId, TenantId = tenantId, RoleName = roleName },
            tx);

        await conn.ExecuteAsync(
            "INSERT INTO user_roles (user_id, role_id) VALUES (@UserId, @RoleId)",
            new { UserId = userId, RoleId = roleId },
            tx);

        if (isFamily)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO role_permissions (role_id, permission_id)
                SELECT @RoleId, p.id FROM permissions p
                WHERE p.permission_code LIKE 'family_os.%'
                   OR p.permission_code LIKE 'system.%'
                ON CONFLICT DO NOTHING
                """,
                new { RoleId = roleId },
                tx);
        }
        else
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO role_permissions (role_id, permission_id)
                SELECT @RoleId, p.id FROM permissions p
                ON CONFLICT DO NOTHING
                """,
                new { RoleId = roleId },
                tx);
        }

        // Warehouse still required by many admin flows; Family gets a lightweight HOME warehouse.
        const string warehouseSql = """
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, is_default)
            VALUES (@Id, @TenantId, @BranchId, @WarehouseCode, @WarehouseName, 1, TRUE)
            """;

        await conn.ExecuteAsync(warehouseSql, new
        {
            Id = warehouseId,
            TenantId = tenantId,
            BranchId = branchId,
            WarehouseCode = request.WarehouseCode,
            WarehouseName = request.WarehouseName,
        }, tx);

        var branchCount = 1;
        if (!isFamily && request.AdditionalBranches is { Count: > 0 })
        {
            const string extraBranchSql = """
                INSERT INTO branches (id, tenant_id, branch_code, branch_name, address, phone, is_head_office)
                VALUES (@Id, @TenantId, @BranchCode, @BranchName, @Address, @Phone, FALSE)
                """;

            const string extraEmployeeBranchSql = """
                INSERT INTO employee_branches (employee_id, branch_id, is_primary)
                VALUES (@EmployeeId, @BranchId, FALSE)
                """;

            const string extraWarehouseSql = """
                INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, is_default)
                VALUES (@Id, @TenantId, @BranchId, @WarehouseCode, @WarehouseName, 1, FALSE)
                """;

            foreach (var extra in request.AdditionalBranches)
            {
                var extraBranchId = Guid.NewGuid();
                var extraWarehouseId = Guid.NewGuid();

                await conn.ExecuteAsync(extraBranchSql, new
                {
                    Id = extraBranchId,
                    TenantId = tenantId,
                    BranchCode = extra.BranchCode,
                    BranchName = extra.BranchName,
                    Address = extra.BranchAddress,
                    Phone = extra.BranchPhone,
                }, tx);

                await conn.ExecuteAsync(extraEmployeeBranchSql, new
                {
                    EmployeeId = employeeId,
                    BranchId = extraBranchId,
                }, tx);

                await conn.ExecuteAsync(extraWarehouseSql, new
                {
                    Id = extraWarehouseId,
                    TenantId = tenantId,
                    BranchId = extraBranchId,
                    WarehouseCode = extra.WarehouseCode,
                    WarehouseName = extra.WarehouseName,
                }, tx);

                branchCount++;
            }
        }

        if (isFamily)
        {
            await conn.ExecuteAsync(
                "SELECT set_config('app.tenant_id', @Value, true)",
                new { Value = tenantId.ToString() },
                tx);

            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.family (
                    id, tenant_id, display_name, timezone, status
                )
                VALUES (@Id, @TenantId, @DisplayName, 'Asia/Ho_Chi_Minh', 'active')
                """,
                new
                {
                    Id = familyId,
                    TenantId = tenantId,
                    DisplayName = request.TenantName,
                },
                tx);

            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.membership (
                    id, tenant_id, family_id, display_name, role_code, status, sort_order, user_id
                )
                VALUES (
                    @Id, @TenantId, @FamilyId, @DisplayName, 'guardian', 'active', 0, @UserId
                )
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    FamilyId = familyId,
                    DisplayName = request.AdminFullName,
                    UserId = userId,
                },
                tx);
        }

        await tx.CommitAsync(cancellationToken);

        return new CreatePlatformTenantResponse(
            tenantId,
            request.TenantCode,
            request.TenantName,
            branchId,
            userId,
            request.AdminUsername,
            branchCount);
    }

    public async Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(
        CancellationToken cancellationToken)
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
        var rows = await conn.QueryAsync<ModuleRegistryRow>(sql);
        return rows
            .Select(row => new PlatformModuleRegistryItemDto(
                row.ModuleCode,
                row.ModuleName,
                row.Description,
                row.Verticals ?? [],
                row.SortOrder))
            .ToList();
    }

    public async Task<PlatformTenantEntitlementDto> GetTenantEntitlementAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var row = await LoadTenantSettingsRowAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản (tenant).");

        return MapEntitlement(row);
    }

    public async Task<PlatformTenantEntitlementDto> UpdateTenantEntitlementAsync(
        Guid tenantId,
        string vertical,
        IReadOnlyList<string> allowedModules,
        IReadOnlyList<string> enabledModules,
        int? maxBranches,
        CancellationToken cancellationToken)
    {
        var row = await LoadTenantSettingsRowAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản (tenant).");

        var rootNode = string.IsNullOrWhiteSpace(row.SettingsJson)
            ? new System.Text.Json.Nodes.JsonObject()
            : System.Text.Json.Nodes.JsonNode.Parse(row.SettingsJson) as System.Text.Json.Nodes.JsonObject
              ?? new System.Text.Json.Nodes.JsonObject();

        var existingPlatform = rootNode["platform"] as System.Text.Json.Nodes.JsonObject
            ?? new System.Text.Json.Nodes.JsonObject();

        existingPlatform["vertical"] = vertical;
        existingPlatform["allowed_modules"] = new System.Text.Json.Nodes.JsonArray(
            allowedModules.Select(m => System.Text.Json.Nodes.JsonValue.Create(m)).ToArray());
        existingPlatform["enabled_modules"] = new System.Text.Json.Nodes.JsonArray(
            enabledModules.Select(m => System.Text.Json.Nodes.JsonValue.Create(m)).ToArray());

        if (maxBranches is null)
            existingPlatform.Remove("max_branches");
        else
            existingPlatform["max_branches"] = maxBranches.Value;

        if (existingPlatform["schema_version"] is null)
            existingPlatform["schema_version"] = 1;

        rootNode["platform"] = existingPlatform;

        var columnVertical = PlatformVerticalCodes.ToColumnValue(vertical, row.BusinessVertical);

        const string sql = """
            UPDATE tenants
            SET
                business_vertical = @Vertical,
                settings = @SettingsJson::jsonb,
                updated_at = NOW()
            WHERE id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(sql, new
        {
            TenantId = tenantId,
            Vertical = columnVertical,
            SettingsJson = rootNode.ToJsonString(),
        });

        if (affected == 0)
            throw new InvalidOperationException("Không cập nhật được entitlement cho tenant.");

        return await GetTenantEntitlementAsync(tenantId, cancellationToken);
    }

    private async Task<TenantSettingsRow?> LoadTenantSettingsRowAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                tenant_code AS TenantCode,
                tenant_name AS TenantName,
                business_vertical AS BusinessVertical,
                settings AS SettingsJson
            FROM tenants
            WHERE id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TenantSettingsRow>(sql, new { TenantId = tenantId });
    }

    private static PlatformTenantEntitlementDto MapEntitlement(TenantSettingsRow row)
    {
        var vertical = row.BusinessVertical?.Trim();
        if (string.IsNullOrWhiteSpace(vertical))
            vertical = "pharmacy";

        var enabled = new List<string>();
        var allowed = new List<string>();
        int? maxBranches = null;

        if (!string.IsNullOrWhiteSpace(row.SettingsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(row.SettingsJson);
                if (doc.RootElement.TryGetProperty("platform", out var platform))
                {
                    if (platform.TryGetProperty("vertical", out var vEl))
                    {
                        var v = vEl.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(v))
                            vertical = v;
                    }

                    enabled = ReadStringArray(platform, "enabled_modules");
                    allowed = ReadStringArray(platform, "allowed_modules");

                    if (platform.TryGetProperty("max_branches", out var mbEl)
                        && mbEl.ValueKind == JsonValueKind.Number
                        && mbEl.TryGetInt32(out var mb)
                        && mb >= 1)
                    {
                        maxBranches = mb;
                    }
                }
            }
            catch (JsonException)
            {
                // keep defaults
            }
        }

        if (allowed.Count == 0)
            allowed = enabled.Count > 0 ? enabled.ToList() : enabled;

        return new PlatformTenantEntitlementDto(
            row.Id,
            row.TenantCode,
            row.TenantName,
            vertical!,
            allowed,
            enabled,
            maxBranches);
    }

    private static List<string> ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select(x => x.GetString()?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class TenantListRow
    {
        public Guid Id { get; set; }
        public string TenantCode { get; set; } = "";
        public string TenantName { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public short Status { get; set; }
        public string Vertical { get; set; } = "pharmacy";
        public int AllowedModuleCount { get; set; }
        public int EnabledModuleCount { get; set; }
    }

    private sealed class TenantSettingsRow
    {
        public Guid Id { get; set; }
        public string TenantCode { get; set; } = "";
        public string TenantName { get; set; } = "";
        public string? BusinessVertical { get; set; }
        public string? SettingsJson { get; set; }
    }

    private sealed class ModuleRegistryRow
    {
        public string ModuleCode { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public string? Description { get; set; }
        public string[]? Verticals { get; set; }
        public int SortOrder { get; set; }
    }
}
