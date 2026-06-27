using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Catalog;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class ActiveIngredientRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ActiveIngredientRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<ActiveIngredientDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                ingredient_code AS IngredientCode,
                ingredient_name AS IngredientName,
                description AS Description,
                status AS Status
            FROM active_ingredients
            WHERE tenant_id = @TenantId
            ORDER BY ingredient_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ActiveIngredientDto>(sql, new { TenantId })).ToList();
    }

    public async Task<ActiveIngredientDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                ingredient_code AS IngredientCode,
                ingredient_name AS IngredientName,
                description AS Description,
                status AS Status
            FROM active_ingredients
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ActiveIngredientDto>(sql, new { Id = id, TenantId });
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM active_ingredients
                WHERE tenant_id = @TenantId
                  AND ingredient_code = @Code
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { TenantId, Code = code, ExcludeId = excludeId });
    }

    public async Task<Guid> CreateAsync(CreateActiveIngredientRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO active_ingredients (tenant_id, ingredient_code, ingredient_name, description)
            VALUES (@TenantId, @IngredientCode, @IngredientName, @Description)
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            IngredientCode = request.IngredientCode.Trim().ToUpperInvariant(),
            IngredientName = request.IngredientName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        });
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateActiveIngredientRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE active_ingredients SET
                ingredient_name = @IngredientName,
                description = @Description,
                status = @Status,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            IngredientName = request.IngredientName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.Status,
        }) > 0;
    }

    public async Task<int> CountProductUsagesAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int
            FROM product_ingredients pi
            INNER JOIN products p ON p.id = pi.product_id AND p.tenant_id = @TenantId
            WHERE pi.ingredient_id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { Id = id, TenantId });
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE active_ingredients SET status = 2, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND status = 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }
}
