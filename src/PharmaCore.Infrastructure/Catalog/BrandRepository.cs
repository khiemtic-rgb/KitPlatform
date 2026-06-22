using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Catalog;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class BrandRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public BrandRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, brand_code AS BrandCode, brand_name AS BrandName,
                   country_code AS CountryCode, status AS Status
            FROM product_brands
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            ORDER BY brand_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<BrandDto>(sql, new { TenantId = _tenant.TenantId })).ToList();
    }

    public async Task<BrandDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, brand_code AS BrandCode, brand_name AS BrandName,
                   country_code AS CountryCode, status AS Status
            FROM product_brands
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<BrandDto>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<Guid> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO product_brands (tenant_id, brand_code, brand_name, country_code)
            VALUES (@TenantId, @BrandCode, @BrandName, @CountryCode)
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = _tenant.TenantId,
            BrandCode = request.BrandCode.Trim(),
            BrandName = request.BrandName.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? null : request.CountryCode.Trim().ToUpperInvariant(),
        });
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE product_brands SET
                brand_name = @BrandName,
                country_code = @CountryCode,
                status = @Status,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId = _tenant.TenantId,
            BrandName = request.BrandName.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? null : request.CountryCode.Trim().ToUpperInvariant(),
            request.Status,
        }) > 0;
    }

    public async Task<int> CountProductsAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int FROM products
            WHERE brand_id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE product_brands SET deleted_at = NOW(), status = 2, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId = _tenant.TenantId }) > 0;
    }
}
