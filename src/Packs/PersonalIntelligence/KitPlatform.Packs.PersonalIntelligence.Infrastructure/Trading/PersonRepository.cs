using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class PersonRepository : IPersonRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public PersonRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<Person?> GetByIdAsync(
        Guid personId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Person>(
            """
            SELECT
                person_id AS PersonId,
                user_id AS UserId,
                tenant_id AS TenantId,
                display_name AS DisplayName,
                timezone AS Timezone,
                row_version AS RowVersion,
                created_at AS CreatedAt,
                created_by AS CreatedBy,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                deleted_at AS DeletedAt
            FROM pack_personal_intelligence.person
            WHERE person_id = @PersonId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """,
            new { PersonId = personId, TenantId });
    }

    public async Task<Person?> GetByUserAndTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Person>(
            """
            SELECT
                person_id AS PersonId,
                user_id AS UserId,
                tenant_id AS TenantId,
                display_name AS DisplayName,
                timezone AS Timezone,
                row_version AS RowVersion,
                created_at AS CreatedAt,
                created_by AS CreatedBy,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                deleted_at AS DeletedAt
            FROM pack_personal_intelligence.person
            WHERE user_id = @UserId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """,
            new { UserId = userId, TenantId = tenantId });
    }

    public async Task<Guid> CreateAsync(
        Guid userId,
        Guid tenantId,
        string? displayName,
        string timezone,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var personId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.person (
                user_id,
                tenant_id,
                display_name,
                timezone,
                created_by,
                updated_by
            )
            VALUES (
                @UserId,
                @TenantId,
                @DisplayName,
                @Timezone,
                @CreatedBy,
                @CreatedBy
            )
            ON CONFLICT (user_id, tenant_id) DO UPDATE
                SET user_id = EXCLUDED.user_id
            RETURNING person_id
            """,
            new
            {
                UserId = userId,
                TenantId = tenantId,
                DisplayName = displayName,
                Timezone = timezone,
                CreatedBy = createdBy
            });

        return personId;
    }

    public async Task<bool> ExistsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM pack_personal_intelligence.person
            WHERE user_id = @UserId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """,
            new { UserId = userId, TenantId = tenantId });

        return count > 0;
    }
}
