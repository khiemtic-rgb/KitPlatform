using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Security;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Security;

internal sealed class AuditLogQueryService : IAuditLogQuery
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public AuditLogQueryService(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PagedAuditLogsResult> ListAsync(
        string? entityType,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var filters = new List<string> { "a.tenant_id = @TenantId" };
        if (!string.IsNullOrWhiteSpace(entityType))
            filters.Add("a.entity_type = @EntityType");
        if (!string.IsNullOrWhiteSpace(action))
            filters.Add("a.action = @Action");
        if (from.HasValue)
            filters.Add("a.created_at >= @From");
        if (to.HasValue)
            filters.Add("a.created_at < @To");

        var where = string.Join(" AND ", filters);
        var sql = $"""
            SELECT COUNT(*)::int FROM audit_logs a WHERE {where};
            SELECT
                a.id AS Id,
                a.user_id AS UserId,
                u.username AS Username,
                a.entity_type AS EntityType,
                a.entity_id AS EntityId,
                a.action AS Action,
                a.payload::text AS PayloadJson,
                a.created_at AS CreatedAt
            FROM audit_logs a
            LEFT JOIN users u ON u.id = a.user_id
            WHERE {where}
            ORDER BY a.created_at DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            EntityType = entityType?.Trim(),
            Action = action?.Trim(),
            From = from,
            To = to,
            PageSize = pageSize,
            Offset = offset,
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<AuditLogListItemDto>()).ToList();
        return new PagedAuditLogsResult(items, total, page, pageSize);
    }
}
