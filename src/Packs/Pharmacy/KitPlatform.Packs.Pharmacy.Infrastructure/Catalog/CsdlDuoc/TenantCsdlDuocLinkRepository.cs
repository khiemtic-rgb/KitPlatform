using System.Data;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class TenantCsdlDuocLinkRepository
{
    private readonly IDbConnectionFactory _db;

    public TenantCsdlDuocLinkRepository(IDbConnectionFactory db) => _db = db;

    public async Task<TenantCsdlDuocLinkRow?> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var conn = await OpenAsync(cancellationToken);
        const string sql = """
            SELECT tenant_id AS TenantId,
                   enabled AS Enabled,
                   environment AS Environment,
                   username AS Username,
                   password AS Password,
                   practice_license_code AS PracticeLicenseCode,
                   enable_stock_out_sync AS EnableStockOutSync,
                   enable_stock_in_sync AS EnableStockInSync,
                   status AS Status,
                   last_check_at AS LastCheckAt,
                   last_error AS LastError,
                   connected_at AS ConnectedAt,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt,
                   updated_by AS UpdatedBy
            FROM tenant_csdl_duoc_link
            WHERE tenant_id = @TenantId
            """;
        return await conn.QuerySingleOrDefaultAsync<TenantCsdlDuocLinkRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(TenantCsdlDuocLinkRow row, CancellationToken cancellationToken)
    {
        await using var conn = await OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO tenant_csdl_duoc_link (
                tenant_id, enabled, environment, username, password, practice_license_code,
                enable_stock_out_sync, enable_stock_in_sync, status,
                last_check_at, last_error, connected_at, updated_by, updated_at
            ) VALUES (
                @TenantId, @Enabled, @Environment, @Username, @Password, @PracticeLicenseCode,
                @EnableStockOutSync, @EnableStockInSync, @Status,
                @LastCheckAt, @LastError, @ConnectedAt, @UpdatedBy, NOW()
            )
            ON CONFLICT (tenant_id) DO UPDATE SET
                enabled = EXCLUDED.enabled,
                environment = EXCLUDED.environment,
                username = EXCLUDED.username,
                password = EXCLUDED.password,
                practice_license_code = EXCLUDED.practice_license_code,
                enable_stock_out_sync = EXCLUDED.enable_stock_out_sync,
                enable_stock_in_sync = EXCLUDED.enable_stock_in_sync,
                status = EXCLUDED.status,
                last_check_at = EXCLUDED.last_check_at,
                last_error = EXCLUDED.last_error,
                connected_at = EXCLUDED.connected_at,
                updated_by = EXCLUDED.updated_by,
                updated_at = NOW()
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: cancellationToken));
    }

    private async Task<Npgsql.NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
        await _db.CreateOpenConnectionAsync(cancellationToken);
}

internal sealed class TenantCsdlDuocLinkRow
{
    public Guid TenantId { get; init; }
    public bool Enabled { get; init; }
    public string Environment { get; init; } = "sandbox";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? PracticeLicenseCode { get; init; }
    public bool EnableStockOutSync { get; init; }
    public bool EnableStockInSync { get; init; }
    public string Status { get; init; } = "NotConfigured";
    public DateTime? LastCheckAt { get; init; }
    public string? LastError { get; init; }
    public DateTime? ConnectedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Guid? UpdatedBy { get; init; }
}
