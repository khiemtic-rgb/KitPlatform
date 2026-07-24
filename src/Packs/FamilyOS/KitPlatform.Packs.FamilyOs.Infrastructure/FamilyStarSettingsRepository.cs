using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyStarSettingsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyStarSettingsRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<FamilyStarSettingsRow?> GetAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<FamilyStarSettingsRow>(
            """
            SELECT family_id AS FamilyId,
                   late_t1_minutes AS LateT1Minutes,
                   late_t2_minutes AS LateT2Minutes,
                   late_t3_minutes AS LateT3Minutes,
                   late_half_pct AS LateHalfPct,
                   late_zero_pct AS LateZeroPct,
                   late_penalty_half_pct AS LatePenaltyHalfPct,
                   late_penalty_full_pct AS LatePenaltyFullPct
            FROM pack_family.family_star_settings
            WHERE tenant_id = @TenantId AND family_id = @FamilyId
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<FamilyStarSettingsRow> UpsertAsync(
        Guid familyId,
        FamilyStarTierSettings settings,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<FamilyStarSettingsRow>(
            """
            INSERT INTO pack_family.family_star_settings (
                tenant_id, family_id,
                late_t1_minutes, late_t2_minutes, late_t3_minutes,
                late_half_pct, late_zero_pct, late_penalty_half_pct, late_penalty_full_pct,
                updated_at
            )
            VALUES (
                @TenantId, @FamilyId,
                @LateT1Minutes, @LateT2Minutes, @LateT3Minutes,
                @LateHalfPct, @LateZeroPct, @LatePenaltyHalfPct, @LatePenaltyFullPct,
                NOW()
            )
            ON CONFLICT (family_id) DO UPDATE SET
                late_t1_minutes = EXCLUDED.late_t1_minutes,
                late_t2_minutes = EXCLUDED.late_t2_minutes,
                late_t3_minutes = EXCLUDED.late_t3_minutes,
                late_half_pct = EXCLUDED.late_half_pct,
                late_zero_pct = EXCLUDED.late_zero_pct,
                late_penalty_half_pct = EXCLUDED.late_penalty_half_pct,
                late_penalty_full_pct = EXCLUDED.late_penalty_full_pct,
                updated_at = NOW()
            RETURNING family_id AS FamilyId,
                      late_t1_minutes AS LateT1Minutes,
                      late_t2_minutes AS LateT2Minutes,
                      late_t3_minutes AS LateT3Minutes,
                      late_half_pct AS LateHalfPct,
                      late_zero_pct AS LateZeroPct,
                      late_penalty_half_pct AS LatePenaltyHalfPct,
                      late_penalty_full_pct AS LatePenaltyFullPct
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                settings.LateT1Minutes,
                settings.LateT2Minutes,
                settings.LateT3Minutes,
                settings.LateHalfPct,
                settings.LateZeroPct,
                settings.LatePenaltyHalfPct,
                settings.LatePenaltyFullPct,
            });
    }

    internal sealed class FamilyStarSettingsRow
    {
        public Guid FamilyId { get; init; }
        public int LateT1Minutes { get; init; }
        public int LateT2Minutes { get; init; }
        public int LateT3Minutes { get; init; }
        public int LateHalfPct { get; init; }
        public int LateZeroPct { get; init; }
        public int LatePenaltyHalfPct { get; init; }
        public int LatePenaltyFullPct { get; init; }
    }
}
