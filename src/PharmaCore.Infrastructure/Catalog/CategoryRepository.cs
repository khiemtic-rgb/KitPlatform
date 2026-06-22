using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Catalog;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class CategoryRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CategoryRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id AS Id,
                c.category_code AS CategoryCode,
                c.category_name AS CategoryName,
                c.description AS Description,
                c.parent_id AS ParentId,
                p.category_name AS ParentName,
                c.sort_order AS SortOrder,
                c.status AS Status
            FROM product_categories c
            LEFT JOIN product_categories p ON p.id = c.parent_id
            WHERE c.tenant_id = @TenantId AND c.deleted_at IS NULL
            ORDER BY c.sort_order, c.category_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CategoryDto>(sql, new { TenantId = _tenant.TenantId })).ToList();
    }

    public async Task<CategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id AS Id,
                c.category_code AS CategoryCode,
                c.category_name AS CategoryName,
                c.description AS Description,
                c.parent_id AS ParentId,
                p.category_name AS ParentName,
                c.sort_order AS SortOrder,
                c.status AS Status
            FROM product_categories c
            LEFT JOIN product_categories p ON p.id = c.parent_id
            WHERE c.id = @Id AND c.tenant_id = @TenantId AND c.deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CategoryDto>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<Guid> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO product_categories (tenant_id, parent_id, category_code, category_name, description, sort_order)
            VALUES (@TenantId, @ParentId, @CategoryCode, @CategoryName, @Description, @SortOrder)
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = _tenant.TenantId,
            request.ParentId,
            CategoryCode = request.CategoryCode.Trim(),
            CategoryName = request.CategoryName.Trim(),
            request.Description,
            request.SortOrder,
        });
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE product_categories SET
                parent_id = @ParentId,
                category_name = @CategoryName,
                description = @Description,
                sort_order = @SortOrder,
                status = @Status,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId = _tenant.TenantId,
            request.ParentId,
            CategoryName = request.CategoryName.Trim(),
            request.Description,
            request.SortOrder,
            request.Status,
        }) > 0;
    }

    public async Task<int> CountChildCategoriesAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int FROM product_categories
            WHERE parent_id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<int> CountProductsAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int FROM products
            WHERE category_id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE product_categories SET deleted_at = NOW(), status = 2, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId = _tenant.TenantId }) > 0;
    }
}
