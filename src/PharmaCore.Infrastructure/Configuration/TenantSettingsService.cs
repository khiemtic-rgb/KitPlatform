using System.Data;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Configuration;
using PharmaCore.Application.Inventory;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Configuration;

internal sealed class TenantSettingsService : ITenantSettingsService
{
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
}
