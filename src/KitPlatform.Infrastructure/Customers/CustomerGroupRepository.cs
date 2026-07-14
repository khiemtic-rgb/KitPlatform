using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Customers;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Customers;

internal sealed class CustomerGroupRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CustomerGroupRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<CustomerGroupDto>> ListAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, group_code AS GroupCode, group_name AS GroupName,
                   discount_percent AS DiscountPercent, status AS Status
            FROM customer_groups
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            ORDER BY group_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerGroupDto>(sql, new { TenantId })).ToList();
    }

    public async Task<CustomerGroupDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, group_code AS GroupCode, group_name AS GroupName,
                   discount_percent AS DiscountPercent, status AS Status
            FROM customer_groups
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerGroupDto>(sql, new { Id = id, TenantId });
    }

    public async Task<bool> ExistsActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customer_groups
                WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL AND status = 1
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { Id = id, TenantId });
    }

    public async Task<Guid> CreateAsync(
        string groupCode,
        string groupName,
        decimal discountPercent,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_groups (tenant_id, group_code, group_name, discount_percent)
            VALUES (@TenantId, @GroupCode, @GroupName, @DiscountPercent)
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            GroupCode = groupCode,
            GroupName = groupName,
            DiscountPercent = discountPercent,
        });
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string groupName,
        decimal discountPercent,
        short status,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_groups SET
                group_name = @GroupName,
                discount_percent = @DiscountPercent,
                status = @Status,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            GroupName = groupName,
            DiscountPercent = discountPercent,
            Status = status,
        }) > 0;
    }

    public async Task<int> CountCustomersAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int FROM customers
            WHERE customer_group_id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { Id = id, TenantId });
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_groups SET deleted_at = NOW(), status = 0, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }

    public async Task ClearCustomerGroupRefsAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers SET customer_group_id = NULL, updated_at = NOW()
            WHERE customer_group_id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = id, TenantId });
    }
}
