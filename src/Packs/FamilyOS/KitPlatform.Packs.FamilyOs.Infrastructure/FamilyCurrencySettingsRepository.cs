using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCurrencySettingsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyCurrencySettingsRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<FamilyCurrencySettingsRow?> GetAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<FamilyCurrencySettingsRow>(
            """
            SELECT family_id AS FamilyId,
                   enabled AS Enabled,
                   preset_id AS PresetId,
                   age_band AS AgeBand,
                   daily_budget_override AS DailyBudgetOverride,
                   config_json::text AS ConfigJson
            FROM pack_family.family_currency_settings
            WHERE tenant_id = @TenantId AND family_id = @FamilyId
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<FamilyCurrencySettingsRow> UpsertAsync(
        Guid familyId,
        bool enabled,
        string presetId,
        string? ageBand,
        int? dailyBudgetOverride,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<FamilyCurrencySettingsRow>(
            """
            INSERT INTO pack_family.family_currency_settings (
                tenant_id, family_id, enabled, preset_id, age_band, daily_budget_override,
                config_json, updated_at
            )
            VALUES (
                @TenantId, @FamilyId, @Enabled, @PresetId, @AgeBand, @DailyBudgetOverride,
                '{}'::jsonb, NOW()
            )
            ON CONFLICT (family_id) DO UPDATE SET
                enabled = EXCLUDED.enabled,
                preset_id = EXCLUDED.preset_id,
                age_band = EXCLUDED.age_band,
                daily_budget_override = EXCLUDED.daily_budget_override,
                updated_at = NOW()
            RETURNING family_id AS FamilyId,
                      enabled AS Enabled,
                      preset_id AS PresetId,
                      age_band AS AgeBand,
                      daily_budget_override AS DailyBudgetOverride,
                      config_json::text AS ConfigJson
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                Enabled = enabled,
                PresetId = presetId,
                AgeBand = ageBand,
                DailyBudgetOverride = dailyBudgetOverride,
            });
    }

    public async Task<DateOnly?> GetMemberDobAsync(
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<DateOnly?>(
            """
            SELECT date_of_birth
            FROM pack_family.membership
            WHERE tenant_id = @TenantId AND id = @MemberId AND deleted_at IS NULL
            """,
            new { TenantId, MemberId = memberId });
    }

    public async Task<int> SumPostedStarsTodayAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(SUM(l.delta), 0)::int
            FROM pack_family.star_ledger l
            INNER JOIN pack_family.commitment c
                ON c.id = l.commitment_id AND c.tenant_id = l.tenant_id
            INNER JOIN pack_family.day_flow d
                ON d.id = c.day_flow_id AND d.tenant_id = c.tenant_id
            WHERE l.tenant_id = @TenantId
              AND l.family_id = @FamilyId
              AND l.member_id = @MemberId
              AND d.flow_date = @FlowDate
              AND l.delta > 0
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FlowDate = flowDate,
            });
    }

    public async Task<bool> HasSelfStartEventAsync(
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM pack_family.behavior_event
                WHERE tenant_id = @TenantId
                  AND commitment_id = @CommitmentId
                  AND event_type = 'self_start'
            )
            """,
            new { TenantId, CommitmentId = commitmentId });
    }

    internal sealed class FamilyCurrencySettingsRow
    {
        public Guid FamilyId { get; init; }
        public bool Enabled { get; init; }
        public string PresetId { get; init; } = FamilyCurrencyPreset.BalancedV1Id;
        public string? AgeBand { get; init; }
        public int? DailyBudgetOverride { get; init; }
        public string ConfigJson { get; init; } = "{}";
    }
}
