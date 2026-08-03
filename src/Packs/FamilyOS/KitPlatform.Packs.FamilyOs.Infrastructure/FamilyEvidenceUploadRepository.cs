using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyEvidenceUploadRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyEvidenceUploadRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<DateTimeOffset?> FindRecentDuplicateAsync(
        Guid familyId,
        string sha256Hex,
        int lookbackDays,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DateTimeOffset?>(
            """
            SELECT created_at
            FROM pack_family.evidence_upload
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND content_sha256 = @Sha
              AND created_at >= NOW() - (@Days || ' days')::interval
            ORDER BY created_at DESC
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, Sha = sha256Hex, Days = lookbackDays });
    }

    public async Task InsertAsync(
        Guid familyId,
        Guid? memberId,
        string sha256Hex,
        int byteSize,
        int? width,
        int? height,
        string url,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.evidence_upload (
                id, tenant_id, family_id, member_id, content_sha256,
                byte_size, width, height, url, created_at
            ) VALUES (
                gen_random_uuid(), @TenantId, @FamilyId, @MemberId, @Sha,
                @ByteSize, @Width, @Height, @Url, NOW()
            )
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                Sha = sha256Hex,
                ByteSize = byteSize,
                Width = width,
                Height = height,
                Url = url,
            });
    }
}
