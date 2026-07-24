using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyGraphRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyGraphRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<FamilyRow>> ListFamiliesAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<FamilyRow>(
            """
            SELECT id AS Id, display_name AS DisplayName, timezone AS Timezone,
                   status AS Status, created_at AS CreatedAt
            FROM pack_family.family
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            ORDER BY created_at DESC
            LIMIT 100
            """,
            new { TenantId });
        return rows.AsList();
    }

    public async Task<FamilyRow?> GetFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<FamilyRow>(
            """
            SELECT id AS Id, display_name AS DisplayName, timezone AS Timezone,
                   status AS Status, created_at AS CreatedAt
            FROM pack_family.family
            WHERE tenant_id = @TenantId AND id = @FamilyId AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<IReadOnlyList<MembershipRow>> ListMembersAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MembershipRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, display_name AS DisplayName,
                   role_code AS RoleCode, date_of_birth AS DateOfBirth,
                   account_id AS AccountId, sort_order AS SortOrder, status AS Status
            FROM pack_family.membership
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND deleted_at IS NULL
            ORDER BY sort_order, created_at
            """,
            new { TenantId, FamilyId = familyId });
        return rows.AsList();
    }

    public async Task<Guid> InsertFamilyAsync(
        string displayName,
        string timezone,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.family (tenant_id, display_name, timezone)
            VALUES (@TenantId, @DisplayName, @Timezone)
            RETURNING id
            """,
            new { TenantId, DisplayName = displayName, Timezone = timezone });
    }

    public async Task UpdateFamilyAsync(
        Guid familyId,
        string displayName,
        string timezone,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(
            """
            UPDATE pack_family.family
            SET display_name = @DisplayName,
                timezone = @Timezone,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND id = @FamilyId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                DisplayName = displayName,
                Timezone = timezone,
            });
        if (affected == 0)
            throw new InvalidOperationException("Không cập nhật được gia đình.");
    }

    public async Task<MembershipRow?> GetMemberAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MembershipRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, display_name AS DisplayName,
                   role_code AS RoleCode, date_of_birth AS DateOfBirth,
                   account_id AS AccountId, sort_order AS SortOrder, status AS Status
            FROM pack_family.membership
            WHERE tenant_id = @TenantId AND family_id = @FamilyId
              AND id = @MemberId AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
    }

    public async Task<MembershipRow> InsertMemberAsync(
        Guid familyId,
        string displayName,
        string roleCode,
        DateOnly? dateOfBirth,
        Guid? accountId,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<MembershipRow>(
            """
            INSERT INTO pack_family.membership (
                tenant_id, family_id, display_name, role_code,
                date_of_birth, account_id, sort_order
            )
            VALUES (
                @TenantId, @FamilyId, @DisplayName, @RoleCode,
                @DateOfBirth, @AccountId, @SortOrder
            )
            RETURNING
                id AS Id, family_id AS FamilyId, display_name AS DisplayName,
                role_code AS RoleCode, date_of_birth AS DateOfBirth,
                account_id AS AccountId, sort_order AS SortOrder, status AS Status
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                DisplayName = displayName,
                RoleCode = roleCode,
                DateOfBirth = dateOfBirth,
                AccountId = accountId,
                SortOrder = sortOrder,
            });
    }

    public async Task<MembershipRow?> UpdateMemberAsync(
        Guid familyId,
        Guid memberId,
        string displayName,
        string roleCode,
        DateOnly? dateOfBirth,
        int sortOrder,
        string status,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MembershipRow>(
            """
            UPDATE pack_family.membership
            SET display_name = @DisplayName,
                role_code = @RoleCode,
                date_of_birth = @DateOfBirth,
                sort_order = @SortOrder,
                status = @Status,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @MemberId
              AND deleted_at IS NULL
            RETURNING
                id AS Id, family_id AS FamilyId, display_name AS DisplayName,
                role_code AS RoleCode, date_of_birth AS DateOfBirth,
                account_id AS AccountId, sort_order AS SortOrder, status AS Status
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                DisplayName = displayName,
                RoleCode = roleCode,
                DateOfBirth = dateOfBirth,
                SortOrder = sortOrder,
                Status = status,
            });
    }

    internal sealed class FamilyRow
    {
        public Guid Id { get; init; }
        public string DisplayName { get; init; } = "";
        public string Timezone { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTimeOffset CreatedAt { get; init; }
    }

    internal sealed class MembershipRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public string DisplayName { get; init; } = "";
        public string RoleCode { get; init; } = "";
        public DateOnly? DateOfBirth { get; init; }
        public Guid? AccountId { get; init; }
        public int SortOrder { get; init; }
        public string Status { get; init; } = "";
    }
}
