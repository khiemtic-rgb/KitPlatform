using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyWriteAccessService : IFamilyWriteAccessService
{
    public const string ViewerReadonlyMessageVi =
        "Chế độ xem demo — nhà này chỉ xem, không sửa được.";

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyWriteAccessService(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task EnsureCanMutateAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.UserId == Guid.Empty || familyId == Guid.Empty)
            return;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var role = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                """
                SELECT LOWER(role_code)
                FROM pack_family.membership
                WHERE tenant_id = @TenantId
                  AND family_id = @FamilyId
                  AND user_id = @UserId
                  AND deleted_at IS NULL
                LIMIT 1
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    FamilyId = familyId,
                    UserId = _tenant.UserId,
                },
                cancellationToken: cancellationToken));

        if (string.Equals(role, FamilyMembershipRoles.Viewer, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ViewerReadonlyMessageVi);
    }
}
