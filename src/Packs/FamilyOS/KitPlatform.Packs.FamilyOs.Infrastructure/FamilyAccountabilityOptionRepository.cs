using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAccountabilityOptionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyAccountabilityOptionRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<int> CountAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.accountability_option
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task EnsureDefaultRowAsync(
        Guid familyId,
        FamilyAccountabilityDefaults.Item item,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        // Insert missing system catalog rows only — do not overwrite family edits to labels.
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.accountability_option (
                tenant_id, family_id, kind, code, option_group,
                label_vi, description_vi, is_system, sort_order, status
            )
            SELECT
                @TenantId, @FamilyId, @Kind, @Code, @OptionGroup,
                @LabelVi, @DescriptionVi, TRUE, @SortOrder, 'active'
            WHERE NOT EXISTS (
                SELECT 1
                FROM pack_family.accountability_option o
                WHERE o.tenant_id = @TenantId
                  AND o.family_id = @FamilyId
                  AND o.kind = @Kind
                  AND o.code = @Code
                  AND o.deleted_at IS NULL
            )
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                item.Kind,
                item.Code,
                OptionGroup = item.Group,
                item.LabelVi,
                item.DescriptionVi,
                item.SortOrder,
            });
    }

    public async Task SoftDeleteAsync(
        Guid familyId,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(
            """
            UPDATE pack_family.accountability_option
            SET deleted_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @OptionId
              AND is_system = FALSE
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, OptionId = optionId });
        if (affected == 0)
            throw new InvalidOperationException("Không xóa được mục catalog (chỉ tùy chỉnh).");
    }

    public async Task<IReadOnlyList<OptionRow>> ListAsync(
        Guid familyId,
        string? kind,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<OptionRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, kind AS Kind, code AS Code,
                   option_group AS OptionGroup, label_vi AS LabelVi,
                   description_vi AS DescriptionVi, is_system AS IsSystem,
                   sort_order AS SortOrder, status AS Status
            FROM pack_family.accountability_option
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND (@Kind IS NULL OR kind = @Kind)
            ORDER BY kind, sort_order, created_at
            """,
            new { TenantId, FamilyId = familyId, Kind = kind });
        return rows.AsList();
    }

    public async Task<OptionRow?> GetAsync(
        Guid familyId,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OptionRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, kind AS Kind, code AS Code,
                   option_group AS OptionGroup, label_vi AS LabelVi,
                   description_vi AS DescriptionVi, is_system AS IsSystem,
                   sort_order AS SortOrder, status AS Status
            FROM pack_family.accountability_option
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @OptionId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, OptionId = optionId });
    }

    public async Task<bool> CodeExistsAsync(
        Guid familyId,
        string kind,
        string code,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM pack_family.accountability_option
                WHERE tenant_id = @TenantId
                  AND family_id = @FamilyId
                  AND kind = @Kind
                  AND code = @Code
                  AND deleted_at IS NULL
            )
            """,
            new { TenantId, FamilyId = familyId, Kind = kind, Code = code });
    }

    public async Task<bool> ActiveCodeExistsAsync(
        Guid familyId,
        string kind,
        string code,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM pack_family.accountability_option
                WHERE tenant_id = @TenantId
                  AND family_id = @FamilyId
                  AND kind = @Kind
                  AND code = @Code
                  AND status = 'active'
                  AND deleted_at IS NULL
            )
            """,
            new { TenantId, FamilyId = familyId, Kind = kind, Code = code });
    }

    public async Task<OptionRow> InsertAsync(
        Guid familyId,
        string kind,
        string code,
        string optionGroup,
        string labelVi,
        string descriptionVi,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<OptionRow>(
            """
            INSERT INTO pack_family.accountability_option (
                tenant_id, family_id, kind, code, option_group,
                label_vi, description_vi, is_system, sort_order, status
            )
            VALUES (
                @TenantId, @FamilyId, @Kind, @Code, @OptionGroup,
                @LabelVi, @DescriptionVi, FALSE, @SortOrder, 'active'
            )
            RETURNING
                id AS Id, family_id AS FamilyId, kind AS Kind, code AS Code,
                option_group AS OptionGroup, label_vi AS LabelVi,
                description_vi AS DescriptionVi, is_system AS IsSystem,
                sort_order AS SortOrder, status AS Status
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                Kind = kind,
                Code = code,
                OptionGroup = optionGroup,
                LabelVi = labelVi,
                DescriptionVi = descriptionVi,
                SortOrder = sortOrder,
            });
    }

    public async Task<OptionRow?> UpdateAsync(
        Guid familyId,
        Guid optionId,
        string optionGroup,
        string labelVi,
        string descriptionVi,
        int sortOrder,
        string status,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OptionRow>(
            """
            UPDATE pack_family.accountability_option
            SET option_group = @OptionGroup,
                label_vi = @LabelVi,
                description_vi = @DescriptionVi,
                sort_order = @SortOrder,
                status = @Status,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @OptionId
              AND deleted_at IS NULL
            RETURNING
                id AS Id, family_id AS FamilyId, kind AS Kind, code AS Code,
                option_group AS OptionGroup, label_vi AS LabelVi,
                description_vi AS DescriptionVi, is_system AS IsSystem,
                sort_order AS SortOrder, status AS Status
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                OptionId = optionId,
                OptionGroup = optionGroup,
                LabelVi = labelVi,
                DescriptionVi = descriptionVi,
                SortOrder = sortOrder,
                Status = status,
            });
    }

    internal sealed class OptionRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public string Kind { get; init; } = "";
        public string Code { get; init; } = "";
        public string OptionGroup { get; init; } = "";
        public string LabelVi { get; init; } = "";
        public string DescriptionVi { get; init; } = "";
        public bool IsSystem { get; init; }
        public int SortOrder { get; init; }
        public string Status { get; init; } = "";
    }
}
